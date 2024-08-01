using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using OtterGui.Classes;
using OtterGui.Text;
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkCounterNode.Delegates;
using static GagspeakAPI.Data.Enum.GagList;
using static Lumina.Data.Parsing.Layer.LayerCommon;

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

    private bool ShowGagList = false;
    private string GagSearchString = string.Empty;
    public int SelectedLayer = 0;
    public GagList.GagType SelectedGag = GagType.None;
    private void DrawGagActions()
    {
        // button for applying a gag (will display the dropdown of the gag list to apply when pressed.
        if (_uiShared.IconTextButton(FontAwesomeIcon.CommentDots, ("Apply a Gag to " + PairAliasOrUID), 
            WindowMenuWidth, true))
        {
            ShowGagList = !ShowGagList;
        }
        // if we should show the gag list, 
        if(ShowGagList)
        {
            using (var framedGagApplyChild = ImRaii.Child("GagApplyChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()*2 + ImGui.GetStyle().ItemSpacing.Y), false))
            {
                if (!framedGagApplyChild) return;

                float comboWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply Gag").X - ImGui.GetStyle().ItemSpacing.X * 2;

                using (var gagApplyGroup = ImRaii.Group())
                {
                    // first display dropdown for layer selection
                    ImGui.SetNextItemWidth(comboWidth);
                    ImGui.Combo("##GagApplyLayer", ref SelectedLayer, new string[] { "Layer 1", "Layer 2", "Layer 3" }, 3);
                    UiSharedService.AttachToolTip("Select the layer to apply the gag to.");
                    // now display the dropdown for the gag selection
                    _uiShared.DrawComboSearchable($"Gag Type for Pair", comboWidth, ref GagSearchString,
                        Enum.GetValues<GagList.GagType>(), (gag) => gag.GetGagAlias(), false,
                        (i) =>
                        {
                            // locate the GagData that matches the alias of i
                            SelectedGag = GagList.AliasToGagTypeMap[i.GetGagAlias()];
                        }, SelectedGag);
                    UiSharedService.AttachToolTip("Select the gag to apply to the pair.");
                }
                ImUtf8.SameLineInner();
                if (ImGui.Button("Apply Gag", ImGui.GetContentRegionAvail()))
                {
                    // apply the selected gag. (POSSIBLY TODO: Rework the PushApperance Data to only push a single property to avoid conflicts.)
                    _logger.LogInformation("Pushing updated Appearance Data pair and recipients");
                    // construct the modified appearance data.
                    var newAppearance = UserPairForPerms.UserPairAppearanceData.DeepClone();
                    if (SelectedLayer == 0) { newAppearance.SlotOneGagType = SelectedGag.GetGagAlias(); }
                    else if (SelectedLayer == 1) { newAppearance.SlotTwoGagType = SelectedGag.GetGagAlias(); }
                    else if (SelectedLayer == 2) { newAppearance.SlotThreeGagType = SelectedGag.GetGagAlias(); }

                    // push the new appearance data to all online pairs.
                    // _ = _apiController.PushCharacterAppearanceData(newAppearance, _pairManager.GetOnlineUserDatas());
                }
                UiSharedService.AttachToolTip("Apply the selected gag to " + PairAliasOrUID + " on gag layer" + (SelectedLayer+1) + ".\nTHIS DOES NOT WORK YET.");
            }
        }

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
