using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using System.Numerics;

namespace GagSpeak.UI.Components;

public class SetPreviewComponent
{
    private readonly ILogger<SetPreviewComponent> logger;
    private readonly UiSharedService _uiShared;
    private readonly IpcCallerGlamourer _ipcGlamourer;
    private readonly WardrobeHandler _handler;
    private readonly TextureService _textures;
    private readonly DictStain _stainDictionary;
    private readonly ItemIdVars _itemHelper;
    private readonly GagManager _padlockHandler;

    public SetPreviewComponent(ILogger<SetPreviewComponent> logger,
        UiSharedService uiSharedService, TextureService textureService,
        DictStain stainDictionary)
    {
        _uiShared = uiSharedService;
        _textures = textureService;
        _stainDictionary = stainDictionary;
        GameIconSize = new Vector2(2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
        StainColorCombos = new StainColorCombo(0, _stainDictionary, logger);
    }

    private Vector2 GameIconSize;
    private readonly StainColorCombo StainColorCombos;

    public void DrawRestraintSetPreviewCentered(RestraintSet set, Vector2 contentRegion)
    {
        // We should use the content region space to define how to center the content that we will draw.
        var columnWidth = GameIconSize.X + ImGui.GetFrameHeight();

        // Determine the total width of the table.
        var totalTableWidth = columnWidth * 2 + ImGui.GetStyle().ItemSpacing.X;
        var totalTableHeight = GameIconSize.Y * 6 + 10f;

        // Calculate the offset to center the table within the content region.
        var offsetX = (contentRegion.X - totalTableWidth) / 2;
        var offsetY = (contentRegion.Y - totalTableHeight) / 2;

        // Apply the offset to center the table.
        ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPosX() + offsetX, ImGui.GetCursorPosY()+offsetY));

        DrawRestraintSetDisplay(set);
    }

    public void DrawRestraintSetPreviewTooltip(RestraintSet set)
    {
        ImGui.BeginTooltip();
        DrawRestraintSetDisplay(set);
        ImGui.EndTooltip();
    }

    private void DrawRestraintSetDisplay(RestraintSet set)
    {
        // Draw the table.
        using (var equipIconsTable = ImRaii.Table("equipIconsTable", 2, ImGuiTableFlags.RowBg))
        {
            if (!equipIconsTable) return;
            // Create the headers for the table
            var width = GameIconSize.X + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X;
            // setup the columns
            ImGui.TableSetupColumn("EquipmentSlots", ImGuiTableColumnFlags.WidthFixed, width);
            ImGui.TableSetupColumn("AccessorySlots", ImGuiTableColumnFlags.WidthStretch);

            // draw out the equipment slots
            ImGui.TableNextRow(); ImGui.TableNextColumn();

            foreach (var slot in EquipSlotExtensions.EquipmentSlots)
            {
                set.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                ImGui.SameLine(0, 3);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawStain(set, slot);
                }
            }
            foreach (var slot in BonusExtensions.AllFlags)
            {
                set.BonusDrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
            }

            ImGui.TableNextColumn();

            //draw out the accessory slots
            foreach (var slot in EquipSlotExtensions.AccessorySlots)
            {
                set.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                ImGui.SameLine(0, 3);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawStain(set, slot);
                }
            }
        }
    }

    private void DrawStain(RestraintSet refSet, EquipSlot slot)
    {

        // draw the stain combo for each of the 2 dyes (or just one)
        foreach (var (stainId, index) in refSet.DrawData[slot].GameStain.WithIndex())
        {
            using var id = ImUtf8.PushId(index);
            var found = _stainDictionary.TryGetValue(stainId, out var stain);
            // draw the stain combo, but dont make it hoverable
            using (var disabled = ImRaii.Disabled(true))
            {
                StainColorCombos.Draw($"##stain{refSet.DrawData[slot].Slot}",
                    stain.RgbaColor, stain.Name, found, stain.Gloss, MouseWheelType.None);
            }
        }
    }
}
