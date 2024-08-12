using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using System;
using System.Globalization;
using System.Numerics;
using static PInvoke.User32;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetManager
{
    private readonly ILogger<RestraintSetManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly RestraintSetEditor _editor;
    private readonly WardrobeHandler _handler;
    private readonly TextureService _textures;
    private readonly DictStain _stainDictionary;

    public RestraintSetManager(ILogger<RestraintSetManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        RestraintSetEditor editor, WardrobeHandler handler, 
        TextureService textureService, DictStain stainDictionary)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _editor = editor;
        _handler = handler;
        _textures = textureService;
        _stainDictionary = stainDictionary;

        GameIconSize = new Vector2(2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
        StainColorCombos = new StainColorCombo(0, _stainDictionary, logger);
    }

    private Vector2 GameIconSize;
    private readonly StainColorCombo StainColorCombos;

    private RestraintSet CreatedRestraintSet = new RestraintSet();
    public bool CreatingRestraintSet = false;
    private string LockTimerInputString = string.Empty;

    private LowerString RestraintSetSearchString = LowerString.Empty;
    private List<RestraintSet> FilteredSetList
        => _handler.GetAllSetsForSearch()
            .Where(set => set.Name.Contains(RestraintSetSearchString, StringComparison.OrdinalIgnoreCase))
            .ToList();
    private List<bool> ListItemHovered = new List<bool>();


    public void DrawManageSets(Vector2 cellPadding)
    {
        // if we are creating a pattern
        if (CreatingRestraintSet)
        {
            DrawRestraintSetCreatingHeader();
            ImGui.Separator();
            _editor.DrawRestraintSetEditor(CreatedRestraintSet, cellPadding);
            return; // perform early returns so we dont access other methods
        }

        // if we are simply viewing the main page       
        if (_handler.EditingSetNull)
        {
            DrawSetListing(cellPadding);
            return; // perform early returns so we dont access other methods
        }

        // if we are editing an restraintSet
        if (!_handler.EditingSetNull)
        {
            DrawRestraintSetEditorHeader();
            ImGui.Separator();
            if (_handler.RestraintSetListSize() > 0 && _handler.EditingSetIndex >= 0)
            {
                _editor.DrawRestraintSetEditor(_handler.SetBeingEdited, cellPadding);
            }
        }
    }

    private void DrawSetListing(Vector2 cellPadding)
    {
        var region = ImGui.GetContentRegionAvail();
        var topLeftSideHeight = region.Y;

        using (var managerTable = ImRaii.Table("RestraintsManagerTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
        {
            if (!managerTable) return;
            // setup the columns
            ImGui.TableSetupColumn("SetList", ImGuiTableColumnFlags.WidthFixed, 300f);
            ImGui.TableSetupColumn("PreviewSet", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow(); ImGui.TableNextColumn();

            var regionSize = ImGui.GetContentRegionAvail();

            using (var leftChild = ImRaii.Child($"###SelectableListWardrobe", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
            {
                DrawCreateRestraintSetHeader();
                ImGui.Separator();
                DrawSearchFilter(regionSize.X, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.Separator();
                if (_handler.RestraintSetListSize() > 0)
                {
                    DrawRestraintSetSelectableMenu();
                }
            }

            ImGui.TableNextColumn();

            // grab the index in the highlighted set that is true
            var highlightedIndex = ListItemHovered.FindIndex(x => x.Equals(true));
            // if non are, return
            if (highlightedIndex == -1) return;

            // otherwise, draw.
            DrawRestraintSetPreview(FilteredSetList[highlightedIndex]);
        }
    }

    private void DrawCreateRestraintSetHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize("New RestraintSet");
        }
        var centerYpos = (textSize.Y - iconSize.Y);

        using (ImRaii.Child("CreateRestraintSetHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // draw out the icon button
            if (_uiShared.IconButton(FontAwesomeIcon.Plus))
            {
                // reset the createdRestraintSet to a new restraintSet, and set editing restraintSet to true
                CreatedRestraintSet = new RestraintSet();
                CreatingRestraintSet = true;
            }
            UiSharedService.AttachToolTip("Create a new Restraint Set");
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            _uiShared.BigText("New RestraintSet");
        }
    }

    private void DrawRestraintSetCreatingHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"Creating RestraintSet: {CreatedRestraintSet.Name}");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("EditRestraintSetHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdRestraintSet to a new restraintSet, and set editing restraintSet to true
                CreatedRestraintSet = new RestraintSet();
                CreatingRestraintSet = false;
            }
            UiSharedService.AttachToolTip("Exit to Restraint Set List");
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText(CreatedRestraintSet.Name, ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // the "fuck go back" button.
            using (var disabled = ImRaii.Disabled(CreatedRestraintSet.Name == string.Empty))
            {
                if (_uiShared.IconButton(FontAwesomeIcon.Save))
                {
                    // add the newly created restraintSet to the list of restraintSets
                    _handler.AddNewRestraintSet(CreatedRestraintSet);
                    // reset to default and turn off creating status.
                    CreatedRestraintSet = new RestraintSet();
                    CreatingRestraintSet = false;
                }
                UiSharedService.AttachToolTip("Save and Create Restraint Set");
            }
        }
    }

    private void DrawRestraintSetEditorHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"{_handler.SetBeingEdited.Name}");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("EditRestraintSetHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdRestraintSet to a new restraintSet, and set editing restraintSet to true
                _handler.ClearEditingRestraintSet();
                return;
            }
            UiSharedService.AttachToolTip("Revert edits and return to Restraint Set List");
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText(_handler.SetBeingEdited.Name, ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // for saving contents
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                // reset the createdRestraintSet to a new restraintSet, and set editing restraintSet to true
                _handler.UpdateEditedRestraintSet();
            }
            UiSharedService.AttachToolTip("Save changes to Restraint Set & Return to the main list");

            // right beside it to the right, we need to draw the delete button
            using (var disableDelete = ImRaii.Disabled(UiSharedService.CtrlPressed()))
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(currentYpos);
                if (_uiShared.IconButton(FontAwesomeIcon.Trash))
                {
                    // reset the createdPattern to a new pattern, and set editing pattern to true
                    _handler.RemoveRestraintSet(_handler.EditingSetIndex);
                }
                UiSharedService.AttachToolTip("Delete Restraint Set");
            }
        }
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawSearchFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = RestraintSetSearchString;
        if (ImGui.InputTextWithHint("##RestraintFilter", "Search for Restraint Set", ref filter, 255))
        {
            RestraintSetSearchString = filter;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(RestraintSetSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            RestraintSetSearchString = string.Empty;
        }
    }

    private void DrawRestraintSetSelectableMenu()
    {
        // if list size has changed, refresh the list of hovered items
        if (ListItemHovered.Count != FilteredSetList.Count)
        {
            ListItemHovered.Clear();
            ListItemHovered.AddRange(Enumerable.Repeat(false, FilteredSetList.Count));
        }

        // display the selectable for each restraintSet using a for loop to keep track of the index
        for (int i = 0; i < FilteredSetList.Count; i++)
        {
            var set = FilteredSetList[i];
            DrawRestraintSetSelectable(set, i); // Pass the index to DrawRestraintSetSelectable
        }
    }

    private void DrawRestraintSetSelectable(RestraintSet set, int idx)
    {
        // grab the name of the set
        var name = set.Name;
        // grab the description of the set
        var description = set.Description;
        // grab who the set was locked by
        var lockedBy = set.LockedBy;

        // define our sizes
        var startYpos = ImGui.GetCursorPosY();
        var toggleSize = _uiShared.GetIconButtonSize(set.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff);
        var lockSize = _uiShared.GetIconButtonSize(set.Locked ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock);
        var nameTextSize = ImGui.CalcTextSize(set.Name);
        var descriptionTextSize = ImGui.CalcTextSize(set.Description);
        var lockedByTextSize = ImGui.CalcTextSize(lockedBy);

        // determine the height of this selection and what kind of selection it is.
        var isActiveSet = (set.Enabled == true);
        var isLockedSet = (set.Locked == true);

        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), ListItemHovered[idx]);
        using (ImRaii.Child($"##EditRestraintSetHeader{idx}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeight()*2)))
        {
            // create a group for the bounding area
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                UiSharedService.ColorText(name, ImGuiColors.DalamudWhite2);
            }

            // now draw the lower section out.
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                if (isLockedSet)
                {
                    UiSharedService.ColorText("Locked By:", ImGuiColors.DalamudGrey2);
                    ImGui.SameLine();
                    UiSharedService.ColorText(lockedBy, ImGuiColors.DalamudGrey3);
                }
                else
                {
                    // trim the description to the first 50 characters, then add ... at the end
                    var trimmedDescription = description.Length > 50 ? description.Substring(0, 50) + "..." : description;
                    UiSharedService.ColorText(trimmedDescription, ImGuiColors.DalamudGrey2);
                }
            }
            // now, head to the sameline of the full width minus the width of the button
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - toggleSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (ImGui.GetFrameHeight() * 2 - toggleSize.Y) / 2);
            // draw out the icon button
            var currentYpos = ImGui.GetCursorPosY();
            using (var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f))
            {
                using (var disableSetToggleButton = ImRaii.Disabled(set.Locked))
                {
                    if (_uiShared.IconButton(set.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff))
                    {
                        // set the enabled state of the restraintSet based on its current state so that we toggle it
                        if (set.Enabled)
                            _handler.DisableRestraintSet(idx);
                        else
                            _handler.EnableRestraintSet(idx);
                        // toggle the state & early return so we dont access the child clicked button
                        return;
                    }
                }
            }
        }
        ListItemHovered[idx] = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            _handler.SetEditingRestraintSet(set);
        }
        // if this is the active set, draw a seperator below it
        if (isActiveSet)
        {
            TimeSpan remainingTime = (set.LockedUntil - DateTimeOffset.UtcNow);
            string remainingTimeStr = $"{remainingTime.Days}d{remainingTime.Hours}h{remainingTime.Minutes}m{remainingTime.Seconds}s";
            var lockedDescription = set.Locked ? $"Locked for {remainingTimeStr}" : "Self-lock: XdXhXmXs format..";
            // display a third row for an input text field for the self-lock time
            var iconLock = isActiveSet ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
            var displayText = isLockedSet ? "Unlock Set" : "Lock Set";
            var width = ImGui.GetContentRegionAvail().X - _uiShared.GetIconTextButtonSize(iconLock, displayText) - ImGui.GetStyle().ItemInnerSpacing.X;
            ImGui.SetNextItemWidth(width);
            using (var disableTimeInput = ImRaii.Disabled(isLockedSet))
            {
                ImGui.InputTextWithHint($"##{name}TimerLockField", lockedDescription, ref LockTimerInputString, 24);
            }
            // in the same line draw a button to toggle the lock.
            ImUtf8.SameLineInner();

            // the condition for the icon text button to be disabled, is if the set is locked, and the enabled by != "SelfApplied"
            var disabled = isLockedSet && lockedBy != "SelfApplied";

            if (_uiShared.IconTextButton(iconLock, displayText, null, false, disabled))
            {
                // when we try to unlock, ONLY allow unlock if you are the one who locked it.
                if (isLockedSet && set.LockedBy == "SelfApplied")
                {
                    _handler.UnlockRestraintSet(_handler.GetRestraintSetIndexByName(name), "SelfApplied");
                }
                // if trying to lock it, allow this to happen.
                else
                {
                    // if the time we input is valid, do not clear it.
                    if (_uiShared.TryParseTimeSpan(LockTimerInputString, out var timeSpan))
                    {
                        // parse the timespan to the new offset and lock the set.
                        var endTimeUTC = DateTimeOffset.UtcNow.Add(timeSpan);
                        _handler.LockRestraintSet(_handler.GetRestraintSetIndexByName(name), "SelfApplied", endTimeUTC);
                    }
                    else
                    {
                        LockTimerInputString = "Invalid: use (XdXhXmXs)";
                    }
                }
            }
            UiSharedService.AttachToolTip(disabled ? "Only" + set.LockedBy + "can unlock your set." 
                                                   : set.Locked ? "Unlock this set." : "Lock this set.");
            ImGui.Separator();
        }
    }

    private void DrawRestraintSetPreview(RestraintSet set)
    {
        // embed a new table within this table.
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
            // i am dumb and dont know how to place adjustable divider lengths
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
