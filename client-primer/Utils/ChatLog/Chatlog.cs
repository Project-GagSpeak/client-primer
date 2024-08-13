using Dalamud.Interface;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.Utils.ChatLog;
// an instance of a chatlog.
public class ChatLog
{
    public readonly ChatCircularBuffer<ChatMessage> Messages = new(1000);
    private bool Autoscroll = true;
    private int PreviousMessageCount = 0;
    private readonly Dictionary<string, Vector4> UserColors = new();

    public void AddMessage(ChatMessage message)
        => Messages.PushBack(message);

    public void ClearMessages()
        => Messages.Clear();

    public void PrintImgui()
    {
        ImGui.BeginChild("Chat_log");

        foreach (var x in Messages)
        {
            if (!UserColors.ContainsKey(x.User))
            {
                Vector4 color;
                do
                {
                    float r = (float)new Random().NextDouble();
                    float g = (float)new Random().NextDouble();
                    float b = (float)new Random().NextDouble();
                    color = new Vector4(r, g, b, 1.0f);
                } while ((color.X < 0.4f && color.Y < 0.4f && color.Z < 0.4f) || UserColors.ContainsValue(color));

                UserColors[x.User] = color;
            }
            ImGui.TextColored(UserColors[x.User], $" [{x.User}]");
            ImGui.SameLine();
            ImGui.TextWrapped(x.Message);
        }

        // Always scroll to the bottom after rendering messages
        ImGui.SetScrollHereY(1.0f);

        ImGui.EndChild();
    }
}
