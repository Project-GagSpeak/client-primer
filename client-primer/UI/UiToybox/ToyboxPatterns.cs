using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.IpcHelpers.GameData;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.UI.UiRemote;
using ImGuiNET;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Microsoft.IdentityModel.Tokens;
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
    private readonly PairManager _pairManager;

    public ToyboxPatterns(ILogger<ToyboxPatterns> logger, 
        GagspeakMediator mediator, UiSharedService uiSharedService, 
        PatternHandler patternHandler, PairManager pairManager)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _handler = patternHandler;
        _pairManager = pairManager;
    }

    private PatternData? SelectedPattern = null;
    private int SelectedPatternIdx = -1;
    private Vector2 DefaultItemSpacing { get; set; }
    private LowerString PairSearchString = LowerString.Empty;
    private LowerString PatternSearchString = LowerString.Empty;

    public void DrawPatternManagerPanel()
    {
        // if the pattern list is empty, fetch it
        if(_handler.PatternNames == null) return;

        _uiShared.BigText("Patterns");

        var windowEndX = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth();
        var currentRightSide = windowEndX - (_uiShared.GetIconTextButtonSize(FontAwesomeIcon.Save, "Import") + 
            ImGui.GetStyle().ItemSpacing.X*2 + _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Plus, "Create"));
        ImGui.SameLine(currentRightSide);
        using (_ = ImRaii.Group())
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Plus, "Create"))
            {
                // Open the lovense remote in PATTERN mode
                _mediator.Publish(new UiToggleMessage(typeof(RemotePatternMaker)));
            }
            ImGui.SameLine(0, 4);
            // draw revert button at the same locatoin but right below that button
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileImport, "Import"))
            {
                // Paste in the pattern data from the clipboard if valid.
                SetFromClipboard();
            }
        }

        ImGui.Separator();

        // if the counter is 0, perform an early return. Prevents us from drawing null pattern data.
        if (_handler.PatternNames.Count == 0)
        {
            UiSharedService.ColorText("No patterns found.", ImGuiColors.DalamudYellow);
            return;
        }

        DrawPatterns();
    }

    private void DrawPatterns()
    {
        var searchString = PatternSearchString.Lower;
        // draw the selector on the left
        _uiShared.DrawComboSearchable("##PatternSelector", ImGui.GetContentRegionAvail().X, ref searchString,
            _handler.PatternNames, (i) => i, false,
        (i) =>
        {
            var foundMatch = _handler.GetPatternIdxByName(i);
            if (_handler.IsIndexInBounds(foundMatch))
            {
                SelectedPatternIdx = foundMatch;
                SelectedPattern = _handler.FetchPattern(SelectedPatternIdx);
            }
        }, default);

        // draw out the pattern information
        if(SelectedPatternIdx != -1)
        {
            DrawPatternInfo();
        }
        else
        {
            ImGui.Text("No pattern selected.");
        }
    }

    private void DrawPatternInfo()
    {
        var region = ImGui.GetContentRegionAvail();
        if (SelectedPatternIdx == -1)
        {
            ImGui.Text("No pattern selected.");
            return;
        }
        // display pattern information
        _uiShared.BigText(SelectedPattern!.Name);

        UiSharedService.ColorText("(By " + SelectedPattern!.Author + ")", ImGuiColors.ParsedGold);

        UiSharedService.TextWrapped(SelectedPattern!.Description);

        ImGui.TextUnformatted("Duration : " + SelectedPattern!.Duration);

        if(_uiShared.IconTextButton(FontAwesomeIcon.Repeat, SelectedPattern!.ShouldLoop ? "Looping" : "Not Looping"))
        {
            if(SelectedPattern!.ShouldLoop)
            {
                // set to not looping
                _handler.SetPatternLoop(SelectedPatternIdx, false);
            }
            else
            {
                // set to looping
                _handler.SetPatternLoop(SelectedPatternIdx, true);
            }
        }

        var widthRef = ImGui.GetContentRegionAvail().X;
        var currentWidth = 0f;
        ImGui.Text("Tags: ");
        foreach (string tag in SelectedPattern!.Tags)
        {
            currentWidth += _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Tag, tag);
            if (currentWidth < widthRef)
            {
                ImGui.SameLine();
            }
            else
            {
                // reset currentwidth
                currentWidth = 0f;
            }
            _uiShared.IconTextButton(FontAwesomeIcon.Tag, tag);
        }
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
                var canSeeIcon = _handler.GetUserIsAllowedToView(SelectedPatternIdx, pair.UserData.UID) ? FontAwesomeIcon.Times : FontAwesomeIcon.Check;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    string.Empty, false, true))
                    {
                        if (canSeeIcon == FontAwesomeIcon.Times)
                        {
                            _handler.GrantUserAccessToPattern(SelectedPatternIdx, pair.UserData.UID);
                        }
                        else
                        {
                            _handler.RevokeUserAccessToPattern(SelectedPatternIdx, pair.UserData.UID);
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
    public void DrawPatternFilterList(float width)
    {
        using var group = ImRaii.Group();
        DefaultItemSpacing = ImGui.GetStyle().ItemSpacing;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero).Push(ImGuiStyleVar.FrameRounding, 0);
        ImGui.SetNextItemWidth(width);
        LowerString.InputWithHint("##patternFilter", "Filter Patterns...", ref PatternSearchString, 64);

        DrawPatternSelector(width);
    }

    private void DrawPatternSelector(float width)
    {
        using var child = ImRaii.Child("##PatternSelector", new Vector2(width, ImGui.GetContentRegionAvail().Y), true, ImGuiWindowFlags.NoScrollbar);
        if (!child)
            return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, DefaultItemSpacing);
        // temp store the PatternData of the potentially new item
        int newlySelectedPattern = -1;
        bool itemSelected = false; // Flag to check if an item has been selected


        // draw out the pattern names
        foreach (string patternName in _handler.PatternNames)
        {
            // we determine if it is selected by if it is within the filter we have defined
            bool showPattern = PatternSearchString.IsEmpty || patternName.Contains(PatternSearchString.Lower, StringComparison.OrdinalIgnoreCase);

            // if we should show this pattern
            if(showPattern)
            {
                // draw it as a selectable
                bool isSelected = SelectedPatternIdx.Equals(patternName);
                if (ImGui.Selectable(patternName, isSelected))
                {
                    // update the selected PatternData (could be null, so be careful here.
                    newlySelectedPattern = _handler.GetPatternIdxByName(patternName);
                    itemSelected = true; // Mark that an item has been selected
                }
            }
        }

        // If an item was selected during this ImGui frame, update the selected pattern
        if (itemSelected && newlySelectedPattern != -1)
        {
            SelectedPatternIdx = newlySelectedPattern;
            SelectedPattern = _handler.FetchPattern(SelectedPatternIdx);
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
            _handler.AddPattern(pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not set pattern data from clipboard.{ex.Message}");
        }
    }


}
