using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils.ChatLog;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.Services;

// handles the global chat and pattern discovery social features.
public class DiscoverService : DisposableMediatorSubscriberBase
{
    private readonly PairManager _pairManager;

    public ChatLog GagspeakGlobalChat { get; private set; }
    private bool _connectedFirstTime = false;


    public DiscoverService(ILogger<DiscoverService> logger, GagspeakMediator mediator,
        PairManager pairManager) : base(logger, mediator)
    {
        _pairManager = pairManager;

        // set the chat log up.
        GagspeakGlobalChat = new ChatLog();
        Mediator.Subscribe<GlobalChatMessage>(pairManager, (msg) => AddChatMessage(msg));

        Mediator.Subscribe<ConnectedMessage>(this, (msg) =>
        {
            if (!_connectedFirstTime)
            {
                _connectedFirstTime = true;
                AddSystemWelcome();
            }
        });
    }

    private void AddSystemWelcome()
    {
        GagspeakGlobalChat.AddMessage(new ChatMessage("System", "System", null, "Welcome to the GagSpeak Global Chat!. " +
            "Your Name in here is Anonymous to anyone you have not yet added. Feel free to say hi!"));
    }


    private void AddChatMessage(GlobalChatMessage msg)
    {
        string SenderName = "Anon. Kinkster";

        // extract the userdata from the message
        var userData = msg.ChatMessage.MessageSender;
        // grab the list of our currently online pairs.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == userData.UID);

        // determine the displayname
        if (msg.FromSelf) SenderName = msg.ChatMessage.MessageSender.AliasOrUID;
        if (matchedPair != null) SenderName = matchedPair.GetNickname() ?? matchedPair.UserData.AliasOrUID;

        // construct the chat message struct to add.
        ChatMessage msgToAdd = new ChatMessage
        {
            UID = userData.UID,
            Name = SenderName,
            SupporterTier = userData.SupporterTier ?? CkSupporterTier.NoRole,
            Message = msg.ChatMessage.Message,
        };

        GagspeakGlobalChat.AddMessage(msgToAdd);
    }





}
