using GagSpeak.Utils;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record RestraintSet
{
    /// <summary> The name of the pattern </summary>
    public string Name { get; set; } = "New Restraint Set";

    /// <summary> The description of the pattern </summary>
    public string Description { get; set; } = "Enter Description Here...";

    public bool Enabled { get; set; } = false;

    public bool Locked { get; set; } = false;

    public string EnabledBy { get; set; } = string.Empty;

    public string LockedBy { get; set; } = string.Empty;

    public DateTimeOffset LockedUntil { get; set; } = DateTimeOffset.MinValue;

    public Dictionary<EquipSlot, EquipDrawData> DrawData { get; set; } = new Dictionary<EquipSlot, EquipDrawData>(
        // handler for the creation of new draw data on set creation.
            EquipSlotExtensions.EqdpSlots.Select(slot => new KeyValuePair<EquipSlot, EquipDrawData>(
            slot,
            new EquipDrawData(ItemIdVars.NothingItem(slot))
            {
                Slot = slot,
                IsEnabled = false,
            }
        ))
    );

    public Dictionary<BonusItemFlag, BonusDrawData> BonusDrawData { get; set; } = new Dictionary<BonusItemFlag, BonusDrawData>(
            // handler for the creation of new draw data on set creation.
            BonusExtensions.AllFlags.Select(slot => new KeyValuePair<BonusItemFlag, BonusDrawData>(
            slot,
            new BonusDrawData(BonusItem.Empty(slot))
            {
                Slot = slot,
                IsEnabled = false,
            }
        ))
    );

    public List<AssociatedMod> AssociatedMods { get; private set; } = new();

    /// <summary> Controls which UID's are able to see this restraint set. </summary>
    public List<string> ViewAccess { get; private set; } = new();

    /// <summary> 
    /// The Hardcore Set Properties to apply when restraint set is toggled.
    /// The string indicates the UID associated with the set properties.
    /// </summary>
    public Dictionary<string, HardcoreSetProperties> SetProperties { get; set; } = new();

    // parameterless constructor for serialization
    public RestraintSet() { }

    public JObject Serialize()
    {
        var serializer = new JsonSerializer();
        serializer.Converters.Add(new EquipItemConverter());
        // serizlie the data

        // for the DrawData dictionary.
        var drawDataArray = new JArray();
        // serialize each item in it
        foreach (var pair in DrawData)
        {
            drawDataArray.Add(new JObject()
            {
                ["EquipmentSlot"] = pair.Key.ToString(),
                ["DrawData"] = pair.Value.Serialize()
            });
        }

        // for the BonusDrawData dictionary.
        var bonusDrawDataArray = new JArray();
        // serialize each item in it
        foreach (var pair in BonusDrawData)
        {
            bonusDrawDataArray.Add(new JObject()
            {
                ["BonusItemFlag"] = pair.Key.ToString(),
                ["BonusDrawData"] = pair.Value.Serialize()
            });
        }

        // for the AssociatedMods list.
        var associatedModsArray = new JArray();
        // serialize each item in it
        foreach (var mod in AssociatedMods)
        {
            associatedModsArray.Add(mod.Serialize());
        }

        // for the set properties
        var setPropertiesArray = new JArray();
        // serialize each item in it
        foreach (var pair in SetProperties)
        {
            setPropertiesArray.Add(new JObject()
            {
                ["UID"] = pair.Key,
                ["HardcoreSetProperties"] = pair.Value.Serialize()
            });
        }

        return new JObject()
        {
            ["Name"] = Name,
            ["Description"] = Description,
            ["Enabled"] = Enabled,
            ["Locked"] = Locked,
            ["EnabledBy"] = EnabledBy,
            ["LockedBy"] = LockedBy,
            ["LockedUntil"] = LockedUntil.UtcDateTime.ToString("o"),
            ["DrawData"] = drawDataArray,
            ["BonusDrawData"] = bonusDrawDataArray,
            ["AssociatedMods"] = associatedModsArray,
            ["ViewAccess"] = new JArray(ViewAccess),
            ["SetProperties"] = setPropertiesArray
        };
    }


    public void Deserialize(JObject jsonObject)
    {
        Name = jsonObject["Name"]?.Value<string>() ?? string.Empty;
        Description = jsonObject["Description"]?.Value<string>() ?? string.Empty;
        Enabled = jsonObject["Enabled"]?.Value<bool>() ?? false;
        Locked = jsonObject["Locked"]?.Value<bool>() ?? false;
        EnabledBy = jsonObject["EnabledBy"]?.Value<string>() ?? string.Empty;
        LockedBy = jsonObject["LockedBy"]?.Value<string>() ?? string.Empty;
        if (jsonObject["LockedUntil"]?.Type == JTokenType.String)
        {
            string lockedUntilStr = jsonObject["LockedUntil"].Value<string>();
            if (DateTimeOffset.TryParse(lockedUntilStr, out DateTimeOffset result))
            {
                LockedUntil = result;
            }
            else
            {
                LockedUntil = DateTimeOffset.MinValue; // Or handle the parse failure as appropriate
            }
        }
        else
        {
            LockedUntil = DateTimeOffset.MinValue; // Default or error value if the token is not a string
        }

        try
        {
            var drawDataArray = jsonObject["DrawData"]?.Value<JArray>();
            if (drawDataArray != null)
            {
                foreach (var item in drawDataArray)
                {
                    var itemObject = item.Value<JObject>();
                    if (itemObject != null)
                    {
                        var equipmentSlot = (EquipSlot)Enum.Parse(typeof(EquipSlot), itemObject["EquipmentSlot"]?.Value<string>() ?? string.Empty);
                        var drawData = new EquipDrawData(ItemIdVars.NothingItem(equipmentSlot));
                        drawData.Deserialize(itemObject["DrawData"]?.Value<JObject>());
                        // Use the indexer to add or replace the entry
                        DrawData[equipmentSlot] = drawData;
                    }
                }
            }

            // Deserialize the BonusDrawData
            if (jsonObject["BonusDrawData"] is JObject bonusDrawDataObj)
            {
                foreach (var (slot, bonusDrawData) in BonusDrawData)
                {
                    if (bonusDrawDataObj[slot.ToString()] is JObject slotObj)
                    {
                        bonusDrawData.Deserialize(slotObj);
                        // add it to the bonus draw data
                        BonusDrawData[slot] = bonusDrawData;
                    }
                }
            }

            // Deserialize the AssociatedMods
            if (jsonObject["AssociatedMods"] is JArray associatedModsArray)
            {
                AssociatedMods = associatedModsArray.Select(mod => mod.ToObject<AssociatedMod>()).ToList();
            }

            // Deserialize the ViewAccess
            if (jsonObject["ViewAccess"] is JArray viewAccessArray)
            {
                ViewAccess = viewAccessArray.Select(viewer => viewer.Value<string>()).ToList();
            }

            // Deserialize the SetProperties
            if (jsonObject["SetProperties"] is JObject setPropertiesObj)
            {
                foreach (var (uid, setProperties) in SetProperties)
                {
                    if (setPropertiesObj[uid] is JObject uidObj)
                    {
                        setProperties.Deserialize(uidObj);
                        // add it to the set properties
                        SetProperties[uid] = setProperties;
                    }
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to deserialize RestraintSet {Name} {e}");
        }
    }
}
