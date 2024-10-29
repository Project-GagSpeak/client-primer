using GagspeakAPI.Enums;

namespace GagSpeak.Utils.ChatLog;
public record struct ChatMessage
{
    public string UID { get; init; }
    public string Name { get; init; }
    public CkSupporterTier SupporterTier { get; init; }
    public string Message { get; init; }
    public DateTime TimeStamp { get; init; }

    public ChatMessage(string uid, string name, CkSupporterTier? supporterTier, string message)
    {
        UID = uid;
        Name = name;
        SupporterTier = supporterTier ?? CkSupporterTier.NoRole;
        Message = message;
        TimeStamp = DateTime.Now;
    }
}
