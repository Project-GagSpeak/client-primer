using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Profile;

/// <summary>
/// Helper Functions for Drawing out the components.
/// </summary>
public partial class KinkPlateUI : WindowMediatorSubscriberBase
{
    public static void AddImageRounded(ImDrawListPtr drawList, IDalamudTextureWrap? wrap, Vector2 topLeftPos, Vector2 size, float rounding, bool tt = false, string ttText = "")
    {
        try
        {
            if (wrap is { } validWrap)
            {
                ImGui.GetWindowDrawList().AddImageRounded(
                    validWrap.ImGuiHandle,
                    topLeftPos,
                    topLeftPos + size,
                    Vector2.Zero,
                    Vector2.One,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)),
                    rounding);
                if(tt)
                {
                    AddRelativeTooltip(topLeftPos, size, ttText);
                }
            }
        }
        catch (Exception ex) { StaticLogger.Logger.LogError($"Error: {ex}"); }
    }

    public static void AddImage(ImDrawListPtr drawList, IDalamudTextureWrap? wrap, Vector2 pos, Vector2 size, Vector4? tint = null, bool tt = false, string ttText = "")
    {
        try
        {
            if (wrap is { } validWrap)
            {
                // handle tint.
                var actualTint = tint ?? new Vector4(1f, 1f, 1f, 1f);
                // handle image.
                drawList.AddImage(validWrap.ImGuiHandle, pos, pos + size, Vector2.Zero, Vector2.One, ImGui.GetColorU32(actualTint));
                if(tt)
                {
                    AddRelativeTooltip(pos, size, ttText);
                }
            }
        }
        catch (Exception ex) { StaticLogger.Logger.LogError($"Error: {ex}"); }
    }

    private void CloseButton(ImDrawListPtr drawList)
    {
        var btnPos = CloseButtonPos;
        var btnSize = CloseButtonSize;

        var closeButtonColor = HoveringCloseButton ? ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)) : ImGui.GetColorU32(ImGuiColors.ParsedPink);

        drawList.AddLine(btnPos, btnPos + btnSize, closeButtonColor, 3);
        drawList.AddLine(new Vector2(btnPos.X + btnSize.X, btnPos.Y), new Vector2(btnPos.X, btnPos.Y + btnSize.Y), closeButtonColor, 3);


        ImGui.SetCursorScreenPos(btnPos);
        if (ImGui.InvisibleButton($"CloseButton##KinkPlateClose" + Pair.UserData.UID, btnSize))
        {
            this.IsOpen = false;
        }
        HoveringCloseButton = ImGui.IsItemHovered();
    }

    private void DrawLimitedDescription(string desc, Vector4 color, Vector2 size)
    {
        // Get the basic line height.
        float lineHeight = ImGui.CalcTextSize("A").Y;
        int totalLines = (int)(size.Y / lineHeight) - 1; // Total lines to display based on height
        string newDescText = "";
        string[] words = desc.Split(' ');
        int currentLines = 0;

        while (newDescText.Length < desc.Length && currentLines < totalLines)
        {
            // Calculate how much of the message fits in the available space
            string fittingMessage = string.Empty;
            float currentWidth = 0;

            // Build the fitting message
            foreach (var word in words)
            {
                float wordWidth = ImGui.CalcTextSize(word + " ").X;

                // Check if adding this word exceeds the available width
                if (currentWidth + wordWidth > size.X)
                {
                    break; // Stop if it doesn't fit
                }

                fittingMessage += word + " ";
                currentWidth += wordWidth;
            }

            currentLines++;
            newDescText += fittingMessage.TrimEnd();

            // Only add newline if we're not on the last line
            if (currentLines < totalLines && newDescText.Length < desc.Length)
            {
                newDescText += "\n";
            }

            if (newDescText.Length < desc.Length)
            {
                words = desc.Substring(newDescText.Length).TrimStart().Split(' ');
            }
        }

        // Final check of truncated text before rendering
        _logger.LogDebug($"Truncated Description:\n {newDescText}");
        UiSharedService.ColorTextWrapped(newDescText, color);
    }

    public static void AddRelativeTooltip(Vector2 pos, Vector2 size, string text)
    {
        // add a scaled dummy over this area.
        ImGui.SetCursorScreenPos(pos);
        ImGuiHelpers.ScaledDummy(size);
        UiSharedService.AttachToolTip(text);
    }
}
