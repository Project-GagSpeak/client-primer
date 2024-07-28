using Dalamud.Interface;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI.UiRemote;
public class ChatLog
{
    public static readonly ChatCircularBuffer<ChatMessage> Messages = new(100);
    private static bool Autoscroll = true;
    private static int PreviousMessageCount = 0;
    private static readonly Dictionary<string, Vector4> UserColors = new();

    public static void AddMessage(string user, string message) 
        => Messages.PushBack(new ChatMessage(user, message));

    public static void ClearMessages() 
        => Messages.Clear();

    public void PrintImgui()
    {
        ImGui.BeginChild("Chat_log");

        foreach (var x in Messages)
        {
            if (!UserColors.ContainsKey(x.User))
            {
                UserColors[x.User] = new Vector4((float)new Random().NextDouble(), 
                    (float)new Random().NextDouble(), (float)new Random().NextDouble(), 1.0f);
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
