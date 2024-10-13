using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Extensions;
using Microsoft.Extensions.Hosting;
using OtterGui;
using System.Numerics;
using System.Security.Cryptography;

namespace GagSpeak.Services;
public class CursedLootService : DisposableMediatorSubscriberBase, IHostedService
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly GagManager _gagManager;
    private readonly PlayerCharacterData _playerData;
    private readonly CursedLootHandler _handler;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly AppearanceService _appearanceChange;
    private readonly IChatGui _chatGui;
    private readonly IDataManager _gameData;
    private readonly IObjectTable _objects;
    private readonly ITargetManager _targets;

    public CursedLootService(ILogger<CursedLootService> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, GagManager gagManager,
        PlayerCharacterData playerData, CursedLootHandler handler,
        OnFrameworkService frameworkUtils, AppearanceService appearanceChange,
        IChatGui chatGui, IDataManager gameData, IObjectTable objects,
        ITargetManager targets) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _gagManager = gagManager;
        _playerData = playerData;
        _handler = handler;
        _frameworkUtils = frameworkUtils;
        _appearanceChange = appearanceChange;
        _chatGui = chatGui;
        _gameData = gameData;
        _objects = objects;
        _targets = targets;

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, _ => GetNearbyTreasure());
        Mediator.Subscribe<FrameworkUpdateMessage>(this, _ => CheckDungeonTreasureOpen());
    }

    private Task? _openTreasureTask;

    // Store the last interacted chestId so we dont keep spam opening the same chest.
    private static ulong NearestTreasureId = ulong.MaxValue;
    private static ulong LastOpenedChestId = 0;
    private static DateTime LastInteraction = DateTime.MinValue;
    private bool AttemptedOpen = false;
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
        if (LastOpenedChestId == treasureNearby.GameObjectId) return;

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
                if (_openTreasureTask != null && !_openTreasureTask.IsCompleted)
                    return;

                Logger.LogTrace("Attempting to open coffer, checking loot instance on next framework tick");
                _openTreasureTask = CheckLootTables(NearestTreasureId);
                return;
            }
        }
    }

    private async Task CheckLootTables(ulong objectId)
    {
        try
        {
            Logger.LogInformation("Checking tables in the next 500ms!");
            await Task.Delay(1000);
            Logger.LogInformation("Checking tables!");
            await _frameworkUtils.RunOnFrameworkThread(() =>
            {
                unsafe
                {
                    if (Loot.Instance()->Items.ToArray().Any(x => x.ChestObjectId == objectId) && objectId != LastOpenedChestId)
                    {
                        Logger.LogTrace("One of the loot items is the nearest treasure and we just previously attempted to open one.");
                        LastOpenedChestId = NearestTreasureId;
                        NearestTreasureId = ulong.MaxValue;
                        LastInteraction = DateTime.Now;
                        ApplyCursedLoot();
                    }
                    else
                    {
                        Logger.LogTrace("No loot items are the nearest treasure, or we have already opened this chest.");
                    }
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            _openTreasureTask = null;
        }
    }

    /// <summary>
    /// Fired whenever we open a chest in a dungeon.
    /// </summary>
    private async void ApplyCursedLoot()
    {
        // throw warning and return if our size is already capped at 6.
        if (_handler.ActiveItems.Count >= 6)
        {
            Logger.LogWarning("Cannot apply Cursed Loot, as the player already has 6 active cursed items.");
            return;
        }

        // get the percent change to apply
        var percentChange = _handler.LockChance;

        // Calculate if we should apply it or not. If we fail to roll a success, return.
        Random random = new Random();
        int randomValue = random.Next(0, 101);
        if (randomValue > percentChange) return;

        // send event that we are having cursed loot applied.
        UnlocksEventManager.AchievementEvent(UnlocksEvent.CursedDungeonLootFound);

        // Obtain a randomly selected cursed item from the inactive items in the pool.
        var enabledPoolCount = _handler.InactiveItemsInPool.Count;
        Logger.LogDebug("Randomly selecting an index between 0 and " + enabledPoolCount + " for cursed loot.");

        Guid selectedLootId = Guid.Empty;
        var randomIndex = random.Next(0, enabledPoolCount);
        Logger.LogDebug("Selected Index: " + randomIndex + " ("+ _handler.InactiveItemsInPool[randomIndex].Name+")");
        if (_handler.InactiveItemsInPool[randomIndex].IsGag)
        {
            var availableSlot = _playerData.AppearanceData!.GagSlots.IndexOf(x => x.GagType.ToGagType() == GagType.None);
            // If no slot is available, make a new list that is without any items marked as IsGag, and roll again.
            if (availableSlot is not -1)
            {
                Logger.LogDebug("A Gag Slot is available to apply and lock. Doing so now!");
                selectedLootId = _handler.InactiveItemsInPool[randomIndex].LootId;
                // Notify the client of their impending fate~
                _chatGui.PrintError(new SeStringBuilder().AddItalics("As the coffer opens, cursed loot spills " +
                    "forth, silencing your mouth with a Gag now strapped on tight!").BuiltString);
                // generate the length they will be locked for:
                var lockTimeGag = GetRandomTimeSpan(_handler.LowerLockLimit, _handler.UpperLockLimit, random);
                // apply the gag via the gag manager at the available slot we found.
                _gagManager.OnGagTypeChanged((GagLayer)availableSlot, _playerData.AppearanceData!.GagSlots[availableSlot].GagType.ToGagType(), true, true);
                // lock the gag.
                var padlockData = new PadlockData()
                {
                    Layer = (GagLayer)availableSlot,
                    PadlockType = Padlocks.MimicPadlock, 
                    Timer = DateTimeOffset.UtcNow.Add(lockTimeGag), 
                    Assigner = Globals.SelfApplied
                };
                _gagManager.OnGagLockChanged(padlockData, NewState.Enabled, true, true);
                // Activate the cursed loot item.
                _handler.ActivateCursedItem(selectedLootId, DateTimeOffset.UtcNow.Add(lockTimeGag));
                Logger.LogInformation($"Cursed Loot Applied!");
                return;
            }
            else
            {
                Logger.LogWarning("No Gag Slots Available, Rolling Again.");
                var inactiveSetsWithoutGags = _handler.ItemsNotInPool.Where(x => !x.IsGag).ToList();
                var randomIndexNoGag = random.Next(0, inactiveSetsWithoutGags.Count);
                Logger.LogDebug("Selected Index: " + randomIndexNoGag + " (" + inactiveSetsWithoutGags[randomIndexNoGag].Name + ")");
                selectedLootId = inactiveSetsWithoutGags[randomIndexNoGag].LootId;
                // Notify the client of their impending fate~
                _chatGui.PrintError(new SeStringBuilder().AddItalics("As the coffer opens, cursed loot spills " +
                    "forth, binding you tightly in an inescapable snare of restraints!").BuiltString);
                // generate the length they will be locked for:
                var lockTime = GetRandomTimeSpan(_handler.LowerLockLimit, _handler.UpperLockLimit, random);
                // Activate the cursed loot item.
                _handler.ActivateCursedItem(selectedLootId, DateTimeOffset.UtcNow.Add(lockTime));
                Logger.LogInformation($"Cursed Loot Applied!");
                return;
            }
        }
        else
        {
            selectedLootId = _handler.InactiveItemsInPool[randomIndex].LootId;
            // Notify the client of their impending fate~
            _chatGui.PrintError(new SeStringBuilder().AddItalics("As the coffer opens, cursed loot spills " +
                "forth, binding you tightly in an inescapable snare of restraints!").BuiltString);
            // generate the length they will be locked for:
            var lockTime = GetRandomTimeSpan(_handler.LowerLockLimit, _handler.UpperLockLimit, random);
            // Activate the cursed loot item.
            _handler.ActivateCursedItem(selectedLootId, DateTimeOffset.UtcNow.Add(lockTime));
            Logger.LogInformation($"Cursed Loot Applied!");
            return;
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

    // Encase we ever want to implement the Gag Stuff i guess.
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
