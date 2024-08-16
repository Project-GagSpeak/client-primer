using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.IpcHelpers.GameData;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UI.UiRemote;
using ImGuiNET;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Classes;
using System.Numerics;

namespace GagSpeak.UI.UiToybox;

public class ToyboxPatterns
{
    private readonly ILogger<ToyboxPatterns> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly PatternHandler _handler;
    private readonly PatternPlaybackService _playbackService;
    private readonly PairManager _pairManager;

    public ToyboxPatterns(ILogger<ToyboxPatterns> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        PatternHandler patternHandler, PatternPlaybackService playbackService, 
        PairManager pairManager)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _handler = patternHandler;
        _playbackService = playbackService;
        _pairManager = pairManager;
    }

    private Vector2 DefaultItemSpacing { get; set; } // TODO: remove

    // Private accessor vars for list management.
    private List<bool> ListItemHovered = new List<bool>();
    private LowerString PatternSearchString = LowerString.Empty;
    private LowerString PairSearchString = LowerString.Empty;

    public void DrawPatternManagerPanel()
    {
        // if we are simply viewing the main page, display list of patterns  
        if (_handler.EditingPatternNull)
        {
            DrawCreateOrImportPatternHeader();
            ImGui.Separator();
            if (_handler.PatternListSize() > 0)
                DrawPatternSelectableMenu();

            return; // perform early returns so we dont access other methods
        }

        // if we are editing an pattern
        if (!_handler.EditingPatternNull)
        {
            DrawPatternEditorHeader();
            ImGui.Separator();
            if (_handler.PatternListSize() > 0 && _handler.EditingPatternIndex >= 0)
                DrawPatternEditor(_handler.PatternBeingEdited);
        }
    }

    private void DrawCreateOrImportPatternHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        var importSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.FileImport, "Import");
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize("New Pattern");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("CreatePatternHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Y position centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // draw out the icon button
            if (_uiShared.IconButton(FontAwesomeIcon.Plus))
            {
                // Open the lovense remote in PATTERN mode
                _mediator.Publish(new UiToggleMessage(typeof(RemotePatternMaker)));
            }
            UiSharedService.AttachToolTip("Click me begin creating a new Pattern!");
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            _uiShared.BigText("New Pattern");

            // now calculate it so that the cursors Y position centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - importSize - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // draw revert button at the same location but right below that button
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileImport, "Import"))
            {
                // Paste in the pattern data from the clipboard if valid.
                SetFromClipboard();
            }
            UiSharedService.AttachToolTip("Click me paste in a new pattern currently copied to your clipboard!");
        }
    }

    private void DrawPatternEditorHeader()
    {
        // use button rounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"Editing Pattern");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("EditPatternHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2), false, ImGuiWindowFlags.NoScrollbar))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdPattern to a new pattern, and set editing pattern to true
                _handler.ClearEditingPattern();
                return;
            }
            UiSharedService.AttachToolTip("Discard Pattern Changes & Return to Pattern List");
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText("Editing Pattern", ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X * 2 - ImGui.GetStyle().ItemSpacing.X * 2);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // for saving contents
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                // reset the createdPattern to a new pattern, and set editing pattern to true
                _handler.UpdateEditedPattern();
            }
            UiSharedService.AttachToolTip("Save changes to Pattern & Return to Pattern List");

            // right beside it to the right, we need to draw the delete button
            using (var disableDelete = ImRaii.Disabled(!UiSharedService.CtrlPressed()))
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(currentYpos);
                if (_uiShared.IconButton(FontAwesomeIcon.Trash))
                {
                    // reset the createdPattern to a new pattern, and set editing pattern to true
                    _handler.RemovePattern(_handler.EditingPatternIndex);
                }
            }
            UiSharedService.AttachToolTip("Delete this Pattern\n(Must hold CTRL while clicking to delete)");
        }
    }

    private void DrawPatternSelectableMenu()
    {
        // if list size has changed, refresh the list of hovered items
        if (ListItemHovered.Count != _handler.PatternListSize())
        {
            ListItemHovered.Clear();
            ListItemHovered.AddRange(Enumerable.Repeat(false, _handler.PatternListSize()));
        }

        // display the selectable for each pattern using a for loop to keep track of the index
        for (int i = 0; i < _handler.PatternListSize(); i++)
        {
            DrawPatternSelectable(i); // Pass the index to DrawPatternSelectable
        }
    }

    private void DrawPatternSelectable(int idx)
    {
        // grab the pattern to draw details of.
        var tmpPattern = _handler.GetPattern(idx);
        // fetch the name of the pattern, and its text size
        var name = tmpPattern.Name;
        Vector2 tmpAlarmTextSize;

        var nameTextSize = ImGui.CalcTextSize(name);
        // fetch the author name (should only ever be UID, Alias, or Anonymous)
        var author = tmpPattern.Author;
        var authorTextSize = ImGui.CalcTextSize(author);
        // fetch the duration of the pattern
        var duration = tmpPattern.Duration;
        var durationTextSize = ImGui.CalcTextSize(duration);
        // fetch the list of tags.
        var tags = tmpPattern.Tags;
        using (_uiShared.UidFont.Push())
        {
            tmpAlarmTextSize = ImGui.CalcTextSize($"{name}");
        }
        // Get Style sizes
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var patternToggleButton = tmpPattern.IsActive ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play;
        var patternToggleButtonSize = _uiShared.GetIconButtonSize(tmpPattern.IsActive ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play);

        // create the selectable
        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), ListItemHovered[idx]);
        using (ImRaii.Child($"##PatternSelectable{idx}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 65f)))
        {
            // create a group for the bounding area
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                _uiShared.BigText($"{name}");
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((tmpAlarmTextSize.Y - nameTextSize.Y) / 2));
                if (tmpPattern.ShouldLoop)
                {
                    using (var loopColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedPink))
                    {
                        using (var font = ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            ImGui.TextUnformatted(FontAwesomeIcon.Repeat.ToIconString());
                        }
                    }
                }
            }

            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                UiSharedService.ColorText(author, ImGuiColors.DalamudGrey2);
                ImGui.SameLine();
                UiSharedService.ColorText("|  " + duration, ImGuiColors.DalamudGrey3);
                // Below this, we should draw out the tags in a row, with a max of 5 tags. If they extend the width of the window, stop drawing.
                var currentWidth = 0f;
                var widthRef = ImGui.GetContentRegionAvail().X;
            }

            // now, head to the same line of the full width minus the width of the button
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - patternToggleButtonSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (65f - patternToggleButtonSize.Y) / 2);
            // draw out the icon button
            if (_uiShared.IconButton(patternToggleButton))
            {
                // set the enabled state of the pattern based on its current state so that we toggle it
                if (tmpPattern.IsActive)
                {
                    _playbackService.StopPattern(idx, true);
                }
                else
                {
                    _playbackService.PlayPattern(idx, tmpPattern.StartPoint, tmpPattern.Duration, true);
                }
                // toggle the state & early return so we dont access the childclicked button
                return;
            }
            UiSharedService.AttachToolTip("Play / Stop this Pattern");
        }
        ListItemHovered[idx] = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            _handler.SetEditingPattern(tmpPattern, idx);
        }
        UiSharedService.AttachToolTip("Click me to edit this pattern.");
    }

    private void DrawPatternEditor(PatternData patternToEdit)
    {
        var region = ImGui.GetContentRegionAvail();
        // Display the name of the pattern in the top center.
        var refName = patternToEdit.Name;
        using (_uiShared.UidFont.Push())
        {
            _uiShared.EditableTextFieldWithPopup("Pattern Name", ref refName, 32, "Name here...");
            UiSharedService.AttachToolTip("Right-Click to edit name.");
        }
        patternToEdit.Name = refName;

        using (var group = ImRaii.Group())
        {
            // under this, in gold, draw out the Pattern Duration
            using (var loopColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGold))
            {
                ImGui.TextUnformatted("Author: ");
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(patternToEdit.Author);
        }


        using (var group = ImRaii.Group())
        {
            using (var loopColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGold))
            {
                ImGui.TextUnformatted("Duration: ");
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(patternToEdit.Duration);
        }

        using (var group = ImRaii.Group())
        {
            using (var loopColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGold))
            {
                ImGui.TextUnformatted("Looping: ");
            }
            ImGui.SameLine();
            if(_uiShared.IconTextButton(FontAwesomeIcon.Repeat, patternToEdit.ShouldLoop ? "Looping" : "Not Looping", null, true))
            {
                // change state
                patternToEdit.ShouldLoop = !patternToEdit.ShouldLoop;
            }
        }

        // grab the current cursorPosY ref.
        var cursorPosY = ImGui.GetCursorPosY();

        using (var group = ImRaii.Group())
        {
            using (var loopColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGold))
            {
                ImGui.TextUnformatted("Pattern Description: ");
            }

            var descRef = patternToEdit.Description;
            _uiShared.EditableTextFieldWithPopup("Pattern Description", ref descRef, 200, "Description here...");
            UiSharedService.AttachToolTip("Right-Click to edit description.");
            patternToEdit.Description = descRef;
        }

        // move cursor down 3 ImGui.GetFrameHeightWithSpacing(), then draw the seperator
        ImGui.SetCursorPosY(cursorPosY + 3 * ImGui.GetFrameHeightWithSpacing());
        ImGui.Separator();

        // Define the pattern playback parameters 
        TimeSpan patternDurationTimeSpan = _handler.GetPatternLength(patternToEdit.Name);
        var newStartDuration = patternToEdit.StartPoint;
        _uiShared.DrawTimeSpanCombo("Playback Start-Point", patternDurationTimeSpan, ref newStartDuration,
            UiSharedService.GetWindowContentRegionWidth() / 2);
        patternToEdit.StartPoint = newStartDuration;

        // calc the max playback length minus the start point we set to declare the max allowable duration to play
        bool parseSuccess = TimeSpan.TryParseExact(newStartDuration, "hh\\:mm\\:ss", null, out TimeSpan startPointTimeSpan);
        if (!parseSuccess)
        {
            parseSuccess = TimeSpan.TryParseExact(newStartDuration, "mm\\:ss", null, out startPointTimeSpan);
        }

        // If parsing fails or duration exceeds the max allowed duration, set it to the max duration
        if (!parseSuccess || startPointTimeSpan > patternDurationTimeSpan)
        {
            startPointTimeSpan = patternDurationTimeSpan;
        }
        var maxPlaybackDuration = patternDurationTimeSpan - startPointTimeSpan;

        // define the duration to playback
        var newPlaybackDuration = patternToEdit.PlaybackDuration;
        _uiShared.DrawTimeSpanCombo("Playback Duration", maxPlaybackDuration, ref newPlaybackDuration,
            UiSharedService.GetWindowContentRegionWidth() / 2);
        patternToEdit.PlaybackDuration = newPlaybackDuration;

        ImGui.Separator();
        // display filterable search list
        DrawSearchFilter(ImGui.GetContentRegionAvail().X, ImGui.GetStyle().ItemSpacing.X);
        using (var table = ImRaii.Table("userListForVisibility", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(region.X, ImGui.GetContentRegionAvail().Y)))
        {
            if (!table) return;

            ImGui.TableSetupColumn("Alias/UID", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Can View", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Can View ").X);
            ImGui.TableHeadersRow();

            var PairList = _pairManager.DirectPairs
                .Where(pair => string.IsNullOrEmpty(PairSearchString) ||
                               pair.UserData.AliasOrUID.Contains(PairSearchString, StringComparison.OrdinalIgnoreCase) ||
                               (pair.GetNickname() != null && pair.GetNickname().Contains(PairSearchString, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p.GetNickname() ?? p.UserData.AliasOrUID, StringComparer.OrdinalIgnoreCase);

            foreach (Pair pair in PairList)
            {
                using var tableId = ImRaii.PushId("userTable_" + pair.UserData.UID);

                ImGui.TableNextColumn(); // alias or UID of user.
                var nickname = pair.GetNickname();
                var text = nickname == null ? pair.UserData.AliasOrUID : nickname + " (" + pair.UserData.UID + ")";
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(text);

                ImGui.TableNextColumn();
                // display nothing if they are not in the list, otherwise display a check
                var canSeeIcon = patternToEdit.AllowedUsers.Contains(pair.UserData.UID) ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    string.Empty, false, true))
                    {
                        if (canSeeIcon == FontAwesomeIcon.Times)
                        {
                            patternToEdit.AllowedUsers.Add(pair.UserData.UID);
                        }
                        else
                        {
                            patternToEdit.AllowedUsers.Remove(pair.UserData.UID);
                        }
                    }
                }
            }
        }
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawSearchFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = PairSearchString;
        if (ImGui.InputTextWithHint("##filter", "Filter for UID/notes", ref filter, 255))
        {
            PairSearchString = filter;
        }
        ImGui.SameLine();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(PairSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            PairSearchString = string.Empty;
        }
    }

    private void SetFromClipboard()
    {
        try
        {
            // Get the JSON string from the clipboard
            string base64 = ImGui.GetClipboardText();
            // Deserialize the JSON string back to pattern data
            var bytes = Convert.FromBase64String(base64);
            // Decode the base64 string back to a regular string
            var version = bytes[0];
            version = bytes.DecompressToString(out var decompressed);
            // Deserialize the string back to pattern data
            PatternData pattern = JsonConvert.DeserializeObject<PatternData>(decompressed) ?? new PatternData();
            // Ensure the pattern has a unique name
            string baseName = _handler.EnsureUniqueName(pattern.Name);

            // Set the active pattern
            _logger.LogInformation("Set pattern data from clipboard");
            _handler.AddNewPattern(pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not set pattern data from clipboard.{ex.Message}");
        }
    }


}
