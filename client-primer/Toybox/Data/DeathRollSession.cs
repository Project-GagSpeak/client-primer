using Dalamud.Utility;

namespace GagSpeak.Toybox.Data;

public enum LatestRoller { Initializer, Opponent }

// Trigger Controller helps manage the currently active triggers and listens in on the received action effects
public class DeathRollSession
{
    public LatestRoller LastRoller = LatestRoller.Initializer;
    public string Initializer { get; init; } // The player who initiated the death roll
    public string Opponent { get; private set; } = string.Empty; // The player who is the opponent in the death 
    public int CurrentRollCap { get; private set; } = 999; // The current highest roll
    public bool IsComplete { get; private set; } = false;
    public DateTime LastRoll { get; private set; } = DateTime.UtcNow; // The time of the last roll

    public DeathRollSession(string initializer, int initialRoll)
    {
        Initializer = initializer;
        CurrentRollCap = initialRoll;
        LastRoller = LatestRoller.Initializer;
        IsComplete = false;
        LastRoll = DateTime.UtcNow;
    }

    public bool SessionExpired => DateTime.UtcNow - LastRoll > TimeSpan.FromMinutes(2);

    public bool TryNextRoll(string playerNameWithWorld, int rolledValue, int maxRollValue)
    {
        if (IsComplete) { StaticLogger.Logger.LogDebug("Game is already complete, cannot roll again."); return false; }

        // if the maxRollValue != currentRollCap, return.
        if (maxRollValue != CurrentRollCap)
        {
            StaticLogger.Logger.LogWarning("Player {player} is trying to roll, but the maxRollValue does not match the currentRollCap.", playerNameWithWorld);
            return false;
        }

        // dont allow the person to roll again if they are the last roller.
        if ((LastRoller == LatestRoller.Initializer && playerNameWithWorld == Initializer) || (LastRoller == LatestRoller.Opponent && playerNameWithWorld == Opponent))
        {
            StaticLogger.Logger.LogWarning("Player {player} is trying to roll again, but they are the last roller.", playerNameWithWorld);
            return false;
        }

        // By making it here, we matched the roll cap, opponent is not set, and they are not the initializer, so set the opponent.
        if (Opponent.IsNullOrEmpty() && playerNameWithWorld != Initializer)
        {
            Opponent = playerNameWithWorld;
            LastRoll = DateTime.UtcNow;
            LastRoller = LatestRoller.Opponent;
            CurrentRollCap = rolledValue;

            if (rolledValue is 1)
                EndGame();

            return true;
        }
        else if (LastRoller == LatestRoller.Initializer && playerNameWithWorld == Opponent)
        {
            // let the opponents roll be processed.
            LastRoller = LatestRoller.Opponent;
            LastRoll = DateTime.UtcNow;
            CurrentRollCap = rolledValue;

            if (rolledValue is 1)
                EndGame();
            return true;
        }
        else if (LastRoller == LatestRoller.Opponent && playerNameWithWorld == Initializer)
        {
            // let the initializers roll be processed.
            LastRoll = DateTime.UtcNow;
            LastRoller = LatestRoller.Initializer;
            CurrentRollCap = rolledValue;

            if (rolledValue is 1)
                EndGame();
            return true;
        }
        return false;
    }

    private void EndGame()
    {
        IsComplete = true;
        string loser = LastRoller == LatestRoller.Initializer ? Initializer : Opponent;
        StaticLogger.Logger.LogTrace("DeathRoll Finished, with the loser {loser} rolling a 1, ending the game.", loser);
    }
}
