using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
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
    private const float ComboWidth = 200;

    private readonly IDataManager _gameData;
    private readonly TextureService _textures;
    private readonly ISharedImmediateTexture _sharedTexture;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly UiSharedService _uiShared;
    private readonly DictStain StainData;
    private readonly ItemData ItemData;

    private GagDrawData UnsavedDrawData = null!;
    private GagList.GagType SelectedGag;
    private LowerString GagSearchString = LowerString.Empty;
    private Vector2 IconSize;
    private float ComboLength;
    private Vector2 DefaultItemSpacing;
    private readonly GameItemCombo[] GameItemCombo;
    private readonly StainColorCombo StainCombo;

    private bool hasShown = false;

    public GagStoragePanel(ILogger<GagStoragePanel> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, UiSharedService uiSharedService,
        DictStain stainData, ItemData itemData, TextureService textures,
        IDataManager gameData) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _uiShared = uiSharedService;
        _textures = textures;
        _gameData = gameData;
        StainData = stainData;
        ItemData = itemData;

        // create a new gameItemCombo for each equipment piece type, then store them into the array.
        GameItemCombo = EquipSlotExtensions.EqdpSlots.Select(e => new GameItemCombo(_gameData, e, ItemData, Logger)).ToArray();
        StainCombo = new StainColorCombo(ComboWidth - 20, StainData, Logger);
    }

    public void DrawGagStoragePanel()
    {
        try
        {
            if (UnsavedDrawData == null && !hasShown)
            {
                SelectedGag = GagList.GagType.None;
                UnsavedDrawData = _clientConfigs.GetDrawData(SelectedGag);
            }
        }
        catch (Exception e)
        {
            hasShown = true;
            Logger.LogError(e, "Failed to get gag data.");
            // print the details of the current dictionary to the logger.
            foreach (var gag in _clientConfigs.GagStorageConfig.GagStorage.GagEquipData)
            {
                Logger.LogDebug($"Gag: {gag.Key}");
                Logger.LogDebug($"Item: {gag.Value.IsEnabled}");
                Logger.LogDebug($"Slot: {gag.Value.Slot}");
                Logger.LogDebug($"GameItem: {gag.Value.GameItem}");
                Logger.LogDebug($"GameStain: {gag.Value.GameStain}");
            }
            return;
        }
        // define icon size and combo length
        IconSize = new Vector2(3 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y*2);
        ComboLength = ComboWidth * ImGuiHelpers.GlobalScale;

        // create a secondary table in this for prettiness
        using (var table2 = ImRaii.Table("GagDrawerCustomizerHeader", 2))
        {
            // do not continue if table not valid
            if (!table2) return;

            // setup columns.
            ImGui.TableSetupColumn("##StorageFilterList", ImGuiTableColumnFlags.WidthFixed, 160 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("##GagItemConfig", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow(); ImGui.TableNextColumn();
            // draw out the filter list
            DrawGagFilterList(160f);

            ImGui.TableNextColumn();

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 5);

            // draw out the configuration
            _uiShared.BigText("Gag Glamour");

            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 7.5f);
            ImGui.AlignTextToFramePadding();
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                _clientConfigs.UpdateGagItem(SelectedGag, UnsavedDrawData);
            }
            ImGui.NewLine();

            // on the new line, lets draw out a group, containing the image, and the slot, item, and stain listings.
            using (var gagStorage = ImRaii.Group())
            {

                // draw out the listing for the slot, item, and stain(s). Also make sure that the bigtext it centered with the displayitem
                try
                {
                    UnsavedDrawData.GameItem.DrawIcon(_textures, IconSize, UnsavedDrawData.Slot);
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
                    if (ImGui.Combo(" Slot##WardrobeEquipSlot", ref refValue,
                        EquipSlotExtensions.EqdpSlots.Select(slot => slot.ToName()).ToArray(), EquipSlotExtensions.EqdpSlots.Count))
                    {
                        // Update the selected slot when the combo box selection changes
                        UnsavedDrawData.Slot = EquipSlotExtensions.EqdpSlots[refValue];
                        UnsavedDrawData.ActiveSlotId = EquipSlotExtensions.EqdpSlots.Select((s, i) => new { s, i })
                                        .FirstOrDefault(x => x.s == UnsavedDrawData.Slot)?.i ?? -1;
                        // reset display and/or selected item to none.
                        UnsavedDrawData.GameItem = ItemIdVars.NothingItem(UnsavedDrawData.Slot);
                    }

                    DrawEquip(GameItemCombo, StainCombo, StainData, ComboLength);
                }
            }

            // draw out the configuration
            _uiShared.BigText("Customize+ Preset");

            ImGui.NewLine();
            ImGui.TextWrapped($"Select the Customize+ Preset you want to keep applied while the gag is worn:");

            // attached audio when worn
            _uiShared.BigText("Spacial Audio");

            ImGui.TextWrapped($"Select the kind of gagged audio you want to play while gagged:");

            // draw debug metrics
            ImGui.NewLine();
            ImGui.Text($"Gag Name: {SelectedGag.GetGagAlias()}");
            ImGui.Text($"IsEnabled: {UnsavedDrawData.IsEnabled}");
            ImGui.Text($"Slot: {UnsavedDrawData.Slot}");
            ImGui.Text($"GameItem: {UnsavedDrawData.GameItem}");
            ImGui.Text($"GameItemID: {UnsavedDrawData.GameItem.ItemId}");
            ImGui.Text($"GameStain: {UnsavedDrawData.GameStain}");
            ImGui.Text($"ActiveSlotListId: {UnsavedDrawData.ActiveSlotId}");
        }
    }
    #region GagSelector
    public void DrawGagFilterList(float width)
    {
        using var group = ImRaii.Group();
        DefaultItemSpacing = ImGui.GetStyle().ItemSpacing;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero).Push(ImGuiStyleVar.FrameRounding, 0);
        ImGui.SetNextItemWidth(width);
        LowerString.InputWithHint("##gagFilter", "Filter Gags...", ref GagSearchString, 64);

        DrawGagSelector(width);
    }

    private void DrawGagSelector(float width)
    {
        using var child = ImRaii.Child("##GagSelector", new Vector2(width, ImGui.GetContentRegionAvail().Y), true, ImGuiWindowFlags.NoScrollbar);
        if (!child)
            return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, DefaultItemSpacing);
        bool itemSelected = false; // Flag to check if an item has been selected
        GagList.GagType? newlySelectedGag = null; // Temporarily store the newly selected gag

        foreach (var gag in Enum.GetValues(typeof(GagList.GagType)).Cast<GagList.GagType>())
        {
            if (gag == GagList.GagType.None) continue;
            // Determine if the gag should be shown based on the search string
            bool showGag = GagSearchString.IsEmpty || gag.ToString().Contains(GagSearchString.Lower, StringComparison.OrdinalIgnoreCase);

            if (showGag)
            {
                bool isSelected = SelectedGag.Equals(gag);
                if (ImGui.Selectable(gag.ToString(), isSelected))
                {
                    newlySelectedGag = gag; // Update the temporary selection
                    itemSelected = true; // Mark that an item has been selected
                }
            }
        }

        // If an item was selected during this ImGui frame, update the SelectedGag
        if (itemSelected && newlySelectedGag.HasValue)
        {
            SelectedGag = newlySelectedGag.Value;
            UnsavedDrawData = _clientConfigs.GetDrawData(SelectedGag);
        }
    }
    #endregion GagSelector

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
        var change = combo.Draw(UnsavedDrawData.GameItem.Name, UnsavedDrawData.GameItem.ItemId, width, ComboWidth, " Item");

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
        ImGui.Text(" Dyes");
    }
}
