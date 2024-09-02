using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Enum;
using NAudio.Wave;
using GagSpeak.Utils;

namespace GagSpeak.Toybox.Services;
// handles the management of the connected devices or simulated vibrator.
public class PatternPlaybackService : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;

    public PatternPlaybackService(ILogger<PatternPlaybackService> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs)
        : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
    }

    // Public Accessors.
    public bool PlaybackActive { get; private set; } = false;
    public bool ShouldRunPlayback => PlaybackByteRange.Count > 0 && ActivePattern != null;
    public PatternData? ActivePattern => _clientConfigs.GetActiveRunningPattern() ?? null;
    public List<byte> PlaybackByteRange { get; private set; } = [];

    public void CalculateSubsetPatternByteData(TimeSpan startPoint, TimeSpan playbackDuration)
    {
        if (ActivePattern == null) return;

        Logger.LogDebug($"Start point at {startPoint} and duration at {playbackDuration}");
        Logger.LogDebug("Total byte count of original pattern data: " + ActivePattern.PatternByteData.Count);

        // Convert start point and duration to indices
        int _startIndex = (int)(startPoint.TotalSeconds * 50);
        int _endIndex = playbackDuration == TimeSpan.Zero
            ? ActivePattern.PatternByteData.Count
            : _startIndex + (int)(playbackDuration.TotalSeconds * 50);

        // Ensure indices are within bounds
        _startIndex = Math.Max(0, _startIndex);
        _endIndex = Math.Min(ActivePattern.PatternByteData.Count, _endIndex);

        // Log the details
        Logger.LogDebug($"Calculating subset pattern byte data from {_startIndex} to {_endIndex}");

        // Get the subset of the pattern byte data
        PlaybackByteRange = ActivePattern.PatternByteData.Skip(_startIndex).Take(_endIndex - _startIndex).ToList();
    }

    public void PlayPattern(int patternIdx, TimeSpan patternStartPoint, TimeSpan patternDuration, bool publishToMediator)
    {
        // stop any other active patterns if there are any.
        if (_clientConfigs.IsAnyPatternPlaying())
        {
            var activeIdx = _clientConfigs.ActivePatternIdx();
            if (activeIdx != patternIdx)
            {
                // stop the active pattern (maybe set this to false since we are sending another update right after)
                StopPattern(activeIdx, true);
            }
        }

        // set the pattern state to enabled for this index.
        _clientConfigs.SetPatternState(patternIdx, true, publishToMediator);

        // afterwards, if no patterns are active, throw a warning and return.
        if (ActivePattern == null)
        {
            Logger.LogWarning("Cannot play pattern, no active patterns were found.");
            return;
        }

        // calculate the byte range of the active pattern
        CalculateSubsetPatternByteData(ActivePattern.StartPoint, ActivePattern.PlaybackDuration);

        // Set PlaybackActive to true only if ShouldRunPlayback is true
        PlaybackActive = ShouldRunPlayback;

        // publish the toggle to the mediator for the playback to recieve its update notif. (because playback uses this service)
        Mediator.Publish(new PlaybackStateToggled(patternIdx, NewState.Enabled));
    }

    public void StopPattern(int patternIdx, bool publishToMediator)
    {
        // if there are no active patterns. throw a warning and return
        if (!_clientConfigs.IsAnyPatternPlaying())
        {
            Logger.LogWarning("Cannot stop pattern, no patterns were set to play.");
            return;
        }

        // set the pattern state to disabled for this index.
        _clientConfigs.SetPatternState(patternIdx, false, publishToMediator);

        // if disabled, void the playback range and stop the playback
        // by deactivating PlaybackActive first, we prevent playback from reading off a empty list, avoiding crashes.
        PlaybackActive = false;
        PlaybackByteRange = [];

        // publish the toggle to the mediator (if we should)
        Mediator.Publish(new PlaybackStateToggled(patternIdx, NewState.Disabled));
    }

    public string GetPatternNameFromIdx(int patternIdx)
        => _clientConfigs.FetchPattern(patternIdx).Name ?? "Unknown";

    public int GetPatternIdxFromName(string patternName)
        => _clientConfigs.GetPatternIdxByName(patternName);

    public int GetIdxOfActivePattern()
        => _clientConfigs.ActivePatternIdx();

}





