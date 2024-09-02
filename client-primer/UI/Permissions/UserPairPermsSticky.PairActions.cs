using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Utils.PermissionHelpers;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using OtterGui.Text;
using System.Numerics;
using static GagspeakAPI.Data.Enum.GagList;

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
            if (UserPairForPerms.LastReceivedAppearanceData != null)
            {
                ImGui.TextUnformatted("Gag Actions");
                DrawGagActions();
            }

            if (UserPairForPerms.LastReceivedWardrobeData != null)
            {
                ImGui.TextUnformatted("Wardrobe Actions");
                DrawWardrobeActions();
            }

            if (UserPairForPerms.LastReceivedAliasData != null)
            {
                ImGui.TextUnformatted("Puppeteer Actions");
                DrawPuppeteerActions();
            }

            if (UserPairForPerms.LastReceivedIpcData != null && UserPairForPerms.IsVisible)
            {
                ImGui.TextUnformatted("Moodles Actions");
                DrawMoodlesActions();
            }

            if (UserPairForPerms.LastReceivedToyboxData != null)
            {
                ImGui.TextUnformatted("Toybox Actions");
                DrawToyboxActions();
            }

            if (UserPairForPerms.UserPairUniquePairPerms.InHardcore)
            {
                ImGui.TextUnformatted("Hardcore Actions");
                DrawHardcoreActions();
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
        if (_uiShared.IconTextButton(FontAwesomeIcon.CommentDots, ("Apply a Gag to " + PairUID),
            WindowMenuWidth, true, !lockDisableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures))
        {
            ShowGagList = !ShowGagList;
        }
        UiSharedService.AttachToolTip("Apply a Gag to " + PairUID + ". Click to view options.");

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
        if (_uiShared.IconTextButton(FontAwesomeIcon.Lock, ("Lock " + PairUID + "'s Gag"),
            WindowMenuWidth, true, (!lockDisableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures) || !disableCondition))
        {
            ShowGagLock = !ShowGagLock;
        }
        UiSharedService.AttachToolTip("Lock " + PairUID + "'s Gag. Click to view options.");

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

                if (success) ShowGagLock = false;
            }
            ImGui.Separator();
        }


        // button to unlock the current layers gag. (references appearance data of pair. only visible while locked.)
        if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, ("Unlock " + PairUID + "'s Gag"),
            WindowMenuWidth, true, lockDisableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures))
        {
            ShowGagUnlock = !ShowGagUnlock;
        }
        UiSharedService.AttachToolTip("Unlock " + PairUID + "'s Gag. Click to view options.");

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

                if (success) ShowGagUnlock = false;
            }
            ImGui.Separator();
        }


        // button to remove the current layers gag. (references appearance data of pair. only visible while gagged.)
        if (_uiShared.IconTextButton(FontAwesomeIcon.TimesCircle, ("Remove " + PairUID + "'s Gag"),
            WindowMenuWidth, true, (!disableCondition || !UserPairForPerms.UserPairUniquePairPerms.GagFeatures) && lockDisableCondition))
        {
            ShowGagRemove = !ShowGagRemove;
        }
        UiSharedService.AttachToolTip("Remove " + PairUID + "'s Gag. Click to view options.");

        if (ShowGagRemove)
        {
            using (var framedGagRemoveChild = ImRaii.Child("GagRemoveChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!framedGagRemoveChild) return;

                float comboWidth = WindowMenuWidth - ImGui.CalcTextSize("Remove Gag").X - ImGui.GetStyle().ItemSpacing.X * 2;

                GagAndLockPairkHelpers.DrawGagRemoveWindow(UserPairForPerms, comboWidth, UserPairForPerms.UserData.UID,
                    _logger, _uiShared, _apiController, out bool success);

                if (success) ShowGagRemove = false;
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
        if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Update " + PairUID + " with your Name", WindowMenuWidth, true))
        {
            var name = _frameworkUtils.GetPlayerNameAsync().GetAwaiter().GetResult();
            var world = _frameworkUtils.GetHomeWorldIdAsync().GetAwaiter().GetResult();
            var worldName = _uiShared.WorldData[(ushort)world];
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
    private bool ShowApplyPairMoodles = false;
    private bool ShowApplyOwnMoodles = false;
    private bool ShowApplyPairPresets = false;
    private bool ShowApplyOwnPresets = false;
    private bool ShowRemoveMoodles = false;
    private bool ShowClearMoodles = false;
    private void DrawMoodlesActions()
    {
        // determine disable logic.
        bool ApplyPairsMoodleToPairDisabled = !UserPairForPerms.UserPairUniquePairPerms.PairCanApplyYourMoodlesToYou || UserPairForPerms.LastReceivedIpcData?.MoodlesStatuses.Count <= 0;
        bool ApplyOwnMoodleToPairDisabled = !UserPairForPerms.UserPairUniquePairPerms.PairCanApplyOwnMoodlesToYou || LastCreatedCharacterData?.MoodlesStatuses.Count <= 0;
        bool RemovePairsMoodlesDisabled = !UserPairForPerms.UserPairUniquePairPerms.AllowRemovingMoodles || UserPairForPerms.LastReceivedIpcData?.MoodlesDataStatuses.Count <= 0;
        bool ClearPairsMoodlesDisabled = !UserPairForPerms.UserPairUniquePairPerms.AllowRemovingMoodles || UserPairForPerms.LastReceivedIpcData?.MoodlesData == string.Empty;

        // button for adding a Moodle by GUID from the paired user's Moodle list to the paired user. (Requires PairCanApplyYourMoodlesToYou)
        if (_uiShared.IconTextButton(FontAwesomeIcon.PersonCirclePlus, "Apply a Moodle from their list", WindowMenuWidth, true, ApplyPairsMoodleToPairDisabled))
        {
            ShowApplyPairMoodles = !ShowApplyPairMoodles;
        }
        UiSharedService.AttachToolTip("Applies a Moodle from " + UserPairForPerms.UserData.AliasOrUID + "'s Moodles List to them.");

        if (ShowApplyPairMoodles)
        {
            using (var child = ImRaii.Child("ApplyPairMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (UserPairForPerms.LastReceivedIpcData != null)
                {
                    MoodlesHelpers.DrawPairStatusSelection(UserPairForPerms.LastReceivedIpcData.MoodlesStatuses, buttonWidth, PairUID, PairNickOrAliasOrUID, _moodlesService, _logger);
                    ImUtf8.SameLineInner();
                    MoodlesHelpers.ApplyPairStatusButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, out bool success);
                    if (success) ShowApplyPairMoodles = false;
                }
                else
                {
                    UiSharedService.ColorText(PairUID + " has no Moodles to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }

        // button for Applying a preset by GUID from the paired user's preset list to the paired user. (Requires PairCanApplyYourMoodlesToYou)
        if (_uiShared.IconTextButton(FontAwesomeIcon.FileCirclePlus, "Apply a Preset from their list", WindowMenuWidth, true, ApplyPairsMoodleToPairDisabled))
        {
            ShowApplyPairPresets = !ShowApplyPairPresets;
        }
        UiSharedService.AttachToolTip("Applies a Preset from " + UserPairForPerms.UserData.AliasOrUID + "'s Presets List to them.");

        if (ShowApplyPairPresets)
        {
            using (var child = ImRaii.Child("ApplyPairPresets", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (UserPairForPerms.LastReceivedIpcData != null)
                {
                    MoodlesHelpers.DrawPairPresetSelection(UserPairForPerms.LastReceivedIpcData, buttonWidth, PairUID, PairNickOrAliasOrUID, _uiShared, _logger);
                    ImUtf8.SameLineInner();
                    // validate the apply button
                    MoodlesHelpers.ApplyPairPresetButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, out bool success);
                    if (success) ShowApplyPairPresets = false;
                }
                else
                {
                    UiSharedService.ColorText(PairUID + " has no Presets to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }



        // button for adding a Moodle by GUID from client's Moodle list to paired user. (Requires PairCanApplyOwnMoodlesToYou)
        if (_uiShared.IconTextButton(FontAwesomeIcon.UserPlus, "Apply a Moodle from your list", WindowMenuWidth, true, ApplyOwnMoodleToPairDisabled))
        {
            ShowApplyOwnMoodles = !ShowApplyOwnMoodles;
        }
        UiSharedService.AttachToolTip("Applies a Moodle from your Moodles List to " + PairUID + ".");

        if (ShowApplyOwnMoodles)
        {
            using (var child = ImRaii.Child("ApplyOwnMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (LastCreatedCharacterData != null)
                {
                    MoodlesHelpers.DrawOwnStatusSelection(LastCreatedCharacterData.MoodlesStatuses, buttonWidth, PairUID, PairNickOrAliasOrUID, _moodlesService, _logger);
                    ImUtf8.SameLineInner();
                    // validate the apply button
                    MoodlesHelpers.ApplyOwnStatusButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, LastCreatedCharacterData, PairNickOrAliasOrUID, out bool success);
                    if (success) ShowApplyOwnMoodles = false;
                }
                else
                {
                    UiSharedService.ColorText("You have no Moodles to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }

        // button for Applying a preset by GUID from the client's preset list to the paired user. (Requires PairCanApplyOwnMoodlesToYou)
        if (_uiShared.IconTextButton(FontAwesomeIcon.FileCirclePlus, "Apply a Preset from your list", WindowMenuWidth, true, ApplyOwnMoodleToPairDisabled))
        {
            ShowApplyOwnPresets = !ShowApplyOwnPresets;
        }
        UiSharedService.AttachToolTip("Applies a Preset from your Presets List to " + PairUID + ".");

        if (ShowApplyOwnPresets)
        {
            using (var child = ImRaii.Child("ApplyOwnPresets", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (LastCreatedCharacterData != null)
                {
                    MoodlesHelpers.DrawOwnPresetSelection(LastCreatedCharacterData, buttonWidth, PairUID, PairNickOrAliasOrUID, _uiShared, _logger);
                    ImUtf8.SameLineInner();
                    ImUtf8.SameLineInner();
                    // validate the apply button
                    MoodlesHelpers.ApplyOwnPresetButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, LastCreatedCharacterData, PairNickOrAliasOrUID, out bool success);
                    if (success) ShowApplyOwnPresets = false;
                }
                else
                {
                    UiSharedService.ColorText("You have no Presets to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }

        // button for removing a Moodle by GUID from the paired user's list. (Requires AllowsRemovingMoodles)
        if (_uiShared.IconTextButton(FontAwesomeIcon.UserMinus, "Remove a Moodle from " + PairUID, WindowMenuWidth, true, RemovePairsMoodlesDisabled))
        {
            ShowRemoveMoodles = !ShowRemoveMoodles;
        }
        UiSharedService.AttachToolTip("Removes a Moodle from " + PairUID + "'s Statuses.");

        if (ShowRemoveMoodles)
        {
            using (var child = ImRaii.Child("RemoveMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Remove").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                MoodlesHelpers.DrawPairActiveStatusSelection(UserPairForPerms.LastReceivedIpcData!.MoodlesDataStatuses, buttonWidth, PairUID, PairNickOrAliasOrUID, _moodlesService, _logger);
                ImUtf8.SameLineInner();
                // validate the apply button
                MoodlesHelpers.RemoveMoodleButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, out bool success);
                if (success) ShowRemoveMoodles = false;
            }
            ImGui.Separator();
        }

        // button for clearing all moodles from the paired user.
        if (_uiShared.IconTextButton(FontAwesomeIcon.UserSlash, "Clear all Moodles from " + PairUID, WindowMenuWidth, true, ClearPairsMoodlesDisabled))
        {
            ShowClearMoodles = !ShowClearMoodles;
        }
        UiSharedService.AttachToolTip("Clears all Moodles from " + PairUID + "'s Statuses.");

        if (ShowClearMoodles)
        {
            using (var child = ImRaii.Child("ClearMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;
                MoodlesHelpers.ClearMoodlesButton(UserPairForPerms, _apiController, _frameworkUtils, _uiShared, WindowMenuWidth, out bool success);
                if (success) ShowClearMoodles = false;
            }
        }

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

    private void DrawHardcoreActions()
    {
        // conditions for disabled actions
        bool playerTargeted = _clientState.LocalPlayer != null && _clientState.LocalPlayer.TargetObject != null;
        bool playerCloseEnough = playerTargeted && Vector3.Distance(_clientState.LocalPlayer?.Position ?? default, _clientState.LocalPlayer?.TargetObject?.Position ?? default) < 3;

        var forceFollowIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToFollow ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.PersonWalkingArrowRight;
        var forceFollowText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToFollow ? $"Have {PairNickOrAliasOrUID} stop following you." : $"Make {PairNickOrAliasOrUID} follow you.";
        bool disableForceFollow = !playerCloseEnough || !playerTargeted || !UserPairForPerms.UserPairUniquePairPerms.AllowForcedFollow || !UserPairForPerms.IsVisible;
        if (_uiShared.IconTextButton(forceFollowIcon, forceFollowText, WindowMenuWidth, true, disableForceFollow))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToFollow", !perm.IsForcedToFollow)));
        }

        var forceSitIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToSit ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Chair;
        var forceSitText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToSit ? $"Let {PairNickOrAliasOrUID} stand again." : $"Force {PairNickOrAliasOrUID} to sit.";
        bool disableForceSit = !UserPairForPerms.UserPairUniquePairPerms.AllowForcedSit || UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToGroundSit;
        if (_uiShared.IconTextButton(forceSitIcon, forceSitText, WindowMenuWidth, true, disableForceSit))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToSit", !perm.IsForcedToSit)));
        }

        var forceGroundSitIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToGroundSit ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Chair;
        var forceGroundSitText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToGroundSit ? $"Let {PairNickOrAliasOrUID} stand again." : $"Force {PairNickOrAliasOrUID} to their knees.";
        bool disableForceGroundSit = !UserPairForPerms.UserPairUniquePairPerms.AllowForcedSit || UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToSit;
        if (_uiShared.IconTextButton(forceGroundSitIcon, forceGroundSitText, WindowMenuWidth, true, disableForceGroundSit))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToGroundSit", !perm.IsForcedToGroundSit)));
        }

        var forceToStayIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToStay ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.HouseLock;
        var forceToStayText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToStay ? $"Release {PairNickOrAliasOrUID}." : $"Lock away {PairNickOrAliasOrUID}.";
        bool disableForceToStay = !UserPairForPerms.UserPairUniquePairPerms.AllowForcedToStay;
        if (_uiShared.IconTextButton(forceToStayIcon, forceToStayText, WindowMenuWidth, true, disableForceToStay))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToStay", !perm.IsForcedToStay)));
        }

        var toggleBlindfoldIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsBlindfolded ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Mask;
        var toggleBlindfoldText = UserPairForPerms.UserPair!.OtherPairPerms.IsBlindfolded ? $"Remove {PairNickOrAliasOrUID}'s Blindfold." : $"Blindfold {PairNickOrAliasOrUID}.";
        bool disableBlindfoldToggle = !UserPairForPerms.UserPairUniquePairPerms.AllowBlindfold;
        if (_uiShared.IconTextButton(toggleBlindfoldIcon, toggleBlindfoldText, WindowMenuWidth, true, disableBlindfoldToggle))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsBlindfolded", !perm.IsBlindfolded)));
        }
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
