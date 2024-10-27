using ImGuiNET;
using System.Numerics;

namespace GagSpeak.Utils.ChatLog;
// an instance of a chatlog.
public class ChatLog
{
    public readonly ChatCircularBuffer<ChatMessage> Messages = new(1000);
    public bool Autoscroll = true;
    private int PreviousMessageCount = 0;
    private readonly Dictionary<string, Vector4> UserColors = new();

    private static Vector4 CKMistressColor = new Vector4(0.886f, 0.407f, 0.658f, 1f);

    public void AddMessage(ChatMessage message)
        => Messages.PushBack(message);

    public void ClearMessages()
        => Messages.Clear();

    public void PrintChatLogHistory()
    {
        ImGui.BeginChild("GagSpeakGlobalChat");

        foreach (var x in Messages)
        {
            if (!UserColors.ContainsKey(x.UID))
            {
                if (x.SupporterTier is CkSupporterTier.KinkporiumMistress)
                {
                    UserColors[x.UID] = CKMistressColor;
                }
                else
                {
                    // Generate a random color for the user (excluding dark colors)
                    Vector4 color;
                    do
                    {
                        float r = (float)new Random().NextDouble();
                        float g = (float)new Random().NextDouble();
                        float b = (float)new Random().NextDouble();
                        color = new Vector4(r, g, b, 1.0f);
                    } while ((color.X < 0.4f && color.Y < 0.4f && color.Z < 0.4f) || UserColors.ContainsValue(color));
                    UserColors[x.UID] = color;
                }
            }

            // grab cursorscreenpox
            var cursorPos = ImGui.GetCursorScreenPos();
            // Print the user name with color
            ImGui.TextColored(UserColors[x.UID], $"[{x.Name}]");

            // Calculate the width of the user's name plus brackets
            var nameWidth = ImGui.CalcTextSize($"[{x.Name}]").X;
            var spaceWidth = ImGui.CalcTextSize(" ").X;
            int spaceCount = (int)(nameWidth / spaceWidth) + 2;
            string spaces = new string(' ', spaceCount);
            // Print the message with wrapping
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, cursorPos.Y));
            ImGui.TextWrapped(spaces + x.Message);
        }

        // Always scroll to the bottom after rendering messages
        // Only scroll to the bottom if auto-scroll is enabled and a new message is received
        if (Autoscroll && Messages.Count() != PreviousMessageCount)
        {
            ImGui.SetScrollHereY(1.0f);
            PreviousMessageCount = Messages.Count();
        }

        ImGui.EndChild();
    }
}
