using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.Services.ServerConfiguration;
using FFStreamViewer.WebAPI.SignalR.Utils;
using FFStreamViewer.WebAPI.UI.Components;
using FFStreamViewer.WebAPI.UI.Handlers;
using ImGuiNET;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Reflection;

namespace FFStreamViewer.WebAPI.UI;

public class CompactUi : WindowMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly GagspeakConfigService _configService;
    private readonly DrawEntityFactory _drawEntityFactory;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverManager;
    private readonly TopTabMenu _tabMenu;
    private readonly TagHandler _tagHandler;
    private readonly UiSharedService _uiSharedService;
    private readonly UserPairPermsSticky _UserPairPermissionsSticky;

    private List<IDrawFolder> _drawFolders;
    private List<DrawUserPair> _allUserPairDrawsDistinct;
    private Pair? _lastAddedUser;
    private string _lastAddedUserComment = string.Empty;
    private Vector2 _lastPosition = Vector2.One;
    private Vector2 _lastSize = Vector2.One;
    private int _secretKeyIdx = -1;
    private bool _showModalForUserAddition;
    private float _transferPartHeight;
    private bool _wasOpen;
    private float _windowContentWidth;

    // sticky window management
    private bool RootWindowFocused = false;
    private bool ChildWindowFocused = false;
    private bool ChildWindowFocusFixed = false;

    // variables for determining how the permissions window is drawn
    private bool _shouldDrawStickyPerms = false;
    private Pair? _PairToDrawPermissionsFor => _UserPairPermissionsSticky.UserPair;

    public CompactUi(ILogger<CompactUi> logger, UiSharedService uiShared, GagspeakConfigService configService,
        ApiController apiController, PairManager pairManager, ServerConfigurationManager serverManager,
        GagspeakMediator mediator, TagHandler tagHandler, DrawEntityFactory drawEntityFactory,
        UserPairPermsSticky userpermssticky) : base(logger, mediator, "###GagSpeakMainUI")
    {
        _uiSharedService = uiShared;
        _configService = configService;
        _apiController = apiController;
        _pairManager = pairManager;
        _serverManager = serverManager;
        _tagHandler = tagHandler;
        _drawEntityFactory = drawEntityFactory;
        _UserPairPermissionsSticky = userpermssticky;

        _tabMenu = new TopTabMenu(Mediator, _apiController, _pairManager, _uiSharedService);

        AllowPinning = false;
        AllowClickthrough = false;
        TitleBarButtons = new()
        {
            new TitleBarButton()
            {
                Icon = FontAwesomeIcon.Cog,
                Click = (msg) =>
                {
                    Mediator.Publish(new UiToggleMessage(typeof(SettingsUi)));
                },
                IconOffset = new(2,1),
                ShowTooltip = () =>
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Open Gagspeak Settings");
                    ImGui.EndTooltip();
                }
            },
            new TitleBarButton()
            {
                Icon = FontAwesomeIcon.Book,
                Click = (msg) =>
                {
                    Mediator.Publish(new UiToggleMessage(typeof(EventViewerUI)));
                },
                IconOffset = new(2,1),
                ShowTooltip = () =>
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Open Gagspeak Event Viewer");
                    ImGui.EndTooltip();
                }
            }
        };

        // updates the draw folders by recollecting them, and updates the drawPair list of distinct draw pairs
        UpdateDrawFoldersAndUserPairDraws();

        // display info about the folders
        string dev = "Dev Build";
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        WindowName = $"GagSpeak {dev} ({ver.Major}.{ver.Minor}.{ver.Build})###GagSpeakMainUI";

        Toggle();

        Mediator.Subscribe<SwitchToMainUiMessage>(this, (_) => IsOpen = true);
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = false);
        // Mediator.Subscribe<CutsceneEndMessage>(this, (_) => UiSharedService_GposeEnd());
        Mediator.Subscribe<RefreshUiMessage>(this, (msg) =>
        {
            // update drawfolders
            UpdateDrawFoldersAndUserPairDraws();
            // update the cog statuses
            UpdateShouldOpenStatus();
        });

        Mediator.Subscribe<OpenUserPairPermissions>(this, (msg) =>
        {
            logger.LogInformation("OpenUserPairPermission called for {0}", msg.Pair.UserData.AliasOrUID);

            // locate the DrawUserPair in the list where the pair matches the pair in it, and set that bool to true;
            UpdateShouldOpenStatus(msg.Pair);

        });


        Flags |= ImGuiWindowFlags.NoDocking;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(375, 400),
            MaximumSize = new Vector2(375, 2000),
        };
    }

    /// <summary> Updates our draw folders and user pair draws </summary>
    private void UpdateDrawFoldersAndUserPairDraws()
    {
        _drawFolders = GetDrawFolders().ToList();
        _allUserPairDrawsDistinct = _drawFolders
            .SelectMany(folder => folder.DrawPairs) // throughout all the folders
            .DistinctBy(pair => pair.Pair)          // without duplicates
            .ToList();
    }

    /// <summary>
    /// Updates if the permissions window should be opened, optionally based on a specific Pair.
    /// </summary>
    /// <param name="specificPair">The specific Pair to update, or null to use the first Pair with ShouldOpen true.</param>
    private void UpdateShouldOpenStatus(Pair? specificPair = null)
    {
        int indexToKeep = -1;

        // If a specific Pair is provided, find its index. Otherwise, find the first Pair with ShouldOpen set to true.
        if (specificPair != null)
        {
            _logger.LogInformation("Specific Pair provided: {0}", specificPair.UserData.AliasOrUID);
            indexToKeep = _allUserPairDrawsDistinct.FindIndex(pair => pair.Pair == specificPair);
            // Toggle the ShouldOpen status if the Pair is found
            if (indexToKeep != -1)
            {
                _logger.LogInformation("Found specific Pair, toggling ShouldOpen status to {0}", !_allUserPairDrawsDistinct[indexToKeep].ShouldOpen);
                bool currentStatus = _allUserPairDrawsDistinct[indexToKeep].ShouldOpen;
                _allUserPairDrawsDistinct[indexToKeep].ShouldOpen = !currentStatus;
                // If we're turning it off, reset indexToKeep to handle deactivation correctly
                if (currentStatus) indexToKeep = -1;
            }
        }
        else
        {
            _logger.LogInformation("No specific Pair provided, finding first Pair with ShouldOpen true");
            indexToKeep = _allUserPairDrawsDistinct.FindIndex(pair => pair.ShouldOpen);
        }

        _logger.LogDebug("Index to keep: {0} || setting all others to false", indexToKeep);
        // Set ShouldOpen to false for all other DrawUserPairs
        for (int i = 0; i < _allUserPairDrawsDistinct.Count; i++)
        {
            if (i != indexToKeep)
            {
                _allUserPairDrawsDistinct[i].ShouldOpen = false;
            }
        }

        // Update _PairToDrawPermissionsFor based on the current status
        if (indexToKeep != -1)
        {
            _logger.LogDebug("Setting _PairToDrawPermissionsFor to {0}", _allUserPairDrawsDistinct[indexToKeep].Pair.UserData.AliasOrUID);
            _PairToDrawPermissionsFor = _allUserPairDrawsDistinct[indexToKeep].Pair;
        }
        else
        {
            _logger.LogDebug("Setting _PairToDrawPermissionsFor to null");
            _PairToDrawPermissionsFor = null;
        }
    }

    protected override void DrawInternal()
    {
        // get the width of the window content region we set earlier
        _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();

        // if we are not on the current version, display it
        if (!_apiController.IsCurrentVersion)
        {
            var ver = _apiController.CurrentClientVersion;
            var unsupported = "UNSUPPORTED VERSION";
            // push the notice that we are unsupported
            using (_uiSharedService.UidFont.Push())
            {
                var uidTextSize = ImGui.CalcTextSize(unsupported);
                ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X + ImGui.GetWindowContentRegionMin().X) / 2 - uidTextSize.X / 2);
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(ImGuiColors.DalamudRed, unsupported);
            }
            // the wrapped text explanation 
            UiSharedService.ColorTextWrapped($"Your GagSpeak installation is out of date, the current version is {ver.Major}.{ver.Minor}.{ver.Build}. " +
                $"It is highly recommended to keep GagSpeak up to date. Open /xlplugins and update the plugin.", ImGuiColors.DalamudRed);
        }

        // otherwise, if we are on the current version, begin by pushing the main header, containing our UID
        using (ImRaii.PushId("header")) DrawUIDHeader();
        // draw a separation boundry
        ImGui.Separator();
        // then draw the server status, displaying the total number of people online.
        using (ImRaii.PushId("serverstatus")) DrawServerStatus();
        // separate our UI once more.
        ImGui.Separator();

        // if we are connected to the server
        if (_apiController.ServerState is ServerState.Connected)
        {
            // tdisplay the topmenu with our tab selections
            using (ImRaii.PushId("global-topmenu")) _tabMenu.Draw();
            // grab the yposition
            float topMenuEnd = ImGui.GetCursorPosY();
            // then display our pairing list
            using (ImRaii.PushId("pairlist")) DrawPairs();
            // after that, another seperator to create the footer of the window
            ImGui.Separator();
            // fetch the cursor position where the footer is
            float pairlistEnd = ImGui.GetCursorPosY();
            // push a footer here maybe.
            _transferPartHeight = ImGui.GetCursorPosY() - pairlistEnd - ImGui.GetTextLineHeight();

            // anyways, if we should be drawing the sticky permissions window beside it, then do so.
            if (_PairToDrawPermissionsFor != null)
            {
                ChildWindowFocused = _UserPairPermissionsSticky.DrawSticky(_PairToDrawPermissionsFor, topMenuEnd);
            }
            // check if we currently have a permission window open and if so to refocus it when the main window is focused
            FocusChildWhenMainRefocused(ChildWindowFocused);
        }

        // if we have configured to let the UI display a popup to set a nickname for the added UID upon adding them, then do so.
        if (_configService.Current.OpenPopupOnAdd && _pairManager.LastAddedUser != null)
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
                if (_uiSharedService.IconTextButton(FontAwesomeIcon.Save, "Save Nickname"))
                {
                    // once we hit the save nickname button, we should update the nickname we have set for the UID
                    _serverManager.SetNicknameForUid(_lastAddedUser.UserData.UID, _lastAddedUserComment);
                    _lastAddedUser = null;
                    _lastAddedUserComment = string.Empty;
                    _showModalForUserAddition = false;
                }
            }
            UiSharedService.SetScaledWindowSize(275);
            ImGui.EndPopup();
        }

        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        if (_lastSize != size || _lastPosition != pos)
        {
            _lastSize = size;
            _lastPosition = pos;
            Mediator.Publish(new CompactUiChange(_lastSize, _lastPosition));
        }
    }

    private void FocusChildWhenMainRefocused(bool isChildFocused)
    {
        if (_PairToDrawPermissionsFor == null) return;

        // DEFINE CHILD WINDOW NAME
        var windowName = "###PairPermissionStickyUI" + (_PairToDrawPermissionsFor.UserPair.User.AliasOrUID);
        
        // DETERMINE IF WE ARE CURRENTLY FOCUSING THE MAIN WINDOW
        RootWindowFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootWindow);

        // IF MAIN WINDOW IS FOCUSED
        if(RootWindowFocused)
        {
            // AND THE CHILD WINDOW IS NOT FOCUSED AND NOT YET FIXED
            if (!isChildFocused && !ChildWindowFocusFixed)
            {
                // FIX IT
                ImGui.SetWindowFocus(windowName);
                ChildWindowFocusFixed = true;
            }
        }

        // IF BOTH WINDOWS ARE NO LONGER FOCUSED, RESET FOCUS VARIABLES
        if(!isChildFocused && !RootWindowFocused)
        {
            RootWindowFocused = false;
            ChildWindowFocusFixed = false;
        }
    }


    /// <summary>
    /// Not really sure how or when this is ever fired, but we will see in due time i suppose.
    /// </summary>
    private void DrawAddCharacter()
    {
        ImGuiHelpers.ScaledDummy(10f);
        var keys = _serverManager.CurrentServer!.SecretKeys;
        if (keys.Any())
        {
            if (_secretKeyIdx == -1) _secretKeyIdx = keys.First().Key;
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.Plus, "Add current character with secret key"))
            {
                _serverManager.CurrentServer!.Authentications.Add(new GagspeakConfiguration.Models.Authentication()
                {
                    CharacterName = _uiSharedService.PlayerName,
                    WorldId = _uiSharedService.WorldId,
                    SecretKeyIdx = _secretKeyIdx
                });

                _serverManager.Save();

                _ = _apiController.CreateConnections();
            }

            _uiSharedService.DrawCombo("Secret Key##addCharacterSecretKey", keys, (f) => f.Value.FriendlyName, (f) => _secretKeyIdx = f.Key);
        }
        else
        {
            UiSharedService.ColorTextWrapped("No secret keys are configured for the current server.", ImGuiColors.DalamudYellow);
        }
    }

    /// <summary>
    /// Draws the list of pairs belonging to the client user.
    /// </summary>
    private void DrawPairs()
    {
        // span the height of the pair list to be the height of the window minus the transfer section, which we are removing later anyways.
        var ySize = _transferPartHeight == 0
            ? 1
            : (ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y
                + ImGui.GetTextLineHeight() - ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().WindowBorderSize) - _transferPartHeight - ImGui.GetCursorPosY();

        // begin the list child, with no border and of the height calculated above
        ImGui.BeginChild("list", new Vector2(_windowContentWidth, ySize), border: false);

        // for each item in the draw folders,
            // _logger.LogTrace("Drawing {count} folders", _drawFolders.Count);
        foreach (var item in _drawFolders)
        {
            // draw the content
            item.Draw();
        }

        // then end the list child
        ImGui.EndChild();
    }

    /// <summary>
    /// Helper function for drawing the current status of the server, including the number of people online.
    /// </summary>
    private void DrawServerStatus()
    {
        var buttonSize = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Link);
        var userCount = _apiController.OnlineUsers.ToString(CultureInfo.InvariantCulture);
        var userSize = ImGui.CalcTextSize(userCount);
        var textSize = ImGui.CalcTextSize("Users Online");

        string shardConnection = $"Main GagSpeak Server";

        var shardTextSize = ImGui.CalcTextSize(shardConnection);
        var printShard = shardConnection != string.Empty;

        // if the server is connected, then we should display the server info
        if (_apiController.ServerState is ServerState.Connected)
        {
            // fancy math shit for clean display, adjust when moving things around
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - (userSize.X + textSize.X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.ParsedGreen, userCount);
            ImGui.SameLine();
            ImGui.TextUnformatted("Users Online");
        }
        // otherwise, if we are not connected, display that we aren't connected.
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(ImGuiColors.DalamudRed, "Not connected to any server");
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - shardTextSize.X / 2);
        ImGui.TextUnformatted(shardConnection);
        ImGui.SameLine();

        // now we need to display the connection link button beside it.
        var color = UiSharedService.GetBoolColor(!_serverManager.CurrentServer!.FullPause);
        var connectedIcon = !_serverManager.CurrentServer.FullPause ? FontAwesomeIcon.Link : FontAwesomeIcon.Unlink;

        ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - buttonSize.X);
        if (printShard)
        {
            // unsure what this is doing but we can find out lol
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((userSize.Y + textSize.Y) / 2 + shardTextSize.Y) / 2 - ImGui.GetStyle().ItemSpacing.Y + buttonSize.Y / 2);
        }

        // if the server is reconnecting or disconnecting
        if (_apiController.ServerState is not (ServerState.Reconnecting or ServerState.Disconnecting))
        {
            // we need to turn the button from the connected link to the disconnected link.
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                // then display it
                if (_uiSharedService.IconButton(connectedIcon))
                {
                    // and toggle the fullpause for the current server, save the config, and recreate the connections,
                    // placing it into a disconnected state due to the full pause being active. (maybe change this later)
                    _serverManager.CurrentServer.FullPause = !_serverManager.CurrentServer.FullPause;
                    _serverManager.Save();
                    _ = _apiController.CreateConnections();
                }
            }
            // attach the tooltip for the connection / disconnection button)
            UiSharedService.AttachToolTip(!_serverManager.CurrentServer.FullPause ? "Disconnect from " + _serverManager.CurrentServer.ServerName
                                                                                  : "Connect to " + _serverManager.CurrentServer.ServerName);
        }
    }

    /// <summary>
    /// Draws the UID header for the currently connected client (you)
    /// </summary>
    private void DrawUIDHeader()
    {
        // fetch the Uid Text of yourself
        var uidText = GetUidText();

        // push the big boi font for the UID
        using (_uiSharedService.UidFont.Push())
        {
            var uidTextSize = ImGui.CalcTextSize(uidText);
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - (uidTextSize.X / 2));
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(GetUidColor(), uidText);
        }

        // if we are connected
        if (_apiController.ServerState is ServerState.Connected)
        {
            UiSharedService.CopyableDisplayText(_apiController.DisplayName);

            // if the UID does not equal the display name
            if (!string.Equals(_apiController.DisplayName, _apiController.UID, StringComparison.Ordinal))
            {
                // grab the original text size for the UID in the api controller
                var origTextSize = ImGui.CalcTextSize(_apiController.UID);
                // adjust the cursor and redraw the UID (really not sure why this is here but we can trial and error later.
                ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - (origTextSize.X / 2));
                ImGui.TextColored(GetUidColor(), _apiController.UID);
                // give it the same functionality.
                UiSharedService.CopyableDisplayText(_apiController.UID);
            }
        }
        // otherwise, if we are not connected
        else
        {
            // we should display in the color wrapped text the server error.
            UiSharedService.ColorTextWrapped(GetServerError(), GetUidColor());
            if (_apiController.ServerState is ServerState.NoSecretKey)
            {
                // if the connected state is due to not having a secret key,
                // we should ask it to add our character
                DrawAddCharacter();
            }
        }
    }

    private IEnumerable<IDrawFolder> GetDrawFolders()
    {
        List<IDrawFolder> drawFolders = [];

        // the list of all direct pairs.
        var allPairs = _pairManager.DirectPairs;

        // the filters list of pairs will be the pairs that match the filter.
        var filteredPairs = allPairs
            .Where(p =>
            {
                if (_tabMenu.Filter.IsNullOrEmpty()) return true;
                // return a user if the filter matches their alias or ID, playerChara name, or the nickname we set.
                return p.UserData.AliasOrUID.Contains(_tabMenu.Filter, StringComparison.OrdinalIgnoreCase) ||
                       (p.GetNickname()?.Contains(_tabMenu.Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (p.PlayerName?.Contains(_tabMenu.Filter, StringComparison.OrdinalIgnoreCase) ?? false);
            });

        // the alphabetical sort function of the pairs.
        string? AlphabeticalSort(Pair u)
            => (!string.IsNullOrEmpty(u.PlayerName)
                    ? (_configService.Current.PreferNicknamesOverNamesForVisible ? u.GetNickname() : u.PlayerName)
                    : (u.GetNickname() ?? u.UserData.AliasOrUID));

        // filter based on who is online (or paused but that shouldnt exist yet unless i decide to add it later here)
        bool FilterOnlineOrPausedSelf(Pair u)
            => (u.IsOnline || (!u.IsOnline && !_configService.Current.ShowOfflineUsersSeparately));

        // collect the sorted list
        List<Pair> BasicSortedList(IEnumerable<Pair> u)
            => u.OrderByDescending(u => u.IsVisible)
                .ThenByDescending(u => u.IsOnline)
                .ThenBy(AlphabeticalSort, StringComparer.OrdinalIgnoreCase)
                .ToList();

        ImmutableList<Pair> ImmutablePairList(IEnumerable<Pair> u) => u.ToImmutableList();

        // if we should filter visible users
        bool FilterVisibleUsers(Pair u) => u.IsVisible && u.IsDirectlyPaired;

        bool FilterTagusers(Pair u, string tag) => u.IsDirectlyPaired && !u.IsOneSidedPair && _tagHandler.HasTag(u.UserData.UID, tag);

        bool FilterNotTaggedUsers(Pair u) => u.IsDirectlyPaired && !u.IsOneSidedPair && !_tagHandler.HasAnyTag(u.UserData.UID);

        bool FilterOfflineUsers(Pair u) => u.IsDirectlyPaired && (!u.IsOneSidedPair) && !u.IsOnline;


        // if we wish to display our visible users separately, then do so.
        if (_configService.Current.ShowVisibleUsersSeparately)
        {
            // display all visible pairs, without filter
            var allVisiblePairs = ImmutablePairList(allPairs
                .Where(FilterVisibleUsers));
            // display the filtered visible pairs based on the filter we applied
            var filteredVisiblePairs = BasicSortedList(filteredPairs
                .Where(FilterVisibleUsers));

            // add the draw folders based on the 
            drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(TagHandler.CustomVisibleTag, filteredVisiblePairs, allVisiblePairs));
        }

        // grab the tags stored in the tag handler
        var tags = _tagHandler.GetAllTagsSorted();
        // for each tag
        foreach (var tag in tags)
        {
            _logger.LogDebug("Adding Pair Section List Tag: {tag}", tag);
            // display the pairs that have the tag, and are not one sided pairs, and are online or paused
            var allTagPairs = ImmutablePairList(allPairs
                .Where(u => FilterTagusers(u, tag)));
            var filteredTagPairs = BasicSortedList(filteredPairs
                .Where(u => FilterTagusers(u, tag) && FilterOnlineOrPausedSelf(u)));

            // append the draw folders for the tag
            drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(tag, filteredTagPairs, allTagPairs));
        }

        // store the pair list of all online untagged pairs
        var allOnlineNotTaggedPairs = ImmutablePairList(allPairs
            .Where(FilterNotTaggedUsers));
        // store the online untagged pairs
        var onlineNotTaggedPairs = BasicSortedList(filteredPairs
            .Where(u => FilterNotTaggedUsers(u) && FilterOnlineOrPausedSelf(u)));

        // create the draw folders for the online untagged pairs
        _logger.LogDebug("Adding Pair Section List Tag: {tag}", TagHandler.CustomAllTag);
        drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder((
            _configService.Current.ShowOfflineUsersSeparately ? TagHandler.CustomOnlineTag : TagHandler.CustomAllTag),
            onlineNotTaggedPairs, allOnlineNotTaggedPairs));

        // if we want to show offline users seperately,
        if (_configService.Current.ShowOfflineUsersSeparately)
        {
            // then do so.
            var allOfflinePairs = ImmutablePairList(allPairs
                .Where(FilterOfflineUsers));
            var filteredOfflinePairs = BasicSortedList(filteredPairs
                .Where(FilterOfflineUsers));

            // add the folder.
            _logger.LogDebug("Adding Pair Section List Tag: {tag}", TagHandler.CustomOfflineTag);
            drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(TagHandler.CustomOfflineTag, filteredOfflinePairs, 
                allOfflinePairs));

        }

        // finally, add the unpaired users to the list.
        _logger.LogDebug("Adding Unpaired Pairs Section List Tag: {tag}", TagHandler.CustomUnpairedTag);
        drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(TagHandler.CustomUnpairedTag,
            BasicSortedList(filteredPairs.Where(u => u.IsOneSidedPair)),
            ImmutablePairList(allPairs.Where(u => u.IsOneSidedPair))));

        return drawFolders;
    }

    /// <summary> Retrieves the various server error messages based on the current server state.</summary>
    /// <returns> The error message of the server.</returns>
    private string GetServerError()
    {
        return _apiController.ServerState switch
        {
            ServerState.Connecting => "Attempting to connect to the server.",
            ServerState.Reconnecting => "Connection to server interrupted, attempting to reconnect to the server.",
            ServerState.Disconnected => "Currently disconnected from the GagSpeak server.",
            ServerState.Disconnecting => "Disconnecting from server",
            ServerState.Unauthorized => "Server Response: " + _apiController.AuthFailureMessage,
            ServerState.Offline => "The GagSpeak server is currently offline.",
            ServerState.VersionMisMatch => "Your plugin is out of date. Please update your plugin to fix.",
            ServerState.Connected => string.Empty,
            ServerState.NoSecretKey => "No secret key is set for this current character. To create UID's for your alt characters, be sure to claim your account in the CK discord.",
            _ => string.Empty
        };
    }

    /// <summary> Retrieves the various UID text color based on the current server state.</summary>
    /// <returns> The color of the UID text in Vector4 format .</returns>
    private Vector4 GetUidColor()
    {
        return _apiController.ServerState switch
        {
            ServerState.Connecting => ImGuiColors.DalamudYellow,
            ServerState.Reconnecting => ImGuiColors.DalamudRed,
            ServerState.Connected => ImGuiColors.ParsedGreen,
            ServerState.Disconnected => ImGuiColors.DalamudYellow,
            ServerState.Disconnecting => ImGuiColors.DalamudYellow,
            ServerState.Unauthorized => ImGuiColors.DalamudRed,
            ServerState.VersionMisMatch => ImGuiColors.DalamudRed,
            ServerState.Offline => ImGuiColors.DalamudRed,
            ServerState.NoSecretKey => ImGuiColors.DalamudYellow,
            _ => ImGuiColors.DalamudRed
        };
    }

    /// <summary> Retrieves the various UID text based on the current server state.</summary>
    /// <returns> The text of the UID.</returns>
    private string GetUidText()
    {
        return _apiController.ServerState switch
        {
            ServerState.Reconnecting => "Reconnecting",
            ServerState.Connecting => "Connecting",
            ServerState.Disconnected => "Disconnected",
            ServerState.Disconnecting => "Disconnecting",
            ServerState.Unauthorized => "Unauthorized",
            ServerState.VersionMisMatch => "Version mismatch",
            ServerState.Offline => "Unavailable",
            ServerState.NoSecretKey => "No Secret Key",
            ServerState.Connected => _apiController.DisplayName, // displays when connected, your UID
            _ => string.Empty
        };
    }
}
