using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagspeakAPI.Data;

namespace GagSpeak.PlayerData.Handlers;

// Responcible for handling the management of patterns.
// Pattern playback is managed by the PatternPlaybackService.
public class PatternHandler : MediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;

    public PatternHandler(ILogger<PatternHandler> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs) 
        : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
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

    public List<string> PatternNames => _clientConfigs.GetPatternNames();

    public bool IsAnyPatternPlaying() => _clientConfigs.IsAnyPatternPlaying();

    public int GetActivePatternIdx() => _clientConfigs.ActivePatternIdx();

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
    {
        _clientConfigs.RemovePattern(idxToRemove);
        ClearEditingPattern();
    }

    public int PatternListSize()
        => _clientConfigs.GetPatternCount();

    public List<PatternData> GetPatternsForSearch()
        => _clientConfigs.GetPatternsForSearch();

    public PatternData GetPattern(int index) 
        => _clientConfigs.FetchPattern(index);

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

