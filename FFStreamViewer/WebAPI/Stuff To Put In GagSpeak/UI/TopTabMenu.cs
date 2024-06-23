using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ImGuiNET;
using Gagspeak.API.Data.Enum;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI;
using System.Numerics;

namespace FFStreamViewer.WebAPI.UI;

/// <summary>
/// The class for the custom top tab display (the bar in gagspeak just under the users online display)
/// </summary>
public class TopTabMenu
{
    private readonly ApiController _apiController;

    private readonly GagspeakMediator _mediator;

    private readonly PairManager _pairManager;
    private readonly UiSharedService _uiSharedService;
    private string _filter = string.Empty;
    private int _globalControlCountdown = 0;

    private string _pairToAdd = string.Empty;

    private SelectedTab _selectedTab = SelectedTab.None;
    public TopTabMenu(GagspeakMediator mediator, ApiController apiController, 
        PairManager pairManager, UiSharedService uiSharedService)
    {
        _mediator = mediator;
        _apiController = apiController;
        _pairManager = pairManager;
        _uiSharedService = uiSharedService;
    }

    private enum SelectedTab
    {
        None,
        Individual,
        Filter,
        UserConfig
    }

    public string Filter
    {
        get => _filter;
        private set
        {
            if (!string.Equals(_filter, value, StringComparison.OrdinalIgnoreCase))
            {
                _mediator.Publish(new RefreshUiMessage());
            }

            _filter = value;
        }
    }

    private SelectedTab TabSelection
    {
        get => _selectedTab; set
        {
            if (_selectedTab == SelectedTab.Filter && value != SelectedTab.Filter)
            {
                Filter = string.Empty;
            }

            _selectedTab = value;
        }
    }
    public void Draw()
    {
        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var buttonX = (availableWidth - (spacing.X * 3)) / 4f;
        var buttonY = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Pause).Y;
        var buttonSize = new Vector2(buttonX, buttonY);
        var drawList = ImGui.GetWindowDrawList();
        var underlineColor = ImGui.GetColorU32(ImGuiCol.Separator);
        var btncolor = ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0)));

        ImGuiHelpers.ScaledDummy(spacing.Y / 2f);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.User.ToIconString(), buttonSize))
            {
                TabSelection = TabSelection == SelectedTab.Individual ? SelectedTab.None : SelectedTab.Individual;
            }
            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.Individual)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Individual Pair Menu");

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.Filter.ToIconString(), buttonSize))
            {
                TabSelection = TabSelection == SelectedTab.Filter ? SelectedTab.None : SelectedTab.Filter;
            }

            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.Filter)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Filter");

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.UserCog.ToIconString(), buttonSize))
            {
                TabSelection = TabSelection == SelectedTab.UserConfig ? SelectedTab.None : SelectedTab.UserConfig;
            }

            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.UserConfig)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Your User Menu");

        ImGui.NewLine();
        btncolor.Dispose();

        ImGuiHelpers.ScaledDummy(spacing);

        if (TabSelection == SelectedTab.Individual)
        {
            DrawAddPair(availableWidth, spacing.X);
            DrawGlobalIndividualButtons(availableWidth, spacing.X);
        }
        else if (TabSelection == SelectedTab.Filter)
        {
            DrawFilter(availableWidth, spacing.X);
        }
        else if (TabSelection == SelectedTab.UserConfig)
        {
            DrawUserConfig(availableWidth, spacing.X);
        }

        if (TabSelection != SelectedTab.None) ImGuiHelpers.ScaledDummy(3f);
        ImGui.Separator();
    }

    private void DrawAddPair(float availableXWidth, float spacingX)
    {
        var buttonSize = _uiSharedService.GetIconTextButtonSize(FontAwesomeIcon.UserPlus, "Add");
        ImGui.SetNextItemWidth(availableXWidth - buttonSize - spacingX);
        ImGui.InputTextWithHint("##otheruid", "Other players UID/Alias", ref _pairToAdd, 20);
        ImGui.SameLine();
        var alreadyExisting = _pairManager.DirectPairs.Exists(p => string.Equals(p.UserData.UID, _pairToAdd, StringComparison.Ordinal) || string.Equals(p.UserData.Alias, _pairToAdd, StringComparison.Ordinal));
        using (ImRaii.Disabled(alreadyExisting || string.IsNullOrEmpty(_pairToAdd)))
        {
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.UserPlus, "Add"))
            {
                // call the UserAddPair function on the server with the user data transfer object
                _ = _apiController.UserAddPair(new(new(_pairToAdd)));
                _pairToAdd = string.Empty;
            }
        }
        UiSharedService.AttachToolTip("Pair with " + (_pairToAdd.IsNullOrEmpty() ? "other user" : _pairToAdd));
    }

    private void DrawFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiSharedService.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = Filter;
        if (ImGui.InputTextWithHint("##filter", "Filter for UID/notes", ref filter, 255))
        {
            Filter = filter;
        }
        ImGui.SameLine();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(Filter));
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            Filter = string.Empty;
        }
    }

    private void DrawGlobalIndividualButtons(float availableXWidth, float spacingX)
    {
        var buttonX = (availableXWidth - (spacingX * 3)) / 4f;
        var buttonY = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Pause).Y;
        var buttonSize = new Vector2(buttonX, buttonY);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            using var disabled = ImRaii.Disabled(_globalControlCountdown > 0);

            if (ImGui.Button(FontAwesomeIcon.Pause.ToIconString(), buttonSize))
            {
                ImGui.OpenPopup("Individual Pause");
            }
        }
        UiSharedService.AttachToolTip("Globally resume or pause all individual pairs." + UiSharedService.TooltipSeparator
            + (_globalControlCountdown > 0 ? UiSharedService.TooltipSeparator + "Available again in " + _globalControlCountdown + " seconds." : string.Empty));

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            using var disabled = ImRaii.Disabled(_globalControlCountdown > 0);

            if (ImGui.Button(FontAwesomeIcon.VolumeUp.ToIconString(), buttonSize))
            {
                ImGui.OpenPopup("Individual Sounds");
            }
        }
        UiSharedService.AttachToolTip("Globally enable or disable sound sync with all individual pairs."
            + (_globalControlCountdown > 0 ? UiSharedService.TooltipSeparator + "Available again in " + _globalControlCountdown + " seconds." : string.Empty));

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            using var disabled = ImRaii.Disabled(_globalControlCountdown > 0);

            if (ImGui.Button(FontAwesomeIcon.Running.ToIconString(), buttonSize))
            {
                ImGui.OpenPopup("Individual Animations");
            }
        }
        UiSharedService.AttachToolTip("Globally enable or disable animation sync with all individual pairs." + UiSharedService.TooltipSeparator
            + (_globalControlCountdown > 0 ? UiSharedService.TooltipSeparator + "Available again in " + _globalControlCountdown + " seconds." : string.Empty));

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            using var disabled = ImRaii.Disabled(_globalControlCountdown > 0);

            if (ImGui.Button(FontAwesomeIcon.Sun.ToIconString(), buttonSize))
            {
                ImGui.OpenPopup("Individual VFX");
            }
        }
        UiSharedService.AttachToolTip("Globally enable or disable VFX sync with all individual pairs." + UiSharedService.TooltipSeparator
            + (_globalControlCountdown > 0 ? UiSharedService.TooltipSeparator + "Available again in " + _globalControlCountdown + " seconds." : string.Empty));
    }

    private void DrawUserConfig(float availableWidth, float spacingX)
    {
        var buttonX = (availableWidth - spacingX) / 2f;
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.UserCircle, "Edit Gagspeak Profile", buttonX))
        {
            _mediator.Publish(new UiToggleMessage(typeof(EditProfileUi)));
        }
        UiSharedService.AttachToolTip("Edit your Gagspeak Profile");
        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.PersonCircleQuestion, "Chara Data Analysis", buttonX))
        {
            // _mediator.Publish(new UiToggleMessage(typeof(DataAnalysisUi)));
        }
        UiSharedService.AttachToolTip("View and analyze your generated character data");
    }

    private async Task GlobalControlCountdown(int countdown)
    {
#if DEBUG
        return;
#endif

        _globalControlCountdown = countdown;
        while (_globalControlCountdown > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            _globalControlCountdown--;
        }
    }
}
