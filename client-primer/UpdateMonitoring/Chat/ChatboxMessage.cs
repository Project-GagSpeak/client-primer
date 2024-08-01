using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using GagSpeak.UpdateMonitoring.Chat.ChatMonitors;

namespace GagSpeak.UpdateMonitoring.Chat;
/// <summary>
/// This class handles incoming chat messages, combat messages, and other related Messages we care about. 
/// It will then trigger the appropriate chat classes to handle the message.
/// 
/// It is worth noting that this detection occurs after the message is sent to the server, and should not be
/// depended on for translation prior to sending.
/// </summary>
public class ChatBoxMessage
{
    private readonly ILogger<ChatBoxMessage> _logger;
    private readonly IChatGui _chat;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly ChatSender _chatSender;

    public Queue<string> messageQueue; // A Queue of the Messages to send.
    private Stopwatch messageTimer; // Stopwatch Timer for time between messages sent

    /// <summary> This is the constructor for the OnChatMsgManager class. </summary>
    public ChatBoxMessage(ILogger<ChatBoxMessage> logger, IChatGui clientChat, 
        IClientState clientState, IFramework framework, ChatSender chatSender)
    {
        _logger = logger;
        _chat = clientChat;
        _clientState = clientState;
        _framework = framework;
        _chatSender = chatSender;
        // set variables
        messageQueue = new Queue<string>();
        messageTimer = new Stopwatch();
        // set up the event handlers
        _framework.Update += framework_Update;
        _chat.ChatMessage += Chat_OnChatMessage;
    }

    /// <summary> This is the disposer for the OnChatMsgManager class. </summary>
    public void Dispose()
    {
        _framework.Update -= framework_Update;
        _chat.ChatMessage -= Chat_OnChatMessage;
    }

    private void Chat_OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // don't really know what to do with this since 7.0 dropped.... look into further later i suppose.


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
            _logger.LogError($"Failed to send message {e}: {message}");
        }
    }

    /// <summary>
    /// This function will handle the framework update, 
    /// and will send messages to the server if there are any in the queue.
    /// </summary>
    private void framework_Update(IFramework framework)
    {
        try
        {
            if (messageQueue.Count > 0 && _chatSender != null)
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
                            _chatSender.SendMessage(messageQueue.Dequeue());
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning($"{e},{e.Message}");
                        }
                        messageTimer.Restart();
                    }
                }
            }
        }
        catch
        {
            _logger.LogError($"Failed to process Framework Update!");
        }
    }
}

