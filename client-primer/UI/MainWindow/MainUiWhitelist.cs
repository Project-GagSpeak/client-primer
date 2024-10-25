using Dalamud.Interface;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.Permissions;
using GagSpeak.WebAPI;
using GagspeakAPI.Enums;
using ImGuiNET;

namespace GagSpeak.UI.MainWindow;

/// <summary> 
/// Sub-class of the main UI window. Handles drawing the whitelist/contacts tab of the main UI.
/// </summary>
public class MainUiWhitelist : DisposableMediatorSubscriberBase
{
    private readonly MainHub _apiHubMain;
    private readonly UiFactory _uiFactory;
    private readonly UiSharedService _uiShared;
    private readonly UserPairListHandler _userPairListHandler;
    private readonly PairManager _pairManager;
    private readonly GagspeakConfigService _gagspeakConfig;
    private readonly ServerConfigurationManager _serverConfigs;

    public MainUiWhitelist(ILogger<MainUiWhitelist> logger,
        GagspeakMediator mediator, MainHub apiHubMain,
        UiFactory uiFactory, UiSharedService uiSharedService, 
        UserPairListHandler userPairListHandler, PairManager pairManager,
        GagspeakConfigService gagspeakConfig,
        ServerConfigurationManager serverConfigs) : base(logger, mediator)
    {
        _apiHubMain = apiHubMain;
        _uiFactory = uiFactory;
        _uiShared = uiSharedService;
        _userPairListHandler = userPairListHandler;
        _pairManager = pairManager;
        _gagspeakConfig = gagspeakConfig;
        _serverConfigs = serverConfigs;

        // updates the draw folders by recollecting them, and updates the drawPair list of distinct draw pairs
        _userPairListHandler.UpdateDrawFoldersAndUserPairDraws();

        Mediator.Subscribe<RefreshUiMessage>(this, (msg) => _userPairListHandler.UpdateDrawFoldersAndUserPairDraws());
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
    public void DrawWhitelistSection()
    {
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        var _spacingX = ImGui.GetStyle().ItemInnerSpacing.X;

        try
        {
            _userPairListHandler.DrawSearchFilter(_windowContentWidth, _spacingX);
            ImGui.Separator();
            _userPairListHandler.DrawPairs(_windowContentWidth);
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
                _showModalForUserAddition = false;
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
    }
}
