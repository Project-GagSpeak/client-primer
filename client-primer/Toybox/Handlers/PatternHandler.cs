using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using System.Reflection.Metadata;
using static PInvoke.User32;

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

        Mediator.Subscribe<PatternActivedMessage>(this, (msg) => PlaybackRunning = true);

        Mediator.Subscribe<PatternDeactivedMessage>(this, (msg) =>
        {
            ActivePattern = null!;
            PlaybackRunning = false;
        });

        Mediator.Subscribe<PatternRemovedMessage>(this, (msg) => 
        {
            // update thje selected patterns to the new first index if the count is > 0
            if (PatternListSize() > 0)
            {
                SetSelectedPattern(FetchPattern(0), 0);
            }
            else
            {
                ClearSelectedPattern();
            }
        });

    }

    private PatternData? _selectedPattern;
    public int SelectedPatternIdx { get; private set; } = -1;
    public PatternData SelectedPattern
    {
        get
        {
            if (_selectedPattern == null && SelectedPatternIdx >= 0)
            {
                _selectedPattern = _clientConfigs.FetchPattern(SelectedPatternIdx);
            }
            return _selectedPattern!;
        }
        private set => _selectedPattern = value;
    }
    public bool SelectedPatternNull => SelectedPattern == null;

    // public accessor values (We store and update these so that we do not fetch them every draw loop.
    public PatternData ActivePattern { get; private set; } = null!;
    public List<string> PatternNames => _clientConfigs.GetPatternNames();
    public bool PlaybackRunning { get; private set; } = false;

    public void SetSelectedPattern(PatternData pattern, int index)
    {
        SelectedPattern = pattern;
        SelectedPatternIdx = index;
    }

    public void ClearSelectedPattern()
    {
        SelectedPatternIdx = -1;
        SelectedPattern = null!;
    }

    public TimeSpan GetPatternLength(string name)
    {
        var idx = _clientConfigs.GetPatternIdxByName(name);
        if (idx == -1)
        {
            return TimeSpan.Zero;
        }
        return _clientConfigs.GetPatternLength(idx);
    }

    // TODO make it so the pattern also takes in an extra parameter for the max duration to play.
    public void PlayPattern(int idx)
    {
        ActivePattern = _clientConfigs.FetchPattern(idx);
        _clientConfigs.SetPatternState(idx, true);
    }

    public void StopPattern(int idx)
    {
        _clientConfigs.SetPatternState(idx, false);
    }

    /// <summary>
    /// Grabs the number of patterns stored.
    /// </summary>
    /// <returns> The number of patterns stored. </returns>
    public int PatternListSize() => _clientConfigs.GetPatternCount();

    /// <summary>
    /// Get the pattern located at the spesified index
    /// </summary>
    /// <param name="index"> The index of the pattern to get </param>
    /// <returns> The pattern located at the index </returns>
    public PatternData FetchPattern(int index) => _clientConfigs.FetchPattern(index);

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

    /// <summary>
    /// Change the author of a pattern
    /// </summary>
    /// <param name="idx">The index of the pattern to change the author of</param>
    /// <param name="newAuthor">The new author of the pattern</param>
    public void ChangeAuthor(int idx, string newAuthor) => _clientConfigs.SetAuthorForPattern(idx, newAuthor);

    /// <summary>
    /// Add a tag to a pattern
    /// </summary>
    /// <param name="idx">The index of the pattern to add the tag to</param>
    /// <param name="tag">The tag to add to the pattern</param>
    public void AddTagForPattern(int idx, string tag) => _clientConfigs.AddTagToPattern(idx, tag);

    /// <summary>
    /// Remove a tag from a pattern
    /// </summary>
    /// <param name="idx">The index of the pattern to remove the tag from</param>
    /// <param name="tag">The tag to remove from the pattern</param>
    public void RemoveTagForPattern(int idx, string tag) => _clientConfigs.RemoveTagFromPattern(idx, tag);

    /// <summary>
    /// Set whether a pattern should loop
    /// </summary>
    /// <param name="idx">The index of the pattern to set the loop status for</param>
    /// <param name="shouldLoop">Whether the pattern should loop</param>
    public void SetPatternLoop(int idx, bool shouldLoop) => _clientConfigs.SetPatternLoops(idx, shouldLoop);

    /// <summary>
    /// Grant a user access to a pattern
    /// </summary>
    /// <param name="idx">The index of the pattern to grant access to</param>
    /// <param name="user">The user to grant access to the pattern</param>
    public void GrantUserAccessToPattern(int idx, string user) => _clientConfigs.AddTrustedUserToPattern(idx, user);

    /// <summary>
    /// Revoke a user's access to a pattern
    /// </summary>
    /// <param name="idx">The index of the pattern to revoke access from</param>
    /// <param name="user">The user to revoke access from the pattern</param>
    public void RevokeUserAccessToPattern(int idx, string user) => _clientConfigs.RemoveTrustedUserFromPattern(idx, user);

}

