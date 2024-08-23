using Dalamud.Interface;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.IPC;
using System.Numerics;
using OtterGui.Text;
using GagspeakAPI.Data.IPC;
using GagspeakAPI.Data;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Data.Permissions;
using OtterGui;

namespace GagSpeak.Utils.PermissionHelpers;

/// <summary>
/// Various helper functions for the Permissions window.
/// </summary>
public static class MoodlesHelpers
{
    // Gag Action Variables Begin
    private static string MoodlesStatusSearch = string.Empty;
    private static string MoodlesPresetSearch = string.Empty; // may become functional once they add friendly names to presets.
    private static Dictionary<string, PairState> pairStates = new Dictionary<string, PairState>();

    private static PairState GetOrCreatePairState(string UID, string nickOrAliasOrUID)
    {
        if (!pairStates.TryGetValue(UID, out var state))
        {
            state = new PairState() { Name = nickOrAliasOrUID };
            pairStates[UID] = state;
        }
        return state;
    }

    private class PairState
    {
        public string Name;
        public int SelectedStatusIdx = 2;
        public int SelectedPresetIdx = 0;
        public Guid SelectedStatusGuid = Guid.Empty;
        public Guid SelectedPresetGuid = Guid.Empty;
    }

    public static void DrawStatusSelection(CharacterIPCData ipcData, float width, string UID, string nickname, MoodlesService moodlesService, ILogger logger)
    {
        var state = GetOrCreatePairState(UID, nickname);
        // reset selected Idx if list has changed and made the selectable beyond the bounds.
        if (ipcData.MoodlesStatuses.Count <= state.SelectedStatusIdx)
        {
            logger.LogWarning("SelectedStatusIdx was out of bounds. The count was {count} and the selected index was {selectedIdx}", ipcData.MoodlesStatuses.Count, state.SelectedStatusIdx);
            state.SelectedStatusIdx = 0;
        }
            // Draw out the status selector.
        if (moodlesService.DrawMoodleStatusComboSearchable(ipcData, "##Status for " + nickname, ref state.SelectedStatusIdx, width, 1.0f))
        {
            logger.LogTrace("SelectedStatusIdx is now {selectedStatusIdx} with GUID {guid}", state.SelectedStatusIdx, ipcData.MoodlesStatuses[state.SelectedStatusIdx].GUID);
            state.SelectedStatusGuid = ipcData.MoodlesStatuses[state.SelectedStatusIdx].GUID;
        }
        UiSharedService.AttachToolTip("Select a status to apply to " + nickname);
    }

    public static void DrawPresetSelection(CharacterIPCData ipcData, float width, string UID, string nickname, UiSharedService uiShared, ILogger logger)
    {
        var state = GetOrCreatePairState(UID, nickname);
        // reset selected Idx if list has changed and made the selectable beyond the bounds.
        if (ipcData.MoodlesPresets.Count <= state.SelectedPresetIdx) state.SelectedPresetIdx = 0;

        // Draw out the status selector.
        uiShared.DrawComboSearchable("##PresetSelector", width, ref MoodlesPresetSearch, ipcData.MoodlesPresets,
            (preset) => preset.Item1.ToString(), false, 
            (preset) =>
            {
                state.SelectedPresetGuid = preset.Item1;
                state.SelectedPresetIdx = ipcData.MoodlesPresets.IndexOf(preset);
            });

        // Extract the titles of the statuses for the selected preset
        var statusTitles = ipcData.MoodlesPresets[state.SelectedPresetIdx].Item2
            .Select(guid => ipcData.MoodlesStatuses.FirstOrDefault(status => status.GUID == guid).Title)
            .Where(title => !string.IsNullOrEmpty(title));

        // join the titles together and attach it to the tooltip.
        UiSharedService.AttachToolTip("Applies the Moodles:\n " + string.Join("\n", statusTitles));
    }

    #region ApplyPairsMoodles
    public static void ApplyPairStatusButton(Pair Pair, ApiController ApiController, ILogger logger,
        OnFrameworkService frameworkUtils, UiSharedService uiShared, out bool success)
    {
        success = false;

        var state = GetOrCreatePairState(Pair.UserData.UID, Pair.GetNickname() ?? Pair.UserData.AliasOrUID);
        if (Pair.LastReceivedIpcData == null || Pair.LastReceivedIpcData.MoodlesStatuses.Count <= state.SelectedStatusIdx) return;

        bool disabled = Pair.LastReceivedIpcData.MoodlesStatuses[state.SelectedStatusIdx].GUID != state.SelectedStatusGuid;
        if (ImGuiUtil.DrawDisabledButton("Apply##ApplyPairStatus" + Pair.UserData.UID, new Vector2(), string.Empty, disabled))
        {
            // validate the permissions for it.
            var statusInfo = new List<MoodlesStatusInfo> { Pair.LastReceivedIpcData.MoodlesStatuses[state.SelectedStatusIdx] };
            if (!ValidatePermissionForApplication(logger, Pair.UserPairUniquePairPerms, statusInfo)) return;

            Guid statusGuid = state.SelectedStatusGuid;
            logger.LogInformation("Applying status {statusGuid} to {pairNickname}", statusGuid, Pair.GetNickname() ?? Pair.UserData.AliasOrUID);
            _ = ApiController.UserApplyMoodlesByGuid(new ApplyMoodlesByGuidDto(Pair.UserData, new List<Guid> { statusGuid }, IpcToggleType.MoodlesStatus));
            success = true;
            state.SelectedStatusIdx = 0;
            state.SelectedStatusGuid = Guid.Empty;
        }
        UiSharedService.AttachToolTip("Apply the selected status to " + Pair.GetNickname() ?? Pair.UserData.AliasOrUID);
        return;
    }

    public static void ApplyPairPresetButton(Pair Pair, ApiController ApiController, ILogger logger,
        OnFrameworkService frameworkUtils, UiSharedService uiShared, out bool success)
    {
        success = false;

        var state = GetOrCreatePairState(Pair.UserData.UID, Pair.GetNickname() ?? Pair.UserData.AliasOrUID);
        if (Pair.LastReceivedIpcData == null || Pair.LastReceivedIpcData.MoodlesPresets.Count <= state.SelectedPresetIdx) return;

        bool disabled = Pair.LastReceivedIpcData.MoodlesPresets[state.SelectedPresetIdx].Item1 != state.SelectedPresetGuid;
        if (ImGuiUtil.DrawDisabledButton("Apply##ApplyPairPreset" + Pair.UserData.UID, new Vector2(), string.Empty, disabled))
        {
            // compile together the list of statuses to apply by extracting them from our status list where the ID's match.
            List<MoodlesStatusInfo> statusesToApply = new List<MoodlesStatusInfo>();
            foreach (var presetStatusGuid in Pair.LastReceivedIpcData.MoodlesPresets[state.SelectedPresetIdx].Item2)
            {
                var statusToAdd = Pair.LastReceivedIpcData.MoodlesStatuses.Where(s => s.GUID == presetStatusGuid).FirstOrDefault();
                if (statusToAdd.GUID != presetStatusGuid) return;
                // add the status
                statusesToApply.Add(statusToAdd);
            }

            // validate permissions
            if (!ValidatePermissionForApplication(logger, Pair.UserPairUniquePairPerms, statusesToApply)) return;

            Guid statusGuid = state.SelectedPresetGuid;
            _ = ApiController.UserApplyMoodlesByGuid(new ApplyMoodlesByGuidDto(Pair.UserData, statusesToApply.Select(s => s.GUID).ToList(), IpcToggleType.MoodlesPreset));
            success = true;
        }
        UiSharedService.AttachToolTip("Apply the selected status to " + Pair.GetNickname() ?? Pair.UserData.AliasOrUID);
        return;
    }
    #endregion ApplyPairsMoodles

    #region ApplyOwnMoodles
    public static void ApplyOwnStatusButton(Pair pairForApplication, ApiController ApiController, ILogger logger, OnFrameworkService frameworkUtils, 
        UiSharedService uiShared, CharacterIPCData clientPlayerIpc, string pairNickname, out bool success)
    {
        success = false;

        var state = GetOrCreatePairState(pairForApplication.UserData.UID, pairNickname);
        if (clientPlayerIpc == null || clientPlayerIpc.MoodlesStatuses.Count <= state.SelectedStatusIdx) return;

        bool disabled = clientPlayerIpc.MoodlesStatuses[state.SelectedStatusIdx].GUID != state.SelectedStatusGuid;
        if (ImGuiUtil.DrawDisabledButton("Apply##ApplyOwnStatus"+pairForApplication.UserData.UID, new Vector2(), string.Empty, disabled))
        {
            MoodlesStatusInfo status = clientPlayerIpc.MoodlesStatuses[state.SelectedStatusIdx];
            // validate permissions
            if (!ValidatePermissionForApplication(logger, pairForApplication.UserPairUniquePairPerms, new List<MoodlesStatusInfo> { status })) return;

            _ = ApiController.UserApplyMoodlesByStatus(new ApplyMoodlesByStatusDto(pairForApplication.UserData, new List<MoodlesStatusInfo> { status }, IpcToggleType.MoodlesStatus));
            success = true;
            state.SelectedStatusIdx = 0;
            state.SelectedStatusGuid = Guid.Empty;
        }
        UiSharedService.AttachToolTip("Apply the selected status to " + pairNickname);
        return;
    }

    public static void ApplyOwnPresetButton(Pair pairForApplication, ApiController ApiController, ILogger logger, OnFrameworkService frameworkUtils, 
        UiSharedService uiShared, CharacterIPCData clientPlayerIpc, string pairNickname, out bool success)
    {
        success = false;

        var state = GetOrCreatePairState(pairForApplication.UserData.UID, pairNickname);
        if (clientPlayerIpc == null || clientPlayerIpc.MoodlesPresets.Count <= state.SelectedPresetIdx) return;

        bool disabled = clientPlayerIpc.MoodlesPresets[state.SelectedPresetIdx].Item1 != state.SelectedPresetGuid;
        if (ImGuiUtil.DrawDisabledButton("Apply##ApplyOwnPreset" + pairForApplication.UserData.UID, new Vector2(), string.Empty, disabled))
        {
            // compile together the list of statuses to apply by extracting them from our status list where the ID's match.
            List<MoodlesStatusInfo> statusesToApply = new List<MoodlesStatusInfo>();
            foreach (var presetStatusGuid in clientPlayerIpc.MoodlesPresets[state.SelectedPresetIdx].Item2)
            {
                var statusToAdd = clientPlayerIpc.MoodlesStatuses.Where(s => s.GUID == presetStatusGuid).FirstOrDefault();
                if (statusToAdd.GUID != presetStatusGuid) return;
                // add the status
                statusesToApply.Add(statusToAdd);
            }

            // validate permissions
            if(!ValidatePermissionForApplication(logger, pairForApplication.UserPairUniquePairPerms, statusesToApply)) return;

            _ = ApiController.UserApplyMoodlesByStatus(new ApplyMoodlesByStatusDto(pairForApplication.UserData, statusesToApply, IpcToggleType.MoodlesPreset));
            success = true;
            state.SelectedPresetIdx = 0;
            state.SelectedPresetGuid = Guid.Empty;
        }
        UiSharedService.AttachToolTip("Apply the selected status to " + pairNickname);
        return;
    }
    #endregion ApplyOwnMoodles


    #region RemoveMoodles
    public static void RemoveMoodleButton(Pair pairToRemoveMoodlesFrom, ApiController apiController, ILogger logger,
        OnFrameworkService frameworkUtils, UiSharedService uiShared, out bool success)
    {
        success = false;

        var state = GetOrCreatePairState(pairToRemoveMoodlesFrom.UserData.UID, pairToRemoveMoodlesFrom.GetNickname() ?? pairToRemoveMoodlesFrom.UserData.AliasOrUID);
        if (pairToRemoveMoodlesFrom.LastReceivedIpcData == null || pairToRemoveMoodlesFrom.LastReceivedIpcData.MoodlesStatuses.Count <= state.SelectedStatusIdx) return;

        bool disabled = pairToRemoveMoodlesFrom.LastReceivedIpcData.MoodlesStatuses[state.SelectedStatusIdx].GUID != state.SelectedStatusGuid
            || pairToRemoveMoodlesFrom.UserPairUniquePairPerms.AllowRemovingMoodles == false;

        if (ImGuiUtil.DrawDisabledButton("Apply##RemoveStatus"+pairToRemoveMoodlesFrom.UserData.UID, new Vector2(), string.Empty, disabled))
        {
            Guid statusGuid = state.SelectedStatusGuid;
            _ = apiController.UserRemoveMoodles(new RemoveMoodlesDto(pairToRemoveMoodlesFrom.UserData, new List<Guid> { statusGuid }));
            success = true;
            state.SelectedStatusIdx = 0;
            state.SelectedStatusGuid = Guid.Empty;
        }
        UiSharedService.AttachToolTip("Remove the selected status from " + pairToRemoveMoodlesFrom.GetNickname() ?? pairToRemoveMoodlesFrom.UserData.AliasOrUID);
        return;
    }
    #endregion RemoveMoodles

    #region ClearMoodles
    public static void ClearMoodlesButton(Pair pairToClearMoodlesFrom, ApiController apiController,
        OnFrameworkService frameworkUtils, UiSharedService uiShared, out bool success)
    {
        success = false;

        if (!pairToClearMoodlesFrom.UserPairUniquePairPerms.AllowRemovingMoodles || pairToClearMoodlesFrom.LastReceivedIpcData == null) return;

        if (ImGuiUtil.DrawDisabledButton("Apply##ClearStatus" + pairToClearMoodlesFrom.UserData.UID, new Vector2(), string.Empty, pairToClearMoodlesFrom.LastReceivedIpcData.MoodlesData == string.Empty))
        {
            _ = apiController.UserClearMoodles(new(pairToClearMoodlesFrom.UserData));
            success = true;
        }
        UiSharedService.AttachToolTip("Clear all statuses from " + pairToClearMoodlesFrom.GetNickname() ?? pairToClearMoodlesFrom.UserData.AliasOrUID);
        return;
    }
    #endregion ClearMoodles

    public static bool ValidatePermissionForApplication(ILogger logger, UserPairPermissions pairPermsForClient, List<MoodlesStatusInfo> statuses)
    {

        if (!pairPermsForClient.AllowPositiveStatusTypes && statuses.Any(statuses => statuses.Type == StatusType.Positive))
        {
            logger.LogWarning("Client Attempted to apply status(s) with at least one containing a positive status, but they are not allowed to.");
            return false;
        }
        if (!pairPermsForClient.AllowNegativeStatusTypes && statuses.Any(statuses => statuses.Type == StatusType.Negative))
        {
            logger.LogWarning("Client Attempted to apply status(s) with at least one containing a negative status, but they are not allowed to.");
            return false;
        }
        if (!pairPermsForClient.AllowSpecialStatusTypes && statuses.Any(statuses => statuses.Type == StatusType.Special))
        {
            logger.LogWarning("Client Attempted to apply status(s) with at least one containing a special status, but they are not allowed to.");
            return false;
        }

        if (!pairPermsForClient.AllowPermanentMoodles && statuses.Any(statuses => statuses.NoExpire))
        {
            logger.LogWarning("Client Attempted to apply status(s) with at least one containing a permanent status, but they are not allowed to.");
            return false;
        }

        // check the max moodle time exceeding
        if (statuses.Any(status => status.NoExpire == false && // if the status is not permanent, and the time its set for is longer than max allowed time.
            new TimeSpan(status.Days, status.Hours, status.Minutes, status.Seconds) > pairPermsForClient.MaxMoodleTime))
        {
            logger.LogWarning("Client Attempted to apply status(s) with at least one containing a time exceeding the max allowed time.");
            return false;
        }
        // return true if reached here.
        return true;
    }
}

