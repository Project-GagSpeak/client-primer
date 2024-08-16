using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.ChatMessages;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring.Chat.ChatMonitors;

namespace GagSpeak.UpdateMonitoring.Chat;

public struct ChatListener
{
    public string PlayerName;
    public string WorldName;
}

/// <summary>
/// This class handles incoming chat messages, combat messages, and other related Messages we care about. 
/// It will then trigger the appropriate chat classes to handle the message.
/// 
/// It is worth noting that this detection occurs after the message is sent to the server, and should not be
/// depended on for translation prior to sending.
/// </summary>
public class ChatBoxMessage : DisposableMediatorSubscriberBase
{
//    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PuppeteerHandler _puppeteerHandler;
    private readonly ChatSender _chatSender;
    private readonly IChatGui _chat;
    private readonly IClientState _clientState;
    private Stopwatch messageTimer; // Stopwatch Timer for time between messages sent

    /// <summary> This is the constructor for the OnChatMsgManager class. </summary>
    public ChatBoxMessage(ILogger<ChatBoxMessage> logger, GagspeakMediator mediator,
        PuppeteerHandler puppeteerHandler, ChatSender chatSender, IChatGui clientChat, 
        IClientState clientState) : base(logger, mediator)
    {
        _chatSender = chatSender;
        _puppeteerHandler = puppeteerHandler;
        _chat = clientChat;
        _clientState = clientState;
        // set variables
        MessageQueue = new Queue<string>();
        messageTimer = new Stopwatch();
        // set up the event handlers
        _chat.ChatMessage += Chat_OnChatMessage;

        Mediator.Subscribe<FrameworkUpdateMessage>(this, (_) => FrameworkUpdate());

        Mediator.Subscribe<UpdateChatListeners>(this, (msg) => OnUpdateChatListeners());

    }

    public Queue<string> MessageQueue; // the messages to send to the server.
    public List<ChatListener> PlayersToListenFor; // the players to listen for messages from.



    /// <summary> This is the disposer for the OnChatMsgManager class. </summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _chat.ChatMessage -= Chat_OnChatMessage;
    }

    private void OnUpdateChatListeners()
    {
        var listeners = _puppeteerHandler.GetPlayersToListenFor();
        PlayersToListenFor = listeners.Select(x => new ChatListener { PlayerName = x.Item1, WorldName = x.Item2 }).ToList();
    }

    private void Chat_OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // if the sender name is ourself, ignore the message.
        if (sender.TextValue == _clientState.LocalPlayer?.Name.TextValue) return;

        // grab the senders player payload so we can know their name and world.
        var senderPlayerPayload = sender.Payloads.SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;
        if (senderPlayerPayload == null) return;

        string senderName = senderPlayerPayload.PlayerName;
        string senderWorld = senderPlayerPayload.World.Name;

        // check for globalTriggers
        if (_puppeteerHandler.IsValidGlobalTriggerWord(message, type))
        {
            // the message did contain the trigger word, to obtain the message to send.
            SeString msgToSend = _puppeteerHandler.NewMessageFromGlobalTrigger(message, type);
            if (msgToSend.TextValue.IsNullOrEmpty()) return;

            // enqueue the message and log sucess
            Logger.LogInformation(senderName + " used your global trigger phase to make you exeucte a message!");
            MessageQueue.Enqueue(msgToSend.TextValue);
        }

        // check for puppeteer pair triggers
        if (SenderIsInPuppeteerListeners(senderName, senderWorld, out Pair? matchedPair) && matchedPair != null)
        {
            // obtain the trigger phrases for this sender
            var triggerPhrases = matchedPair.UserPairOwnUniquePairPerms.TriggerPhrase.Split('|').ToList();

            // see if valid pair triggerphrase was used
            if (_puppeteerHandler.IsValidPuppeteerTriggerWord(triggerPhrases, message))
            {
                // get the new message to send
                SeString msgToSend = _puppeteerHandler.NewMessageFromPuppeteerTrigger(triggerPhrases, matchedPair.UserPairOwnUniquePairPerms, message, type);

                // convert any alias's set for this user if any are present.
                msgToSend = _puppeteerHandler.ConvertAliasCommandsIfAny(matchedPair.UserData.UID, msgToSend.TextValue);

                // enqueue the message and log sucess
                Logger.LogInformation(senderName + " used your pair trigger phrase to make you execute a message!");
                MessageQueue.Enqueue(msgToSend.TextValue);
            }
        }
    }

    private bool SenderIsInPuppeteerListeners(string name, string world, out Pair? matchedPair)
    {
        matchedPair = null;
        // make sure we are listening for this player.
        if (!PlayersToListenFor.Any(listener => listener.PlayerName == name && listener.WorldName == world)) return false;

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
        try
        {
            if (MessageQueue.Count > 0 && _chatSender != null)
            {
                if (!messageTimer.IsRunning)
                {
                    messageTimer.Start();
                }
                else
                {
                    if (messageTimer.ElapsedMilliseconds > 1500)
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
        catch
        {
            Logger.LogError($"Failed to process Framework Update!");
        }
    }
}

