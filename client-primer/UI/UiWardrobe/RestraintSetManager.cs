using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.AozNoteModule;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetManager : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly IpcCallerGlamourer _ipcGlamourer;
    private readonly RestraintSetEditor _editor;
    private readonly WardrobeHandler _handler;
    private readonly TextureService _textures;
    private readonly DictStain _stainDictionary;
    private readonly ItemIdVars _itemHelper;
    private readonly PadlockHandler _padlockHandler;

    public RestraintSetManager(ILogger<RestraintSetManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        IpcCallerGlamourer ipcGlamourer, RestraintSetEditor editor, 
        WardrobeHandler handler, TextureService textureService, DictStain stainDictionary,
        ItemIdVars itemHelper, PadlockHandler padlockHandler) : base(logger, mediator)
    {
        _uiShared = uiSharedService;
        _ipcGlamourer = ipcGlamourer;
        _editor = editor;
        _handler = handler;
        _textures = textureService;
        _stainDictionary = stainDictionary;
        _itemHelper = itemHelper;
        _padlockHandler = padlockHandler;

        CreatedRestraintSet = new RestraintSet(_itemHelper);

        GameIconSize = new Vector2(2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
        StainColorCombos = new StainColorCombo(0, _stainDictionary, logger);

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) =>
        {
            // recalculate the list selection based on the order.
            LastHoveredIndex = -1;
        });

        Mediator.Subscribe<TooltipSetItemToRestraintSetMessage>(this, (msg) =>
        {
            if (!_handler.EditingSetNull)
            {
                _handler.SetBeingEdited.DrawData[msg.Slot].GameItem = msg.Item;
                Logger.LogDebug($"Set {msg.Slot} to {msg.Item.Name}");
            }
            else
            {
                Logger.LogError("No Restraint Set is currently being edited.");
            }
        });
    }

    private Vector2 GameIconSize;
    private readonly StainColorCombo StainColorCombos;

    private RestraintSet CreatedRestraintSet;
    public bool CreatingRestraintSet = false;
    private string LockTimerInputString = string.Empty;

    private LowerString RestraintSetSearchString = LowerString.Empty;
    private List<RestraintSet> FilteredSetList
    {
        get
        {
            var allSets = _handler.GetAllSetsForSearch();
            var enabledSet = allSets.FirstOrDefault(set => set.Enabled);
            var filteredSets = allSets
                .Where(set => !set.Enabled && set.Name.Contains(RestraintSetSearchString, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return enabledSet != null ? new List<RestraintSet> { enabledSet }.Concat(filteredSets).ToList() : filteredSets;
        }
    }
    private int LastHoveredIndex = -1; // -1 indicates no item is currently hovered



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

            // Draw the preview based on the last hovered index
            if (LastHoveredIndex != -1 && LastHoveredIndex < FilteredSetList.Count)
            {
                DrawRestraintSetPreview(FilteredSetList[LastHoveredIndex]);
            }
            else if (_handler.ActiveSet != null)
            {
                DrawRestraintSetPreview(_handler.ActiveSet);
            }
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
                CreatedRestraintSet = new RestraintSet(_itemHelper);
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
                CreatedRestraintSet = new RestraintSet(_itemHelper);
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
                    CreatedRestraintSet = new RestraintSet(_itemHelper);
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
        var importSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.FileImport, "Import Gear");
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
            float width = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - importSize - iconSize.X * 2 - ImGui.GetStyle().ItemSpacing.X * 3;
            ImGui.SameLine(width);

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // draw revert button at the same location but right below that button
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileImport, "Import Gear", null, false, !IpcCallerGlamourer.APIAvailable || _handler.EditingSetNull))
            {
                _ipcGlamourer.SetRestraintEquipmentFromState(_handler.SetBeingEdited);
                Logger.LogDebug("EquipmentImported from current State");
            }
            UiSharedService.AttachToolTip("Imports your Actor's Equipment Data from your current appearance.");

            ImGui.SameLine();
            ImGui.SetCursorPosY(currentYpos);
            // for saving contents
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                // reset the createdRestraintSet to a new restraintSet, and set editing restraintSet to true
                _handler.UpdateEditedRestraintSet();
            }
            UiSharedService.AttachToolTip("Save changes to Restraint Set & Return to the main list");

            // right beside it to the right, we need to draw the delete button
            ImGui.SameLine();
            ImGui.SetCursorPosY(currentYpos);
            if (_uiShared.IconButton(FontAwesomeIcon.Trash, null, null, !UiSharedService.CtrlPressed()))
            {
                // reset the createdPattern to a new pattern, and set editing pattern to true
                _handler.RemoveRestraintSet(_handler.EditingSetIndex);
            }
            UiSharedService.AttachToolTip("Delete Restraint Set");
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
            LastHoveredIndex = -1;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(RestraintSetSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            RestraintSetSearchString = string.Empty;
            LastHoveredIndex = -1;
        }
    }

    private void DrawRestraintSetSelectableMenu()
    {
        // display the selectable for each restraintSet using a for loop to keep track of the index
        for (int i = 0; i < FilteredSetList.Count; i++)
        {
            var set = FilteredSetList[i];
            DrawRestraintSetSelectable(set, i);

            if(ImGui.IsItemHovered())
                LastHoveredIndex = i;

            // if the item is right clicked, open the popup
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && LastHoveredIndex == i && !FilteredSetList[i].Enabled)
            {
                ImGui.OpenPopup($"RestraintSetContext{i}");
            }
        }

        if (LastHoveredIndex != -1 && LastHoveredIndex < FilteredSetList.Count)
        {
            if (ImGui.BeginPopup($"RestraintSetContext{LastHoveredIndex}"))
            {
                if (ImGui.Selectable("Clone Restraint Set") && FilteredSetList[LastHoveredIndex] != null)
                {
                    _handler.CloneRestraintSet(FilteredSetList[LastHoveredIndex]);
                }
                if (ImGui.Selectable("Delete Set") && FilteredSetList[LastHoveredIndex] != null)
                {
                    _handler.RemoveRestraintSet(_handler.GetRestraintSetIndexByName(FilteredSetList[LastHoveredIndex].Name));
                }
                ImGui.EndPopup();
            }
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

        // if it is the active set, dont push the color, otherwise push the color
        
        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), !isActiveSet && LastHoveredIndex == idx);
        using (ImRaii.Child($"##EditRestraintSetHeader{idx}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeight() * 2 - 5f)))
        {
            var maxAllowedWidth = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - toggleSize.X - ImGui.GetStyle().ItemSpacing.X * 3;
            // create a group for the bounding area
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                var originalCursorPos = ImGui.GetCursorPos();
                // Move the Y pos down a bit, only for drawing this text
                ImGui.SetCursorPosY(originalCursorPos.Y + 2.5f);
                // Draw the text with the desired color
                UiSharedService.ColorText(name, ImGuiColors.DalamudWhite2);
                if(GenericHelpers.TimerPadlocks.Contains(set.LockType))
                {
                    ImGui.SameLine();
                    TimeSpan remainingTime = (set.LockedUntil - DateTimeOffset.UtcNow);
                    var sb = new StringBuilder();
                    if (remainingTime.Days > 0) sb.Append($"{remainingTime.Days}d ");
                    if (remainingTime.Hours > 0) sb.Append($"{remainingTime.Hours}h ");
                    if (remainingTime.Minutes > 0) sb.Append($"{remainingTime.Minutes}m ");
                    if (remainingTime.Seconds > 0 || sb.Length == 0) sb.Append($"{remainingTime.Seconds}s ");
                    string remainingTimeStr = sb.ToString().Trim();

                    UiSharedService.ColorText(remainingTimeStr +" left..", ImGuiColors.ParsedPink);
                }
                // Restore the original cursor position
                ImGui.SetCursorPos(originalCursorPos);

            }

            // now draw the lower section out.
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                if (isLockedSet)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2.5f);
                    UiSharedService.ColorText("Locked By:", ImGuiColors.DalamudGrey2);
                    ImGui.SameLine();
                    UiSharedService.ColorText(lockedBy, ImGuiColors.DalamudGrey3);
                }
                else
                {
                    // if the trimmed descriptions ImGui.CalcTextSize() is larger than the maxAllowedWidth, then trim it.
                    var trimmedDescription = description.Length > 50 ? description.Substring(0, 50) + "..." : description;
                    // Measure the text size
                    var textSize = ImGui.CalcTextSize(trimmedDescription).X;

                    // If the text size exceeds the maximum allowed width, trim it further
                    while (textSize > maxAllowedWidth && trimmedDescription.Length > 3)
                    {
                        trimmedDescription = trimmedDescription.Substring(0, trimmedDescription.Length - 4) + "...";
                        textSize = ImGui.CalcTextSize(trimmedDescription).X;
                    }
                    // move the Y pos up a bit.
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2.5f);
                    UiSharedService.ColorText(trimmedDescription, ImGuiColors.DalamudGrey2);
                }
            }
            // now, head to the sameline of the full width minus the width of the button
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - toggleSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY((ImGui.GetCursorPosY() - (ImGui.GetFrameHeight() * 2 - toggleSize.Y) / 2) - 2.5f);
            // draw out the icon button
            var currentYpos = ImGui.GetCursorPosY();
            using (var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f))
            {
                bool disabled = FilteredSetList.Any(x => x.Locked) || !_handler.WardrobeEnabled || !_handler.RestraintSetsEnabled;
                string ttText = set.Enabled ? (set.Locked ? "Cannot Disable a Locked Set!" : "Disable Active Restraint Set") 
                                            : (!_handler.WardrobeEnabled || !_handler.RestraintSetsEnabled) ? "Wardrobe / Restraint set Permissions not Active."
                                            : (disabled ? "Can't Enable another Set while active Set is Locked!" : "Enable Restraint Set");
                if (_uiShared.IconButton(set.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff, null, set.Name, disabled))
                {
                    // set the enabled state of the restraintSet based on its current state so that we toggle it
                    if (set.Enabled)
                        _handler.DisableRestraintSet(_handler.GetRestraintSetIndexByName(set.Name));
                    else
                        _handler.EnableRestraintSet(_handler.GetRestraintSetIndexByName(set.Name));
                    // toggle the state & early return so we dont access the child clicked button
                    return;
                }
                UiSharedService.AttachToolTip(ttText);
            }
        }
        if (!isActiveSet)
        {
            if (ImGui.IsItemClicked())
            {
                _handler.SetEditingRestraintSet(set);
            }
        }
        // if this is the active set, draw a seperator below it
        if (isActiveSet)
        {
            // obtain the width to use.
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().ItemInnerSpacing.X);
            using (var group = ImRaii.Group())
            {
                var width = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X;

                TimeSpan remainingTime = (set.LockedUntil - DateTimeOffset.UtcNow);
                string remainingTimeStr = $"{remainingTime.Days}d{remainingTime.Hours}h{remainingTime.Minutes}m{remainingTime.Seconds}s";
                var lockedDescription = set.Locked ? $"Locked for {remainingTimeStr}" : "Self-lock: XdXhXmXs format..";
                // draw the padlock dropdown
                var padlockType = set.LockType.ToPadlock() != Padlocks.None ? set.LockType.ToPadlock() : _padlockHandler.PadlockPrevs[3];
                using (ImRaii.Disabled(set.Locked || set.LockType != "None"))
                {
                    _uiShared.DrawCombo($"RestraintSetLock {set.Name}", (width - 1 - _uiShared.GetIconButtonSize(FontAwesomeIcon.Lock).X - ImGui.GetStyle().ItemInnerSpacing.X),
                        Enum.GetValues<Padlocks>().Cast<Padlocks>().Where(p => p != Padlocks.OwnerPadlock && p != Padlocks.OwnerTimerPadlock).ToArray(),
                        (padlock) => padlock.ToName(),
                    (i) =>
                    {
                        _padlockHandler.PadlockPrevs[3] = i;
                    }, padlockType, false);
                }
                ImUtf8.SameLineInner();
                // draw the lock button
                if (_uiShared.IconButton(set.Locked ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock, null, set.Name.ToString(), padlockType == Padlocks.None))
                {
                    if (_padlockHandler.RestraintPasswordValidate(_handler.GetRestraintSetIndexByName(set.Name), set.Locked))
                    {
                        if (set.Locked)
                        {
                            Logger.LogTrace($"Unlocking Restraint Set {set.Name}");
                            // allow using set.EnabledBy here because it will check against the assigner when unlocking.
                            _handler.UnlockRestraintSet(_handler.GetRestraintSetIndexByName(set.Name), set.EnabledBy);
                        }
                        else
                        {
                            Logger.LogTrace($"Locking Restraint Set {set.Name}");
                            _handler.LockRestraintSet(_handler.GetRestraintSetIndexByName(set.Name), _padlockHandler.PadlockPrevs[3].ToString(),
                                _padlockHandler.Passwords[3], UiSharedService.GetEndTimeUTC(_padlockHandler.Timers[3]), "SelfApplied");
                        }
                    }
                    else
                    {
                        Logger.LogDebug($"Failed to validate password for Restraint Set {set.Name}");
                    }
                    // reset the password and timer
                    _padlockHandler.Passwords[3] = string.Empty;
                    _padlockHandler.Timers[3] = string.Empty;
                }
                UiSharedService.AttachToolTip(_padlockHandler.PadlockPrevs[3] == Padlocks.None ? "Select a padlock type before locking" :
                    set.Locked == false ? "Self-Lock this Restraint Set" : 
                    set.LockedBy != "SelfApplied" ? "Only" + set.LockedBy + "can unlock your set." : "Unlock this set.");
                // display associated password field for padlock type.
                _padlockHandler.DisplayPasswordField(3, set.Locked, width);
            }
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
