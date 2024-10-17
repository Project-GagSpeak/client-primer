using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using System.Numerics;

namespace GagSpeak.Achievements.Services;

// possibly remove this if we dont end up having lots if need for a framework monitor update.
// if its so small, just add it to framework updates.
public class AchievementsService : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly OnFrameworkService _frameworkUtils;

    public AchievementsService(ILogger<AchievementsService> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, OnFrameworkService frameworkUtils) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _frameworkUtils = frameworkUtils;

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, _ => CheckAchievementConditions());
    }

    DateTime _lastCheck = DateTime.Now;
    DateTime _lastPlayerCheck = DateTime.Now;

    private unsafe void CheckAchievementConditions()
    {
        // only process this every 5 seconds.
        if ((DateTime.Now - _lastCheck).TotalSeconds < 5)
            return;

        // check if our client is dead, but dont use IsDead, because it's unreliable.
        if (_frameworkUtils.ClientState.LocalPlayer is null)
            return;

        if(_frameworkUtils.ClientState.LocalPlayer.CurrentHp is 0)
            UnlocksEventManager.AchievementEvent(UnlocksEvent.ClientSlain);

        // check if in gold saucer (maybe do something better for this later.
        if (_frameworkUtils.ClientState.TerritoryType is 144)
        {
            // Check Chocobo Racing Achievement.
            if (_frameworkUtils.Condition[ConditionFlag.ChocoboRacing])
            {
                var resultMenu = (AtkUnitBase*)AtkFuckery.GetAddonByName("RaceChocoboResult");
                if (resultMenu != null)
                {
                    if (resultMenu->RootNode->IsVisible())
                        UnlocksEventManager.AchievementEvent(UnlocksEvent.ChocoboRaceFinished);
                }
            }
        }

        // if 15 seconds has passed since the last player check, check the player.
        if ((DateTime.Now - _lastPlayerCheck).TotalSeconds < 15)
            return;

        // update player count
        _lastPlayerCheck = DateTime.Now;

        // we should get the current player object count that is within the range required for crowd pleaser.
        var playersInRange = _frameworkUtils.GetObjectTablePlayers()
            .Where(player => player != _frameworkUtils.ClientState.LocalPlayer
            && Vector3.Distance(_frameworkUtils.ClientState.LocalPlayer.Position, player.Position) < 30f)
            .ToList();
        UnlocksEventManager.AchievementEvent(UnlocksEvent.PlayersInProximity, playersInRange);
    }
}
