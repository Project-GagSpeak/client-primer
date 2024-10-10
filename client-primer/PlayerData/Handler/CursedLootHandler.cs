using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.ConfigurationServices;

namespace GagSpeak.PlayerData.Handlers;

public class CursedLootHandler
{
    private readonly ILogger<CursedLootHandler> _logger;
    private readonly ClientConfigurationManager _clientConfigs;

    public CursedLootHandler(ILogger<CursedLootHandler> logger, ClientConfigurationManager clientConfigs)
    {
        _logger = logger;
        _clientConfigs = clientConfigs;
    }
    private CursedLootStorage Data => _clientConfigs.CursedLootConfig.CursedLootStorage;

    public List<CursedItem> CursedItems => Data.CursedItems;
    public TimeSpan LowerLockLimit => Data.LockRangeLower;
    public TimeSpan UpperLockLimit => Data.LockRangeUpper;
    public int LockChance => Data.LockChance;


    /// <summary>
    /// The currently active cursed items on the player. 
    /// Holds up to a maximum of 6 items. 
    /// Any Extras are discarded.
    /// </summary>
    public List<CursedItem> ActiveItems => Data.CursedItems
        .Where(x => x.AppliedTime != DateTimeOffset.MinValue)
        .Take(6)
        .OrderBy(x => x.AppliedTime)
        .ToList();

    public List<CursedItem> ItemsInPool => Data.CursedItems
        .Where(x => x.InPool)
        .ToList();

    public List<CursedItem> ItemsNotInPool => Data.CursedItems
        .Where(x => !x.InPool)
        .ToList();

    public void SetLowerLimit(TimeSpan time)
    {
        Data.LockRangeLower = time;
        Save();
    }

    public void SetUpperLimit(TimeSpan time)
    {
        Data.LockRangeUpper = time;
        Save();
    }

    public void SetLockChance(int chance)
    {
        Data.LockChance = chance;
        Save();
    }

    public string MakeUniqueName(string originalName)
        => _clientConfigs.EnsureUniqueLootName(originalName);

    public void AddItem(CursedItem item)
        => _clientConfigs.AddCursedItem(item);

    public void RemoveItem(Guid idToRemove)
        => _clientConfigs.RemoveCursedItem(idToRemove);

    public void Save() => _clientConfigs.SaveCursedLoot();





}
