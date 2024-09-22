using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Controllers;
using GagSpeak.Utils;
using Territory = FFXIVClientStructs.FFXIV.Client.Game.UI.TerritoryInfo;

namespace GagSpeak.UpdateMonitoring.Chat;

/// <summary>
/// This class handles incoming chat messages, combat messages, and other related Messages we care about. 
/// It will then trigger the appropriate chat classes to handle the message.
/// 
/// It is worth noting that this detection occurs after the message is sent to the server, and should not be
/// depended on for translation prior to sending.
/// </summary>
public unsafe class ChatBoxMessage : DisposableMediatorSubscriberBase
{
    private readonly PuppeteerHandler _puppeteerHandler;
    private readonly ChatSender _chatSender;
    private readonly TriggerController _triggerController;
    private readonly IChatGui _chat;
    private readonly IClientState _clientState;
    private Stopwatch messageTimer; // Stopwatch Timer for time between messages sent (should no longer be needed since we are not sending chained messages)

    private unsafe Territory* info = Territory.Instance();

    /// <summary> This is the constructor for the OnChatMsgManager class. </summary>
    public ChatBoxMessage(ILogger<ChatBoxMessage> logger, GagspeakMediator mediator,
        PuppeteerHandler puppeteerHandler, ChatSender chatSender, TriggerController triggerController,
        IChatGui clientChat, IClientState clientState) : base(logger, mediator)
    {
        _chatSender = chatSender;
        _puppeteerHandler = puppeteerHandler;
        _triggerController = triggerController;
        _chat = clientChat;
        _clientState = clientState;
        // set variables
        MessageQueue = new Queue<string>();
        messageTimer = new Stopwatch();
        // set up the event handlers
        _chat.ChatMessage += Chat_OnChatMessage;

        Mediator.Subscribe<FrameworkUpdateMessage>(this, (_) => FrameworkUpdate());

        Mediator.Subscribe<UpdateChatListeners>(this, (msg) => OnUpdateChatListeners());

        // load in the initial list of chat listeners
        OnUpdateChatListeners();
    }

    public static Queue<string> MessageQueue; // the messages to send to the server.
    public static List<string> PlayersToListenFor; // players to listen to messages from. (Format of NameWithWorld)

    /// <summary> This is the disposer for the OnChatMsgManager class. </summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _chat.ChatMessage -= Chat_OnChatMessage;
    }

    public static void EnqueueMessage(string message)
    {
        MessageQueue.Enqueue(message);
    }

    private void OnUpdateChatListeners()
    {
        Logger.LogDebug("Updating Chat Listeners", LoggerType.ToyboxTriggers);
        PlayersToListenFor = _puppeteerHandler.GetPlayersToListenFor();
    }

    private void Chat_OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // Don't process messages if we ain't visible.
        if (_clientState.LocalPlayer == null) return;

        // log all types of payloads included in the message.
        /*

        Logger.LogDebug("---------------------");
        Logger.LogDebug("Chat Type: " + (int)type, LoggerType.ToyboxTriggers);
        foreach (var payloadType in message.Payloads)
        {
            string text = payloadType.Type.ToString();
            if (payloadType.Type is PayloadType.RawText) text = text + "(" + payloadType.ToString() + ")";
            if (payloadType.Type is PayloadType.MapLink) text = text + "(" + payloadType.ToString() + ")";
            if (payloadType is PlayerPayload playerPayload)
            {
                Logger.LogInformation("Player Payload: " + playerPayload.PlayerName + "@" + playerPayload.World.Name);
            }
            Logger.LogInformation("Payload Type: " + text);
        }*/
        // Handle the special case where we are checking a DeathRoll
        if (type == (XivChatType)2122 || type == (XivChatType)8266 || type == (XivChatType)4170)
        {
            if (message.Payloads[1] is PlayerPayload)
            {
                // grab the player payload from the message
                var playerPayload = message.Payloads[1] as PlayerPayload;
                if (playerPayload != null)
                {
                    // check for social triggers
                    _triggerController.CheckActiveSocialTriggers(type, playerPayload.PlayerName + "@" + playerPayload.World.Name, sender, message);
                }
            }
            else
            {
                // Should check under our name if this isn't valid as someone elses player payload.
                Logger.LogDebug("Message was from self.", LoggerType.ToyboxTriggers);
                _triggerController.CheckActiveSocialTriggers(type, _clientState.LocalPlayer.GetNameWithWorld(), sender, message);
            }
        }

        // get the player payload of the sender. If we are sending the message, this is null.
        PlayerPayload? senderPlayerPayload = sender.Payloads.SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;
        string senderName = "";
        string senderWorld = "";
        if (senderPlayerPayload == null)
        {
            senderName = _clientState.LocalPlayer.Name.TextValue;
            senderWorld = _clientState.LocalPlayer.HomeWorld.GameData!.Name;
        }
        else
        {
            senderName = senderPlayerPayload.PlayerName;
            senderWorld = senderPlayerPayload.World.Name;
        }

        // route to scan for any active triggers. (block outgoing tells because otherwise they always come up as from the recipient).
        if (type != XivChatType.TellOutgoing)
        {
            _triggerController.CheckActiveChatTriggers(type, senderName + "@" + senderWorld, message.TextValue);
        }

        if (senderName + "@" + senderWorld == _clientState.LocalPlayer.GetNameWithWorld()) return;

        // check for global puppeteer triggers
        if (_puppeteerHandler.IsValidGlobalTriggerWord(message, type))
        {
            // the message did contain the trigger word, to obtain the message to send.
            SeString msgToSend = _puppeteerHandler.NewMessageFromGlobalTrigger(message, type);
            if (msgToSend.TextValue.IsNullOrEmpty()) return;

            // enqueue the message and log sucess
            Logger.LogInformation(senderName + " used your global trigger phase to make you exeucte a message!", LoggerType.Puppeteer);
            EnqueueMessage("/" + msgToSend.TextValue);
        }

        // check for puppeteer pair triggers
        if (SenderIsInPuppeteerListeners(senderName, senderWorld, out Pair? matchedPair) && matchedPair != null)
        {
            // obtain the trigger phrases for this sender
            var triggerPhrases = matchedPair.UserPairOwnUniquePairPerms.TriggerPhrase.Split('|').ToList();
            // see if valid pair trigger-phrase was used
            if (_puppeteerHandler.IsValidPuppeteerTriggerWord(triggerPhrases, message))
            {
                //Logger.LogInformation(senderName + " used your pair trigger phrase to make you execute a message!");
                // get the new message to send
                SeString msgToSend = _puppeteerHandler.NewMessageFromPuppeteerTrigger(triggerPhrases, matchedPair.UserData.UID, matchedPair.UserPairOwnUniquePairPerms, message, type);

                // convert any alias's set for this user if any are present.
                //Logger.LogInformation("message before alias conversion: " + msgToSend.TextValue);
                msgToSend = _puppeteerHandler.ConvertAliasCommandsIfAny(matchedPair.UserData.UID, msgToSend.TextValue);

                //Logger.LogInformation("message after alias conversion: " + msgToSend.TextValue);

                if (!msgToSend.TextValue.IsNullOrEmpty())
                {
                    // enqueue the message and log sucess
                    Logger.LogInformation(senderName + " used your pair trigger phrase to make you execute a message!", LoggerType.Puppeteer);
                    EnqueueMessage("/" + msgToSend.TextValue);
                }
            }
        }
    }

    private bool SenderIsInPuppeteerListeners(string name, string world, out Pair? matchedPair)
    {
        matchedPair = null;
        string nameWithWorld = name + "@" + world;
        // make sure we are listening for this player.
        if (!PlayersToListenFor.Contains(nameWithWorld)) return false;
        // make sure they exist in our alias list config
        var uidOfSender = _puppeteerHandler.GetUIDMatchingSender(name, world);
        if (uidOfSender.IsNullOrEmpty()) return false;

        var pairOfUid = _puppeteerHandler.GetPairOfUid(uidOfSender!);
        if (pairOfUid == null) return false;

        // successful match
        matchedPair = pairOfUid;
        return true;
    }



    /// <summary> <b> SENDS A REAL CHAT MESSAGE TO THE SERVER </b></summary>
    public void SendRealMessage(string message)
    {
        try
        {
            _chatSender.SendMessage(message);
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to send message {e}: {message}");
        }
    }

    /// <summary>
    /// This function will handle the framework update, 
    /// and will send messages to the server if there are any in the queue.
    /// </summary>
    private void FrameworkUpdate()
    {
        if (MessageQueue.Count <= 0 || _chatSender is null) return;

        if (!messageTimer.IsRunning)
        {
            messageTimer.Start();
        }
        else
        {
            if (messageTimer.ElapsedMilliseconds > 500)
            {
                try
                {
                    _chatSender.SendMessage(MessageQueue.Dequeue());
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"{e},{e.Message}");
                }
                messageTimer.Restart();
            }
        }
    }
}

