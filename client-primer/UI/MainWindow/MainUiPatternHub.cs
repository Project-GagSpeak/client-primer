using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.MainWindow;
public class MainUiPatternHub : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly PatternHubService _patternHubService;

    public MainUiPatternHub(ILogger<MainUiPatternHub> logger,
        GagspeakMediator mediator, PatternHubService patternHubService,
        UiSharedService uiSharedService) : base(logger, mediator)
    {
        _patternHubService = patternHubService;
        _uiSharedService = uiSharedService;
    }
    public void DrawPatternHub()
    {
        // get the width of the window content region we set earlier
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();

        // draw the search filter
        DrawSearchFilter(_windowContentWidth);
        ImGui.Separator();

        // draw the results if there are any.
        if (_patternHubService.SearchResults.Count > 0)
        {
            DisplayResults();
        }
        else
        {
            ImGui.Spacing();
            ImGuiUtil.Center("Search something to find results!");
        }
    }

    private void DisplayResults()
    {
        // draw the current status if we are still fetching from the server.
        _patternHubService.DisplayPendingMessages();

        // create a child window here. It will allow us to scroll up and dont in our pattern results search.
        using var patternResultChild = ImRaii.Child("##PatternResultChild", ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.NoScrollbar);
        // display a custom box icon for each search result obtained.
        foreach (var pattern in _patternHubService.SearchResults)
        {
            // draw a unique box for each pattern result. (SELF REMINDER CORDY, DONT CARE TOO MUCH ABOUT THE VISUALS HERE THEY WILL BE REWORKED.
            DrawPatternResultBox(pattern);
        }
    }

    private void DrawPatternResultBox(ServerPatternInfo patternInfo)
    {
        // push rounding window corners
        using var windowRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 5f);
        // push a pink border color for the window border.
        using var borderColor = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var borderCol = ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink);
        // push a less transparent very dark grey background color.
        using var bgColor = ImRaii.PushColor(ImGuiCol.WindowBg, new Vector4(0.25f, 0.2f, 0.2f, 0.9f));
        // create the child window.
        using (var patternResChild = ImRaii.Child($"##PatternResult_{patternInfo.Identifier}", new Vector2(ImGui.GetContentRegionAvail().X, 100), true, ImGuiWindowFlags.ChildWindow))
        {
            using (var group = ImRaii.Group())
            {
                UiSharedService.ColorText(patternInfo.Name, ImGuiColors.DalamudWhite);
                ImGui.SameLine();
                using (var color = ImRaii.PushColor(ImGuiCol.Text, patternInfo.HasLiked ? ImGuiColors.ParsedPink : ImGuiColors.DalamudGrey))
                {
                    if (_uiSharedService.IconButton(FontAwesomeIcon.Heart))
                    {
                        _patternHubService.RatePattern(patternInfo.Identifier);
                    }
                }
                ImGui.SameLine();
                UiSharedService.ColorText(patternInfo.Likes.ToString(), ImGuiColors.DalamudGrey);
                ImGui.SameLine();
                using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudWhite2))
                {
                    if(_uiSharedService.IconTextButton(FontAwesomeIcon.Download, patternInfo.Downloads.ToString()))
                    {
                        _patternHubService.DownloadPatternFromServer(patternInfo.Identifier);
                    }
                }
            }
            // next line:
            _uiSharedService.IconText(FontAwesomeIcon.Portrait);
            ImGui.SameLine();
            UiSharedService.ColorText(patternInfo.Author, ImGuiColors.DalamudGrey);
            ImGui.SameLine();
            _uiSharedService.IconText(FontAwesomeIcon.Stopwatch);
            ImGui.SameLine();
            UiSharedService.ColorText(patternInfo.Length.ToString(), ImGuiColors.ParsedPink);
            var vibeSize = _uiSharedService.GetIconData(FontAwesomeIcon.Water);
            var rotationSize = _uiSharedService.GetIconData(FontAwesomeIcon.GroupArrowsRotate);
            var oscillationSize = _uiSharedService.GetIconData(FontAwesomeIcon.WaveSquare);
            float rightEnd = ImGui.GetWindowContentRegionMax().X - vibeSize.X - rotationSize.X - oscillationSize.X - 3 * ImGui.GetStyle().ItemSpacing.X;
            ImGui.SameLine(rightEnd);
            _uiSharedService.BooleanToColoredIcon(patternInfo.UsesVibrations, true, FontAwesomeIcon.Water);
            ImGui.SameLine();
            _uiSharedService.BooleanToColoredIcon(patternInfo.UsesRotations, true, FontAwesomeIcon.GroupArrowsRotate);
            ImGui.SameLine();
            _uiSharedService.BooleanToColoredIcon(patternInfo.UsesOscillation, true, FontAwesomeIcon.WaveSquare);

            // next line for tags
            _uiSharedService.IconText(FontAwesomeIcon.Tags);
            ImGui.SameLine();
            ImGui.TextUnformatted(string.Join(", ", patternInfo.Tags));
        }
        UiSharedService.AttachToolTip("Additional Information:" + Environment.NewLine
            + "Description: " + patternInfo.Description + Environment.NewLine
            + "Published On: " + patternInfo.UploadedDate);
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawSearchFilter(float availableWidth)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemInnerSpacing, new Vector2(2, ImGui.GetStyle().ItemInnerSpacing.Y));
        float comboSize = ImGui.CalcTextSize("Upload Date    ").X;
        FontAwesomeIcon sortIcon = _patternHubService.CurrentSort == SearchSort.Ascending ? FontAwesomeIcon.SortAmountUp : FontAwesomeIcon.SortAmountDown;
        float sortSize = _uiSharedService.GetIconButtonSize(sortIcon).X;
        float updateSize = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Sync).X;
        string patternFilter = _patternHubService.SearchQuery;
        ImGui.SetNextItemWidth(availableWidth - comboSize - sortSize - updateSize - 3*ImGui.GetStyle().ItemInnerSpacing.X);
        if (ImGui.InputTextWithHint("##patternSearchFilter", "Search for Patterns...", ref patternFilter, 255, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _patternHubService.SearchPatterns(patternFilter);
        }
        UiSharedService.AttachToolTip("Enter fetches results from your search!");
        ImUtf8.SameLineInner();
        if (_uiSharedService.IconButton(FontAwesomeIcon.Sync))
        {
            _patternHubService.UpdateResults();
        }
        UiSharedService.AttachToolTip("Update Search Results");
        ImUtf8.SameLineInner();
        _uiSharedService.DrawCombo("##patternFilterType", comboSize, Enum.GetValues<SearchFilter>(), (filter) => _patternHubService.FilterToName(filter),
        (filter) => { _patternHubService.SetFilter(filter); }, _patternHubService.CurrentFilter, false, ImGuiComboFlags.NoArrowButton);
        UiSharedService.AttachToolTip("Filter Type");
        ImUtf8.SameLineInner();
        if (_uiSharedService.IconButton(sortIcon))
        {
            _patternHubService.ToggleSort();
        }
        UiSharedService.AttachToolTip("Toggle Sort\n(Current: "+_patternHubService.CurrentSort+")");
    }
}

