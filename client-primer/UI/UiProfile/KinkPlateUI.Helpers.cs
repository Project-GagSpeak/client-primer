using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
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
    private void AddImageRounded(ImDrawListPtr drawList, IDalamudTextureWrap? wrap, Vector2 topLeftPos, Vector2 size, float rounding)
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
            }
        }
        catch (Exception ex) { _logger.LogError($"Error: {ex}"); }
    }

    private void AddImage(ImDrawListPtr drawList, IDalamudTextureWrap? wrap, Vector2 topLeftPos, Vector2 size, Vector4? tint = null)
    {
        try
        {
            if (wrap is { } validWrap)
            {
                // handle tint.
                var actualTint = tint ?? new Vector4(1f, 1f, 1f, 1f);
                // handle image.
                drawList.AddImage(validWrap.ImGuiHandle, topLeftPos, topLeftPos + size, Vector2.Zero, Vector2.One, ImGui.GetColorU32(actualTint));
            }
        }
        catch (Exception ex) { _logger.LogError($"Error: {ex}"); }
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
}
