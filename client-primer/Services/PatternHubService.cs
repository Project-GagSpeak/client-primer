using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Enums;
using GagspeakAPI.Dto.Patterns;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.Services;

// handles the global chat and pattern discovery social features.
public class PatternHubService : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PatternHandler _patternHandler;

    private Task<bool>? UploadPatternTask = null;
    private Task<bool>? RemovePatternTask = null;
    private Task<bool>? LikePatternTask = null;
    private Task<string>? DownloadPatternTask = null;
    private Task<List<ServerPatternInfo>>? SearchPatternsTask = null;

    private bool InitialSearchMade = false;

    public PatternHubService(ILogger<PatternHubService> logger, GagspeakMediator mediator,
        ApiController apiController, ClientConfigurationManager clientConfigs,
        PatternHandler patternHandler) : base(logger, mediator)
    {
        _apiController = apiController;
        _clientConfigs = clientConfigs;
        _patternHandler = patternHandler;

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
        if(!InitialSearchMade && _apiController.IsConnected && ApiController.ServerState == ServerState.Connected)
        { InitialSearchMade = true; SearchPatterns(SearchQuery); }

        DisplayTaskStatus(UploadPatternTask, "Uploading Pattern to Servers...", "Pattern uploaded to servers!", "Failed to upload pattern to servers.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
        DisplayTaskStatus(RemovePatternTask, "Removing pattern...", "Pattern removed successfully.", "Failed to remove pattern.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
        DisplayTaskStatus(LikePatternTask, "Liking pattern...", "Like interaction successful", "Like interaction failed.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
        DisplayTaskStatus(DownloadPatternTask, "Downloading pattern...", "Pattern download successful!", "Failed to download pattern.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
        DisplayTaskStatus(SearchPatternsTask, "Searching for patterns...", "Patterns Fetched!", "Failed to retrieve patterns.", ImGuiColors.DalamudGrey, ImGuiColors.HealerGreen, ImGuiColors.DalamudRed);
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
            else if (task.IsFaulted)
            {
                // throw a failure message and set the task to null.
                message = failureMessage;
                color = failureColor;
                task = null;
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
                else if (task is Task<List<ServerPatternInfo>> listTask)
                {
                    message = listTask.Result.Count != 0 ? successMessage : failureMessage;
                    color = listTask.Result.Count != 0 ? successColor : failureColor;
                }
                else { return; } // Unsupported task type
            }

            if (!string.IsNullOrEmpty(message)) UiSharedService.ColorTextCentered(message, color);
        }
    }

    private async Task ClearTaskInOneSecond(Func<Task?> getTask, Action clearTask)
    {
        await Task.Delay(1000);
        if (getTask() != null)
        {
            clearTask();
        }
    }
    public void UpdateResults() => SearchPatterns(SearchQuery);
    public void SearchPatterns(string searchQuery)
    {
        if (SearchPatternsTask != null)
        {
            Logger.LogWarning("SearchPatternsTask already in progress.");
            return;
        }
        SearchPatternsTask = _apiController.SearchPatterns(new(SearchQuery, new List<string>(), CurrentFilter, CurrentSort));
        SearchPatternsTask.ContinueWith(task =>
        {
            // if the result contains an empty list, then we failed to retrieve patterns.
            if (task.Result.Count == 0)
            {
                Logger.LogError("Failed to retrieve patterns from servers.");
                // clean up the search results
                SearchResults.Clear();
            }
            else
            {
                Logger.LogInformation("Retrieved patterns from servers.", LoggerType.PatternHub);
                SearchResults = SearchPatternsTask.Result;
            }
            _ = ClearTaskInOneSecond(() => SearchPatternsTask, () => SearchPatternsTask = null);
        }, TaskScheduler.Default);
    }
    public void DownloadPatternFromServer(Guid patternIdentifier)
    {
        if (DownloadPatternTask != null)
        {
            Logger.LogWarning("DownloadPatternTask already in progress.");
            return;
        }
        Logger.LogTrace("Downloading Pattern from server.", LoggerType.PatternHub);
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
                // add one download count to the pattern.
                SearchResults.FirstOrDefault(x => x.Identifier == patternIdentifier)!.Downloads++;
                // grab the result and deserialize to base64
                var bytes = Convert.FromBase64String(DownloadPatternTask.Result);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                // Deserialize the string back to pattern data
                PatternData pattern = JsonConvert.DeserializeObject<PatternData>(decompressed) ?? new PatternData();
                // if the pattern for whatever reason is enabled, set it to false, and also set appropriate download variables.
                pattern.IsActive = false;
                pattern.IsPublished = false;
                pattern.CreatedByClient = false;
                // Ensure the pattern has a unique name
                string baseName = _clientConfigs.EnsureUniquePatternName(pattern.Name);
                // Set the active pattern
                _clientConfigs.AddNewPattern(pattern);
            }
            _ = ClearTaskInOneSecond(() => DownloadPatternTask, () => DownloadPatternTask = null);
        }, TaskScheduler.Default);
    }

    // Toggles the rating you set for this pattern.
    public void RatePattern(Guid patternIdentifier)
    {
        if (LikePatternTask != null)
        {
            Logger.LogWarning("LikePatternTask already in progress.");
            return;
        }
        // only allow one like at a time.
        PatternInteractingWith = patternIdentifier;
        LikePatternTask = _apiController.LikePattern(patternIdentifier);
        LikePatternTask.ContinueWith(task =>
        {
            if (task.Result)
            {
                Logger.LogDebug("LikePatternTask completed.", LoggerType.PatternHub);
                // update the pattern stuff
                var pattern = SearchResults.FirstOrDefault(x => x.Identifier == PatternInteractingWith);
                if (pattern != null)
                {
                    pattern.Likes += pattern.HasLiked ? -1 : 1;
                    pattern.HasLiked = !pattern.HasLiked;
                }
            }
            else
            {
                Logger.LogError("LikePatternTask failed.");
            }
            // clear the task.
            _ = ClearTaskInOneSecond(() => LikePatternTask, () => LikePatternTask = null);
        }, TaskScheduler.Default);
    }
    public void UploadPatternToServer(int patternIdx)
    {
        if (UploadPatternTask != null)
        {
            Logger.LogWarning("UploadPatternTask already in progress.");
            return;
        }
        // fetch the pattern at the index
        PatternData patternToUpload = _clientConfigs.FetchPattern(patternIdx).DeepClone();
        // disable pattern and save before uploading.
        patternToUpload.IsActive = false;
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
        // perform the api call for the upload.
        UploadPatternTask = _apiController.UploadPattern(patternDto);
        UploadPatternTask.ContinueWith(task =>
        {
            if (task.Result)
            {
                Mediator.Publish(new NotificationMessage("Pattern Upload", "uploaded successful!", NotificationType.Info));
                // update the published state.
                patternToUpload.IsPublished = true;
                _clientConfigs.UpdatePattern(patternToUpload, patternIdx);
                // update the value of the editing pattern if the editing patternIdx is the same as the patternIdx.
                if (_patternHandler.EditingPatternIndex == patternIdx)
                {
                    _patternHandler.PatternBeingEdited.IsPublished = true;
                }
            }
            else
            {
                Mediator.Publish(new NotificationMessage("Pattern Upload", "upload failed!", NotificationType.Error));
            }
            _ = ClearTaskInOneSecond(() => UploadPatternTask, () => UploadPatternTask = null);
        }, TaskScheduler.Default);
    }

    public void RemovePatternFromServer(int patternIdx)
    {
        if (RemovePatternTask != null)
        {
            Logger.LogWarning("RemovePatternTask already in progress.");
            return;
        }

        // fetch the pattern by the guid
        PatternData? patternToRemove = _clientConfigs.FetchPattern(patternIdx);
        if (patternToRemove == null || patternToRemove.UniqueIdentifier == Guid.Empty)
        {
            Logger.LogWarning("Pattern Does not exist in your storage by this GUID"); 
            return;
        }
        // perform the api call for the removal.
        RemovePatternTask = _apiController.RemovePattern(patternToRemove.UniqueIdentifier);
        RemovePatternTask.ContinueWith(task =>
        {
            if (task.Result)
            {
                // if successful. Notify the success.
                Mediator.Publish(new NotificationMessage("Pattern Removal", "removed successful!", NotificationType.Info));
                patternToRemove.IsPublished = false;
                _clientConfigs.UpdatePattern(patternToRemove, patternIdx);
            }
            else
            {
                Mediator.Publish(new NotificationMessage("Pattern Removal", "removal failed!", NotificationType.Error));
            }
            _ = ClearTaskInOneSecond(() => RemovePatternTask, () => RemovePatternTask = null);
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
            _ => "Unknown"
        };
    }
}
