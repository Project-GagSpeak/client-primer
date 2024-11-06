using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Toybox.Data;
using GagSpeak.Toybox.Services;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using System.Text.RegularExpressions;
namespace GagSpeak.Toybox.Controllers;

/// <summary>
/// Service interacts with and manages all active DeathRoll Sessions.
/// </summary>
public sealed class DeathRollService
{
    private readonly ILogger<DeathRollService> _logger;
    private readonly PlayerCharacterData _playerManager;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly OnFrameworkService _frameworkService;
    private readonly TriggerService _triggerService;
    private readonly IChatGui _chatGui;

    public DeathRollService(ILogger<DeathRollService> logger, PlayerCharacterData playerManager,
        ClientConfigurationManager clientConfigs, TriggerService triggerService, 
        OnFrameworkService frameworkService, IChatGui chatGui)
    {
        _logger = logger;
        _playerManager = playerManager;
        _clientConfigs = clientConfigs;
        _frameworkService = frameworkService;
        _triggerService = triggerService;
        _chatGui = chatGui;
    }

    private Dictionary<string, DeathRollSession> MonitoredSessions = new();

    // add a helper function to retrieve the roll cap of the last active session our player is in.
    public int? GetLastRollCap()
    {
        var session = MonitoredSessions.Values.FirstOrDefault(s => s.Opponent == _frameworkService.ClientPlayerNameAndWorld || s.Initializer == _frameworkService.ClientPlayerNameAndWorld);
        return session?.CurrentRollCap ?? null;
    }




    public void ProcessMessage(XivChatType type, string nameWithWorld, SeString message)
    {
        if (_frameworkService.ClientPlayerAddress != nint.Zero || !message.Payloads.Exists(p => p.Type == PayloadType.Icon))
            return;

        var (rollValue, rollCap) = ParseMessage(message.TextValue);

        // if the roll value and cap are 0, its an invalid, so return.
        if (rollValue is 0 && rollCap is 0)
            return;


        if (rollValue is 0)
        {
            _logger.LogDebug($"[DeathRoll] New session started by {nameWithWorld} with cap {rollCap}", LoggerType.ToyboxTriggers);
            StartNewSession(nameWithWorld, rollCap);
        }
        else
        {
            _logger.LogDebug($"[DeathRoll] Continuing session for {nameWithWorld} with roll {rollValue} and cap {rollCap}", LoggerType.ToyboxTriggers);
            ContinueSession(nameWithWorld, rollValue, rollCap);
        }
    }

    private void StartNewSession(string initializer, int initialRollCap)
    {
        // Remove any existing sessions involving this player
        RemovePlayerSessions(initializer);

        // Create and add new session
        var session = new DeathRollSession(initializer, initialRollCap, OnSessionComplete);
        MonitoredSessions[initializer] = session;
        _logger.LogDebug($"[DeathRoll] New session started by {initializer} with cap {initialRollCap}");
    }

    private void ContinueSession(string playerName, int rollValue, int rollCap)
    {
        // Find a matching active session where the cap matches
        var session = MonitoredSessions.Values.FirstOrDefault(s => s.CurrentRollCap == rollCap && !s.IsComplete);

        if (session is null)
        {
            _logger.LogDebug("[DeathRoll] No active session found to match roll.", LoggerType.ToyboxTriggers);
            return;
        }

        // if the opponent is not yet set, we are joining the session with a reply,
        // and should clear all other instances with our name.
        if (session.Opponent == string.Empty)
        {
            _logger.LogDebug($"[DeathRoll] {playerName} joined session with {session.Initializer}.", LoggerType.ToyboxTriggers);
            RemovePlayerSessions(playerName);
        }

        if (session.TryProcessRoll(playerName, rollValue))
        {
            _logger.LogDebug($"[DeathRoll] {playerName} rolled {rollValue} in session.", LoggerType.ToyboxTriggers);
        }
        else
        {
            _logger.LogDebug("[DeathRoll] Invalid roll attempt by " + playerName);
        }
    }

    /// <summary>
    /// Removes all sessions that the playerName is either an opponent or initializer of.
    /// </summary>
    private void RemovePlayerSessions(string playerName)
    {
        foreach (var session in MonitoredSessions.Values.Where(k => k.Initializer == playerName || k.Opponent == playerName).ToList())
        {
            _logger.LogDebug($"[DeathRoll] Removing session involving {playerName} due to them joining / creating another!", LoggerType.ToyboxTriggers);
            MonitoredSessions.Remove(session.Initializer);
        }
    }

    /// <summary>
    /// Triggered by a DeathRoll sessions action upon completion.
    /// </summary>
    private void OnSessionComplete(DeathRollSession session)
    {
        SeStringBuilder se = new SeStringBuilder().AddText("[Gagspeak] ").AddUiForeground("DeathRoll ended. Loser: " + session.LastRoller, 31).AddUiForegroundOff();
        _chatGui.Print(se.BuiltString);
        _logger.LogInformation("[DeathRoll] Session completed and removed.");
        // if we were the loser, then fire the deathroll trigger.
        if (session.LastRoller == _frameworkService.ClientPlayerNameAndWorld)
        {
            foreach (var trigger in _clientConfigs.ActiveSocialTriggers)
                _triggerService.ExecuteTriggerAction(trigger);
        }
        MonitoredSessions.Remove(session.Initializer);
    }

    /// <summary>
    /// Parses the message string for the rolled value and cap value in a DeathRoll.
    /// Roll Value is the lower of two numbers found; Roll Cap is the higher.
    /// If only one number is found, it is assumed to be the Roll Cap.
    /// </summary>
    /// <returns>A tuple containing the Roll Value (-1 if not found) and Roll Cap (-1 if not found).</returns>
    private (int rollValue, int rollCap) ParseMessage(string message)
    {
        var regex = new Regex(@"\b(\d+)\b.*?\b(\d+)?\b");
        var match = regex.Match(message);

        if (!match.Success)
            return (-1, -1);

        int firstNumber = int.Parse(match.Groups[1].Value);
        int secondNumber = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : -1;

        // If only one number is found, treat it as the roll cap
        if (secondNumber is 0)
            return (-1, firstNumber);

        // Otherwise, return the minimum as rollValue and maximum as rollCap
        return (Math.Min(firstNumber, secondNumber), Math.Max(firstNumber, secondNumber));
    }
}
