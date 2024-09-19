using GagspeakAPI.Enums;

namespace GagSpeak.Utils.ChatLog;
public record struct ChatMessage
{
    public string UID;
    public string Name;
    public CkSupporterTier SupporterTier;
    public string Message;

    public ChatMessage(string uid, string name, CkSupporterTier? supporterTier, string message)
    {
        UID = uid;
        Name = name;
        SupporterTier = supporterTier ?? CkSupporterTier.NoRole;
        Message = message;
    }
}
