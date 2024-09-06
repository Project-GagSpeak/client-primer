using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Patterns;
using System.Numerics;

namespace GagSpeak.Services;

// handles the global chat and pattern discovery social features.
public class PatternHubService : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager;

    private Task<bool>? UploadPatternTask = null;
    private Task<bool>? LikePatternTask = null;
    private Task<string>? DownloadPatternTask = null;
    private Task<List<ServerPatternInfo>>? SearchPatternsTask = null;
    public PatternHubService(ILogger<PatternHubService> logger, GagspeakMediator mediator,
        ApiController apiController, ClientConfigurationManager clientConfigs,
        PairManager pairManager) : base(logger, mediator)
    {
        _apiController = apiController;
        _clientConfigs = clientConfigs;
        _pairManager = pairManager;

    }

    private Guid PatternInteractingWith = Guid.Empty;
    public string SearchQuery { get; private set; } = string.Empty;
    public SearchFilter CurrentFilter { get; private set; } = SearchFilter.Downloads;
    public SearchSort CurrentSort { get; private set; } = SearchSort.Descending;
    public List<ServerPatternInfo> SearchResults { get; private set; } = new List<ServerPatternInfo>();

    public void UpdateSearchQuery(string query) => SearchQuery = query;
    public void SetFilter(SearchFilter filter) => CurrentFilter = filter;
    public void ToggleSort() => CurrentSort = CurrentSort == SearchSort.Ascending ? SearchSort.Descending : SearchSort.Ascending;

    // Should be run in the drawloop to check if any tasks have completed.
    public void DisplayPendingMessages()
    {
        DisplayTaskStatus(UploadPatternTask, "Uploading Pattern to Servers...", "Pattern uploaded to servers!", "Failed to upload pattern to servers.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
        DisplayTaskStatus(LikePatternTask, "Liking pattern...", "Like interaction successful", "Like interaction failed.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
        DisplayTaskStatus(DownloadPatternTask, "Downloading pattern...", "Pattern download successful!", "Failed to download pattern.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
        DisplayTaskStatus(SearchPatternsTask, "Searching for patterns...", "Failed to retrieve patterns.", string.Empty, ImGuiColors.DalamudGrey, ImGuiColors.DalamudGrey, ImGuiColors.DalamudGrey);
    }

    private void DisplayTaskStatus<T>(Task<T>? task, string inProgressMessage, string successMessage,
        string failureMessage, Vector4 inProgressColor, Vector4 successColor, Vector4 failureColor)
    {
        if (task != null)
        {
            string message;
            Vector4 color;

            if (!task.IsCompleted)
            {
                message = inProgressMessage;
                color = inProgressColor;
            }
            else
            {
                if (task is Task<bool> boolTask)
                {
                    message = boolTask.Result ? successMessage : failureMessage;
                    color = boolTask.Result ? successColor : failureColor;
                }
                else if (task is Task<string> stringTask)
                {
                    message = stringTask.Result != null ? successMessage : failureMessage;
                    color = stringTask.Result != null ? successColor : failureColor;
                }
                else if (task is Task<List<ServerPatternInfo>>)
                {
                    message = inProgressMessage;
                    color = inProgressColor;
                }
                else { return; } // Unsupported task type
            }

            if (!string.IsNullOrEmpty(message)) UiSharedService.ColorTextCentered(message, color);
        }
    }

    private async Task ClearTaskInFiveSeconds(Func<Task?> getTask, Action clearTask)
    {
        await Task.Delay(5000);
        if (getTask() != null)
        {
            clearTask();
        }
    }
    public void UpdateResults()
    {
        SearchPatternsTask = _apiController.SearchPatterns(new(SearchQuery, new List<string>(), CurrentFilter, CurrentSort));
    }
    public void SearchPatterns(string searchQuery)
    {
        SearchPatternsTask = _apiController.SearchPatterns(new(SearchQuery, new List<string>(), CurrentFilter, CurrentSort));
        SearchPatternsTask.ContinueWith(task =>
        {
            // if the result contains an empty list, then we failed to retrieve patterns.
            if (task.Result.Count == 0)
            {
                Logger.LogError("Failed to retrieve patterns from servers.");
            }
            else
            {
                Logger.LogInformation("Retrieved patterns from servers.");
                SearchResults = SearchPatternsTask.Result;
            }
            _ = ClearTaskInFiveSeconds(() => SearchPatternsTask, () => SearchPatternsTask = null);
        }, TaskScheduler.Default);
    }
    public void DownloadPatternFromServer(Guid patternIdentifier)
    {
        DownloadPatternTask = _apiController.DownloadPattern(patternIdentifier);
        DownloadPatternTask.ContinueWith(task =>
        {
            if (task.Result == string.Empty)
            {
                Mediator.Publish(new NotificationMessage("Pattern Download", "failed to download Pattern from servers.", NotificationType.Error));
            }
            else
            {
                Mediator.Publish(new NotificationMessage("Pattern Download", "finished downloading Pattern from servers.", NotificationType.Info));
                var bytes = Convert.FromBase64String(DownloadPatternTask.Result);
                // Decode the base64 string back to a regular string
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                // Deserialize the string back to pattern data
                PatternData pattern = JsonConvert.DeserializeObject<PatternData>(decompressed) ?? new PatternData();
                // Ensure the pattern has a unique name
                string baseName = _clientConfigs.EnsureUniqueName(pattern.Name);
                // Set the active pattern
                _clientConfigs.AddNewPattern(pattern);
            }
            _ = ClearTaskInFiveSeconds(() => DownloadPatternTask, () => DownloadPatternTask = null);
        }, TaskScheduler.Default);
    }

    // Toggles the rating you set for this pattern.
    public void RatePattern(Guid patternIdentifier)
    {
        if (LikePatternTask != null) return;
        // only allow one like at a time.
        PatternInteractingWith = patternIdentifier;
        LikePatternTask = _apiController.LikePattern(patternIdentifier);
        LikePatternTask.ContinueWith(task =>
        {
            if (task.Result)
            {
                Logger.LogDebug("LikePatternTask completed.");
                // update the pattern stuff
                var pattern = SearchResults.FirstOrDefault(x => x.Identifier == PatternInteractingWith);
                if (pattern != null)
                {
                    pattern.HasLiked = !pattern.HasLiked;
                }
            }
            else
            {
                Logger.LogError("LikePatternTask failed.");
            }
            // clear the task.
            _ = ClearTaskInFiveSeconds(() => LikePatternTask, () => LikePatternTask = null);
        }, TaskScheduler.Default);
    }
    public void UploadPatternToServer(int patternIdx)
    {
        // fetch the pattern at the index
        PatternData patternToUpload = _clientConfigs.FetchPattern(patternIdx);
        // compress the pattern into base64.
        string json = JsonConvert.SerializeObject(patternToUpload);
        // Encode the string to a base64 string
        var compressed = json.Compress(6);
        string base64Pattern = Convert.ToBase64String(compressed);
        // construct the serverPatternInfo for the upload.
        ServerPatternInfo patternInfo = new ServerPatternInfo()
        {
            Identifier = patternToUpload.UniqueIdentifier,
            Name = patternToUpload.Name,
            Description = patternToUpload.Description,
            Author = patternToUpload.Author,
            Tags = patternToUpload.Tags,
            Length = patternToUpload.Duration,
            Looping = patternToUpload.ShouldLoop,
            UsesVibrations = true,
            UsesRotations = false,
            UsesOscillation = false,
        };
        // construct the dto for the upload.
        PatternUploadDto patternDto = new(_apiController.PlayerUserData, patternInfo, base64Pattern);
        // perform the apicall for the upload.
        UploadPatternTask = _apiController.UploadPattern(patternDto);
        UploadPatternTask.ContinueWith(task =>
        {
            if (task.Result)
            {
                Mediator.Publish(new NotificationMessage("Pattern Upload", "uploaded successful!", NotificationType.Info));

            }
            else
            {
                Mediator.Publish(new NotificationMessage("Pattern Upload", "upload failed!", NotificationType.Error));
            }
            _ = ClearTaskInFiveSeconds(() => UploadPatternTask, () => UploadPatternTask = null);
        }, TaskScheduler.Default);
    }

    public string FilterToName(SearchFilter filter)
    {
        return filter switch
        {
            SearchFilter.MostRecent => "Upload Date",
            SearchFilter.Downloads => "Downloads",
            SearchFilter.Likes => "Likes",
            SearchFilter.Author => "Author",
            SearchFilter.DurationTiny => "< 1 min",
            SearchFilter.DurationShort => "< 5 min",
            SearchFilter.DurationMedium => "5-20 min",
            SearchFilter.DurationLong => "20-60 min",
            SearchFilter.DurationExtraLong => "> 1 hour",
            SearchFilter.UsesVibration => "Vibrations",
            SearchFilter.UsesRotation => "Rotations",
            SearchFilter.UsesOscillation => "Oscillations",
            _ => "Unknown"
        };
    }
}
