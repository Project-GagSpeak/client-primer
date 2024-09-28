using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Extensions;
using Microsoft.Extensions.Hosting;
using OtterGui;
using OtterGui.Classes;
using System.Numerics;
using System.Security.Cryptography;

namespace GagSpeak.Services;
public class CursedLootService : DisposableMediatorSubscriberBase, IHostedService
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly GagManager _gagManager;
    private readonly PlayerCharacterData _playerData;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly AppearanceChangeService _appearanceChange;
    private readonly IChatGui _chatGui;
    private readonly IDataManager _gameData;
    private readonly IObjectTable _objects;
    private readonly ITargetManager _targets;

    public CursedLootService(ILogger<CursedLootService> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, GagManager gagManager,
        PlayerCharacterData playerData, OnFrameworkService frameworkUtils,
        AppearanceChangeService appearanceChange, IChatGui chatGui, IDataManager gameData, 
        IObjectTable objects, ITargetManager targets) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _gagManager = gagManager;
        _playerData = playerData;
        _frameworkUtils = frameworkUtils;
        _appearanceChange = appearanceChange;
        _chatGui = chatGui;
        _gameData = gameData;
        _objects = objects;
        _targets = targets;

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, _ => GetNearbyTreasure());
        Mediator.Subscribe<FrameworkUpdateMessage>(this, _ => CheckDungeonTreasureOpen());
    }

    // Store the last interacted chestId so we dont keep spam opening the same chest.
    private static ulong NearestTreasureId = ulong.MaxValue;

    private static ulong LastOpenedChestId = 0;
    private static DateTime LastInteraction = DateTime.MinValue;
    private CursedLootModel CursedLootStorage => _clientConfigs.WardrobeConfig.WardrobeStorage.CursedDungeonLoot;

    private unsafe void GetNearbyTreasure()
    {
        if (!_clientConfigs.GagspeakConfig.CursedDungeonLoot)
            return;

        if (_frameworkUtils._sentBetweenAreas || !_frameworkUtils.InDungeonOrDuty)
            return;

        if (DateTime.Now - LastInteraction < TimeSpan.FromSeconds(5))
            return;

        var player = _frameworkUtils.ClientState.LocalPlayer;
        if (player == null)
            return;
        // we are in a dungeon, so check if there is a nearby coffer.
        var treasureNearby = _objects.Where(o => o.IsTargetable && o.ObjectKind == ObjectKind.Treasure)
            .FirstOrDefault(o =>
            {
                // skip if the object is null
                if (o == null) return false;

                // skip if object is not in open-range to us.
                var dis = Vector3.Distance(player.Position, o.Position) - player.HitboxRadius - o.HitboxRadius;
                if (dis > 7f) return false;

                // If the treasure chest has already been opened, do not process a cursed loot function.
                foreach (var item in Loot.Instance()->Items)
                    if (item.ChestObjectId == o.GameObjectId)
                        return false;

                // Otherwise, we are in range, it's unopened, its a treasure chest, and its targetable.
                return true;
            });
        if (treasureNearby == null) return;
        if (NearestTreasureId == treasureNearby.GameObjectId) return;

        Logger.LogInformation("Found New Treasure Nearby!");
        NearestTreasureId = treasureNearby.GameObjectId;
    }



    // Detect for Cursed Dungeon Loot Interactions.
    private unsafe void CheckDungeonTreasureOpen()
    {
        if (!_clientConfigs.GagspeakConfig.CursedDungeonLoot)
            return;

        if (_frameworkUtils._sentBetweenAreas || !_frameworkUtils.InDungeonOrDuty)
            return;

        if (DateTime.Now - LastInteraction < TimeSpan.FromSeconds(10))
            return;

        var player = _frameworkUtils.ClientState.LocalPlayer;
        if (player == null)
            return;

        if (NearestTreasureId is ulong.MaxValue)
            return;

        // If the client players current target is the treasure and they have just right clicked, fire the cursed loot function.
        if ((_targets.SoftTarget?.GameObjectId == NearestTreasureId)
          || (_targets.MouseOverTarget?.GameObjectId == NearestTreasureId)
          || (_targets.Target?.GameObjectId == NearestTreasureId))
        {
            if (UiSharedService.RightMouseButtonDown() || UiSharedService.Numpad0Pressed())
            {
                Logger.LogDebug("You Opened a Cursed Dungeon Chest, Checking for Cursed Loot!", LoggerType.Restraints);
                // clear the last interacted treasure, reset to max val.
                LastOpenedChestId = NearestTreasureId;
                NearestTreasureId = ulong.MaxValue;
                LastInteraction = DateTime.Now;
                ApplyCursedLoot();
            }
        }
    }

    /// <summary>
    /// Fired whenever we open a chest in a dungeon.
    /// </summary>
    private async void ApplyCursedLoot()
    {
        // get the percent change to apply
        var percentChange = CursedLootStorage.LockChance;

        Random random = new Random();
        // Generates a number from 0 to 100
        int randomValue = random.Next(0, 101);
        // if we missed the chance to apply the cursed loot, return.
        if (randomValue > percentChange) return;

        // Otherwise, we can apply it. So Fetch the list of our cursed sets.
        var cursedSets = CursedLootStorage.CursedItems;

        // Select, at random, an index from the list of cursed sets.
        var randomIndex = random.Next(0, cursedSets.Count);
        int cursedSetIdx = _clientConfigs.GetSetIdxByGuid(cursedSets[randomIndex].RestraintGuid);
        if (cursedSetIdx == -1)
        {
            Logger.LogWarning("The Set that was attempted to be applied was not found in the wardrobe!");
            return;
        }

        // Notify them in chat they found Cursed Bondage Loot.
        _chatGui.PrintError(new SeStringBuilder().AddItalics("As the coffer opens, cursed loot spills " +
            "forth, binding you tightly in an inescapable snare of restraints!").BuiltString);

        // Enable the set for the player. Await for the Application to Occur.
        await _clientConfigs.SetRestraintSetState(cursedSetIdx, Globals.SelfApplied, NewState.Enabled, true);

        // After it has occured, log the event.
        Logger.LogInformation($"Cursed Loot Applied!");

        // Generate a random string that is 40 characters long and can contain only letters.
        var randomString = GenerateRandomString(40);

        // get the random timespan to lock the set for.
        var lockTime = GetRandomTimeSpan(CursedLootStorage.LockRangeLower, CursedLootStorage.LockRangeUpper, random);

        // get the datetimeOffset from DateTime.UtcNow
        var lockUntil = DateTimeOffset.UtcNow.Add(lockTime);

        // Construct a password timer padlock and lock the active restraint set with it.
        _clientConfigs.LockRestraintSet(
            cursedSetIdx,
            Padlocks.TimerPasswordPadlock.ToName(),
            randomString,
            lockUntil,
            Globals.SelfApplied
            );

        // check if the cursed items gag item is not GagType.None
        if (cursedSets[randomIndex].AttachedGag is not GagType.None)
        {
            _chatGui.PrintError(new SeStringBuilder().AddItalics("Before you can even attempt to escape, the coffer spits out a gag, "+
                "it's buckle wrapping around your head, fastening it firmly in place!").BuiltString);

            // find the first available gag slot currently at GagType.None. if none are found, do not execute.
            var availableLayer = _playerData.AppearanceData!.GagSlots.IndexOf(g => g.GagType.ToGagType() == GagType.None);
            if (availableLayer == -1) return;

            // Apply the gag to that slot.
            Logger.LogDebug($"Cursed Gag Equipped!", LoggerType.GagManagement);
            await _appearanceChange.UpdateGagsAppearance((GagLayer)availableLayer, cursedSets[randomIndex].AttachedGag, NewState.Enabled);
            _gagManager.OnGagTypeChanged((GagLayer)availableLayer, cursedSets[randomIndex].AttachedGag, true);

            // now lock it.
            var padlockData = new PadlockData(
                (GagLayer)availableLayer,
                Padlocks.TimerPasswordPadlock,
                randomString,
                lockUntil,
                Globals.SelfApplied);
            _gagManager.OnGagLockChanged(padlockData, NewState.Locked, false);
        }
    }
    public static TimeSpan GetRandomTimeSpan(TimeSpan min, TimeSpan max, Random random)
    {
        // if the min is greater than the max, make the timespan 1 second and return.
        if (min > max) return TimeSpan.FromSeconds(5);

        double minSeconds = min.TotalSeconds;
        double maxSeconds = max.TotalSeconds;
        double randomSeconds = random.NextDouble() * (maxSeconds - minSeconds) + minSeconds;
        return TimeSpan.FromSeconds(randomSeconds);
    }

    public static string GenerateRandomString(int length, string? allowableChars = null)
    {
        if (string.IsNullOrEmpty(allowableChars))
            allowableChars = @"ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";

        // Generate random data
        var rnd = RandomNumberGenerator.GetBytes(length);

        // Generate the output string
        var allowable = allowableChars.ToCharArray();
        var l = allowable.Length;
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = allowable[rnd[i] % l];

        return new string(chars);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Cursed Dungeon Loot Service Started!");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Cursed Dungeon Loot Service Stopped!");
        return Task.CompletedTask;
    }
}
