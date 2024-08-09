using GagSpeak.ResourceManager.VfxStructs;
using Penumbra.String;

namespace GagSpeak.ResourceManager;

public static unsafe class ResourceUtils
{
    public static int ComputeHash(ByteString path, GetResourceParameters* resParams)
    {
        if (resParams == null || !resParams->IsPartialRead) return path.Crc32;

        return ByteString.Join(
            (byte)'.',
            path,
            ByteString.FromStringUnsafe(resParams->SegmentOffset.ToString("x"), true),
            ByteString.FromStringUnsafe(resParams->SegmentLength.ToString("x"), true)
        ).Crc32;
    }

    public static byte[] GetBgCategory(string expansion, string zone)
    {
        var ret = BitConverter.GetBytes(2u);
        if (expansion == "ffxiv") return ret;
        // ex1/03_abr_a2/fld/a2f1/level/a2f1 -> [02 00 03 01]
        // expansion = ex1
        // zone = 03_abr_a2
        var expansionTrimmed = expansion.Replace("ex", "");
        var zoneTrimmed = zone.Split('_')[0];
        ret[2] = byte.Parse(zoneTrimmed);
        ret[3] = byte.Parse(expansionTrimmed);
        return ret;
    }

    public static byte[] GetDatCategory(uint prefix, string expansion)
    {
        var ret = BitConverter.GetBytes(prefix);
        if (expansion == "ffxiv") return ret;
        // music/ex4/BGM_EX4_Field_Ult_Day03.scd
        // 04 00 00 0C
        var expansionTrimmed = expansion.Replace("ex", "");
        ret[3] = byte.Parse(expansionTrimmed);
        return ret;
    }
}
