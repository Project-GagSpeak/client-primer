using Dalamud.Interface;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// </summary>
public partial class UserPairPermsSticky
{
    public void DrawPairActionFunctions()
    {
        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Common Pair Functions");

        // draw the common client functions
        DrawCommonClientMenu();

        // Online Pair Actions
        ImGui.TextUnformatted("Gag Actions");
        DrawGagActions();

        ImGui.TextUnformatted("Wardrobe Actions");
        DrawWardrobeActions();

        ImGui.TextUnformatted("Puppeteer Actions");
        DrawPuppeteerActions();

        ImGui.TextUnformatted("Moodles Actions");
        DrawMoodlesActions();

        ImGui.TextUnformatted("Toybox Actions");
        DrawToyboxActions();

        // individual Menu
        ImGui.TextUnformatted("Individual Pair Functions");
        DrawIndividualMenu();
    }

    private void DrawCommonClientMenu()
    {
        if (!UserPairForPerms.IsPaused)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.User, "Open Profile", WindowMenuWidth, true))
            {
                _displayHandler.OpenProfile(UserPairForPerms);
                ImGui.CloseCurrentPopup();
            }
            UiSharedService.AttachToolTip("Opens the profile for this user in a new window");
        }
        if (UserPairForPerms.IsPaired)
        {
            var pauseIcon = UserPairForPerms.UserPair!.OwnPairPerms.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
            var pauseIconSize = _uiShared.GetIconButtonSize(pauseIcon);
            var pauseText = UserPairForPerms.UserPair!.OwnPairPerms.IsPaused ? $"Unpause {UserPairForPerms.UserData.AliasOrUID}" : $"Pause {UserPairForPerms.UserData.AliasOrUID}";
            if (_uiShared.IconTextButton(pauseIcon, pauseText, WindowMenuWidth, true))
            {
                var perm = UserPairForPerms.UserPair!.OwnPairPerms;
                _ = _apiController.UserUpdateOwnPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                    new KeyValuePair<string, object>("IsPaused", !perm.IsPaused)));
            }
            UiSharedService.AttachToolTip(!UserPairForPerms.UserPair!.OwnPairPerms.IsPaused
            ? "Pause pairing with " + UserPairForPerms.UserData.AliasOrUID
                : "Resume pairing with " + UserPairForPerms.UserData.AliasOrUID);
        }
        if (UserPairForPerms.IsVisible)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Reload last data", WindowMenuWidth, true))
            {
                UserPairForPerms.ApplyLastReceivedIpcData(forced: true);
                ImGui.CloseCurrentPopup();
            }
            UiSharedService.AttachToolTip("This reapplies the last received character data to this character");
        }

        ImGui.Separator();
    }

    private void DrawGagActions()
    {
        // button for applying a gag (will display the dropdown of the gag list to apply when pressed.

        // button to lock the current layers gag. (references the gag applied, only interactable when layer is gagged.)

        // button to unlock the current layers gag. (references appearance data of pair. only visible while locked.)

        // button to remove the current layers gag. (references appearance data of pair. only visible while unlocked & gagged).
        ImGui.Separator();
    }

    private bool ShowSetList = false;
    private int RestraintSetSelction = 0;
    private void DrawWardrobeActions()
    {
        // draw the apply-restraint-set button.
        if (UserPairForPerms.IsOnline && UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets)
        {
            // do not display if the restraint set count is empty. (temporarily removed for visibility purposes.
            if (_uiShared.IconTextButton(FontAwesomeIcon.PersonCircleCheck, "Apply Restraint Set", WindowMenuWidth,
                true))//, UserPairForPerms.UserPairWardrobeData.OutfitNames.Count == 0))
            {
                ShowSetList = !ShowSetList;
            }
            UiSharedService.AttachToolTip("Applies a Restraint Set to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");

            // show the restraint set list if the button is clicked.
            if (ShowSetList)
            {
                if (ImGui.ListBox($"##RestraintSelection{UserPairForPerms.UserData.AliasOrUID}", ref RestraintSetSelction,
                    UserPairForPerms.UserPairWardrobeData.OutfitNames.ToArray(), UserPairForPerms.UserPairWardrobeData.OutfitNames.Count))
                {
                    // apply the selected restraint set.
                    _logger.LogInformation("Applying restraint set {0} to {1}", UserPairForPerms.UserPairWardrobeData.OutfitNames[RestraintSetSelction], UserPairForPerms.UserData.AliasOrUID);
                }
            }
        }

        // draw the lock restraint set button.

        // draw the unlock restraint set button.

        // draw the remove restraint set button.

        ImGui.Separator();
    }

    private void DrawPuppeteerActions()
    {
        // draw the Alias List popout ref button. (opens a popout window 


        ImGui.Separator();
    }

    // All of these actions will only display relative to the various filters that the Moodle has applied.
    // initially test these buttons on self.
    private void DrawMoodlesActions()
    {
        // button for adding a Moodle by GUID from client's Moodle list to paired user. (Requires PairCanApplyOwnMoodlesToYou)

        // button for adding a Moodle by GUID from the paired user's Moodle list to the paired user. (Requires PairCanApplyYourMoodlesToYou)

        // button for Applying a preset by GUID from the client's preset list to the paired user. (Requires PairCanApplyOwnMoodlesToYou)

        // button for Applying a preset by GUID from the paired user's preset list to the paired user. (Requires PairCanApplyYourMoodlesToYou)

        // button for removing a Moodle by GUID from client's Moodle list from the paired user. (Requires PairCanApplyOwnMoodlesToYou)

        // button for removing a Moodle by GUID from the paired user's Moodle list from the paired user. (Requires PairCanApplyYourMoodlesToYou)

        // button for removing a preset by GUID from the client's preset list from the paired user. (Requires PairCanApplyOwnMoodlesToYou)

        // button for removing a preset by GUID from the paired user's preset list from the paired user. (Requires PairCanApplyYourMoodlesToYou)

        // button for clearing all moodles from the paired user.

        ImGui.Separator();
    }

    private void DrawToyboxActions()
    {
        // button for turning on or off the pairs connected toys. (Requires ChangeToyState)

        // button for viewing the pairs current alarm list. (Requires VibratorAlarms)

        // button for setting an alarm on the pairs connected toys. (Requires VibratorAlarms)

        // button for executing a pattern on a pairs connected toys. (Requires CanExecutePatterns)

        // button for enabling or disabling a pairs trigger. (Requires CanExecuteTriggers)

        // button for sending a trigger you have created to the pair. (Requires CanSendTriggers)


        ImGui.Separator();
    }

    private void DrawIndividualMenu()
    {
        var entryUID = UserPairForPerms.UserData.AliasOrUID;

        if (UserPairForPerms.IndividualPairStatus != GagspeakAPI.Data.Enum.IndividualPairStatus.None)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Unpair Permanently", WindowMenuWidth, true) && UiSharedService.CtrlPressed())
            {
                _ = _apiController.UserRemovePair(new(UserPairForPerms.UserData));
            }
            UiSharedService.AttachToolTip("Hold CTRL and click to unpair permanently from " + entryUID);
        }
        else
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Plus, "Pair individually", WindowMenuWidth, true))
            {
                _ = _apiController.UserAddPair(new(UserPairForPerms.UserData));
            }
            UiSharedService.AttachToolTip("Pair individually with " + entryUID);
        }
    }

}
