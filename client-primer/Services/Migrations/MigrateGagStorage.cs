using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Utils;
using GagspeakAPI.Enums;
using Penumbra.GameData.Enums;

namespace GagSpeak.Services;

// handles the global chat and pattern discovery social features.
public class MigrateGagStorage
{
    private readonly ILogger<MigrateGagStorage> _logger;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly ItemIdVars _ItemHelper;
    private readonly string _oldGagStorageDirectory;
    public MigrateGagStorage(ILogger<MigrateGagStorage> logger,
        ClientConfigurationManager clientConfigs, ItemIdVars itemHelper,
        string configDirectory)
    {
        _logger = logger;
        _clientConfigs = clientConfigs;
        _ItemHelper = itemHelper;
        _oldGagStorageDirectory = Path.Combine(configDirectory, "..", "GagSpeak", "GagStorage.json");
    }


    public bool OldGagStorageLoaded { get; private set; } = false;
    public OldGagStorage OldGagStorage { get; private set; } = new OldGagStorage();
    public void LoadOldGagStorage()
    {
        var oldGagStorageFetched = new OldGagStorage();

        if (!File.Exists(_oldGagStorageDirectory))
        {
            _logger.LogWarning($"Old GagStorage file not found at {_oldGagStorageDirectory}");
            OldGagStorage = oldGagStorageFetched;
            return;
        }

        try
        {
            var text = File.ReadAllText(_oldGagStorageDirectory);
            var jsonObject = JObject.Parse(text);
            var gagEquipDataToken = jsonObject["GagEquipData"]?.Value<JObject>();

            if (gagEquipDataToken != null)
            {
                foreach (var gagData in gagEquipDataToken)
                {
                    var gagType = (GagType)Enum.Parse(typeof(GagType), gagData.Key);
                    if (gagData.Value is JObject itemObject)
                    {
                        string slotString = itemObject["Slot"]?.Value<string>() ?? string.Empty;
                        EquipSlot slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), slotString);
                        var drawData = new OldEquipDrawData(_ItemHelper, ItemIdVars.NothingItem(slot));
                        drawData.Deserialize(itemObject);
                        oldGagStorageFetched.OldGagEquipData.Add(gagType, drawData);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading old gag storage: {ex}");
        }

        OldGagStorageLoaded = true;
        OldGagStorage = oldGagStorageFetched;
    }

    public void MigrateGagStorageToCurrentGagStorage()
    {
        var newGagStoragetorageAll = new GagStorage();

        newGagStoragetorageAll.GagEquipData = Enum.GetValues(typeof(GagType))
            .Cast<GagType>()
            .ToDictionary(gagType => gagType, gagType => new GagDrawData(_ItemHelper, ItemIdVars.NothingItem(EquipSlot.Head)));

        foreach (var (gagType, oldDrawData) in OldGagStorage.OldGagEquipData)
        {
            if (newGagStoragetorageAll.GagEquipData.ContainsKey(gagType))
            {
                newGagStoragetorageAll.GagEquipData[gagType] = new GagDrawData(_ItemHelper, oldDrawData.GameItem)
                {
                    IsEnabled = oldDrawData.IsEnabled,
                    Slot = oldDrawData.Slot,
                    GameItem = oldDrawData.GameItem,
                    GameStain = oldDrawData.GameStain,
                };
            }
        }

        // set the new storage to the old one's data.
        _clientConfigs.UpdateGagStorageDictionary(newGagStoragetorageAll.GagEquipData);
    }
}

public class OldGagStorage
{
    public Dictionary<GagType, OldEquipDrawData> OldGagEquipData = [];
}
