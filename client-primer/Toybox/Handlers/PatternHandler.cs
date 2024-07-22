using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;

namespace GagSpeak.PlayerData.Handlers;

public class PatternHandler : MediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly DeviceHandler _IntifaceHandler;

    public PatternHandler(ILogger<PatternHandler> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PlayerCharacterManager playerManager, DeviceHandler handler)
        : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerManager = playerManager;
        _IntifaceHandler = handler;

        Mediator.Subscribe<PatternActivedMessage>(this, (msg) =>
        {
            ActivePattern = _clientConfigs.GetPatternFromIndex(msg.PatternIndex);
        });

        Mediator.Subscribe<PatternDeactivedMessage>(this, (msg) =>
        {
            ActivePattern = null!;
        });

        // probably dont need this because c# magic?
        Mediator.Subscribe<PatternDataChanged>(this, (msg) =>
        {
            // update the list of names
            PatternNames = _clientConfigs.GetPatternNames();
            if (ActivePattern != null)
            {
                ActivePattern = _clientConfigs.GetPatternFromIndex(msg.PatternIndex);
            }
        });
    }

    // public accessor values
    public PatternData ActivePattern { get; private set; } = null!;
    public List<string> PatternNames { get; private set; } = null!;

    /// <summary>
    /// Check if the index is in bounds of the pattern list
    /// </summary>
    /// <param name="idx"> The index to check </param>
    /// <returns> True if the index is in bounds, false otherwise. </returns>
    public bool IsIndexInBounds(int idx) => _clientConfigs.IsIndexInBounds(idx);

    /// <summary>
    /// Check if any pattern is currently playing
    /// </summary>
    /// <returns> True if any pattern is playing, false otherwise. </returns>
    public bool IsAnyPatternPlaying() => _clientConfigs.IsAnyPatternPlaying();

    /// <summary>
    /// Get the index of the currently active pattern
    /// </summary>
    /// <returns> The index of the currently active pattern. </returns>
    public int GetActivePatternIdx() => _clientConfigs.ActivePatternIdx();

    /// <summary>
    /// Ensure the name of a pattern is unique among the other patterns
    /// </summary>
    /// <param name="baseName"> The base name of the pattern </param>
    /// <returns> The unique name of the pattern </returns>
    public string EnsureUniqueName(string baseName) => _clientConfigs.EnsureUniqueName(baseName);

    /// <summary>
    /// Add a new pattern to the client configuration
    /// </summary>
    /// <param name="newPattern"> The new pattern to add </param>
    public void AddPattern(PatternData newPattern) => _clientConfigs.AddNewPattern(newPattern);

    /// <summary>
    /// Remove a pattern from the client configuration
    /// </summary>
    /// <param name="indexToRemove"> The index of the pattern to remove </param>
    public void RemovePattern(int indexToRemove) => _clientConfigs.RemovePattern(indexToRemove);

    /// <summary>
    /// Get the index of a pattern by its name
    /// </summary>
    /// <param name="name"> The name of the pattern to get the index of </param>
    /// <returns> The index of the pattern </returns>
    public int GetPatternIdxByName(string name) => _clientConfigs.GetPatternIdxByName(name);


    /// <summary>
    /// Get the pattern located at the spesified index
    /// </summary>
    /// <param name="index"> The index of the pattern to get </param>
    /// <returns> The pattern located at the index </returns>
    public PatternData GetPatternFromIndex(int index) => _clientConfigs.GetPatternFromIndex(index);

    /// <summary>
    /// Gets the list of strings reflecting the names of the patterns.
    /// </summary>
    /// <returns></returns>
    public void InitalizePatternNames() => PatternNames = _clientConfigs.GetPatternNames();

    public string GetPatternName(int idx) => _clientConfigs.GetNameForPattern(idx);
    public string GetPatternDescription(int idx) => _clientConfigs.GetDescriptionForPattern(idx);
    public string GetPatternAuthor(int idx) => _clientConfigs.GetAuthorForPattern(idx);
    public List<string> GetTagsForPattern(int idx) => _clientConfigs.GetTagsForPattern(idx);
    public string GetDurationForPattern(int idx) => _clientConfigs.GetDurationForPattern(idx);
    public bool GetIfPatternLoops(int idx) => _clientConfigs.PatternLoops(idx);
    public bool GetUserIsAllowedToView(int idx, string userID) => _clientConfigs.GetUserIsAllowedToView(idx, userID);

    /// <summary>
    /// Rename a pattern
    /// </summary>
    /// <param name="idx"> The index of the pattern to rename </param>
    /// <param name="name"> The new name of the pattern </param>
    public void RenamePattern(int idx, string name) => _clientConfigs.SetNameForPattern(idx, name);

    /// <summary>
    /// Change the description of a pattern
    /// </summary>
    /// <param name="idx"> The index of the pattern to change the description of </param>
    /// <param name="newDesc"> The new description of the pattern </param>
    public void ChangeDescription(int idx, string newDesc) => _clientConfigs.ModifyDescription(idx, newDesc);

    public void ChangeAuthor(int idx, string newAuthor) => _clientConfigs.SetAuthorForPattern(idx, newAuthor);

    public void AddTagForPattern(int idx, string tag) => _clientConfigs.AddTagToPattern(idx, tag);

    public void RemoveTagForPattern(int idx, string tag) => _clientConfigs.RemoveTagFromPattern(idx, tag);

    public void SetPatternLoop(int idx, bool shouldLoop) => _clientConfigs.SetPatternLoops(idx, shouldLoop);

    public void GrantUserAccessToPattern(int idx, string user) => _clientConfigs.AddTrustedUserToPattern(idx, user);

    public void RevokeUserAccessToPattern(int idx, string user) => _clientConfigs.RemoveTrustedUserFromPattern(idx, user);
}

