using Dalamud.Interface;
using Dalamud.Utility;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Enums;
using ImGuiNET;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// 
/// Yes its messy, yet it's long, but i functionalized it best i could for the insane 
/// amount of logic being performed without adding too much overhead.
/// </summary>
public partial class PairStickyUI
{
    public InteractionType Opened = InteractionType.None;
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

            if (UserPairForPerms.UserPairUniquePairPerms.InHardcore && (UniqueShockCollarPermsExist() || GlobalShockCollarPermsExist()))
            {
                ImGui.TextUnformatted("Hardcore Shock Collar Actions.");
                DrawHardcoreShockCollarActions();
            }
        }

        // individual Menu
        ImGui.TextUnformatted("Individual Pair Functions");
        DrawIndividualMenu();
    }

    private bool UniqueShockCollarPermsExist() => !UserPairForPerms.UserPairUniquePairPerms.ShockCollarShareCode.IsNullOrEmpty() && UserPairForPerms.LastPairPiShockPermsForYou.MaxIntensity != -1;
    private bool GlobalShockCollarPermsExist() => !UserPairForPerms.UserPairGlobalPerms.GlobalShockShareCode.IsNullOrEmpty() && UserPairForPerms.LastPairGlobalShockPerms.MaxIntensity != -1;

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

        if (!UserPairForPerms.IsPaused)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.ExclamationTriangle, "Report GagSpeak Profile", WindowMenuWidth, true))
            {
                ImGui.CloseCurrentPopup();
                Mediator.Publish(new ReportKinkPlateMessage(UserPairForPerms));
            }
            UiSharedService.AttachToolTip("Snapshot this user's ProfileData and send it as a reported profile.");
        }

        if (UserPairForPerms.IsPaired)
        {
            var pauseIcon = UserPairForPerms.UserPair!.OwnPairPerms.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
            var pauseText = UserPairForPerms.UserPair!.OwnPairPerms.IsPaused ? $"Unpause {UserPairForPerms.UserData.AliasOrUID}" : $"Pause {UserPairForPerms.UserData.AliasOrUID}";
            if (_uiShared.IconTextButton(pauseIcon, pauseText, WindowMenuWidth, true))
            {
                var perm = UserPairForPerms.UserPair!.OwnPairPerms;
                _ = _apiHubMain.UserUpdateOwnPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                    new KeyValuePair<string, object>("IsPaused", !perm.IsPaused)));
            }
            UiSharedService.AttachToolTip(!UserPairForPerms.UserPair!.OwnPairPerms.IsPaused
            ? "Pause pairing with " + UserPairForPerms.UserData.AliasOrUID
                : "Resume pairing with " + UserPairForPerms.UserData.AliasOrUID);
        }
        if (UserPairForPerms.IsVisible)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Reload IPC data", WindowMenuWidth, true))
            {
                UserPairForPerms.ApplyLastReceivedIpcData(forced: true);
                ImGui.CloseCurrentPopup();
            }
            UiSharedService.AttachToolTip("This reapplies the latest data from Customize+ and Moodles");
        }

        ImGui.Separator();
    }

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

            _ = _apiHubMain.UserPushPairDataAliasStorageUpdate(new OnlineUserCharaAliasDataDto
                (UserPairForPerms.UserData, dataToPush, DataUpdateKind.PuppeteerPlayerNameRegistered));
            _logger.LogDebug("Sent Puppeteer Name to " + UserPairForPerms.UserData.AliasOrUID, LoggerType.Permissions);
        }
        UiSharedService.AttachToolTip("Sends your Name & World to this pair so their puppeteer will listen for messages from you.");
        ImGui.Separator();
    }

    private void DrawIndividualMenu()
    {
        var entryUID = UserPairForPerms.UserData.AliasOrUID;

        if (UserPairForPerms.IndividualPairStatus != IndividualPairStatus.None)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Unpair Permanently", WindowMenuWidth, true, !KeyMonitor.CtrlPressed()))
            {
                _ = _apiHubMain.UserRemovePair(new(UserPairForPerms.UserData));
            }
            UiSharedService.AttachToolTip("Hold CTRL and click to unpair permanently from " + entryUID);
        }
    }
}
