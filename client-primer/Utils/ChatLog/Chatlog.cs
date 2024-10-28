using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.UI;
using ImGuiNET;
using OtterGui.Text;
using System.Numerics;
using System.Windows.Forms;

namespace GagSpeak.Utils.ChatLog;
// an instance of a chatlog.
public class ChatLog
{
    public readonly ChatCircularBuffer<ChatMessage> Messages = new(1000);
    private int PreviousMessageCount = 0;
    private readonly Dictionary<string, Vector4> UserColors = new();
    private static Vector4 CKMistressColor = new Vector4(0.886f, 0.407f, 0.658f, 1f);
    private static Vector4 CkMistressText = new Vector4(1, 0.711f, 0.843f, 1f);
    public DateTime TimeCreated { get; set; }
    public bool Autoscroll = true;


    public ChatLog()
    {
        TimeCreated = DateTime.UtcNow;
    }

    public void AddMessage(ChatMessage message)
        => Messages.PushBack(message);

    public void AddMessageRange(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            Messages.PushBack(message);
        }
    }

    public void ClearMessages()
        => Messages.Clear();

    public void PrintChatLogHistory(bool showMessagePreview, string previewMessage)
    {
        using (ImRaii.Child("##GlobalChatLog"+TimeCreated.ToString()))
        {
            var region = ImGui.GetContentRegionAvail();
            var ySpacing = ImGui.GetStyle().ItemInnerSpacing.Y;
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
                        bool isBright;
                        do
                        {
                            float r = (float)new Random().NextDouble();
                            float g = (float)new Random().NextDouble();
                            float b = (float)new Random().NextDouble();
                            // Calculate brightness as a sum of the RGB channels
                            float brightness = r + g + b;

                            // Ensure at least one channel is high or the brightness is above a threshold
                            isBright = brightness > 1.8f || r > 0.5f || g > 0.5f || b > 0.5f;
                            color = new Vector4(r, g, b, 1.0f);
                        } while (!isBright || UserColors.ContainsValue(color));
                        UserColors[x.UID] = color;
                    }
                }

                // grab cursorscreenposx
                var cursorPos = ImGui.GetCursorScreenPos();
                // Print the user name with color
                ImGui.TextColored(UserColors[x.UID], $"[{x.Name}]");

                // Calculate the width of the user's name plus brackets
                var nameWidth = ImGui.CalcTextSize($"[{x.Name}]").X;

                // Get the remaining width available in the current row
                var availableWidth = ImGui.GetContentRegionAvail().X - nameWidth;
                float msgWidth = ImGui.CalcTextSize(x.Message).X;
                // If the total width is less than available, print in one go
                if (msgWidth <= availableWidth)
                {
                    ImUtf8.SameLineInner();
                    if(x.SupporterTier is CkSupporterTier.KinkporiumMistress)
                        UiSharedService.ColorText(x.Message, CkMistressText);
                    else
                        ImGui.Text(x.Message);
                }
                else
                {
                    // Calculate how much of the message fits in the available space
                    string fittingMessage = string.Empty;
                    string[] words = x.Message.Split(' ');
                    float currentWidth = 0;

                    // Build the fitting message
                    foreach (var word in words)
                    {
                        float wordWidth = ImGui.CalcTextSize(word + " ").X;

                        // Check if adding this word exceeds the available width
                        if (currentWidth + wordWidth > availableWidth)
                        {
                            break; // Stop if it doesn't fit
                        }

                        fittingMessage += word + " ";
                        currentWidth += wordWidth;
                    }

                    // Print the fitting part of the message
                    ImUtf8.SameLineInner();
                    if(x.SupporterTier is CkSupporterTier.KinkporiumMistress)
                        UiSharedService.ColorText(fittingMessage.TrimEnd(), CkMistressText);
                    else
                        ImGui.Text(fittingMessage.TrimEnd());

                    // Draw the remaining part of the message wrapped
                    string wrappedMessage = x.Message.Substring(fittingMessage.Length).TrimStart();
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ySpacing);
                    if(x.SupporterTier is CkSupporterTier.KinkporiumMistress)
                        UiSharedService.ColorTextWrapped(wrappedMessage, CkMistressText);
                    else
                        ImGui.TextWrapped(wrappedMessage);
                }
            }

            // Always scroll to the bottom after rendering messages
            // Only scroll to the bottom if auto-scroll is enabled and a new message is received
            if (Autoscroll && Messages.Count() != PreviousMessageCount)
            {
                ImGui.SetScrollHereY(1.0f);
                PreviousMessageCount = Messages.Count();
            }

            // draw the text preview if we should.
            if (showMessagePreview && !string.IsNullOrWhiteSpace(previewMessage))
            {
                DrawTextWrapBox(previewMessage, region);
            }
        }
    }

    private void DrawTextWrapBox(string message, Vector2 currentRegion)
    {
        var drawList = ImGui.GetWindowDrawList();
        var padding = new Vector2(5, 2);

        // Set the wrap width based on the available region
        var wrapWidth = currentRegion.X - padding.X * 2;

        // Estimate text size with wrapping
        var textSize = ImGui.CalcTextSize(message, wrapWidth: wrapWidth);

        // Calculate the height of a single line for the given wrap width
        float singleLineHeight = ImGui.CalcTextSize("A").Y;
        int lineCount = (int)Math.Ceiling(textSize.Y / singleLineHeight);

        // Calculate the total box size based on line count
        var boxSize = new Vector2(currentRegion.X, lineCount * singleLineHeight + padding.Y * 2);

        // Position the box above the input, offset by box height
        var boxPos = ImGui.GetCursorScreenPos() - new Vector2(0, boxSize.Y +10);

        // Draw semi-transparent background
        drawList.AddRectFilled(boxPos, boxPos + boxSize, ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, .7f)), 5);

        // Begin a child region for the wrapped text
        ImGui.SetCursorScreenPos(boxPos + padding);
        using (ImRaii.Child("##TextWrapBox", new Vector2(wrapWidth, boxSize.Y), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapWidth);
            ImGui.TextWrapped(message);
            ImGui.PopTextWrapPos();
        }

        // Reset cursor to avoid overlap
        ImGui.SetCursorScreenPos(boxPos + new Vector2(0, boxSize.Y + 5));
    }
}
