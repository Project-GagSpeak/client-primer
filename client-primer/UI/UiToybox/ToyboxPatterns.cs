using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.IpcHelpers.GameData;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UI.UiRemote;
using GagSpeak.Utils;
using ImGuiNET;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Text;
using System.Numerics;
using System.Security.Claims;
using static System.Net.Mime.MediaTypeNames;

namespace GagSpeak.UI.UiToybox;

public class ToyboxPatterns
{
    private readonly ILogger<ToyboxPatterns> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly PatternHandler _handler;
    private readonly PlaybackService _playbackService;
    private readonly PatternHubService _patternHubService;
    private readonly PairManager _pairManager;

    public ToyboxPatterns(ILogger<ToyboxPatterns> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        PatternHandler patternHandler, PlaybackService playbackService,
        PatternHubService patternHubService, PairManager pairManager)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _handler = patternHandler;
        _playbackService = playbackService;
        _patternHubService = patternHubService;
        _pairManager = pairManager;
    }

    // Private accessor vars for list management.
    private LowerString PatternSearchString = LowerString.Empty;
    private List<PatternData> FilteredPatternsList
        => _handler.GetPatternsForSearch()
            .Where(pattern => pattern.Name.Contains(PatternSearchString, StringComparison.OrdinalIgnoreCase))
            .ToList();
    private List<bool> ListItemHovered = new List<bool>();

    private LowerString PairSearchString = LowerString.Empty;

    public void DrawPatternManagerPanel()
    {
        var regionSize = ImGui.GetContentRegionAvail();

        // if we are simply viewing the main page, display list of patterns  
        if (_handler.EditingPatternNull)
        {
            DrawCreateOrImportPatternHeader();
            ImGui.Separator();
            DrawSearchFilter(regionSize.X, ImGui.GetStyle().ItemInnerSpacing.X);
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
        FontAwesomeIcon publishOrTakedownIcon = _handler.PatternBeingEdited.IsPublished ? FontAwesomeIcon.Ban : FontAwesomeIcon.Upload;
        string publishOrTakedownText = _handler.PatternBeingEdited.IsPublished ? "Unpublish" : "Publish";
        var publishOrTakedownSize = _uiShared.GetIconTextButtonSize(publishOrTakedownIcon, publishOrTakedownText);
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"Editor");
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
                UiSharedService.ColorText("Editor", ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() 
                - publishOrTakedownSize - iconSize.X * 2 - ImGui.GetStyle().ItemSpacing.X*3);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();

            // draw out the icon button
            if (_uiShared.IconTextButton(publishOrTakedownIcon, publishOrTakedownText, null, false, !_handler.PatternBeingEdited.CreatedByClient))
            {
                ImGui.OpenPopup("UploadPopup");
                var buttonPos = ImGui.GetItemRectMin();
                var buttonSize = ImGui.GetItemRectSize();
                ImGui.SetNextWindowPos(new Vector2(buttonPos.X, buttonPos.Y + buttonSize.Y));
            }

            // for saving contents
            ImGui.SameLine();
            ImGui.SetCursorPosY(currentYpos);
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                // reset the createdPattern to a new pattern, and set editing pattern to true
                _handler.UpdateEditedPattern();
            }
            UiSharedService.AttachToolTip("Save changes to Pattern & Return to Pattern List");

            // right beside it to the right, we need to draw the delete button
            using (var disableDelete = ImRaii.Disabled(!KeyMonitor.CtrlPressed()))
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(currentYpos);
                if (_uiShared.IconButton(FontAwesomeIcon.Trash))
                {
                    // reset the createdPattern to a new pattern, and set editing pattern to true
                    _handler.RemovePattern(_handler.PatternBeingEdited.UniqueIdentifier);
                }
            }
            UiSharedService.AttachToolTip("Delete this Pattern\n(Must hold CTRL while clicking to delete)");

            try
            {
                if (ImGui.BeginPopup("UploadPopup"))
                {
                    string text = _handler.PatternBeingEdited.IsPublished ? "Remove Pattern from Server?" : "Upload Pattern to Server?";
                    ImGuiUtil.Center(text);
                    var width = (ImGui.GetContentRegionAvail().X / 2) - ImGui.GetStyle().ItemInnerSpacing.X;
                    if (ImGui.Button("Yes, I'm Sure", new Vector2(width, 25f)))
                    {
                        if(_handler.PatternBeingEdited.IsPublished)
                        {
                            _patternHubService.RemovePatternFromServer(_handler.EditingPatternIndex);
                        }
                        else
                        {
                            _patternHubService.UploadPatternToServer(_handler.EditingPatternIndex);
                            UnlocksEventManager.AchievementEvent(UnlocksEvent.PatternAction, PatternInteractionKind.Published, Guid.Empty, false);
                        }
                        ImGui.CloseCurrentPopup();
                    }
                    ImUtf8.SameLineInner();
                    if (ImGui.Button("Fuck, go back", new Vector2(width, 25f)))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }
            catch (Exception ex)
            {
                // prevent crashes.
                _logger.LogError(ex.ToString());
            }
        }
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawSearchFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = PatternSearchString;
        if (ImGui.InputTextWithHint("##PatternSearchStringFilter", "Search for a Pattern", ref filter, 255))
        {
            PatternSearchString = filter;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(PatternSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            PatternSearchString = string.Empty;
        }
    }

    private void DrawPatternSelectableMenu()
    {
        var region = ImGui.GetContentRegionAvail();
        var topLeftSideHeight = region.Y;
        // if list size has changed, refresh the list of hovered items
        if (ListItemHovered.Count != _handler.PatternListSize())
        {
            ListItemHovered.Clear();
            ListItemHovered.AddRange(Enumerable.Repeat(false, FilteredPatternsList.Count));
        }

        using (var leftChild = ImRaii.Child($"###SelectablePatternList", region with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoScrollbar))
        {
            // display the selectable for each pattern using a for loop to keep track of the index
            for (int i = 0; i < FilteredPatternsList.Count; i++)
            {
                var pattern = FilteredPatternsList[i];
                DrawPatternSelectable(pattern, i); // Pass the index to DrawPatternSelectable
            }
        }
    }

    private void DrawPatternSelectable(PatternData pattern, int idx)
    {
        // fetch the name of the pattern, and its text size
        var name = pattern.Name;
        Vector2 tmpAlarmTextSize;
        using (_uiShared.UidFont.Push()) { tmpAlarmTextSize = ImGui.CalcTextSize(name); }

        var author = pattern.Author;
        var authorTextSize = ImGui.CalcTextSize(author);
        
        // fetch the duration of the pattern
        var duration = pattern.Duration.Hours > 0 ? pattern.Duration.ToString("hh\\:mm\\:ss") : pattern.Duration.ToString("mm\\:ss");
        var durationTextSize = ImGui.CalcTextSize(duration);
        
        // fetch the list of tags.
        var tags = pattern.Tags;

        // get loop icon size
        var loopIconSize = _uiShared.GetIconData(FontAwesomeIcon.Repeat);

        // Get Style sizes
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var patternToggleButton = pattern.IsActive ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play;
        var patternToggleButtonSize = _uiShared.GetIconButtonSize(pattern.IsActive ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play);

        // create the selectable
        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), ListItemHovered[idx]);
        using (ImRaii.Child($"##PatternSelectable{pattern.UniqueIdentifier}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 65f)))
        {
            // create a group for the bounding area
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                // get Y pos
                var currentYpos = ImGui.GetCursorPosY();
                _uiShared.BigText(name);
                ImGui.SameLine();
                ImGui.SetCursorPosY(currentYpos + ((tmpAlarmTextSize.Y - loopIconSize.Y)/1.5f));

                if (pattern.ShouldLoop)
                {
                    _uiShared.IconText(FontAwesomeIcon.Repeat, ImGuiColors.ParsedPink);
                }
                if (pattern.IsPublished)
                {
                    ImGui.SameLine();
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((tmpAlarmTextSize.Y - loopIconSize.Y)/1.5f));
                    _uiShared.IconText(FontAwesomeIcon.Globe, ImGuiColors.ParsedPink);
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
                var widthRef = ImGui.GetContentRegionAvail().X;
            }

            // now, head to the same line of the full width minus the width of the button
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - patternToggleButtonSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (65f - patternToggleButtonSize.Y) / 2);
            // draw out the icon button
            if (_uiShared.IconButton(patternToggleButton))
            {
                // set the enabled state of the pattern based on its current state so that we toggle it
                if (pattern.IsActive)
                {
                    _playbackService.StopPattern(pattern.UniqueIdentifier, true);
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.PatternAction, PatternInteractionKind.Stopped, pattern.UniqueIdentifier, false);
                }
                else
                {
                    _playbackService.PlayPattern(pattern.UniqueIdentifier, pattern.StartPoint, pattern.Duration, true);
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.PatternAction, PatternInteractionKind.Started, pattern.UniqueIdentifier, false);
                }
                // toggle the state & early return so we dont access the childclicked button
                return;
            }
            UiSharedService.AttachToolTip("Play / Stop this Pattern");
        }
        ListItemHovered[idx] = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            _handler.SetEditingPattern(pattern, idx);
        }
        UiSharedService.AttachToolTip("Click me to edit this pattern.");
    }

    private void DrawPatternEditor(PatternData patternToEdit)
    {
        if (ImGui.BeginTabBar("PatternEditorTabBar"))
        {
            if (ImGui.BeginTabItem("Display Info"))
            {
                DrawDisplayInfo(patternToEdit);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Adjustments"))
            {
                DrawAdjustments(patternToEdit);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Access"))
            {
                DrawAccess(patternToEdit);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawDisplayInfo(PatternData pattern)
    {
        // identifier
        UiSharedService.ColorText("Identifier", ImGuiColors.ParsedGold);
        UiSharedService.ColorText(pattern.UniqueIdentifier.ToString(), ImGuiColors.DalamudGrey);

        // name
        var refName = pattern.Name;
        UiSharedService.ColorText("Pattern Name", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputTextWithHint("##PatternName", "Name Here...", ref refName, 50))
        {
            pattern.Name = refName;
        }
        _uiShared.DrawHelpText("Define the name for the Pattern.");
        // author
        var refAuthor = pattern.Author;
        UiSharedService.ColorText("Author", ImGuiColors.ParsedGold);
        using (var disableAuthor = ImRaii.Disabled(!pattern.CreatedByClient))
        {
            ImGui.SetNextItemWidth(200f);
            if (ImGui.InputTextWithHint("##PatternAuthor", "Author Here...", ref refAuthor, 25))
            {
                pattern.Author = refAuthor;
            }
        }
        _uiShared.DrawHelpText("Define the author for the Pattern.\n(Shown as Publisher name if uploaded)");

        // description
        var refDescription = pattern.Description;
        UiSharedService.ColorText("Description", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        if (UiSharedService.InputTextWrapMultiline("##PatternDescription", ref refDescription, 100, 3, 225f))
        {
            pattern.Description = refDescription;
        }
        _uiShared.DrawHelpText("Define the description for the Pattern.\n(Shown on tooltip hover if uploaded)");
    }

    private void DrawAdjustments(PatternData pattern)
    {
        // total duration
        ImGui.Spacing();
        UiSharedService.ColorText("Total Duration", ImGuiColors.ParsedGold);
        ImGui.SameLine();
        ImGui.TextUnformatted(pattern.Duration.Hours > 0 ? pattern.Duration.ToString("hh\\:mm\\:ss") : pattern.Duration.ToString("mm\\:ss"));
        
        // looping
        ImGui.Spacing();
        UiSharedService.ColorText("Pattern Loop State", ImGuiColors.ParsedGold);
        ImGui.SameLine();
        if (_uiShared.IconTextButton(FontAwesomeIcon.Repeat, pattern.ShouldLoop ? "Looping" : "Not Looping", null, true)) pattern.ShouldLoop = !pattern.ShouldLoop;

        TimeSpan patternDurationTimeSpan = pattern.Duration;
        TimeSpan patternStartPointTimeSpan = pattern.StartPoint;
        TimeSpan patternPlaybackDuration = pattern.PlaybackDuration;

        // playback start point
        UiSharedService.ColorText("Pattern Start-Point Timestamp", ImGuiColors.ParsedGold);
        string formatStart = patternDurationTimeSpan.Hours > 0 ? "hh\\:mm\\:ss" : "mm\\:ss";
        _uiShared.DrawTimeSpanCombo("PatternStartPointTimeCombo", patternDurationTimeSpan, ref patternStartPointTimeSpan, 150f, formatStart, false);
        pattern.StartPoint = patternStartPointTimeSpan;

        // time difference calculation.
        if (pattern.StartPoint > patternDurationTimeSpan) pattern.StartPoint = patternDurationTimeSpan;
        TimeSpan maxPlaybackDuration = patternDurationTimeSpan - pattern.StartPoint;

        // playback duration
        ImGui.Spacing();
        UiSharedService.ColorText("Pattern Playback Duration", ImGuiColors.ParsedGold);
        string formatDuration = patternPlaybackDuration.Hours > 0 ? "hh\\:mm\\:ss" : "mm\\:ss";
        _uiShared.DrawTimeSpanCombo("Pattern Playback Duration", maxPlaybackDuration, ref patternPlaybackDuration, 150f, formatDuration, false);
        pattern.PlaybackDuration = patternPlaybackDuration;
    }

    private void DrawAccess(PatternData pattern)
    {
        DrawUidSearchFilter(ImGui.GetContentRegionAvail().X);
        using (var table = ImRaii.Table("userListForVisibility", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail()))
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
                var canSeeIcon = pattern.AllowedUsers.Contains(pair.UserData.UID) ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    string.Empty, false, true))
                    {
                        if (canSeeIcon == FontAwesomeIcon.Times)
                        {
                            pattern.AllowedUsers.Add(pair.UserData.UID);
                        }
                        else
                        {
                            pattern.AllowedUsers.Remove(pair.UserData.UID);
                        }
                    }
                }
            }
        }
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawUidSearchFilter(float availableWidth)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - ImGui.GetStyle().ItemInnerSpacing.X);
        string filter = PairSearchString;
        if (ImGui.InputTextWithHint("##filter", "Filter for UID/notes", ref filter, 255))
        {
            PairSearchString = filter;
        }
        ImUtf8.SameLineInner();
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
