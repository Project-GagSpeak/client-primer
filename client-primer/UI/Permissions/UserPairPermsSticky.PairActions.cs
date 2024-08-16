using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using System.Numerics;
using GagSpeak.Utils.PermissionHelpers;
using static GagspeakAPI.Data.Enum.GagList;
using OtterGui.Text;
using GagSpeak.Services.Mediator;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.Connection;

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

        if (UserPairForPerms != null && UserPairForPerms.IsOnline)
        {
            // Online Pair Actions
            if(UserPairForPerms.LastReceivedAppearanceData != null)
            {
                ImGui.TextUnformatted("Gag Actions");
                DrawGagActions();
            }

            if(UserPairForPerms.LastReceivedWardrobeData != null)
            {
                ImGui.TextUnformatted("Wardrobe Actions");
                DrawWardrobeActions();
            }

            if(UserPairForPerms.LastReceivedAliasData != null)
            {
                ImGui.TextUnformatted("Puppeteer Actions");
                DrawPuppeteerActions();
            }

            if(UserPairForPerms.LastReceivedIpcData != null)
            {
                ImGui.TextUnformatted("Moodles Actions");
                DrawMoodlesActions();
            }

            if(UserPairForPerms.LastReceivedToyboxData != null)
            {
                ImGui.TextUnformatted("Toybox Actions");
                DrawToyboxActions();
            }
        }

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

    #region GagActions
    private bool ShowGagList = false;
    private bool ShowGagLock = false;
    private bool ShowGagUnlock = false;
    private bool ShowGagRemove = false;
    private void DrawGagActions()
    {
        // draw the layer
        GagAndLockPairkHelpers.DrawGagLayerSelection(ImGui.GetContentRegionAvail().X, UserPairForPerms.UserData.UID);

        // fetch it for ref
        var layerSelected = GagAndLockPairkHelpers.GetSelectedLayer(UserPairForPerms.UserData.UID);

        var disableCondition = layerSelected switch
        {
            0 => UserPairForPerms.LastReceivedAppearanceData!.SlotOneGagType != GagType.None.GetGagAlias(),
            1 => UserPairForPerms.LastReceivedAppearanceData!.SlotTwoGagType != GagType.None.GetGagAlias(),
            2 => UserPairForPerms.LastReceivedAppearanceData!.SlotThreeGagType != GagType.None.GetGagAlias(),
            _ => true // Default to true if an invalid layer is selected
        };

        var lockDisableCondition = layerSelected switch
        {
            0 => UserPairForPerms.LastReceivedAppearanceData!.SlotOneGagPadlock == Padlocks.None.ToString(),
            1 => UserPairForPerms.LastReceivedAppearanceData!.SlotTwoGagPadlock == Padlocks.None.ToString(),
            2 => UserPairForPerms.LastReceivedAppearanceData!.SlotThreeGagPadlock == Padlocks.None.ToString(),
            _ => true // Default to true if an invalid layer is selected
        };


        // button for applying a gag (will display the dropdown of the gag list to apply when pressed.
        if (_uiShared.IconTextButton(FontAwesomeIcon.CommentDots, ("Apply a Gag to " + PairAliasOrUID),
            WindowMenuWidth, true, disableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures))
        {
            ShowGagList = !ShowGagList;
        }
        UiSharedService.AttachToolTip("Apply a Gag to " + PairAliasOrUID + ". Click to view options.");

        if (ShowGagList)
        {
            using (var framedGagApplyChild = ImRaii.Child("GagApplyChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!framedGagApplyChild) return;

                float comboWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply Gag").X - ImGui.GetStyle().ItemSpacing.X * 2;

                GagAndLockPairkHelpers.DrawGagApplyWindow(UserPairForPerms, comboWidth, UserPairForPerms.UserData.UID,
                    _logger, _uiShared, _apiController, out bool success);

                if (success) ShowGagList = false;
            }
            ImGui.Separator();
        }


        // button to lock the current layers gag. (references the gag applied, only interactable when layer is gagged.)
        if (_uiShared.IconTextButton(FontAwesomeIcon.Lock, ("Lock " + PairAliasOrUID + "'s Gag"),
            WindowMenuWidth, true, (!lockDisableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures) || !disableCondition))
        {
            ShowGagLock = !ShowGagLock;
        }
        UiSharedService.AttachToolTip("Lock " + PairAliasOrUID + "'s Gag. Click to view options.");

        if (ShowGagLock)
        {
            // grab if we should expand window height or not prior to drawing it
            bool expandHeight = GagAndLockPairkHelpers.ShouldExpandPasswordWindow(UserPairForPerms.UserData.UID);
            using (var framedGagLockChild = ImRaii.Child("GagLockChild", new Vector2(WindowMenuWidth, 
                ImGui.GetFrameHeight() * (expandHeight ? 2 + ImGui.GetStyle().ItemSpacing.Y : 1)), false))
            {
                if (!framedGagLockChild) return;

                float comboWidth = WindowMenuWidth - ImGui.CalcTextSize("Lock Gag").X - ImGui.GetStyle().ItemSpacing.X * 2;

                GagAndLockPairkHelpers.DrawGagLockWindow(UserPairForPerms, comboWidth, UserPairForPerms.UserData.UID,
                    _logger, _uiShared, _apiController, out bool success);

                if(success) ShowGagLock = false;
            }
            ImGui.Separator();
        }


        // button to unlock the current layers gag. (references appearance data of pair. only visible while locked.)
        if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, ("Unlock " + PairAliasOrUID + "'s Gag"),
            WindowMenuWidth, true, lockDisableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures))
        {
            ShowGagUnlock = !ShowGagUnlock;
        }
        UiSharedService.AttachToolTip("Unlock " + PairAliasOrUID + "'s Gag. Click to view options.");

        if (ShowGagUnlock)
        {
            bool expandHeight = GagAndLockPairkHelpers.ShouldExpandPasswordWindow(UserPairForPerms.UserData.UID);
            using (var framedGagUnlockChild = ImRaii.Child("GagUnlockChild", new Vector2(WindowMenuWidth,
                ImGui.GetFrameHeight() * (expandHeight ? 2 + ImGui.GetStyle().ItemSpacing.Y : 1)), false))
            {
                if (!framedGagUnlockChild) return;

                float comboWidth = WindowMenuWidth - ImGui.CalcTextSize("Unlock Gag").X - ImGui.GetStyle().ItemSpacing.X * 2;

                GagAndLockPairkHelpers.DrawGagUnlockWindow(UserPairForPerms, comboWidth, UserPairForPerms.UserData.UID,
                    _logger, _uiShared, _apiController, out bool success);

                if(success) ShowGagUnlock = false;
            }
            ImGui.Separator();
        }


        // button to remove the current layers gag. (references appearance data of pair. only visible while gagged.)
        if (_uiShared.IconTextButton(FontAwesomeIcon.TimesCircle, ("Remove " + PairAliasOrUID + "'s Gag"),
            WindowMenuWidth, true, (!disableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures) && lockDisableCondition))
        {
            ShowGagRemove = !ShowGagRemove;
        }
        UiSharedService.AttachToolTip("Remove " + PairAliasOrUID + "'s Gag. Click to view options.");

        if (ShowGagRemove)
        {
            using (var framedGagRemoveChild = ImRaii.Child("GagRemoveChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!framedGagRemoveChild) return;

                float comboWidth = WindowMenuWidth - ImGui.CalcTextSize("Remove Gag").X - ImGui.GetStyle().ItemSpacing.X * 2;

                GagAndLockPairkHelpers.DrawGagRemoveWindow(UserPairForPerms, comboWidth, UserPairForPerms.UserData.UID,
                    _logger, _uiShared, _apiController, out bool success);

                if(success) ShowGagRemove = false;
            }
        }
        ImGui.Separator();
    }
    #endregion GagActions

    #region WardrobeActions

    private bool ShowSetApply = false;
    private bool ShowSetLock = false;
    private bool ShowSetUnlock = false;
    private bool ShowSetRemove = false;
    private void DrawWardrobeActions()
    {
        bool applyButtonDisabled = !UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets || UserPairForPerms.LastReceivedWardrobeData!.OutfitNames.Count <= 0;
        bool lockButtonDisabled = !UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets || UserPairForPerms.LastReceivedWardrobeData!.ActiveSetName == string.Empty;
        bool unlockButtonDisabled = !UserPairForPerms.UserPairUniquePairPerms.UnlockRestraintSets || !UserPairForPerms.LastReceivedWardrobeData!.ActiveSetIsLocked;
        bool removeButtonDisabled = !UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets || UserPairForPerms.LastReceivedWardrobeData!.ActiveSetName == string.Empty;
        
        // draw the apply-restraint-set button.
        if (_uiShared.IconTextButton(FontAwesomeIcon.Handcuffs, "Apply Restraint Set", WindowMenuWidth, true, applyButtonDisabled))
        {
            ShowSetApply = !ShowSetApply;
        }
        UiSharedService.AttachToolTip("Applies a Restraint Set to " + UserPairForPerms.UserData.AliasOrUID + ". Click to select set.");

        // show the restraint set list if the button is clicked.
        if (ShowSetApply)
        {
            using (var frameSetApplyChild = ImRaii.Child("SetApplyChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!frameSetApplyChild) return;

                float buttonWidth = WindowMenuWidth - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Female, "Apply Set") - ImGui.GetStyle().ItemSpacing.X;

                WardrobeHelpers.DrawRestraintSetSelection(UserPairForPerms, buttonWidth, UserPairForPerms.UserData.UID, _uiShared);
                ImGui.SameLine(0, 2);
                WardrobeHelpers.DrawApplySet(UserPairForPerms, UserPairForPerms.UserData.UID, _logger, _uiShared, _apiController, out bool success);

                if (success) ShowSetApply = false;
            }
            ImGui.Separator();
        }

        // draw the lock restraint set button.
        if (_uiShared.IconTextButton(FontAwesomeIcon.Lock, "Lock Restraint Set", WindowMenuWidth, true, lockButtonDisabled))
        {
            ShowSetLock = !ShowSetLock;
        }
        UiSharedService.AttachToolTip("Locks the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");

        if (ShowSetLock)
        {
            using (var frameSetLockChild = ImRaii.Child("SetLockChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y), false))
            {
                if (!frameSetLockChild) return;

                float buttonWidth = ImGui.GetContentRegionAvail().X - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Lock, "Lock Set") - ImUtf8.ItemInnerSpacing.X;

                using (var restraintSetLockGroup = ImRaii.Group())
                {
                    using (var disabledSelection = ImRaii.Disabled())
                    {
                        WardrobeHelpers.DrawRestraintSetSelection(UserPairForPerms, ImGui.GetContentRegionAvail().X, UserPairForPerms.UserData.UID, _uiShared);
                    }
                    WardrobeHelpers.DrawLockRestraintSet(UserPairForPerms, buttonWidth, UserPairForPerms.UserData.UID, _logger, _uiShared, _apiController, out bool success);

                    if (success) ShowSetLock = false;
                }
            }
            ImGui.Separator();
        }

        // draw the unlock restraint set button.
        if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, "Unlock Restraint Set", WindowMenuWidth, true, unlockButtonDisabled))
        {
            ShowSetUnlock = !ShowSetUnlock;
        }
        UiSharedService.AttachToolTip("Unlocks the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");

        if (ShowSetUnlock)
        {
            using (var frameSetUnlockChild = ImRaii.Child("SetUnlockChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!frameSetUnlockChild) return;

                WardrobeHelpers.DrawUnlockSet(UserPairForPerms, UserPairForPerms.UserData.UID, _logger, _uiShared, _apiController, out bool success);

                if (success) ShowSetUnlock = false;
            }
            ImGui.Separator();
        }

        // draw the remove restraint set button.
        if (_uiShared.IconTextButton(FontAwesomeIcon.TimesCircle, "Remove Restraint Set", WindowMenuWidth, true, removeButtonDisabled))
        {
            ShowSetRemove = !ShowSetRemove;
        }
        UiSharedService.AttachToolTip("Removes the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");

        if (ShowSetRemove)
        {
            using (var frameSetRemoveChild = ImRaii.Child("SetRemoveChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!frameSetRemoveChild) return;

                WardrobeHelpers.DrawRemoveSet(UserPairForPerms, UserPairForPerms.UserData.UID, _logger, _uiShared, _apiController, out bool success);

                if (success) ShowSetRemove = false;
            }
        }

        ImGui.Separator();
    }

    #endregion WardrobeActions

    #region PuppeteerActions

    private void DrawPuppeteerActions()
    {
        // draw the Alias List popout ref button. (opens a popout window 
        if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Update Pair With Name", WindowMenuWidth, true))
        {
            // compile the alias data to send including our own name and world information, along with an empty alias list.
            var dataToPush = new CharacterAliasData()
            {
                CharacterName = name,
                CharacterWorld = worldName,
                AliasList = new List<AliasTrigger>()
            };

            _ = _apiController.UserPushPairDataAliasStorageUpdate(new OnlineUserCharaAliasDataDto
                (UserPairForPerms.UserData, dataToPush, DataUpdateKind.PuppeteerPlayerNameRegistered));
            _logger.LogDebug("Sent Puppeteer Name to " + UserPairForPerms.UserData.AliasOrUID);
        }
        UiSharedService.AttachToolTip("Sends your Name & World to this pair so their puppeteer will listen for messages from you.");

        ImGui.Separator();
    }

    #endregion PuppeteerActions

    #region MoodlesActions

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

    #endregion MoodlesActions

    #region ToyboxActions

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

    #endregion ToyboxActions

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
