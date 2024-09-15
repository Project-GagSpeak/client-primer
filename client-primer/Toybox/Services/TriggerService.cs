using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.VibeServer;
using Lumina.Excel.GeneratedSheets;
using GameAction = Lumina.Excel.GeneratedSheets.Action;

namespace GagSpeak.Toybox.Services;

// handles the management of the connected devices or simulated vibrator.
public class TriggerService : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly ToyboxVibeService _vibeService;
    private readonly IClientState _clientState;
    private readonly IDataManager _dataManager;

    public List<ClassJob> ClassJobs { get; private set; } = new List<ClassJob>();
    public List<ClassJob> BattleClassJobs => ClassJobs.Where(x => x.Role != 0).ToList();
    public Dictionary<uint, List<GameAction>> LoadedActions { get; private set; } = new Dictionary<uint, List<GameAction>>();

    public TriggerService(ILogger<TriggerService> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        ToyboxVibeService vibeService, IClientState clientState,
        IDataManager dataManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _vibeService = vibeService;
        _clientState = clientState;
        _dataManager = dataManager;
        ClassJobs = _dataManager.GetExcelSheet<ClassJob>()?.ToList() ?? new List<ClassJob>();
    }

    public VibratorMode CurrentVibratorModeUsed => _clientConfigs.GagspeakConfig.VibratorMode;

    public void TryUpdateClassJobList()
    {
        Logger.LogDebug("Attempting to update ClassJob list.");
        if (ClassJobs.Count == 0)
        {
            ClassJobs = _dataManager.GetExcelSheet<ClassJob>()?.ToList() ?? new List<ClassJob>();
            Logger.LogDebug($"ClassJob list updated. Total jobs: {ClassJobs.Count}");
        }
    }

    public ClassJob? GetClientClassJob()
    {
        var clientClassJob = ClassJobs?.FirstOrDefault(x => x.RowId == _clientState.LocalPlayer?.ClassJob.Id);
        return clientClassJob ?? default;
    }

    public void CacheJobActionList(uint JobId)
    {
        Logger.LogDebug($"Attempting to cache actions for JobId: {JobId}");
        if (JobId == uint.MaxValue)
        {
            Logger.LogWarning("Invalid JobId: uint.MaxValue. No actions cached.");
            return;
        }

        // Otherwise, store or load actions for the job
        if (!LoadedActions.ContainsKey(JobId))
        {
            // Fetch all actions for the jobId and add to the dictionary if we haven't cached it already
            var actions = _dataManager.GetExcelSheet<GameAction>()?
                .Where(row => row.IsPlayerAction && row.ClassJob.Value != null && row.ClassJob.Value.RowId == JobId)
                .ToList() ?? new List<GameAction>();

            LoadedActions[JobId] = actions;
            Logger.LogDebug($"Cached {actions.Count} actions for JobId: {JobId}");
        }
        else
        {
            Logger.LogDebug($"Actions for JobId: {JobId} are already cached.");
        }
    }



    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}





