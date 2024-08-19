using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.Permissions;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.UserPair;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using System.Numerics;
using static GagSpeak.UI.Components.MainTabMenu;

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
    // store the created texturewrap for the supporter tier image so we are not loading it every single time.
    private IDalamudTextureWrap? _supporterWrap = null;
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

    protected override void Dispose(bool disposing)
    {
        _supporterWrap?.Dispose();
        _supporterWrap = null;
        base.Dispose(disposing);
    }

    public void DrawPairedClient()
    {
        // get the current screen cursor pos
        var cursorPos = ImGui.GetCursorPosX();
        using var id = ImRaii.PushId(GetType() + _id);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), _wasHovered);
        using (ImRaii.Child(GetType() + _id, new System.Numerics.Vector2(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetCursorPosX(), ImGui.GetFrameHeight())))
        {
            ImUtf8.SameLineInner();
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
        // if they were a supporter, go back to the start and draw the image.
        if (!_pair.UserData.SupporterTier.Equals(CkSupporterTier.NoRole)) DrawSupporterIcon(cursorPos);
    }

    public void DrawPairedClientListForm()
    {
        var cursorPos = ImGui.GetCursorPosX();
        using var id = ImRaii.PushId(GetType() + _id);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), _wasHovered);
        using (ImRaii.Child(GetType() + _id, new Vector2(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetCursorPosX(), ImGui.GetFrameHeight())))
        {
            ImUtf8.SameLineInner();
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
        // if they were a supporter, go back to the start and draw the image.
        if (!_pair.UserData.SupporterTier.Equals(CkSupporterTier.NoRole)) DrawSupporterIcon(cursorPos);
    }

    private string tooltipString = "";
    private void DrawSupporterIcon(float cursorPos)
    {
        ImGui.SameLine(cursorPos);
        ImGui.SetCursorPosX(cursorPos - _uiSharedService.GetIconData(FontAwesomeIcon.EllipsisV).X - ImGui.GetStyle().ItemSpacing.X);
        // fetch new image if needed, otherwise use existing
        if (_supporterWrap == null)
        {
            // fetch the supporter wrap.
            switch (_pair.UserData.SupporterTier)
            {
                case CkSupporterTier.ServerBooster:
                    _supporterWrap = _uiSharedService.RentSupporterBooster();
                    tooltipString = (_pair.GetNickname() ?? _pair.UserData.AliasOrUID) + " is supporting the discord with a server Boost!";
                    break;
                case CkSupporterTier.IllustriousSupporter:
                    _supporterWrap = _uiSharedService.RentSupporterTierOne();
                    tooltipString = (_pair.GetNickname() ?? _pair.UserData.AliasOrUID) + " is supporting CK as a Illustrious Supporter";
                    break;
                case CkSupporterTier.EsteemedPatron:
                    _supporterWrap = _uiSharedService.RentSupporterTierTwo();
                    tooltipString = (_pair.GetNickname() ?? _pair.UserData.AliasOrUID) + " is supporting CK as a Esteemed Patron";
                    break;
                case CkSupporterTier.DistinguishedConnoisseur:
                    _supporterWrap = _uiSharedService.RentSupporterTierThree();
                    tooltipString = (_pair.GetNickname() ?? _pair.UserData.AliasOrUID) + " is supporting CK as a Distinguished Connoisseur";
                    break;
                case CkSupporterTier.KinkporiumMistress:
                    _supporterWrap = _uiSharedService.RentSupporterTierFour();
                    tooltipString = (_pair.GetNickname() ?? _pair.UserData.AliasOrUID) + " is the Shop Mistress of CK, and the Dev of GagSpeak.";
                    break;
                default:
                    break;
            }
        }

        if ((_supporterWrap is { } supporterImage))
        {
            ImGui.Image(supporterImage.ImGuiHandle, new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight()));
        }
        UiSharedService.AttachToolTip(tooltipString);
        // return to the end of the line.
    }

    private void DrawLeftSide()
    {
        var userPairText = string.Empty;

        ImGui.AlignTextToFramePadding();

        if (!_pair.IsOnline)
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            _uiSharedService.IconText(_pair.IndividualPairStatus == IndividualPairStatus.OneSided
                ? FontAwesomeIcon.ArrowsLeftRight
                : _pair.IndividualPairStatus == IndividualPairStatus.Bidirectional
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
            _uiSharedService.IconText(_pair.IndividualPairStatus == IndividualPairStatus.Bidirectional
                ? FontAwesomeIcon.User : FontAwesomeIcon.Users);
            userPairText = _pair.UserData.AliasOrUID + " is online";
        }

        if (_pair.IndividualPairStatus == IndividualPairStatus.OneSided)
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
            // open the permission setting window
            _mediator.Publish(new OpenUserPairPermissions(_pair, StickyWindowType.PairActionFunctions));
        }

        currentRightSide -= permissionsButtonSize.X + spacingX;
        ImGui.SameLine(currentRightSide);
        if (_uiSharedService.IconButton(FontAwesomeIcon.Cog))
        {
            if (Pair == null)
            {
                Logger.LogWarning("Pair is null");
            }
            // if we press the cog, we should modify its appearance, and set that we are drawing for this pair to true
            _mediator.Publish(new OpenUserPairPermissions(_pair, StickyWindowType.ClientPermsForPair));
        }
        UiSharedService.AttachToolTip("Set your Permissions for " + _pair.UserData.AliasOrUID);

        currentRightSide -= permissionsButtonSize.X + spacingX;
        ImGui.SameLine(currentRightSide);
        if (_uiSharedService.IconButton(FontAwesomeIcon.Search))
        {
            // if we press the cog, we should modify its appearance, and set that we are drawing for this pair to true
            _mediator.Publish(new OpenUserPairPermissions(_pair, StickyWindowType.PairPerms));
        }
        UiSharedService.AttachToolTip("Inspect " + _pair.UserData.AliasOrUID + "'s permissions");

        return currentRightSide;
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
