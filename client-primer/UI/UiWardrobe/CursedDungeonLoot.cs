using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.IpcHelpers.Moodles;
using GagSpeak.Interop.IpcHelpers.Penumbra;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.Utils;
using GagspeakAPI.Data.IPC;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.UiWardrobe;

public class CursedDungeonLoot : DisposableMediatorSubscriberBase
{
    private readonly PlayerCharacterData _clientPlayerData;
    private readonly SetPreviewComponent _drawDataHelper;
    private readonly ModAssociations _relatedMods;
    private readonly MoodlesAssociations _relatedMoodles;
    private readonly CursedLootHandler _handler;
    private readonly GagspeakConfigService _mainConfig;
    private readonly UiSharedService _uiShared;

    public CursedDungeonLoot(ILogger<CursedDungeonLoot> logger,
        GagspeakMediator mediator, PlayerCharacterData clientPlayerData, 
        SetPreviewComponent drawDataHelper, ModAssociations relatedMods, 
        MoodlesAssociations relatedMoodles, CursedLootHandler handler, 
        GagspeakConfigService mainConfig,  UiSharedService uiShared) 
        : base(logger, mediator)
    {
        _clientPlayerData = clientPlayerData;
        _drawDataHelper = drawDataHelper;
        _relatedMods = relatedMods;
        _relatedMoodles = relatedMoodles;
        _handler = handler;
        _mainConfig = mainConfig;
        _uiShared = uiShared;
    }

    // private vars used for searches or temp storage for handling hovers and value changes.
    private LowerString ItemSearchStr = LowerString.Empty;
    private int HoveredItemIndex = -1;
    private int ExpandedItemIndex = -1;
    private int HoveredCursedPoolIdx = -1;
    private bool CreatorExpanded = false;
    private string? TempLowerTimerRange = null;
    private string? TempUpperTimerRange = null;
    private List<CursedItem> FilteredItemList
    {
        get => _handler.CursedItems
            .Where(set => set.Name.Contains(ItemSearchStr, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private CursedItem? NewItem = null; // is null when not creating?

    public void DrawCursedLootPanel()
    {
        // inform user they dont have the settings enabled for it, and return.
        if (!_mainConfig.Current.CursedDungeonLoot)
        {
            _uiShared.BigText("Must Enable Cursed Dungeon Loot");
            UiSharedService.ColorText("This can be found in the Global GagSpeak Settings", ImGuiColors.ParsedGold);
            return;
        }

        if (NewItem is null) 
            NewItem = new CursedItem();

        // split thye UI veritcally in half. The right side will display the sets in the pool and the 
        var region = ImGui.GetContentRegionAvail();
        var topLeftSideHeight = region.Y;
        var width = ImGui.GetContentRegionAvail().X * .55f;
        using (ImRaii.Table("CursedLootItems", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
        {
            // setup the columns
            ImGui.TableSetupColumn("CursedItemList", ImGuiTableColumnFlags.WidthFixed, width);
            ImGui.TableSetupColumn("ActiveAndEnabledCursedItems", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow(); ImGui.TableNextColumn();

            var regionSize = ImGui.GetContentRegionAvail();

            using (ImRaii.Child($"###CursedItemsList", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
            {
                CursedLootHeader("Your Cursed Items");
                ImGui.Separator();
                DrawSearchFilter(regionSize.X, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.Separator();
                // draw the new cursed item display, this should be in grey, and unique.
                if (CustomSelectable(NewItem, false, isExpanded: CreatorExpanded, isNewItem: true))
                {
                    CreatorExpanded = !CreatorExpanded;
                }

                // if we have any cursed items, we should draw them
                if (_handler.CursedItems.Count > 0)
                {
                    bool itemGotHovered = false;
                    for (int i = 0; i < FilteredItemList.Count; i++)
                    {
                        var item = FilteredItemList[i];
                        bool isHovered = i == HoveredItemIndex;
                        if (CustomSelectable(item, isHovered, isExpanded: ExpandedItemIndex == i))
                        {
                            if(ExpandedItemIndex == i)
                                ExpandedItemIndex = -1;
                            else
                                ExpandedItemIndex = i;
                        }
                        if (!(ExpandedItemIndex == i) && ImGui.IsItemHovered())
                        {
                            itemGotHovered = true;
                            HoveredItemIndex = i;
                        }
                    }
                    if (!itemGotHovered)
                    {
                        HoveredItemIndex = -1;
                    }
                }
            }
            ImGui.TableNextColumn();

            regionSize = ImGui.GetContentRegionAvail();
            using (ImRaii.Child($"###ActiveCursedSets", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
            {
                CursedLootHeader("Enabled Pool");
                ImGui.Separator();
                DrawLockRangesAndChance(regionSize.X);
                ImGui.Separator();
                if (_handler.ItemsInPool.Count > 0)
                {
                    bool activeCursedItemGotHovered = false;
                    for (int i = 0; i < _handler.ItemsInPool.Count; i++)
                    {
                        var item = _handler.ItemsInPool[i];
                        bool isHovered = i == HoveredCursedPoolIdx;

                        CustomSelectable(item, isHovered, drawingEnabledPool: true);
                        if (ImGui.IsItemHovered())
                        {
                            activeCursedItemGotHovered = true;
                            HoveredCursedPoolIdx = i;
                        }
                    }
                    if (!activeCursedItemGotHovered)
                    {
                        HoveredCursedPoolIdx = -1;
                    }
                }
            }
        }
    }

    public bool CustomSelectable(CursedItem item, bool isHovered, bool isExpanded = false, bool isNewItem = false, bool drawingEnabledPool = false)
    {
        var wasSelected = false;

        using var borderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 4f);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(4, 4));
        using var borderCol = isNewItem
            ? ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.DalamudGrey)
            : ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink);
        // push a less transparent very dark grey background color.
        using var bgColor = isHovered
            ? ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered))
            : ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.25f, 0.2f, 0.2f, 0.4f));

        var caretButtonSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.CaretDown);
        var nameTextSize = ImGui.CalcTextSize(item.Name);

        var selectableSize = isExpanded
            ? new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeight() * 9 + ImGui.GetStyle().ItemSpacing.Y * 11 + ImGui.GetStyle().WindowPadding.Y * 2)
            : new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2);

        using (ImRaii.Child($"##CustomSelectable" + item.Name + item.LootId, selectableSize, true))
        {
            // if we are drawing the item as an item not in the list, we need to 
            using (ImRaii.Group())
            {
                // display name, then display the downloads and likes on the other side.
                var yPos = ImGui.GetCursorPosY();

                // draw out the item name. if we are editing it will be displayed differently.
                if (isExpanded)
                {
                    ImGui.SetCursorPosY(yPos + ((ImGui.GetFrameHeight() - 23) / 2) + 0.5f); // 23 is the input text box height
                    ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
                    var itemName = item.Name;
                    if (ImGui.InputTextWithHint("##ItemName" + item.LootId, "Item Name...", ref itemName, 36, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        item.Name = itemName;
                    }
                }
                else
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(item.Name);
                }

                float width = 0f;
                
                if(!drawingEnabledPool)
                    width += caretButtonSize.X;

                // add to width if we are not creating a new item, add another for adding to and from the pool.
                if (!isNewItem && (item.InPool && drawingEnabledPool || !item.InPool && !drawingEnabledPool))
                    width += caretButtonSize.X + ImGui.GetStyle().ItemInnerSpacing.X;

                // if we are expanded and making a new item, we should add the width for the save button.
                if (isExpanded && isNewItem)
                    width += caretButtonSize.X + ImGui.GetStyle().ItemInnerSpacing.X;

                // if we are expanded and we are not creating a new item, add the option to delete the item.
                if (isExpanded && !isNewItem && !item.InPool)
                    width += caretButtonSize.X + ImGui.GetStyle().ItemInnerSpacing.X;

                ImGui.SameLine(ImGui.GetContentRegionAvail().X - width);

                using (ImRaii.PushColor(ImGuiCol.Text, isExpanded ? (isNewItem ? ImGuiColors.DalamudWhite : ImGuiColors.ParsedPink) : ImGuiColors.DalamudGrey))
                {
                    // if we expanded, we should draw the save button.
                    if (isExpanded && isNewItem)
                    {
                        if (_uiShared.IconButton(FontAwesomeIcon.Plus, disabled: item.AppliedTime != DateTimeOffset.MinValue, inPopup: true))
                        {
                            _handler.AddItem(item);
                            NewItem = null; // reset the new item.
                            Logger.LogInformation("Saving this item!");
                        }
                        UiSharedService.AttachToolTip("Add this Cursed Item!");
                        ImUtf8.SameLineInner();
                    }

                    // if we are not creating a new item, we should draw the add to pool button.
                    if (!isNewItem && (item.InPool && drawingEnabledPool || !item.InPool && !drawingEnabledPool))
                    {
                        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGold))
                        {
                            var icon = item.InPool ? FontAwesomeIcon.ArrowLeft : FontAwesomeIcon.ArrowRight;
                            if (_uiShared.IconButton(icon, disabled: item.AppliedTime != DateTimeOffset.MinValue, inPopup: true))
                            {
                                item.InPool = !item.InPool;
                                _handler.Save();
                            }
                            UiSharedService.AttachToolTip(item.InPool
                                ? "Remove this Item to the Cursed Loot Pool!"
                                : "Add this Item to the Cursed Loot Pool!");
                        }
                        ImUtf8.SameLineInner();
                    }

                    // add a button for pushing to and from the pool.
                    if (isExpanded && !isNewItem && !item.InPool)
                    {
                        if (_uiShared.IconButton(FontAwesomeIcon.Trash, disabled: item.AppliedTime != DateTimeOffset.MinValue || !UiSharedService.ShiftPressed(), inPopup: true))
                        {
                            _handler.RemoveItem(item.LootId);
                            Logger.LogInformation("Removing this item!");
                        }
                        UiSharedService.AttachToolTip("Remove this Cursed Item from your storage! (Hold Shift)");
                        ImUtf8.SameLineInner();
                    }

                    // add the carot button to expand the item.
                    if(!drawingEnabledPool)
                    {
                        if (_uiShared.IconButton(isExpanded ? FontAwesomeIcon.CaretUp : FontAwesomeIcon.CaretDown, inPopup: true))
                        {
                            wasSelected = true;
                        }
                    }
                }
            }

            // draw out additional info if it was selected:
            if (isExpanded)
            {
                // define some of the basic options.
                var canOverride = item.CanOverride;
                if(ImGui.Checkbox("Can Be Overridden", ref canOverride))
                {
                    item.CanOverride = canOverride;
                    _handler.Save();
                }   
                UiSharedService.AttachToolTip("If this item can be overridden by another cursed item in the pool." + Environment.NewLine
                    + "(Must have a higher Precedence to do so)");

                ImGui.SameLine();
                var precedence = item.OverridePrecedence;
                _uiShared.DrawCombo("##ItemPrecedence", ImGui.GetContentRegionAvail().X, Enum.GetValues<Precedence>(), 
                    (clicked) => clicked.ToName(),
                    onSelected: (i) => 
                    { 
                        item.OverridePrecedence = i; 
                        _handler.Save(); 
                    }, 
                    initialSelectedItem: item.OverridePrecedence);
                UiSharedService.AttachToolTip("The Precedence of this item when comparing to other items in the pool." + Environment.NewLine
                    + "Items with higher Precedence will be layered ontop of items in the same slot with lower Precedence.");



                // Define the related Glamour Item
                ImGui.Separator();
                _drawDataHelper.DrawEquipDataDetailedSlot(item.AppliedItem, ImGui.GetContentRegionAvail().X);
                UiSharedService.AttachToolTip("The Item that will be applied to the player when this Cursed Item is active.");

                // Define the related Mod
                ImGui.Separator();
                using (ImRaii.Group())
                {
                    var buttonWidth = _uiShared.GetIconButtonSize(FontAwesomeIcon.Redo).X + _uiShared.GetIconButtonSize(FontAwesomeIcon.Times).X;
                    _relatedMods.DrawCursedItemSelection(item, ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemInnerSpacing.Y - _uiShared.GetIconButtonSize(FontAwesomeIcon.VoteYea).X);
                    ImUtf8.SameLineInner();
                    if(_uiShared.IconButton(FontAwesomeIcon.VoteYea, disabled: _relatedMods.CurrentSelection.Mod.Name.IsNullOrEmpty()))
                    {
                        item.AssociatedMod.Mod = _relatedMods.CurrentSelection.Mod;
                        item.AssociatedMod.ModSettings = _relatedMods.CurrentSelection.Settings;
                        _handler.Save();
                    }
                    UiSharedService.AttachToolTip("Make this mod bound to the cursed Item.");

                    string truncatedText = item.AssociatedMod.Mod.Name.IsNullOrEmpty() 
                        ? "No Mod Selected" 
                        : (item.AssociatedMod.Mod.Name.Length > 33)
                            ? item.AssociatedMod.Mod.Name.Substring(0, 33) + "..." 
                            : item.AssociatedMod.Mod.Name;

                    ImGui.AlignTextToFramePadding();
                    UiSharedService.ColorText(" " + truncatedText, ImGuiColors.ParsedGold);
                    UiSharedService.AttachToolTip(item.AssociatedMod.Mod.Name.IsNullOrEmpty()
                        ? "Select a Mod from the list above first."
                        : "The Selected Mod bound to this cursed Item.\nFull Name: " + item.AssociatedMod.Mod.Name);

                    // use sameline to jump to the end minus two button width.
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemInnerSpacing.X - buttonWidth);

                    // set icon and help text
                    var icon = item.AssociatedMod.DisableWhenInactive ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                    var helpText = item.AssociatedMod.DisableWhenInactive ? "Mod will be disabled once the cursed item is removed." : "Mod will stay enabled once the cursed item is removed.";
                    if (_uiShared.IconButton(icon, disabled: item.AssociatedMod.Mod.Name.IsNullOrEmpty()))
                    {
                        item.AssociatedMod.DisableWhenInactive = !item.AssociatedMod.DisableWhenInactive;
                        _handler.Save();
                    }
                    UiSharedService.AttachToolTip(helpText);

                    ImUtf8.SameLineInner();

                    var icon2 = item.AssociatedMod.RedrawAfterToggle ? FontAwesomeIcon.Redo : FontAwesomeIcon.Slash;
                    var helpText2 = item.AssociatedMod.RedrawAfterToggle ? "Perform a redraw after Cursed Item is Applied/Removed (nessisary for VFX/Animation Mods)" : "Perform no redraw on item apply/remove";
                    if (_uiShared.IconButton(icon2, disabled: item.AssociatedMod.Mod.Name.IsNullOrEmpty()))
                    {
                        item.AssociatedMod.RedrawAfterToggle = !item.AssociatedMod.RedrawAfterToggle;
                        _handler.Save();
                    }
                    UiSharedService.AttachToolTip(helpText2);
                }

                if(!_clientPlayerData.IpcDataNull)
                {
                    // Define the related Moodle
                    ImGui.Separator();
                    _uiShared.DrawCombo("##CursedItemMoodleType", 90f, Enum.GetValues<IpcToggleType>(), 
                        (clicked) => clicked.ToName(),
                        onSelected: (i) => 
                        { 
                            item.MoodleType = i; 
                            item.MoodleIdentifier = Guid.Empty;
                            _handler.Save();
                        }, 
                        initialSelectedItem: item.MoodleType, 
                        flags: ImGuiComboFlags.NoArrowButton);
                    UiSharedService.AttachToolTip("The type of Moodle to apply with this item.");

                    ImUtf8.SameLineInner();
                    _relatedMoodles.MoodlesStatusSelectorForCursedItem(item, _clientPlayerData.LastIpcData!, ImGui.GetContentRegionAvail().X);

                    // in new line, display the current moodle status or preset.
                    var moodleText = string.Empty;
                    if(item.MoodleType is IpcToggleType.MoodlesStatus)
                    {
                        var title = _clientPlayerData.LastIpcData!.MoodlesStatuses.FirstOrDefault(x => x.GUID == item.MoodleIdentifier).Title;
                        if(!title.IsNullOrEmpty())
                            moodleText = "Status: " + title.StripColorTags();
                        else
                            moodleText = (item.MoodleIdentifier == Guid.Empty ? "None Selected" : "ERROR");
                    }
                    else
                    {
                        moodleText = "Preset: " + (item.MoodleIdentifier == Guid.Empty ? "None Selected" : item.MoodleIdentifier);
                    }
                    ImGui.AlignTextToFramePadding();
                    UiSharedService.ColorText(moodleText, ImGuiColors.ParsedGold);
                    // if the identifier is valid and we are hovering it.
                    if (item.MoodleIdentifier != Guid.Empty && ImGui.IsItemHovered())
                    {
                        if(item.MoodleType is IpcToggleType.MoodlesStatus)
                        {
                            ImGui.SetTooltip("This Moodle Status will be applied with the item.");
                        }

                        if(item.MoodleType is IpcToggleType.MoodlesPreset)
                        {
                            // get the list of friendly names for each guid in the preset
                            var test = _clientPlayerData.LastIpcData!.MoodlesPresets
                                .FirstOrDefault(x => x.Item1 == item.MoodleIdentifier).Item2
                                .Select(x => _clientPlayerData.LastIpcData.MoodlesStatuses
                                .FirstOrDefault(y => y.GUID == x).Title ?? "No FriendlyName Set for this Moodle") ?? new List<string>();
                            ImGui.SetTooltip($"This Preset Enables the Following Moodles:\n" + string.Join(Environment.NewLine, test));
                        }
                    }
                }
            }
        }
        return wasSelected;
    }

    private void CursedLootHeader(string text)
    {
        var startYpos = ImGui.GetCursorPosY();
        Vector2 textSize;
        using (_uiShared.UidFont.Push()) { textSize = ImGui.CalcTextSize(text); }
        using (ImRaii.Child("CursedSetsHeader" + text, new Vector2(UiSharedService.GetWindowContentRegionWidth(), 40)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X / 2 - textSize.X / 2));
            ImGui.SetCursorPosY(startYpos + 3f);
            _uiShared.BigText(text);
        }
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawSearchFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = ItemSearchStr;
        if (ImGui.InputTextWithHint("##CursedItemFilter", "Search your Cursed Items", ref filter, 255))
        {
            ItemSearchStr = filter;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(ItemSearchStr));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            ItemSearchStr = string.Empty;
        }
    }

    public void DrawLockRangesAndChance(float availableWidth)
    {
        // Define the widths for input fields and the slider
        float inputWidth = (availableWidth - _uiShared.GetIconData(FontAwesomeIcon.HourglassHalf).X - ImGui.GetStyle().ItemInnerSpacing.X * 2 - ImGui.CalcTextSize("100.9%  ").X) / 2;

        // Input Field for the first range
        ImGui.SetNextItemWidth(inputWidth);
        var spanLow = _handler.LowerLockLimit;
        TempLowerTimerRange = spanLow == TimeSpan.Zero ? string.Empty : _uiShared.TimeSpanToString(spanLow);
        if (ImGui.InputTextWithHint("##Timer_Input_Lower", "Ex: 0h2m7s", ref TempLowerTimerRange, 12, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (_uiShared.TryParseTimeSpan(TempLowerTimerRange, out var timeSpan))
                _handler.SetLowerLimit(timeSpan);
        }
        UiSharedService.AttachToolTip("Min Cursed Lock Time.");

        ImUtf8.SameLineInner();
        _uiShared.IconText(FontAwesomeIcon.HourglassHalf, ImGuiColors.ParsedGold);
        ImUtf8.SameLineInner();
        // Input Field for the second range
        ImGui.SetNextItemWidth(inputWidth);
        var spanHigh = _handler.UpperLockLimit;
        TempUpperTimerRange = spanHigh == TimeSpan.Zero ? string.Empty : _uiShared.TimeSpanToString(spanHigh);
        if (ImGui.InputTextWithHint("##Timer_Input_Upper", "Ex: 0h2m7s", ref TempUpperTimerRange, 12, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (_uiShared.TryParseTimeSpan(TempUpperTimerRange, out var timeSpan))
                _handler.SetUpperLimit(timeSpan);
        }
        UiSharedService.AttachToolTip("Max Cursed lock Time.");

        ImUtf8.SameLineInner();
        // Slider for percentage adjustment
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var percentage = _handler.LockChance;
        if (ImGui.DragInt("##Percentage", ref percentage, 0.1f, 0, 100, "%d%%"))
        {
            _handler.SetLockChance(percentage);
        }
        UiSharedService.AttachToolTip("The % Chance that opening Dungeon Loot will contain Cursed Bondage Loot.");
    }
}
