using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagspeakAPI.Data;

namespace GagSpeak.PlayerData.Handlers;

public class PatternHandler : MediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;

    public PatternHandler(ILogger<PatternHandler> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs) 
        : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;

        Mediator.Subscribe<PatternActivedMessage>(this, (msg) => PlaybackRunning = true);

        Mediator.Subscribe<PatternDeactivedMessage>(this, (msg) => PlaybackRunning = false);
    }

    // Handles the pattern stuff
    private PatternData? _patternBeingEdited;
    public int EditingPatternIndex { get; private set; } = -1;
    public PatternData PatternBeingEdited
    {
        get
        {
            if (_patternBeingEdited == null && EditingPatternIndex >= 0)
            {
                _patternBeingEdited = _clientConfigs.FetchPattern(EditingPatternIndex);
            }
            return _patternBeingEdited!;
        }
        private set => _patternBeingEdited = value;
    }
    public bool EditingPatternNull => PatternBeingEdited == null;


    // Store a reference to the currently active pattern so that we can allow our Pattern Playback to reference it
    public PatternData? ActivePattern => _clientConfigs.GetActiveRunningPattern();
    public List<string> PatternNames => _clientConfigs.GetPatternNames();
    /// <summary> 
    /// Used by pattern Playback to determine if a Pattern is playing. 
    /// (UPDATE TO REF THE BOOL FOR ACTIVE PATTERN) 
    /// </summary>
    public bool IsAnyPatternPlaying() => _clientConfigs.IsAnyPatternPlaying();

    /// <summary>
    /// Get the index of the currently active pattern
    /// </summary>
    /// <returns> The index of the currently active pattern. </returns>
    public int GetActivePatternIdx() => _clientConfigs.ActivePatternIdx();
    public bool PlaybackRunning { get; private set; } = false; // convert to ref to if ActivePattern is null later if possible.

    public void SetEditingPattern(PatternData pattern, int index)
    {
        PatternBeingEdited = pattern;
        EditingPatternIndex = index;
    }

    public void ClearEditingPattern()
    {
        EditingPatternIndex = -1;
        PatternBeingEdited = null!;
    }

    public void UpdateEditedPattern()
    {
        // update the pattern in the client configs
        _clientConfigs.UpdatePattern(PatternBeingEdited, EditingPatternIndex);
        // clear the editing pattern
        ClearEditingPattern();
    }

    public TimeSpan GetPatternLength(string name) 
        => (_clientConfigs.GetPatternIdxByName(name) == -1) 
            ? TimeSpan.Zero 
            : _clientConfigs.GetPatternLength(_clientConfigs.GetPatternIdxByName(name));


    public void AddNewPattern(PatternData newPattern)
        => _clientConfigs.AddNewPattern(newPattern);

    public void RemovePattern(int idxToRemove)
        => _clientConfigs.RemovePattern(idxToRemove);

    public int PatternListSize()
        => _clientConfigs.GetPatternCount();

    public PatternData GetPattern(int index) 
        => _clientConfigs.FetchPattern(index);

    // add a fancy play pattern method here that not only takes in a pattern index, but also the pattern start point and playback duration.
    public void PlayPattern(int idx, string startPoint = "00:00", string duration = "00:00")
        => _clientConfigs.SetPatternState(idx, true, startPoint, duration, true);

    public void PlayPatternCallback(string patternName, string startPoint = "00:00", string duration = "00:00")
    {
        var idx = _clientConfigs.GetPatternIdxByName(patternName);
        _clientConfigs.SetPatternState(idx, true, startPoint, duration, false);
    }

    public void StopPattern(int idx)
        => _clientConfigs.SetPatternState(idx, false, "", "", true);

    public void StopPatternCallback(string patternName)
    {
        var idx = _clientConfigs.GetPatternIdxByName(patternName);
        _clientConfigs.SetPatternState(idx, false, "", "", false);
    }

    public void UpdatePatternStatesFromCallback(List<PatternInfo> PatternInfoList)
        => _clientConfigs.UpdatePatternStatesFromCallback(PatternInfoList);


    // Called by the AlarmManager. Scans if index of active pattern is in bounds of list.
    public bool IsIndexInBounds(int idx) => _clientConfigs.IsIndexInBounds(idx);

    public string EnsureUniqueName(string name) => _clientConfigs.EnsureUniqueName(name);

    /// <summary> 
    /// Get the index of a pattern by its name
    /// </summary>
    /// <param name="name"> The name of the pattern to get the index of </param>
    /// <returns> The index of the pattern </returns>
    public int GetPatternIdxByName(string name) => _clientConfigs.GetPatternIdxByName(name);

}

