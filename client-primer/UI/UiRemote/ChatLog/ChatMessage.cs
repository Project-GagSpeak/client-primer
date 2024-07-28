namespace GagSpeak.UI.UiRemote;
public record struct ChatMessage
{
    public string User;
    public string Message;

    public ChatMessage(string user, string message)
    {
        User = user;
        Message = message;
    }
}
