using GagspeakAPI.Data.Enum;

namespace GagSpeak.Utils.ChatLog;
public record struct ChatMessage
{
    public string User;
    public CkSupporterTier SupporterTier;
    public string Message;

    public ChatMessage(string user, CkSupporterTier? supporterTier, string message)
    {
        User = user;
        SupporterTier = supporterTier ?? CkSupporterTier.NoRole;
        Message = message;
    }
}
