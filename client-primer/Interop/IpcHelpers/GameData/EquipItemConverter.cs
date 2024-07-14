using System;
using Newtonsoft.Json;
using Penumbra.GameData.Structs;

namespace GagSpeak.Interop.IpcHelpers.GameData;
// had to make this to get a workaround for the fact that the json read-write doesn't read from private readonly structs, and equipItem is a private readonly struct
public class EquipItemConverter : Newtonsoft.Json.JsonConverter
{
    #pragma warning disable CS8765, CS8604
    public override bool CanConvert(Type objectType) {
        if(objectType == typeof(EquipItem)) {
            //_logger.LogInformation($"[EquipItemConverter] Can convert {objectType}");
            return true;
        } else {
            return false;
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
        //_logger.LogInformation($"[EquipItemConverter] Reading JSON for {objectType}");
        if(objectType == typeof(EquipItem)) {
            var surrogate = serializer.Deserialize<EquipItemSurrogate>(reader);
            return (EquipItem)surrogate;
        }
        return null!;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
        //_logger.LogInformation($"[EquipItemConverter] Writing JSON for {value}");
        var surrogate = (EquipItemSurrogate)(EquipItem)value;
        serializer.Serialize(writer, surrogate);
    }
    #pragma warning restore CS8765, CS8604
}
