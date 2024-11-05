using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
#nullable disable
namespace GagSpeak.GagspeakConfiguration;

public class WardrobeConfigService : ConfigurationServiceBase<WardrobeConfig>
{
    public const string ConfigName = "wardrobe.json";
    public const bool PerCharacterConfig = true;
    public WardrobeConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

    protected override JObject MigrateConfig(JObject oldConfigJson, int readVersion)
    {
        JObject newConfigJson = oldConfigJson;
        // if migrating from any version less than 2, to 2
        if (readVersion <= 2)
        {
            newConfigJson = MigrateFromV2toV3(oldConfigJson);
        }
        else
        {
            newConfigJson = oldConfigJson;
        }
        return newConfigJson;
    }

    private JObject MigrateFromV2toV3(JObject oldConfigJson)
    {
        // Create a new JObject for V3
        var v3Data = new JObject
        {
            ["Version"] = 3,
            ["WardrobeStorage"] = new JObject()
        };

        // Get the V2 WardrobeStorage and RestraintSets
        var v2WardrobeStorage = (JObject)oldConfigJson["WardrobeStorage"];
        var v2RestraintSets = (JArray)v2WardrobeStorage["RestraintSets"];
        var v3RestraintSets = new JArray();

        foreach (var v2Set in v2RestraintSets)
        {
            var v3Set = new JObject
            {
                ["Name"] = v2Set["Name"],
                ["Description"] = v2Set["Description"],
                ["Enabled"] = v2Set["Enabled"],
                ["EnabledBy"] = v2Set["EnabledBy"],
                ["Locked"] = v2Set["Locked"],
                ["LockType"] = v2Set["LockType"],
                ["LockPassword"] = v2Set["LockPassword"],
                ["LockedUntil"] = v2Set["LockedUntil"],
                ["LockedBy"] = v2Set["LockedBy"],
                ["ForceHeadgear"] = false, // Assuming default values for new properties
                ["ForceVisor"] = false,
                ["DrawData"] = new JObject()
            };

            // Process DrawData
            var v2DrawData = (JArray)v2Set["DrawData"];
            foreach (var drawItem in v2DrawData)
            {
                var slot = drawItem["EquipmentSlot"].ToString();
                var drawData = drawItem["DrawData"];

                var v3DrawItem = new JObject
                {
                    ["Slot"] = slot,
                    ["IsEnabled"] = drawData["IsEnabled"],
                    ["CustomItemId"] = drawData["GameItem"]["Id"],
                    ["GameStain"] = drawData["GameStain"]
                };

                v3Set["DrawData"][slot] = v3DrawItem;
            }

            // Copy BonusDrawData directly
            v3Set["BonusDrawData"] = v2Set["BonusDrawData"];

            // Copy AssociatedMods directly
            v3Set["AssociatedMods"] = v2Set["AssociatedMods"];

            // Initialize new collections for AssociatedMoodles, ViewAccess, SetTraits
            v3Set["AssociatedMoodles"] = new JArray();
            v3Set["ViewAccess"] = v2Set["ViewAccess"];
            v3Set["SetTraits"] = v2Set["SetTraits"];

            v3RestraintSets.Add(v3Set);
        }

        // Assign the transformed restraint sets to V3 data
        v3Data["WardrobeStorage"]["RestraintSets"] = v3RestraintSets;

        // Migrate BlindfoldInfo
        var v2BlindfoldInfo = (JObject)v2WardrobeStorage["BlindfoldInfo"];
        if (v2BlindfoldInfo != null)
        {
            var v2BlindfoldItem = (JObject)v2BlindfoldInfo["BlindfoldItem"];
            var v3BlindfoldItem = new JObject
            {
                ["IsEnabled"] = v2BlindfoldItem["IsEnabled"],
                ["Slot"] = v2BlindfoldItem["Slot"],
                ["GameItem"] = v2BlindfoldItem["GameItem"]["Id"],
                ["GameStain"] = v2BlindfoldItem["GameStain"]
            };

            var v3BlindfoldInfo = new JObject
            {
                ["ForceHeadgear"] = false, // Assuming default values for new properties
                ["ForceVisor"] = false,
                ["BlindfoldMoodles"] = new JArray(),
                ["BlindfoldItem"] = v3BlindfoldItem
            };

            v3Data["WardrobeStorage"]["BlindfoldInfo"] = v3BlindfoldInfo;
        }


        return v3Data;
    }


    protected override WardrobeConfig DeserializeConfig(JObject configJson)
    {
        var config = new WardrobeConfig();
        // Deserialize WardrobeStorage
        JToken wardrobeStorageToken = configJson["WardrobeStorage"];
        if (wardrobeStorageToken != null)
        {
            WardrobeStorage wardrobeStorage = new WardrobeStorage();

            var restraintSetsArray = wardrobeStorageToken["RestraintSets"]?.Value<JArray>();
            if (restraintSetsArray == null)
            {
                throw new Exception("RestraintSets property is missing in RestraintSets.json");
            }

            // we are in version 1, so we should deserialize appropriately
            foreach (var item in restraintSetsArray)
            {
                var restraintSet = new RestraintSet();
                var ItemValue = item.Value<JObject>();
                if (ItemValue != null)
                {
                    restraintSet.Deserialize(ItemValue);
                    wardrobeStorage.RestraintSets.Add(restraintSet);
                }
                else
                {
                    throw new Exception("restraint set contains invalid property");
                }
            }

            // Assuming BlindfoldInfo follows a similar pattern
            JToken blindfoldInfoToken = wardrobeStorageToken["BlindfoldInfo"];
            if (blindfoldInfoToken != null)
            {
                BlindfoldModel blindfoldModel = new BlindfoldModel();
                blindfoldModel.Deserialize((JObject)blindfoldInfoToken);
                wardrobeStorage.BlindfoldInfo = blindfoldModel;
            }
            else
            {
                wardrobeStorage.BlindfoldInfo = new BlindfoldModel();
            }

            config.WardrobeStorage = wardrobeStorage; // loads the wardrobe storage into the stored config.
        }
        return config;
    }

    protected override string SerializeConfig(WardrobeConfig config)
    {
        // serialize here.
        JObject configObject = new JObject
        {
            ["Version"] = config.Version,
            ["WardrobeStorage"] = new JObject()
        };

        JArray restraintSetsArray = new JArray();
        foreach (RestraintSet restraintSet in config.WardrobeStorage.RestraintSets)
        {
            restraintSetsArray.Add(restraintSet.Serialize());
        }
        configObject["WardrobeStorage"]["RestraintSets"] = restraintSetsArray;

        // Use Serialize method for BlindfoldInfo
        JObject blindfoldInfoObject = config.WardrobeStorage.BlindfoldInfo.Serialize();

        // Add blindfoldInfoObject to the WardrobeStorage JObject
        configObject["WardrobeStorage"]["BlindfoldInfo"] = blindfoldInfoObject;

        return configObject.ToString(Formatting.Indented);
    }
}
