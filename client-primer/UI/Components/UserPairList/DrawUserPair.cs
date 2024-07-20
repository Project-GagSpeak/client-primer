using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.Permissions;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.UserPair;
using ImGuiNET;

namespace GagSpeak.UI.Components.UserPairList;

/// <summary>
/// Class handling the draw function for a singular user pair that the client has. (one row)
/// </summary>
public class DrawUserPair : DisposableMediatorSubscriberBase
{
    protected readonly ApiController _apiController;
    protected readonly IdDisplayHandler _displayHandler;
    protected readonly GagspeakMediator _mediator;
    protected Pair _pair;
    private readonly string _id;
    private readonly SelectTagForPairUi _selectTagForPairUi;
    private readonly UiSharedService _uiSharedService;
    private float _menuWidth = -1;
    private bool _wasHovered = false;
    public DrawUserPair(ILogger<DrawUserPair> logger, string id, Pair entry, ApiController apiController,
        IdDisplayHandler uIDDisplayHandler, GagspeakMediator gagspeakMediator, SelectTagForPairUi selectTagForPairUi,
        UiSharedService uiSharedService) : base(logger, gagspeakMediator)
    {
        _id = id;
        _pair = entry;
        _apiController = apiController;
        _displayHandler = uIDDisplayHandler;
        _mediator = gagspeakMediator;
        _selectTagForPairUi = selectTagForPairUi;
        _uiSharedService = uiSharedService;
    }

    public Pair Pair => _pair;
    public UserPairDto UserPair => _pair.UserPair!;

    public void DrawPairedClient()
    {
        using var id = ImRaii.PushId(GetType() + _id);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), _wasHovered);
        using (ImRaii.Child(GetType() + _id, new System.Numerics.Vector2(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetCursorPosX(), ImGui.GetFrameHeight())))
        {
            DrawLeftSide();
            ImGui.SameLine();
            var posX = ImGui.GetCursorPosX();

            float rightSide = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()
                              - (_uiSharedService.GetIconButtonSize(FontAwesomeIcon.EllipsisV).X);

            rightSide = DrawRightSide();

            DrawName(posX, rightSide, false);
        }
        _wasHovered = ImGui.IsItemHovered();
        color.Dispose();
    }

    public void DrawPairedClientListForm()
    {
        using var id = ImRaii.PushId(GetType() + _id);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), _wasHovered);
        using (ImRaii.Child(GetType() + _id, new System.Numerics.Vector2(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetCursorPosX(), ImGui.GetFrameHeight())))
        {
            DrawLeftSide();
            ImGui.SameLine();
            var posX = ImGui.GetCursorPosX();

            float rightSide = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()
                              - (_uiSharedService.GetIconButtonSize(FontAwesomeIcon.EllipsisV).X);
            DrawName(posX, rightSide, true);
        }
        // if we left clicked this item, we should set the selected pair to this pair.
        if (ImGui.IsItemClicked())
        {
            _mediator.Publish(new UpdateDisplayWithPair(_pair));
        }
        _wasHovered = ImGui.IsItemHovered();
        color.Dispose();
    }

    private void DrawLeftSide()
    {
        var userPairText = string.Empty;

        ImGui.AlignTextToFramePadding();

        if (!_pair.IsOnline)
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            _uiSharedService.IconText(_pair.IndividualPairStatus == GagspeakAPI.Data.Enum.IndividualPairStatus.OneSided
                ? FontAwesomeIcon.ArrowsLeftRight
                : _pair.IndividualPairStatus == GagspeakAPI.Data.Enum.IndividualPairStatus.Bidirectional
                    ? FontAwesomeIcon.User : FontAwesomeIcon.Users);
            userPairText = _pair.UserData.AliasOrUID + " is offline";
        }
        else if (_pair.IsVisible)
        {
            _uiSharedService.IconText(FontAwesomeIcon.Eye, ImGuiColors.ParsedGreen);
            userPairText = _pair.UserData.AliasOrUID + " is visible: " + _pair.PlayerName + Environment.NewLine + "Click to target this player";
            if (ImGui.IsItemClicked())
            {
                _mediator.Publish(new TargetPairMessage(_pair));
            }
        }
        else
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            _uiSharedService.IconText(_pair.IndividualPairStatus == GagspeakAPI.Data.Enum.IndividualPairStatus.Bidirectional
                ? FontAwesomeIcon.User : FontAwesomeIcon.Users);
            userPairText = _pair.UserData.AliasOrUID + " is online";
        }

        if (_pair.IndividualPairStatus == GagspeakAPI.Data.Enum.IndividualPairStatus.OneSided)
        {
            userPairText += UiSharedService.TooltipSeparator + "User has not added you back";
        }
        else if (_pair.IndividualPairStatus == GagspeakAPI.Data.Enum.IndividualPairStatus.Bidirectional)
        {
            userPairText += UiSharedService.TooltipSeparator + "You are directly Paired";
        }

        UiSharedService.AttachToolTip(userPairText);

        ImGui.SameLine();
    }

    private void DrawName(float leftSide, float rightSide, bool isASelectable)
    {
        _displayHandler.DrawPairText(_id, _pair, leftSide, () => rightSide - leftSide, isASelectable);
    }

    private float DrawRightSide()
    {
        var permissionsButtonSize = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Cog);
        var barButtonSize = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.EllipsisV);
        var spacingX = ImGui.GetStyle().ItemSpacing.X / 2;
        var windowEndX = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth();
        var currentRightSide = windowEndX - barButtonSize.X;

        ImGui.SameLine(currentRightSide);
        ImGui.AlignTextToFramePadding();
        if (_uiSharedService.IconButton(FontAwesomeIcon.EllipsisV))
        {
            ImGui.OpenPopup("User Flyout Menu");
        }

        currentRightSide -= permissionsButtonSize.X + spacingX;
        ImGui.SameLine(currentRightSide);
        if (_uiSharedService.IconButton(FontAwesomeIcon.Cog))
        {
            // if we press the cog, we should modify its apperance, and set that we are drawing for this pair to true
            _mediator.Publish(new OpenUserPairPermissions(_pair, StickyWindowType.PairPerms));
        }
        UiSharedService.AttachToolTip(!_pair.IsOnline
            ? "Change " + _pair.UserData.AliasOrUID + "'s permissions"
            : "Close " + _pair.UserData.AliasOrUID + "'s permissions window");

        currentRightSide -= permissionsButtonSize.X + spacingX;
        ImGui.SameLine(currentRightSide);
        if (_uiSharedService.IconButton(FontAwesomeIcon.Wrench))
        {
            // if we press the cog, we should modify its appearance, and set that we are drawing for this pair to true
            _mediator.Publish(new OpenUserPairPermissions(_pair, StickyWindowType.ClientPermsForPair));
        }
        UiSharedService.AttachToolTip(!_pair.IsOnline
            ? "Change your permission access for " + _pair.UserData.AliasOrUID
            : "Close your permissions access window");


        if (ImGui.BeginPopup("User Flyout Menu"))
        {
            using (ImRaii.PushId($"buttons-{_pair.UserData.UID}"))
            {
                ImGui.TextUnformatted("Common Pair Functions");
                DrawCommonClientMenu();
                ImGui.Separator();
                DrawPairedClientMenu();
                if (_menuWidth <= 0)
                {
                    _menuWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
                }
            }

            ImGui.EndPopup();
        }

        return currentRightSide - spacingX;
    }

    private void DrawPairedClientMenu()
    {
        if (ImGui.BeginMenu("Gag Interactions"))
        {
            DrawGagInteractionsMenu("Undermost Layer");
            DrawGagInteractionsMenu("Middle Layer");
            DrawGagInteractionsMenu("Outermost Layer");
            ImGui.EndMenu();
        }
        ImGui.Separator();

        if (ImGui.BeginMenu("Wardrobe Interactions"))
        {
            if (ImGui.BeginMenu("Enable Restraint Set"))
            {
                // Code to display list of pair's restraint sets
                ImGui.EndMenu();
            }
            if (ImGui.MenuItem("Lock Active Set"))
            {
                // locks the restraint set, if given permission
            }
            if (ImGui.MenuItem("Unlock Active Set"))
            {
                // unlocks the restraint set, if given permission
            }
            if (ImGui.MenuItem("Disable Restraint Set"))
            {
                // removes the restraint set, if given permission
            }
            ImGui.EndMenu();
        }
        ImGui.Separator();

        if (ImGui.BeginMenu("Puppeteer Interactions"))
        {
            if (ImGui.MenuItem("TriggerPhrase"))
            {
                // display trigger phrase
            }
            if (ImGui.MenuItem("Start Character"))
            {
                // show start char
            }
            if (ImGui.MenuItem("End Character"))
            {
                // show end char
            }
            if (ImGui.BeginMenu("Alias List"))
            {
                // display the list of alias's, with tooltips of what they do
            }
            ImGui.EndMenu();
        }
        ImGui.Separator();

        if (ImGui.BeginMenu("Toybox Interactions"))
        {
            if (ImGui.MenuItem("Vibrator Remote"))
            {
                // open vibrator remote preset to request control to this pair.
            }
            if (ImGui.BeginMenu("Patterns"))
            {
                // display list of patterns
                // for each pattern:
                if (ImGui.MenuItem("Execute"))
                {
                    // execute the pattern
                }
            }
            if (ImGui.MenuItem("Stop Pattern"))
            {

            }
            if (ImGui.MenuItem("Lock Toybox"))
            {
                // lock the toybox
            }
            if (ImGui.MenuItem("Unlock Toybox"))
            {
                // unlock the toybox
            }
            ImGui.EndMenu();
        }


        DrawIndividualMenu();
    }

    private void DrawGagInteractionsMenu(string layerID)
    {
        if (ImGui.BeginMenu(layerID))
        {
            if (ImGui.MenuItem("Apply Gag"))
            {
                // Code to display list of gags
            }
            if (ImGui.BeginMenu("Lock Gag"))
            {
                // Code to list lock types
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Unlock Gag"))
            {
                // Code to list lock types and possibly show a password insertion field
                ImGui.EndMenu();
            }
            if (ImGui.MenuItem("Remove Gag"))
            {
                // Action to remove gag on this layer
            }
            ImGui.EndMenu();
        }
    }


    private void DrawCommonClientMenu()
    {
        if (!_pair.IsPaused)
        {
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.User, "Open Profile", _menuWidth, true))
            {
                _displayHandler.OpenProfile(_pair);
                ImGui.CloseCurrentPopup();
            }
            UiSharedService.AttachToolTip("Opens the profile for this user in a new window");
        }
        if (_pair.IsPaired)
        {
            var pauseIcon = _pair.UserPair!.OwnPairPerms.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
            var pauseIconSize = _uiSharedService.GetIconButtonSize(pauseIcon);
            var pauseText = _pair.UserPair!.OwnPairPerms.IsPaused ? $"Unpause {_pair.UserData.AliasOrUID}" : $"Pause {_pair.UserData.AliasOrUID}";
            if (_uiSharedService.IconTextButton(pauseIcon, pauseText, _menuWidth, true))
            {
                var perm = _pair.UserPair!.OwnPairPerms;
                _ = _apiController.UserUpdateOwnPairPerm(new UserPairPermChangeDto(_pair.UserData,
                    new KeyValuePair<string, object>("IsPaused", !perm.IsPaused)));
            }
            UiSharedService.AttachToolTip(!_pair.UserPair!.OwnPairPerms.IsPaused
                ? "Pause pairing with " + _pair.UserData.AliasOrUID
                : "Resume pairing with " + _pair.UserData.AliasOrUID);
        }
        if (_pair.IsVisible)
        {
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.Sync, "Reload last data", _menuWidth, true))
            {
                _pair.ApplyLastReceivedIpcData(forced: true);
                ImGui.CloseCurrentPopup();
            }
            UiSharedService.AttachToolTip("This reapplies the last received character data to this character");
        }

        ImGui.Separator();
    }

    private void DrawIndividualMenu()
    {
        var entryUID = _pair.UserData.AliasOrUID;

        if (_pair.IndividualPairStatus != GagspeakAPI.Data.Enum.IndividualPairStatus.None)
        {
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.Trash, "Unpair Permanently", _menuWidth, true) && UiSharedService.CtrlPressed())
            {
                _ = _apiController.UserRemovePair(new(_pair.UserData));
            }
            UiSharedService.AttachToolTip("Hold CTRL and click to unpair permanently from " + entryUID);
        }
        else
        {
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.Plus, "Pair individually", _menuWidth, true))
            {
                _ = _apiController.UserAddPair(new(_pair.UserData));
            }
            UiSharedService.AttachToolTip("Pair individually with " + entryUID);
        }
    }
}
