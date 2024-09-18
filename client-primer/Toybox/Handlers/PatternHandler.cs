using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagspeakAPI.Data;

namespace GagSpeak.PlayerData.Handlers;

// Responcible for handling the management of patterns.
// Pattern playback is managed by the PlaybackService.
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

    public void AddNewPattern(PatternData newPattern)
        => _clientConfigs.AddNewPattern(newPattern);

    public void RemovePattern(Guid idToRemove)
    {
        _clientConfigs.RemovePattern(idToRemove);
        ClearEditingPattern();
    }

    public int PatternListSize()
        => _clientConfigs.GetPatternCount();

    public List<PatternData> GetPatternsForSearch()
        => _clientConfigs.GetPatternsForSearch();

    public string EnsureUniqueName(string name) => _clientConfigs.EnsureUniquePatternName(name);
}

