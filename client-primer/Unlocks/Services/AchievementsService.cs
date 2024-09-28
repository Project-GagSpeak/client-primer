using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;

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

    private unsafe void CheckAchievementConditions()
    {
        // check if in gold saucer (maybe do something better for this later.
        if (_frameworkUtils.ClientState.TerritoryType is 144)
        {
            // Check Chocobo Racing Achievement.
            if (_frameworkUtils.Condition[ConditionFlag.ChocoboRacing])
            {
                var resultMenu = (AtkUnitBase*)GenericHelpers.GetAddonByName("RaceChocoboResult");
                if (resultMenu != null)
                {
                    if (resultMenu->RootNode->IsVisible())
                    {
                        // invoke the thing.
                        Logger.LogInformation("Would be invoking the achievement Check now");
                    }
                }
            }
        }
    }
}
