using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagSpeak.WebAPI.Utils;
using Microsoft.Extensions.Hosting;

namespace GagSpeak.UpdateMonitoring;

/// <summary>
/// The service responsible for handling framework updates and other Dalamud related services.
/// </summary>
public class OnFrameworkService : IHostedService, IMediatorSubscriber
{
    private readonly ILogger<OnFrameworkService> _logger;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly IDataManager _gameData;
    private readonly IFramework _framework;
    private readonly IGameGui _gameGui;
    private readonly IObjectTable _objectTable;
    private readonly IPartyList _partyList;
    private readonly ITargetManager _targetManager;
    private ushort _lastZone = 0;
    public bool _sentBetweenAreas = false;
    public bool _hasDied = false;
    public uint PlayerClassJobId = 0;
    public bool IsLoggedIn { get; private set; }
    public short LastCommendationsCount { get; private set; } = 0;

    private DateTime _delayedFrameworkUpdateCheck = DateTime.Now; // for letting us know if we are in a delayed framework check


    // The list of player characters, to associate their hashes with the player character name and addresses. Useful for indicating if they are visible or not.
    private readonly Dictionary<string, (string Name, nint Address)> _playerCharas;
    private readonly List<string> _notUpdatedCharas = [];
    public Lazy<Dictionary<ushort, string>> WorldData { get; private set; }

    public ulong TargetObjectId => _targetManager.Target?.GameObjectId ?? ulong.MaxValue;
    public bool IsInCutscene { get; private set; } = false;
    public bool IsZoning => _condition[ConditionFlag.BetweenAreas] || _condition[ConditionFlag.BetweenAreas51];
    public ActionRoles PlayerJobRole => (ActionRoles)(_clientState.LocalPlayer?.ClassJob?.GameData?.Role ?? 0);
    public IntPtr ClientPlayerAddress; // player address
    public static bool GlamourChangeEventsDisabled = false; // 1st variable responsible for handling glamour change events
    public byte PlayerLevel => _clientState.LocalPlayer?.Level ?? byte.MaxValue;
    public bool InPvP => _clientState.IsPvP;
    public bool InDungeonOrDuty => _condition[ConditionFlag.BoundByDuty] || _condition[ConditionFlag.BoundByDuty56] || _condition[ConditionFlag.BoundByDuty95];
    public int PartyListSize => _partyList.Count;
    public IClientState ClientState => _clientState;
    public ICondition Condition => _condition;
    public bool IsInMainCity => _gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Aetheryte>()?
        .Any(x => x.IsAetheryte && x.Territory.Row == _clientState.TerritoryType && x.Territory.Value?.TerritoryIntendedUse == 0) ?? false;
    public string MainCityName => _gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Aetheryte>()?
        .FirstOrDefault(x => x.IsAetheryte && x.Territory.Row == _clientState.TerritoryType && x.Territory.Value?.TerritoryIntendedUse == 0)?.PlaceName.ToString() ?? "Unknown";



    // the mediator for Gagspeak's event services
    public GagspeakMediator Mediator { get; }

    public OnFrameworkService(ILogger<OnFrameworkService> logger, GagspeakMediator mediator,
        IClientState clientState, ICondition condition, IDataManager gameData, IFramework framework, 
        IGameGui gameGui, IObjectTable objectTable, IPartyList partyList, ITargetManager targetManager)
    {
        _logger = logger;
        _clientState = clientState;
        _condition = condition;
        _gameData = gameData;
        _framework = framework;
        _gameGui = gameGui;
        _objectTable = objectTable;
        _partyList = partyList;
        _targetManager = targetManager;
        Mediator = mediator;

        ClientPlayerAddress = GetPlayerPointerAsync().GetAwaiter().GetResult();

        _playerCharas = new(StringComparer.Ordinal);

        WorldData = new(() =>
        {
            return gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>(Dalamud.Game.ClientLanguage.English)!
                .Where(w => w.IsPublic && !w.Name.RawData.IsEmpty)
                .ToDictionary(w => (ushort)w.RowId, w => w.Name.ToString());
        });

        // stores added pairs character name and addresses when added.
        mediator.Subscribe<TargetPairMessage>(this, (msg) =>
        {
            if (clientState.IsPvP) return;
            var name = msg.Pair.PlayerName;
            if (string.IsNullOrEmpty(name)) return;
            var addr = _playerCharas.FirstOrDefault(f => string.Equals(f.Value.Name, name, StringComparison.Ordinal)).Value.Address;
            if (addr == nint.Zero) return;
            _ = RunOnFrameworkThread(() =>
            {
                targetManager.Target = CreateGameObject(addr);
            }).ConfigureAwait(false);
        });
    }

    public void OpenMapWithMapLink(MapLinkPayload mapLink) => _gameGui.OpenMapWithMapLink(mapLink);
    public string GetEmoteName(uint emoteId) => _gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Emote>()?.GetRow(emoteId)?.Name.AsReadOnly().ExtractText().Replace("\u00AD", "") ?? $"Emote#{emoteId}";
    public static unsafe short GetCurrentCommendationCount() => PlayerState.Instance()->PlayerCommendations;

    public DeepDungeonType? GetDeepDungeonType()
    {
        if (_gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>()?.GetRow(_clientState.TerritoryType) is { } territoryInfo)
        {
            return territoryInfo switch
            {
                { TerritoryIntendedUse: 31, ExVersion.Row: 0 or 1 } => DeepDungeonType.PalaceOfTheDead,
                { TerritoryIntendedUse: 31, ExVersion.Row: 2 } => DeepDungeonType.HeavenOnHigh,
                { TerritoryIntendedUse: 31, ExVersion.Row: 4 } => DeepDungeonType.EurekaOrthos,
                _ => null
            };
        }
        return null;
    }

    #region FrameworkMethods
    /// <summary> Ensures that we are running on the games framework thread. Throws exception if we are not. </summary>
    public void EnsureIsOnFramework()
    {
        if (!_framework.IsInFrameworkUpdateThread) throw new InvalidOperationException("Can only be run on Framework");
    }

    /// <summary> Create a game object based off its pointer address reference </summary>
    /// <param name="reference">The pointer address of the game object</param>
    /// <returns>ClientState.Objects.Types.GameObject, type of Gameobject</returns>
    public IGameObject? CreateGameObject(nint reference)
    {
        // ensure we are on the framework thread
        EnsureIsOnFramework();
        // then createObjectReference
        return _objectTable.CreateObjectReference(reference);
    }

    /// <summary> An asyncronous task that create a game object based on its pointer address.</summary>
    /// <param name="reference">The pointer address of the game object</param>
    /// <returns></returns>Task of Dalamud.Game.ClientState.Objects.Types.GameObject, type of Gameobject</returns>
    public async Task<IGameObject?> CreateGameObjectAsync(nint reference)
    {
        return await RunOnFrameworkThread(() => _objectTable.CreateObjectReference(reference)).ConfigureAwait(false);
    }

    public IGameObject? SearchObjectTableById(ulong id)
    {
        EnsureIsOnFramework();
        return _objectTable.SearchById(id);
    }

    public async Task<IGameObject?> SearchObjectTableByIdAsync(uint id)
    {
        return await RunOnFrameworkThread(() => _objectTable.SearchById(id)).ConfigureAwait(false);
    }


    /// <summary> Get the player character from the object table based on the pointer address</summary>
    public IPlayerCharacter? GetIPlayerCharacterFromObjectTable(IntPtr address)
    {
        EnsureIsOnFramework();
        return (IPlayerCharacter?)_objectTable.CreateObjectReference(address);
    }

    /// <summary> Get the player character from the object table based on the pointer address asynchronously</summary>
    public async Task<IPlayerCharacter?> GetIPlayerCharacterFromObjectTableAsync(IntPtr address)
    {
        return await RunOnFrameworkThread(() => (IPlayerCharacter?)_objectTable.CreateObjectReference(address)).ConfigureAwait(false);
    }

    public List<IPlayerCharacter> GetObjectTablePlayers()
    {
        EnsureIsOnFramework();
        return _objectTable.OfType<IPlayerCharacter>().ToList();
    }

    public async Task<List<IPlayerCharacter>> GetObjectTablePlayersAsync()
    {
        return await RunOnFrameworkThread(GetObjectTablePlayers).ConfigureAwait(false);
    }


    /// <summary> Get if the player is not null, and if FFXIVClientState determines the playercharacter is valid </summary>
    /// <returns>a boolean telling us if the player character is present or not</returns>
    public bool GetIsPlayerPresent()
    {
        EnsureIsOnFramework();
        return _clientState.LocalPlayer != null && _clientState.LocalPlayer.IsValid();
    }

    /// <summary> Get if the player is not null, and if FFXIVClientState determines the playercharacter is valid
    /// <para>This is done in Async as a task</para>
    /// </summary>
    /// <returns>a boolean telling us if the player character is present or not</returns>
    public async Task<bool> GetIsPlayerPresentAsync()
    {
        return await RunOnFrameworkThread(GetIsPlayerPresent).ConfigureAwait(false);
    }

    /// <summary> Gets the player name. </summary>
    /// <returns> The local player character's name. </returns>
    public string GetPlayerName()
    {
        EnsureIsOnFramework();
        return _clientState.LocalPlayer?.Name.ToString() ?? "--";
    }

    /// <summary> Gets the player name asynchronously </summary>
    /// <returns> The local player character's name. </returns>
    public async Task<string> GetPlayerNameAsync()
    {
        return await RunOnFrameworkThread(GetPlayerName).ConfigureAwait(false);
    }

    /// <summary> Gets the player name hashed </summary>
    /// <returns> The local player character's name hashed </returns>
    public async Task<string> GetPlayerNameHashedAsync()
    {
        return await RunOnFrameworkThread(() => (GetPlayerName(), (ushort)GetHomeWorldId()).GetHash256()).ConfigureAwait(false);
    }

    public ulong GetPlayerLocalContentId()
    {
        EnsureIsOnFramework();
        return _clientState.LocalContentId;
    }

    public async Task<ulong> GetPlayerLocalContentIdAsync()
    {
        return await RunOnFrameworkThread(GetPlayerLocalContentId).ConfigureAwait(false);
    }


    /// <summary> Gets the player characters pointer address</summary>
    /// <returns> The pointer address of the player character</returns>
    public nint GetPlayerPointer()
    {
        EnsureIsOnFramework();
        return _clientState.LocalPlayer?.Address ?? nint.Zero;
    }

    /// <summary> Gets the player characters pointer address in async</summary>
    /// <returns> The pointer address of the player character</returns>
    public async Task<nint> GetPlayerPointerAsync()
    {
        return await RunOnFrameworkThread(GetPlayerPointer).ConfigureAwait(false);
    }

    /// <summary> Gets the player characters homeworld ID</summary>
    /// <returns> a <c>uint</c> of your IPlayerCharacters homeworld ID</returns>
    public uint GetHomeWorldId()
    {
        EnsureIsOnFramework();
        return _clientState.LocalPlayer!.HomeWorld.Id;
    }

    /// <summary> Gets the player characters homeworld ID asynchronously</summary>
    /// <returns> a <c>uint</c> of Your IPlayerCharacters homeworld ID</returns>
    public async Task<uint> GetHomeWorldIdAsync()
    {
        return await RunOnFrameworkThread(GetHomeWorldId).ConfigureAwait(false);
    }

    /// <summary> Gets the player characters ID of the world they are currently in.</summary>
    /// <returns> a <c>uint</c> type for the ID of the current world.</returns>
    public uint GetWorldId()
    {
        EnsureIsOnFramework();
        return _clientState.LocalPlayer!.CurrentWorld.Id;
    }

    /// <summary> Gets the player characters ID of the world they are currently in asynchronously.</summary>
    /// <returns> a <c>uint</c> type for the ID of the current world.</returns>
    public async Task<uint> GetWorldIdAsync()
    {
        return await RunOnFrameworkThread(GetWorldId).ConfigureAwait(false);
    }

    /// <summary> Gets the player characters ID of the world they are currently in.</summary>
    /// <returns> a <c>uint</c> type for the ID of the current world.</returns>
    public nint GetIPlayerCharacterFromCachedTableByIdent(string characterName)
    {
        if (_playerCharas.TryGetValue(characterName, out var pchar)) return pchar.Address;
        return nint.Zero;
    }


    /// <summary> Run the task on the framework thread </summary>
    /// <param name="act">an action to run if any</param>
    public async Task RunOnFrameworkThread(Action act)
    {
        if (!_framework.IsInFrameworkUpdateThread)
        {
            await _framework.RunOnFrameworkThread(act).ContinueWith((_) => Task.CompletedTask).ConfigureAwait(false);
            while (_framework.IsInFrameworkUpdateThread) // yield the thread again, should technically never be triggered
            {
                _logger.LogTrace("Still on framework");
                await Task.Delay(1).ConfigureAwait(false);
            }
        }
        else
        {
            act();
        }
    }
    /// <summary> Run the task on the framework thread </summary>
    /// <param name="func">a function to run if any</param>"
    public async Task<T> RunOnFrameworkThread<T>(Func<T> func)
    {
        if (!_framework.IsInFrameworkUpdateThread)
        {
            var result = await _framework.RunOnFrameworkThread(func).ContinueWith((task) => task.Result).ConfigureAwait(false);
            while (_framework.IsInFrameworkUpdateThread) // yield the thread again, should technically never be triggered
            {
                _logger.LogTrace("Still on framework");
                await Task.Delay(1).ConfigureAwait(false);
            }
            return result;
        }

        return func.Invoke();
    }


    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting OnFrameworkService");
        // subscribe to the framework updates
        _framework.Update += FrameworkOnUpdate;

        _logger.LogInformation("Started OnFrameworkService");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Stopping {type}", GetType());
        // unsubscribe from all mediator messages
        Mediator.UnsubscribeAll(this);
        // unsubscribe from the framework updates
        _framework.Update -= FrameworkOnUpdate;
        return Task.CompletedTask;
    }

    /// <summary> Try and find a player by their name hash (ident ((identity))</summary>
    /// <param name="ident">The identity (has) of a player character</param>
    /// <returns>The name and address of the player if found (not sure how it finds it)</returns>
    internal (string Name, nint Address) FindPlayerByNameHash(string ident)
    {
        _playerCharas.TryGetValue(ident, out var result);
        return result;
    }

    /// <summary> Run An action on the Framework Delayed by a set number of ticks. </summary>
    public async Task RunOnFrameworkTickDelayed(Action act, int ticks)
    {
        await _framework.RunOnTick(() => act(), delayTicks: ticks);
    }

    /// <summary> The method that is called when the framework updates </summary>
    private void FrameworkOnUpdate(IFramework framework) => FrameworkOnUpdateInternal();
    #endregion FrameworkMethods
    /// <summary> the unsafe internal framework update method </summary>
    private unsafe void FrameworkOnUpdateInternal()
    {
        // If the local player is dead or null, return after setting the hasDied flag to true
        if (_clientState.LocalPlayer is null) return;

        // if player has died, set hasDied to true.
        if(_clientState.LocalPlayer.IsDead) { _hasDied = true; return; }

        // if the player is no longer dead but hasDied is true, set it back to false.
        if (_hasDied) { _hasDied = false; }


        // we need to update our stored player characters to know if they are still valid, and to update our pair handlers
        // Begin by adding the range of existing player character keys
        var playerCharacters = _objectTable.OfType<IPlayerCharacter>().ToList();
        _notUpdatedCharas.AddRange(_playerCharas.Keys);

        // for each object in the renderable object table
        foreach (var chara in playerCharacters)
        {
            var charaName = chara.Name.ToString();
            var hash = (charaName, ((BattleChara*)chara.Address)->Character.HomeWorld).GetHash256();

            _notUpdatedCharas.Remove(hash);
            _playerCharas[hash] = (charaName, chara.Address);
        }

        foreach (var notUpdatedChara in _notUpdatedCharas)
        {
            _playerCharas.Remove(notUpdatedChara);
        }

        // clear the list of not updated characters
        _notUpdatedCharas.Clear();


        // check if we are in the middle of a delayed framework update
        var isNormalFrameworkUpdate = DateTime.Now < _delayedFrameworkUpdateCheck.AddSeconds(1);

        // if we are zoning, 
        if (IsZoning)
        {
            // get the zone
            var zone = _clientState.TerritoryType;
            // if the zone is different from the last zone
            if (_lastZone != zone)
            {
                // set the last zone to the current zone
                _lastZone = zone;
                // if we are marked as not sent between areas
                if (!_sentBetweenAreas)
                {
                    // we know we are starting a zone switch, so publish it to the mediator and set sent between areas to true
                    _logger.LogDebug("Zone switch/Gpose start");
                    _sentBetweenAreas = true;
                    Mediator.Publish(new ZoneSwitchStartMessage());
                    if (_clientState.IsGPosing) Mediator.Publish(new GPoseStartMessage());
                }
            }
            // do an early return so we dont hit the sentBetweenAreas conditional below
            return;
        }

        // this is called while are zoning between areas has ended
        if (_sentBetweenAreas)
        {
            _logger.LogDebug("Zone switch/Gpose end");
            _sentBetweenAreas = false;
            Mediator.Publish(new ZoneSwitchEndMessage());
            // if our commendation count is different, update it and invoke the event with the difference.
            var newCommendations = PlayerState.Instance()->PlayerCommendations;
            if (newCommendations != LastCommendationsCount)
            {
                LastCommendationsCount = newCommendations;
                _logger.LogInformation("Commendations increased by {0}", newCommendations - LastCommendationsCount);
                Mediator.Publish(new CommendationsIncreasedMessage(newCommendations - LastCommendationsCount));
            }

            if (!_clientState.IsGPosing) Mediator.Publish(new GPoseEndMessage());
        }

        if (_condition[ConditionFlag.WatchingCutscene] && !IsInCutscene)
        {
            _logger.LogDebug("Cutscene start");
            IsInCutscene = true;
            Mediator.Publish(new CutsceneBeginMessage());
        }
        else if (!_condition[ConditionFlag.WatchingCutscene] && IsInCutscene)
        {
            _logger.LogDebug("Cutscene end");
            IsInCutscene = false;
            Mediator.Publish(new CutsceneEndMessage());
        }


        // publish the framework update message
        Mediator.Publish(new FrameworkUpdateMessage());

        // if this is a normal framework update, then return
        if (isNormalFrameworkUpdate)
            return;
        //_logger.LogInformation("Zone: " + (_gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>()!
        //    .GetRow(_clientState.TerritoryType)!.PlaceName.Value!.Name ?? "UnknownZone") + " ("+ _clientState.TerritoryType + ")"); 
        // otherwise, if it is abnormal, then try to fetch the local player
        var localPlayer = _clientState.LocalPlayer;

        // if it is not null (they exist) and isLoggedIn is not true
        if (localPlayer != null && !IsLoggedIn)
        {
            // they have logged in, so set IsLoggedIn to true, and publish the DalamudLoginMessage
            _logger.LogDebug("Logged in");
            IsLoggedIn = true;
            _lastZone = _clientState.TerritoryType;
            ClientPlayerAddress = GetPlayerPointerAsync().GetAwaiter().GetResult();
            PlayerClassJobId = _clientState.LocalPlayer?.ClassJob.Id ?? 0;
            LastCommendationsCount = PlayerState.Instance()->PlayerCommendations;
            Mediator.Publish(new DalamudLoginMessage());
        }
        // otherwise, if the local player is null and isLoggedIn is true, meaning they just logged out
        else if (localPlayer == null && IsLoggedIn)
        {
            // so log it and publish the DalamudLogoutMessage
            _logger.LogDebug("Logged out");
            IsLoggedIn = false;
            Mediator.Publish(new DalamudLogoutMessage());
        }

        // push the delayed framework update message to the mediator for things like the UI and the online player manager
        Mediator.Publish(new DelayedFrameworkUpdateMessage());
        // set the latest framework updatecheck
        _delayedFrameworkUpdateCheck = DateTime.Now;
    }
}

