using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GagSpeak.UpdateMonitoring.Chat;
using Lumina.Excel.GeneratedSheets;
using System.Collections.ObjectModel;
using ClientStructFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;


namespace GagSpeak.UpdateMonitoring;
public class EmoteMonitor
{
    private readonly ILogger<EmoteMonitor> _logger;
    private readonly ChatSender _chatSender;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly IClientState _clientState;
    private readonly IDataManager _gameData;
    private Task? EnforceCyclePoseTask;

    private static unsafe AgentEmote* EmoteAgentRef = (AgentEmote*)ClientStructFramework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Emote);
    public unsafe EmoteMonitor(ILogger<EmoteMonitor> logger, ChatSender chatSender,
        OnFrameworkService frameworkUtils, IClientState clientState, IDataManager dataManager)
    {
        _logger = logger;
        _chatSender = chatSender;
        _frameworkUtils = frameworkUtils;
        _clientState = clientState;
        _gameData = dataManager;
        EmoteData = _gameData.GetExcelSheet<Emote>()!.Where(x=> x.RowId is (50 or 52) || x.EmoteMode.Value?.ConditionMode is 3).ToDictionary(x => x.RowId, x => x).AsReadOnly();
    }

    public static readonly ushort[] StandIdleList = new ushort[] { 0, 91, 92, 107, 108, 218, 219 };
    public static readonly ushort[] SitIdList = new ushort[] { 50, 95, 96, 254, 255 };
    public static readonly ushort[] GroundsitIdList = new ushort[] { 52, 97, 98, 117 };
    public static ReadOnlyDictionary<uint, Emote> EmoteData = new ReadOnlyDictionary<uint, Emote>(new Dictionary<uint, Emote>());
    // create a IEnumerable array that only consists of the emote data from keys of 50 and 52.
    public static IEnumerable<Emote> SitEmoteComboList => EmoteData.Where(x => x.Key == 50 || x.Key == 52).Select(x => x.Value);
    public static IEnumerable<Emote> EmoteComboList => EmoteData.Values.ToArray();
    public static string GetEmoteName(uint emoteId)
    {
        if (EmoteData.TryGetValue(emoteId, out var emote))
            return emote?.Name.AsReadOnly().ExtractText().Replace("\u00AD", "") ?? $"Emote#{emoteId}";
        return $"Emote#{emoteId}";
    }

    public unsafe ushort CurrentEmoteId() => ((Character*)(_frameworkUtils.ClientPlayerAddress))->EmoteController.EmoteId;
    public unsafe byte CurrentCyclePose() => ((Character*)(_frameworkUtils.ClientPlayerAddress))->EmoteController.CPoseState;
    public static unsafe bool CanUseEmote(ushort emoteId) => EmoteAgentRef->CanUseEmote(emoteId); // This is valid for both if its not unlocked or if you are on cooldown.
    public static unsafe void ExecuteEmote(ushort emoteId)
    {
        if (!CanUseEmote(emoteId)) 
            return; 
        EmoteAgentRef->ExecuteEmote(emoteId);
    }

    public static int EmoteCyclePoses(ushort emoteId)
    {
        if(IsStandingIdle(emoteId)) return 7;
        if(IsSitting(emoteId)) return 4;
        if(IsGroundSitting(emoteId)) return 3;
        return 1; // 1 meaning there only exists one state, not that it has 2 variants.
    }
    public static bool IsStandingIdle(ushort emoteId) => StandIdleList.Contains(emoteId);
    public static bool IsSitting(ushort emoteId) => SitIdList.Contains(emoteId);
    public static bool IsGroundSitting(ushort emoteId) => GroundsitIdList.Contains(emoteId);
    public static bool IsSittingAny(ushort emoteId) => SitIdList.Concat(GroundsitIdList).Contains(emoteId);
    public static bool IsAnyPoseWithCyclePose(ushort emoteId) => SitIdList.Concat(GroundsitIdList).Concat(StandIdleList).Contains(emoteId);

    public void EnsureOnEmote(ushort emoteId)
    {
        if (!CanUseEmote(emoteId))
            return;

        if (CurrentEmoteId() != emoteId)
            ExecuteEmote(emoteId);
    }


    public void ForceCyclePose(byte expectedCyclePose)
    {
        if (EnforceCyclePoseTask is not null && !EnforceCyclePoseTask.IsCompleted)
            return;

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
                // Set the cycle attempts based on the type of sit being used, so it is equal to the cpose count.
                var cycleAttempts = EmoteMonitor.EmoteCyclePoses(currentPose);
                // Attempt the cycles.
                for (var i = 0; i < cycleAttempts; i++)
                {
                    var current = CurrentCyclePose();

                    if (current == expectedCyclePose)
                        break;

                    _logger.LogTrace("Cycle Pose State was [" + current + "], expected [" + expectedCyclePose + "]. Sending /cpose.", LoggerType.HardcoreMovement);
                    _chatSender.SendMessage("/cpose");
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
