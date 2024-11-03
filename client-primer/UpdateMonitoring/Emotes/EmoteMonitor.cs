using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GagSpeak.UpdateMonitoring.Chat;
using GagspeakAPI.Extensions;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using System.Collections.ObjectModel;
using ClientStructFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;


namespace GagSpeak.UpdateMonitoring;
public class EmoteMonitor
{
    private readonly ILogger<EmoteMonitor> _logger;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly IClientState _clientState;
    private readonly IDataManager _gameData;

    private static unsafe AgentEmote* EmoteAgentRef = (AgentEmote*)ClientStructFramework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Emote);
    public unsafe EmoteMonitor(ILogger<EmoteMonitor> logger, OnFrameworkService frameworkUtils, 
        IClientState clientState, IDataManager dataManager)
    {
        _logger = logger;
        _frameworkUtils = frameworkUtils;
        _clientState = clientState;
        _gameData = dataManager;
        EmoteDataAll = _gameData.GetExcelSheet<Emote>()!;
        EmoteDataLoops = EmoteDataAll.Where(x => x.RowId is (50 or 52) || x.EmoteMode.Value?.ConditionMode is 3).ToDictionary(x => x.RowId, x => x).AsReadOnly();
        // .Where(x => x.RowId is (50 or 52) || x.EmoteMode.Value?.ConditionMode is 3).ToDictionary(x => x.RowId, x => x).AsReadOnly();
        // initialize the commands list.
        foreach (var emoteCommand in EmoteDataAll.Where(x=> x.EmoteCategory?.Value?.RowId is not 3))
        {
            // if the row id is (42 or 24), add it to a temporary list.
            var cmd = emoteCommand.TextCommand.Value?.Command;
            if (cmd != null && cmd != "") EmoteCommands.Add(cmd);
            cmd = emoteCommand.TextCommand.Value?.ShortCommand;
            if (cmd != null && cmd != "") EmoteCommands.Add(cmd);
            cmd = emoteCommand.TextCommand.Value?.Alias;
            if (cmd != null && cmd != "") EmoteCommands.Add(cmd);
            cmd = emoteCommand.TextCommand.Value?.ShortAlias;
            if (cmd != null && cmd != "") EmoteCommands.Add(cmd);
        }
        // sort the EmoteCommands alphabetically.
        EmoteCommands = EmoteCommands.OrderBy(x => x).ToHashSet();

        // Obtain all the yes no commands.
        var yesNoCommands = new HashSet<string>();
        foreach (var emoteCommand in EmoteDataAll.Where(x => x.RowId is (42 or 24) && x.EmoteCategory?.Value?.RowId is not 3))
        {
            // if the row id is (42 or 24), add it to a temporary list.
            var cmd = emoteCommand.TextCommand.Value?.Command;
            if (cmd != null && cmd != "") yesNoCommands.Add(cmd);
            cmd = emoteCommand.TextCommand.Value?.ShortCommand;
            if (cmd != null && cmd != "") yesNoCommands.Add(cmd);
            cmd = emoteCommand.TextCommand.Value?.Alias;
            if (cmd != null && cmd != "") yesNoCommands.Add(cmd);
            cmd = emoteCommand.TextCommand.Value?.ShortAlias;
            if (cmd != null && cmd != "") yesNoCommands.Add(cmd);
        }
        // Make the EmoteCommandsYesNoAccepted the EmoteCommands.Except the yesNoCommands.
        EmoteCommandsYesNoAccepted = EmoteCommands.Except(yesNoCommands).ToHashSet();
        // log all recorded emotes.
        //_logger.LogDebug("Emote Commands: " + string.Join(", ", EmoteCommands));
    }

    public static readonly ushort[] StandIdleList = new ushort[] { 0, 91, 92, 107, 108, 218, 219 };
    public static readonly ushort[] SitIdList = new ushort[] { 50, 95, 96, 254, 255 };
    public static readonly ushort[] GroundSitIdList = new ushort[] { 52, 97, 98, 117 };
    public static ExcelSheet<Emote> EmoteDataAll = null!;
    public static ReadOnlyDictionary<uint, Emote> EmoteDataLoops = null!;
    public static HashSet<string> EmoteCommands = [];
    public static HashSet<string> EmoteCommandsYesNoAccepted = [];

    // create a IEnumerable array that only consists of the emote data from keys of 50 and 52.
    public static IEnumerable<Emote> SitEmoteComboList => EmoteDataLoops.Where(x => x.Key == 50 || x.Key == 52).Select(x => x.Value);
    public static IEnumerable<Emote> EmoteComboList => EmoteDataLoops.Values.ToArray();
    public static string GetEmoteName(uint emoteId)
    {
        if (EmoteDataLoops.TryGetValue(emoteId, out var emote)) return emote?.Name.AsReadOnly().ExtractText().Replace("\u00AD", "") ?? $"Emote#{emoteId}";
        return $"Emote#{emoteId}";
    }

    public unsafe ushort CurrentEmoteId() => ((Character*)(_frameworkUtils.ClientPlayerAddress))->EmoteController.EmoteId;
    public unsafe byte CurrentCyclePose() => ((Character*)(_frameworkUtils.ClientPlayerAddress))->EmoteController.CPoseState;
    public unsafe bool InPositionLoop() => ((Character*)(_frameworkUtils.ClientPlayerAddress))->Mode is CharacterModes.InPositionLoop;

    // This is valid for both if its not unlocked or if you are on cooldown.
    public static unsafe bool CanUseEmote(ushort emoteId) => EmoteAgentRef->CanUseEmote(emoteId);

    // Perform the Emote if we can execute it.
    public static unsafe void ExecuteEmote(ushort emoteId)
    {
        if (!CanUseEmote(emoteId)) return;
        EmoteAgentRef->ExecuteEmote(emoteId);
    }

    // Obtain the number of cycle poses available for the given emote ID.
    public static int EmoteCyclePoses(ushort emoteId)
    {
        if (IsStandingIdle(emoteId)) return 7;
        if (IsSitting(emoteId)) return 4;
        if (IsGroundSitting(emoteId)) return 3;
        return 0;
    }
    public static bool IsStandingIdle(ushort emoteId) => StandIdleList.Contains(emoteId);
    public static bool IsSitting(ushort emoteId) => SitIdList.Contains(emoteId);
    public static bool IsGroundSitting(ushort emoteId) => GroundSitIdList.Contains(emoteId);
    public static bool IsSittingAny(ushort emoteId) => SitIdList.Concat(GroundSitIdList).Contains(emoteId);
    public static bool IsAnyPoseWithCyclePose(ushort emoteId) => SitIdList.Concat(GroundSitIdList).Concat(StandIdleList).Contains(emoteId);

    /// <summary>
    /// Examines our current Emote State and processes if we should be forcing any emote or not.
    /// <para>
    /// This function is extra overhead to avoid players being falsely flagged in bad states 
    /// and firing way more emotes than they should.
    /// </para>
    /// It will need to account for many factors, so try and make it optimized.
    /// </summary>
    /// <param name="emoteState"> The Requested Forced Emote State to Ensure. </param>
    /// <returns> True if we should handle emote or Cycle Pose, false if we shouldnt do either. </returns>
    public bool ShouldHandleExpected(GlobalPermExtensions.EmoteState emoteState, out bool doEmote, out bool doCyclePose, out bool ensureNoSit)
    {
        ushort currentEmote = CurrentEmoteId();
        byte currentCyclePose = CurrentCyclePose();
        bool inPositionLoop = InPositionLoop();
        // Assume we need to make no changes.
        doEmote = false;
        doCyclePose = false;
        ensureNoSit = false;

        // If our expected Emote is 50, handle sitting.
        if (emoteState.EmoteID is 50)
        {
            if (!IsSitting(currentEmote))
            {
                // Check to see if we might actually be sitting, but are just shifting between our cposes. (no change req.)
                if (inPositionLoop)
                    return false;

                // We are actually not sitting, so we need to execute the sit emote, but not the cycle pose.
                doEmote = true;
                return true;
            }
            // We are sitting, so check the cycle pose.
            if (currentCyclePose != emoteState.CyclePoseByte)
            {
                // Only enforce this if our cycle pose state is not already running.
                if (EnforceCyclePoseTask is not null && !EnforceCyclePoseTask.IsCompleted)
                    return false;

                // do the cycle pose thang.
                doCyclePose = true;
                return true;
            }
            // We are already in the correct cycle pose, so we don't need to do anything.
            return false;
        }

        // Handle the GroundSitBullshittery.
        if (emoteState.EmoteID is 52)
        {
            if (!IsGroundSitting(currentEmote))
            {
                // Check to see if we might actually be sitting, but are just shifting between our Cycle Poses. (no change req.)
                if (inPositionLoop)
                    return false;

                // We are actually not sitting, so we need to execute the sit emote, but not the cycle pose.
                doEmote = true;
                return true;
            }
            // We are sitting, so check the cycle pose.
            if (currentCyclePose != emoteState.CyclePoseByte)
            {
                // Only enforce this if our cycle pose state is not already running.
                if (EnforceCyclePoseTask is not null && !EnforceCyclePoseTask.IsCompleted)
                    return false;

                // do the cycle pose thang.
                doCyclePose = true;
                return true;
            }
            // We are already in the correct cycle pose, so we don't need to do anything.
            return false;
        }

        // if our expected state is not 50 or 52, but we are in any state, we need to execute /sit instead.
        if (IsSittingAny(currentEmote) && emoteState.EmoteID is not (50 or 52))
        {
            _logger.LogDebug("Forcing player to stand up from sitting state.", LoggerType.HardcoreMovement);
            // We are sitting, so we need to execute the sit emote, but not the cycle pose.
            ensureNoSit = true;
            return true;
        }

        // Handling Any other emote requires no Cycle Pose shifts.
        if (currentEmote != emoteState.EmoteID)
        {
            // We are not in the correct emote, so we need to execute the emote, but not the cycle pose.
            doEmote = true;
            return true;
        }

        return false;
    }

    public static bool IsCyclePoseTaskRunning => EnforceCyclePoseTask is not null && !EnforceCyclePoseTask.IsCompleted;
    private static Task? EnforceCyclePoseTask;

    public void ForceCyclePose(byte expectedCyclePose)
    {
        if (EmoteMonitor.IsCyclePoseTaskRunning) return;
        _logger.LogDebug("Forcing player into cycle pose: " + expectedCyclePose, LoggerType.HardcoreMovement);
        EnforceCyclePoseTask = ForceCyclePoseInternal(expectedCyclePose);
    }

    /// <summary>
    /// Force player into a certain CyclePose. Will not fire if Task is currently running.
    /// </summary>
    private async Task ForceCyclePoseInternal(byte expectedCyclePose)
    {
        try
        {
            // Only do this task if we are currently in a groundsit pose.
            var currentPose = CurrentEmoteId();
            // if our emote is not any type of sit, dont perform this task.
            if (IsAnyPoseWithCyclePose(currentPose))
            {
                // Attempt the cycles, break out when we hit the count.
                for (var i = 0; i < 7; i++)
                {
                    var current = CurrentCyclePose();

                    if (current == expectedCyclePose)
                        break;

                    _logger.LogTrace("Cycle Pose State was [" + current + "], expected [" + expectedCyclePose + "]. Sending /cpose.", LoggerType.HardcoreMovement);
                    ExecuteEmote(90);
                    // give some delay before re-execution.
                    await Task.Delay(500);
                }
            }
        }
        finally
        {
            EnforceCyclePoseTask = null;
        }
    }
}
