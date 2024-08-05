using Dalamud.Interface;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.Permissions;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Enum;
using ImGuiNET;

namespace GagSpeak.UI.MainWindow;

/// <summary> 
/// Sub-class of the main UI window. Handles drawing the whitelist/contacts tab of the main UI.
/// </summary>
public class MainUiWhitelist : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly UiFactory _uiFactory;
    private readonly UiSharedService _uiShared;
    private readonly UserPairListHandler _userPairListHandler;
    private readonly PairManager _pairManager;
    private readonly GagspeakConfigService _gagspeakConfig;
    private readonly ServerConfigurationManager _serverConfigs;

    public MainUiWhitelist(ILogger<MainUiWhitelist> logger,
        GagspeakMediator mediator, ApiController apiController,
        UiFactory uiFactory, UiSharedService uiSharedService, 
        UserPairListHandler userPairListHandler, PairManager pairManager,
        GagspeakConfigService gagspeakConfig,
        ServerConfigurationManager serverConfigs) : base(logger, mediator)
    {
        _apiController = apiController;
        _uiFactory = uiFactory;
        _uiShared = uiSharedService;
        _userPairListHandler = userPairListHandler;
        _pairManager = pairManager;
        _gagspeakConfig = gagspeakConfig;
        _serverConfigs = serverConfigs;

        // updates the draw folders by recollecting them, and updates the drawPair list of distinct draw pairs
        _userPairListHandler.UpdateDrawFoldersAndUserPairDraws();

        Mediator.Subscribe<RefreshUiMessage>(this, (msg) =>
        {
            // update draw folders
            _userPairListHandler.UpdateDrawFoldersAndUserPairDraws();
            // update the cog statuses
            // UpdateShouldOpenStatus();
        });
    }

    // Attributes related to the drawing of the whitelist / contacts / pair list
    private Pair? _lastAddedUser;
    private string _lastAddedUserComment = string.Empty;
    // If we should draw sticky perms for the currently selected user pair.
    private bool _shouldDrawStickyPerms = false;
    private bool _showModalForUserAddition;

    // hold a reference to the sticky pair permissions we are drawing
    private Pair PairToDrawPermissionsFor = null!;

    /// <summary>
    /// Main Draw function for the Whitelist/Contacts tab of the main UI
    /// </summary>
    public float DrawWhitelistSection()
    {
        // get the width of the window content region we set earlier
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        var _spacingX = ImGui.GetStyle().ItemSpacing.X;
        float pairlistEnd = 0;

        try
        {
            // show the search filter just above the contacts list to form a nice separation.
            _userPairListHandler.DrawSearchFilter(_windowContentWidth, ImGui.GetStyle().ItemSpacing.X);
            ImGui.Separator();

            // then display our pairing list
            _userPairListHandler.DrawPairs(_windowContentWidth);
            ImGui.Separator();
            // fetch the cursor position where the footer is
            pairlistEnd = ImGui.GetCursorPosY();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error drawing whitelist section");
        }

        // if we have configured to let the UI display a popup to set a nickname for the added UID upon adding them, then do so.
        if (_gagspeakConfig.Current.OpenPopupOnAdd && _pairManager.LastAddedUser != null)
        {
            // set the last added user to the last added user from the pair manager
            _lastAddedUser = _pairManager.LastAddedUser;

            // set the pair managers one to null, so this menu wont spam itself
            _pairManager.LastAddedUser = null;

            // prompt the user to set the nickname via the popup
            ImGui.OpenPopup("Set a Nickname for New User");

            // set if we should show the modal for added user to true,
            _showModalForUserAddition = true;

            // and clear the last added user comment 
            _lastAddedUserComment = string.Empty;
        }

        // the modal for setting a nickname for a newly added user, using the popup window flags in the shared service.
        if (ImGui.BeginPopupModal("Set a Nickname for New User", ref _showModalForUserAddition, UiSharedService.PopupWindowFlags))
        {
            // if the last added user is null, then we should not show the modal
            if (_lastAddedUser == null)
            {
                _showModalForUserAddition = false;
            }
            // but if they are still present, meaning we have not yet given them a nickname, then display the modal
            else
            {
                // inform the user the pair has been successfully added
                UiSharedService.TextWrapped($"You have successfully added {_lastAddedUser.UserData.AliasOrUID}. Set a local note for the user in the field below:");
                // display the input text field where they can input the nickname
                ImGui.InputTextWithHint("##nicknameforuser", $"Nickname for {_lastAddedUser.UserData.AliasOrUID}", ref _lastAddedUserComment, 100);
                if (_uiShared.IconTextButton(FontAwesomeIcon.Save, "Save Nickname"))
                {
                    // once we hit the save nickname button, we should update the nickname we have set for the UID
                    _serverConfigs.SetNicknameForUid(_lastAddedUser.UserData.UID, _lastAddedUserComment);
                    _lastAddedUser = null;
                    _lastAddedUserComment = string.Empty;
                    _showModalForUserAddition = false;
                }
            }
            UiSharedService.SetScaledWindowSize(275);
            ImGui.EndPopup();
        }

        // return a push to the footer to know where to draw our bottom tab bar
        return ImGui.GetCursorPosY() - pairlistEnd - ImGui.GetTextLineHeight();
    }

    /// <summary>
    /// Funky ass logic to determine if we should open sticky permission window for the selected pair. 
    /// Also determines if we show pairs permissions, or our permissions for the pair.
    /// </summary>
    /*private void UpdateShouldOpenStatus(Pair? specificPair = null, StickyWindowType type = StickyWindowType.None)
    {
        // Default assumption.
        var indexToKeep = -1;
        if (specificPair == null)
        {
            Logger.LogTrace("No specific Pair provided, finding first Pair with ShouldOpen true");
            indexToKeep = _userPairListHandler.AllPairDrawsDistinct.FindIndex(pair => pair.Pair.ShouldOpenPermWindow);
        }
        else
        {
            Logger.LogTrace("Specific Pair provided: {0}", specificPair.UserData.AliasOrUID);
            indexToKeep = _userPairListHandler.AllPairDrawsDistinct.FindIndex(pair => pair.Pair == specificPair);
        }
        // Toggle the ShouldOpen status if the Pair is found
        if (indexToKeep != -1)
        {
            Logger.LogDebug("Found specific Pair, Checking current window type.");
            var currentStatus = _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow;
            // If we're turning it off, reset indexToKeep to handle deactivation correctly
            if (currentStatus && type == _userPairPermsSticky.DrawType)
            {
                Logger.LogTrace("Requested Window is same type as current, toggling off");
                _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow = !currentStatus;
                indexToKeep = -1;
            }
            else if (!currentStatus && type != StickyWindowType.None && _userPairPermsSticky.DrawType == StickyWindowType.None)
            {
                Logger.LogTrace("Requesting to open window from currently closed state");
                _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow = true;
            }
            else if (currentStatus && (type == StickyWindowType.PairPerms || type == StickyWindowType.ClientPermsForPair) && _userPairPermsSticky.DrawType != type)
            {
                Logger.LogTrace("Requesting to change window type from client to pair, or vise versa");
            }
            else if (!currentStatus && (type == StickyWindowType.PairPerms || type == StickyWindowType.ClientPermsForPair))
            {
                Logger.LogTrace("Opening the same window but for another user, switching pair and changing index");
                _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow = !currentStatus;
            }
            else
            {
                Logger.LogTrace("Don't know exactly how you got here");
            }
        }

        Logger.LogDebug("Index to keep: {0} || setting all others to false", indexToKeep);
        // Set ShouldOpen to false for all other DrawUserPairs
        for (var i = 0; i < _userPairListHandler.AllPairDrawsDistinct.Count; i++)
        {
            if (i != indexToKeep)
            {
                _userPairListHandler.AllPairDrawsDistinct[i].Pair.ShouldOpenPermWindow = false;
            }
        }

        // Update _PairToDrawPermissionsFor based on the current status
        if (indexToKeep != -1)
        {
            Logger.LogTrace("Setting _PairToDrawPermissionsFor to {0}", _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.UserData.AliasOrUID);
            _PairToDrawPermissionsFor = _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair;
            if (type != StickyWindowType.None)
            {
                _userPairPermsSticky.DrawType = type;
            }
        }
        else
        {
            Logger.LogTrace("Setting _PairToDrawPermissionsFor to null && Setting DrawType to none");
            _PairToDrawPermissionsFor = null;
            _userPairPermsSticky.DrawType = StickyWindowType.None;
        }
    }*/
}
