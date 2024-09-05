using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.IpcHelpers.Moodles;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Numerics;

namespace GagSpeak.UI.Tabs.WardrobeTab;
/// <summary> This class is used to handle the ConfigSettings Tab. </summary>
public class GagStoragePanel : DisposableMediatorSubscriberBase
{
    private const float ComboWidth = 175f;

    private readonly IDataManager _gameData;
    private readonly TextureService _textures;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly UiSharedService _uiShared;
    private readonly DictStain StainData;
    private readonly ItemData ItemData;
    private readonly MoodlesAssociations _relatedMoodles;

    private LowerString GagSearchString = LowerString.Empty;
    private Vector2 IconSize;
    private float ComboLength;
    private Vector2 DefaultItemSpacing;
    private readonly GameItemCombo[] GameItemCombo;
    private readonly StainColorCombo StainCombo;

    public GagStoragePanel(ILogger<GagStoragePanel> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, UiSharedService uiSharedService,
        DictStain stainData, ItemData itemData, TextureService textures,
        MoodlesAssociations relatedMoodles, IDataManager gameData) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _uiShared = uiSharedService;
        _textures = textures;
        _gameData = gameData;
        StainData = stainData;
        ItemData = itemData;
        _relatedMoodles = relatedMoodles;

        // create a new gameItemCombo for each equipment piece type, then store them into the array.
        GameItemCombo = EquipSlotExtensions.EqdpSlots.Select(e => new GameItemCombo(_gameData, e, ItemData, Logger)).ToArray();
        StainCombo = new StainColorCombo(ComboWidth - 20, StainData, Logger);

        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) => LastCreatedCharacterData = msg.CharacterIPCData);
    }

    // Info related to the person we are inspecting.
    private CharacterIPCData LastCreatedCharacterData = null!;

    private string GagFilterSearchString = string.Empty;
    private GagDrawData UnsavedDrawData = null!;
    private GagList.GagType SelectedGag = GagList.GagType.BallGag;

    private void DrawGagStorageHeader()
    {
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        Vector2 textSize;
        using (_uiShared.UidFont.Push()) { textSize = ImGui.CalcTextSize("Select Gag Type"); }
        var saveSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Save, "Save");
        var centerYpos = (textSize.Y - ImGui.GetFrameHeight());

        using (ImRaii.Child("MoodlesManagerHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeight() + (centerYpos - startYpos) * 2)))
        {
            // now next to it we need to draw the header text
            ImGui.SameLine(ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText("Select Gag Type", ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - saveSize - 175f - ImGui.GetStyle().ItemSpacing.X * 2);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            _uiShared.DrawComboSearchable("GagStorage Gag Type", 175f, ref GagFilterSearchString,
            Enum.GetValues<GagList.GagType>().Where(gag => gag != GagList.GagType.None), (gag) => gag.GetGagAlias(), false, 
            (i) => 
            { 
                // grab the new gag info.
                SelectedGag = GagList.AliasToGagTypeMap[i.GetGagAlias()];
                UnsavedDrawData = _clientConfigs.GetDrawData(SelectedGag);
            }, SelectedGag);

            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - saveSize - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // draw revert button at the same location but right below that button
            if (_uiShared.IconTextButton(FontAwesomeIcon.Save, "Save"))
            {
                _clientConfigs.UpdateGagItem(SelectedGag, UnsavedDrawData!);
                _lastSaveTime = DateTime.UtcNow;
            }
            UiSharedService.AttachToolTip("View the list of Moodles Statuses");
        }
    }

    public void DrawGagStoragePanel()
    {
        DrawGagStorageHeader();
        ImGui.Separator();
        var cellPadding = ImGui.GetStyle().CellPadding;
        using var tabBar = ImRaii.TabBar("GagStorageEditor");

        if (tabBar)
        {
            var gagGlamour = ImRaii.TabItem("Gag Glamour");
            if (gagGlamour)
            {
                DrawGagGlamour();
            }
            gagGlamour.Dispose();

            var gagMoodles = ImRaii.TabItem("Moodles");
            if (gagMoodles)
            {
                DrawGagMoodles(cellPadding.Y);
            }
            gagMoodles.Dispose();

            var gagAudio = ImRaii.TabItem("Audio");
            if (gagAudio)
            {
                _uiShared.BigText("Audio WIP");
            }
            gagAudio.Dispose();

            if(DateTime.UtcNow - _lastSaveTime < TimeSpan.FromSeconds(5))
            {
                using (var disabled = ImRaii.Disabled())
                {
                    using (var style = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0, 1, 0, 1)))
                    {
                        var gagSaved = ImRaii.TabItem("GagData Saved Successfully!");
                        gagSaved.Dispose();
                    }
                }
            }
        }

    }

    DateTime _lastSaveTime = DateTime.MinValue;
    private void DrawSavedNotification()
    {
        using var tabBar = ImRaii.TabBar("GagStorageEditor");
        if (tabBar)
        {
            var gagSaved = ImRaii.TabItem("Saved");
            if (gagSaved)
            {
                ImGui.Text("Data Saved");
            }
            gagSaved.Dispose();
        }
    }


    private void DrawGagMoodles(float cellPaddingY)
    {
        if (LastCreatedCharacterData == null)
        {
            ImGui.TextWrapped("No Character Data Found. Please select a character to edit.");
            return;
        }

        if (UnsavedDrawData == null)
        {
            SelectedGag = GagList.GagType.BallGag;
            UnsavedDrawData = _clientConfigs.GetDrawData(SelectedGag);
        }

        try
        {
            using var table = ImRaii.Table("MoodlesSelections", 2, ImGuiTableFlags.BordersInnerV);
            if (!table) return;

            ImGui.TableSetupColumn("MoodleSelection", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("FinalizedPreviewList", ImGuiTableColumnFlags.WidthFixed, 200f);

            ImGui.TableNextRow(); ImGui.TableNextColumn();

            using (var child = ImRaii.Child("##RestraintMoodleStatusSelection", new(ImGui.GetContentRegionAvail().X - 1f, ImGui.GetContentRegionAvail().Y / 2), false))
            {
                if (!child) return;
                _relatedMoodles.DrawMoodlesStatusesListForItem(UnsavedDrawData, LastCreatedCharacterData, cellPaddingY, false);
            }
            ImGui.Separator();
            using (var child2 = ImRaii.Child("##RestraintMoodlePresetSelection", -Vector2.One, false))
            {
                if (!child2) return;
                _relatedMoodles.DrawMoodlesStatusesListForItem(UnsavedDrawData, LastCreatedCharacterData, cellPaddingY, true);
            }


            ImGui.TableNextColumn();
            // Filter the MoodlesStatuses list to get only the moodles that are in AssociatedMoodles
            var associatedMoodles = LastCreatedCharacterData.MoodlesStatuses
                .Where(moodle => UnsavedDrawData.AssociatedMoodles.Contains(moodle.GUID))
                .ToList();
            // draw out all the active associated moodles in the restraint set with thier icon beside them.
            UiSharedService.ColorText("Moodles Applied with Set:", ImGuiColors.ParsedPink);
            ImGui.Separator();
            foreach (var moodle in associatedMoodles)
            {
                using (var group = ImRaii.Group())
                {

                    var currentPos = ImGui.GetCursorPos();
                    if (moodle.IconID != 0 && currentPos != Vector2.Zero)
                    {
                        var statusIcon = _uiShared.GetGameStatusIcon((uint)((uint)moodle.IconID + moodle.Stacks - 1));

                        if (statusIcon is { } wrap)
                        {
                            ImGui.SetCursorPos(currentPos);
                            ImGui.Image(statusIcon.ImGuiHandle, MoodlesService.StatusSize);
                        }
                    }
                    ImGui.SameLine();
                    float shiftAmmount = (MoodlesService.StatusSize.Y - ImGui.GetTextLineHeight()) / 2;
                    ImGui.SetCursorPosY(currentPos.Y + shiftAmmount);
                    ImGui.Text(moodle.Title);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error Drawing Moodles Options for Restraint Set.");
        }
    }

    private void DrawGagAudio()
    {

    }

    private void DrawGagGlamour()
    {
        if (UnsavedDrawData == null)
        {
            SelectedGag = GagList.GagType.BallGag;
            UnsavedDrawData = _clientConfigs.GetDrawData(SelectedGag);
        }
        // define icon size and combo length
        IconSize = new Vector2(3 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y*2);
        ComboLength = ComboWidth * ImGuiHelpers.GlobalScale;

        // on the new line, lets draw out a group, containing the image, and the slot, item, and stain listings.
        using (var gagStorage = ImRaii.Group())
        {
            try
            {
                UnsavedDrawData.GameItem.DrawIcon(_textures, IconSize, UnsavedDrawData.Slot);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    Logger.LogTrace($"Item changed to {ItemIdVars.NothingItem(UnsavedDrawData.Slot)} [{ItemIdVars.NothingItem(UnsavedDrawData.Slot).ItemId}] " +
                        $"from {UnsavedDrawData.GameItem} [{UnsavedDrawData.GameItem.ItemId}]");
                    UnsavedDrawData.GameItem = ItemIdVars.NothingItem(UnsavedDrawData.Slot);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to draw gag icon.");
            }
            // right beside it, draw a secondary group of 3
            ImGui.SameLine(0, 6);
            using (var group = ImRaii.Group())
            {
                // display the wardrobe slot for this gag
                var refValue = UnsavedDrawData.ActiveSlotId;
                ImGui.SetNextItemWidth(ComboLength);
                if (ImGui.Combo(" Equipment Slot##WardrobeEquipSlot", ref refValue,
                    EquipSlotExtensions.EqdpSlots.Select(slot => slot.ToName()).ToArray(), EquipSlotExtensions.EqdpSlots.Count))
                {
                    // Update the selected slot when the combo box selection changes
                    UnsavedDrawData.Slot = EquipSlotExtensions.EqdpSlots[refValue];
                    // reset display and/or selected item to none.
                    UnsavedDrawData.GameItem = ItemIdVars.NothingItem(UnsavedDrawData.Slot);
                }

                DrawEquip(GameItemCombo, StainCombo, StainData, ComboLength);
            }
        }

        ImGui.Separator();
        _uiShared.BigText("Adjustments");

        var refEnabled = UnsavedDrawData!.IsEnabled;
        if (ImGui.Checkbox("Enable "+SelectedGag.GetGagAlias(), ref refEnabled))
        {
            UnsavedDrawData.IsEnabled = refEnabled;
            Logger.LogTrace($"Gag {SelectedGag.GetGagAlias()} is now {(UnsavedDrawData.IsEnabled ? "enabled" : "disabled")}");
        }
        _uiShared.DrawHelpText("When enabled, allows Item-AutoEquip to function with this Gag."+Environment.NewLine
            + "When disabled, this Gag Glamour will not be auto equipped, even with Item Auto-Equip on.");

        var refHelmetForced = UnsavedDrawData.ForceHeadgearOnEnable;
        if (ImGui.Checkbox($"Force Headgear", ref refHelmetForced))
        {
            UnsavedDrawData.ForceHeadgearOnEnable = refHelmetForced;
            Logger.LogTrace($"Gag {SelectedGag.GetGagAlias()} will now {(UnsavedDrawData.ForceHeadgearOnEnable ? "force headgear on" : "not force headgear on")} when enabled");
        }
        _uiShared.DrawHelpText("When enabled, your [Hat Visible] property in Glamourer will be set to enabled. Making headgear visible.");

        var refVisorForced = UnsavedDrawData.ForceVisorOnEnable;
        if (ImGui.Checkbox($"Force Visor", ref refVisorForced))
        {
            UnsavedDrawData.ForceVisorOnEnable = refVisorForced;
            Logger.LogTrace($"Gag {SelectedGag.GetGagAlias()} will now {(UnsavedDrawData.ForceVisorOnEnable ? "force visor on" : "not force visor on")} when enabled");
        }
        _uiShared.DrawHelpText("When enabled, your [Visor Visible] property in Glamourer will be set to enabled. Making visor visible.");

        ImGui.Spacing();
        ImGui.TextUnformatted("Customize+ Preset selection added here once posing is less of a pain in the ass.");
    }

    public void DrawEquip(GameItemCombo[] _gameItemCombo, StainColorCombo _stainCombo, DictStain _stainData, float _comboLength)
    {
        using var id = ImRaii.PushId((int)UnsavedDrawData.Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var left = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        using var group = ImRaii.Group();
        DrawItem(out var label, right, left, _comboLength, _gameItemCombo);
        DrawStain(_comboLength, _stainCombo, _stainData);
    }

    private void DrawItem(out string label, bool clear, bool open, float width,
    GameItemCombo[] _gameItemCombo)
    {
        // draw the item combo.
        var combo = _gameItemCombo[UnsavedDrawData.Slot.ToIndex()];
        label = combo.Label;
        if (open)
        {
            GenericHelpers.OpenCombo($"##GagShelfItem{SelectedGag}{combo.Label}");
            Logger.LogTrace($"{combo.Label} Toggled");
        }
        // draw the combo
        var change = combo.Draw(UnsavedDrawData.GameItem.Name, UnsavedDrawData.GameItem.ItemId, width, ComboWidth, " Gag Glamour Item");

        // conditionals to detect for changes in the combo's
        if (change && !UnsavedDrawData.GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            Logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ItemId}] " +
                $"to {UnsavedDrawData.GameItem} [{UnsavedDrawData.GameItem.ItemId}]");
            UnsavedDrawData.GameItem = combo.CurrentSelection;
        }

        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            Logger.LogTrace($"Item changed to {ItemIdVars.NothingItem(UnsavedDrawData.Slot)} [{ItemIdVars.NothingItem(UnsavedDrawData.Slot).ItemId}] " +
                $"from {UnsavedDrawData.GameItem} [{UnsavedDrawData.GameItem.ItemId}]");
            UnsavedDrawData.GameItem = ItemIdVars.NothingItem(UnsavedDrawData.Slot);
        }
    }

    private void DrawStain(float width, StainColorCombo _stainCombo, DictStain _stainData)
    {
        // fetch the correct stain from the stain data
        var widthStains = (width - ImUtf8.ItemInnerSpacing.X * (UnsavedDrawData.GameStain.Count - 1)) / UnsavedDrawData.GameStain.Count;

        foreach (var (stainId, index) in UnsavedDrawData.GameStain.WithIndex())
        {
            using var id = ImUtf8.PushId(index);
            var found = _stainData.TryGetValue(stainId, out var stain);
            // draw the stain combo.
            var change = _stainCombo.Draw($"##stain{UnsavedDrawData.Slot}", stain.RgbaColor, stain.Name, found, stain.Gloss, widthStains);
            if (index < UnsavedDrawData.GameStain.Count - 1)
                ImUtf8.SameLineInner(); // instantly go to draw the next one.

            // if we had a change made, update the stain data.
            if (change)
            {
                if (_stainData.TryGetValue(_stainCombo.CurrentSelection.Key, out stain))
                {
                    // if changed, change it.
                    UnsavedDrawData.GameStain = UnsavedDrawData.GameStain.With(index, stain.RowIndex);
                }
                else if (_stainCombo.CurrentSelection.Key == Stain.None.RowIndex)
                {
                    // if set to none, reset it to default
                    UnsavedDrawData.GameStain = UnsavedDrawData.GameStain.With(index, Stain.None.RowIndex);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                // reset the stain to default
                UnsavedDrawData.GameStain = UnsavedDrawData.GameStain.With(index, Stain.None.RowIndex);
            }
        }
        ImUtf8.SameLineInner();
        ImGui.Text(" Applied Dyes");
    }
}
