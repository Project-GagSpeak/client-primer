using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.WebAPI.Utils;
using GagSpeak.UI.Handlers;
using ImGuiNET;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using OtterGuiInternal.Structs;
using GagSpeak.UI.Permissions;
using GagspeakAPI.Data.Enum;
using GagSpeak.GagspeakConfiguration;
using GagSpeak;
using GagSpeak.UI;
using GagSpeak.WebAPI;
using static FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutManager;
using GagSpeak.UI.Components.UserPairList;

namespace GagSpeak.UI.MainWindow;

/// <summary> 
/// Partial class responsible for drawing the contacts list section of the MainWindowUI 
/// Could potentially abstract the userDrawList into its own class so it can be reused in other places.
/// </summary>
public partial class MainWindowUI
{
    // Attributes related to the drawing of the whitelist / contacts / pair list
    private Pair? _lastAddedUser;
    private string _lastAddedUserComment = string.Empty;
    // If we should draw sticky perms for the currently selected user pair.
    private bool _shouldDrawStickyPerms = false;
    private bool _showModalForUserAddition;
    private Pair? _PairToDrawPermissionsFor
    {
        get => _userPairPermissionsSticky.UserPairForPerms;
        set => _userPairPermissionsSticky.UserPairForPerms = value;
    }

    /// <summary>
    /// Main Draw function for the Whitelist/Contacts tab of the main UI
    /// </summary>
    private float DrawWhitelistSection(ref float lowerPartHeight)
    {
        // get the width of the window content region we set earlier
        _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        float pairlistEnd = 0;

        // if we are connected to the server
        if (_apiController.ServerState is ServerState.Connected)
        {
            // draw the heading label with its left and right buttons.
            ImGui.Text("Header Placeholder");
            // show the search filter just above the contacts list to form a nice separation.
            using (ImRaii.PushId("search-filter")) _userPairListHandler.DrawSearchFilter(_windowContentWidth, ImGui.GetStyle().ItemSpacing.X);
            // then display our pairing list
            using (ImRaii.PushId("pair-list")) _userPairListHandler.DrawPairs(ref lowerPartHeight, _windowContentWidth);
            // fetch the cursor position where the footer is
            pairlistEnd = ImGui.GetCursorPosY();

            // Draw sticky permissions window the client pair if we should.
            if (_PairToDrawPermissionsFor != null)
            {
                _userPairPermissionsSticky.DrawSticky();
            }
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

        // return a push to the footer to know where to draw our bottom tab bar
        return ImGui.GetCursorPosY() - pairlistEnd - ImGui.GetTextLineHeight();
    }

    /// <summary>
    /// Draws the list of pairs belonging to the client user.
    /// </summary>
    private void DrawPairs()
    {
        // span the height of the pair list to be the height of the window minus the transfer section, which we are removing later anyways.
        var ySize = _transferPartHeight == 0
            ? 1
            : ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y
                + ImGui.GetTextLineHeight() - ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().WindowBorderSize - _transferPartHeight - ImGui.GetCursorPosY();

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
    /// Funky ass logic to determine if we should open sticky permission window for the selected pair. 
    /// Also determines if we show pairs permissions, or our permissions for the pair.
    /// </summary>
    private void UpdateShouldOpenStatus(Pair? specificPair = null, StickyWindowType type = StickyWindowType.None)
    {
        // Default assumption.
        var indexToKeep = -1;
        if (specificPair == null)
        {
            _logger.LogTrace("No specific Pair provided, finding first Pair with ShouldOpen true");
            indexToKeep = _userPairListHandler.AllPairDrawsDistinct.FindIndex(pair => pair.Pair.ShouldOpenPermWindow);
        }

        _logger.LogTrace("Specific Pair provided: {0}", specificPair.UserData.AliasOrUID);
        indexToKeep = _userPairListHandler.AllPairDrawsDistinct.FindIndex(pair => pair.Pair == specificPair);
        // Toggle the ShouldOpen status if the Pair is found
        if (indexToKeep != -1)
        {
            _logger.LogDebug("Found specific Pair, Checking current window type.");
            var currentStatus = _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow;
            // If we're turning it off, reset indexToKeep to handle deactivation correctly
            if (currentStatus && type == _userPairPermissionsSticky.DrawType)
            {
                _logger.LogTrace("Requested Window is same type as current, toggling off");
                _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow = !currentStatus;
                indexToKeep = -1;
            }
            else if (!currentStatus && type != StickyWindowType.None && _userPairPermissionsSticky.DrawType == StickyWindowType.None)
            {
                _logger.LogTrace("Requesting to open window from currently closed state");
                _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow = true;
            }
            else if (currentStatus && (type == StickyWindowType.PairPerms || type == StickyWindowType.ClientPermsForPair) && _userPairPermissionsSticky.DrawType != type)
            {
                _logger.LogTrace("Requesting to change window type from client to pair, or vise versa");
            }
            else if (!currentStatus && (type == StickyWindowType.PairPerms || type == StickyWindowType.ClientPermsForPair))
            {
                _logger.LogTrace("Opening the same window but for another user, switching pair and changing index");
                _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.ShouldOpenPermWindow = !currentStatus;
            }
            else
            {
                _logger.LogTrace("Don't know exactly how you got here");
            }
        }

        _logger.LogDebug("Index to keep: {0} || setting all others to false", indexToKeep);
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
            _logger.LogTrace("Setting _PairToDrawPermissionsFor to {0}", _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair.UserData.AliasOrUID);
            _PairToDrawPermissionsFor = _userPairListHandler.AllPairDrawsDistinct[indexToKeep].Pair;
            if (type != StickyWindowType.None)
            {
                _userPairPermissionsSticky.DrawType = type;
            }
        }
        else
        {
            _logger.LogTrace("Setting _PairToDrawPermissionsFor to null && Setting DrawType to none");
            _PairToDrawPermissionsFor = null;
            _userPairPermissionsSticky.DrawType = StickyWindowType.None;
        }
    }
}
