using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using GagSpeak.Services.Textures;
using GagspeakAPI.Data.VibeServer;
using ImGuiNET;
using OtterGui;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Numerics;
using static GagspeakAPI.Data.Enum.GagList;

namespace GagSpeak.Utils;

public static class UtilsExtensions
{
    /// <summary> Draw a game icon display (not icon button or anything) </summary>
    public static void DrawIcon(this EquipItem item, TextureService textures, Vector2 size, EquipSlot slot)
    {
        var isEmpty = item.PrimaryId.Id == 0;
        var (ptr, textureSize, empty) = textures.GetIcon(item, slot);
        if (empty)
        {
            var (bgColor, tint) = isEmpty
                ? (ImGui.GetColorU32(ImGuiCol.FrameBg), Vector4.One)
                : (ImGui.GetColorU32(ImGuiCol.FrameBgActive), new Vector4(0.3f, 0.3f, 0.3f, 1f));
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(pos, pos + size, bgColor, 5 * ImGuiHelpers.GlobalScale);
            if (ptr != nint.Zero)
                ImGui.Image(ptr, size, Vector2.Zero, Vector2.One, tint);
            else
                ImGui.Dummy(size);
        }
        else
        {
            ImGuiUtil.HoverIcon(ptr, textureSize, size);
        }
    }

    public static void DrawIcon(this BonusItem item, TextureService textures, Vector2 size, BonusItemFlag slot)
    {
        var isEmpty = item.ModelId.Id == 0;
        var (ptr, textureSize, empty) = textures.GetIcon(item, slot);
        if (empty)
        {
            var (bgColor, tint) = isEmpty
                ? (ImGui.GetColorU32(ImGuiCol.FrameBg), Vector4.One)
                : (ImGui.GetColorU32(ImGuiCol.FrameBgActive), new Vector4(0.3f, 0.3f, 0.3f, 1f));
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(pos, pos + size, bgColor, 5 * ImGuiHelpers.GlobalScale);
            if (ptr != nint.Zero)
                ImGui.Image(ptr, size, Vector2.Zero, Vector2.One, tint);
            else
                ImGui.Dummy(size);
        }
        else
        {
            ImGuiUtil.HoverIcon(ptr, textureSize, size);
        }
    }

    public static string ExtractText(this SeString seStr, bool onlyFirst = false)
    {
        StringBuilder sb = new();
        foreach (var x in seStr.Payloads)
        {
            if (x is TextPayload tp)
            {
                sb.Append(tp.Text);
                if (onlyFirst) break;
            }
            if (x.Type == PayloadType.Unknown && x.Encode().SequenceEqual<byte>([0x02, 0x1d, 0x01, 0x03]))
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }

    public static TimeSpan GetTimespanFromTimespanString(this string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return TimeSpan.Zero;

        if (TimeSpan.TryParseExact(pattern, "hh\\:mm\\:ss", null, out var timeSpan) && timeSpan.TotalHours >= 1)
        {
            return timeSpan;
        }
        else if (TimeSpan.TryParseExact(pattern, "mm\\:ss", null, out timeSpan))
        {
            return timeSpan;
        }
        return TimeSpan.Zero;
    }

    public static string GetNameWithWorld(this IPlayerCharacter pc)
        => pc == null ? null : (pc.Name.ToString() + "@" + pc.HomeWorld.GameData.Name);


    /// <summary>
    /// Not my code, pulled from:
    /// https://github.com/PunishXIV/PunishLib/blob/8cea907683c36fd0f9edbe700301a59f59b6c78e/PunishLib/ImGuiMethods/ImGuiEx.cs
    /// </summary>
    public static readonly Dictionary<string, float> CenteredLineWidths = new();
    public static void ImGuiLineCentered(string id, Action func)
    {
        if (CenteredLineWidths.TryGetValue(id, out var dims))
        {
            ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2 - dims / 2);
        }
        var oldCur = ImGui.GetCursorPosX();
        func();
        ImGui.SameLine(0, 0);
        CenteredLineWidths[id] = ImGui.GetCursorPosX() - oldCur;
        ImGui.Dummy(Vector2.Zero);
    }

    public static string TriggerKindToString(this TriggerKind type)
    {
        return type switch
        {
            TriggerKind.Chat => "Chat Trigger",
            TriggerKind.SpellAction => "Action Trigger",
            TriggerKind.HealthPercent => "Health% Trigger",
            TriggerKind.RestraintSet => "Restraint Trigger",
            TriggerKind.GagState => "GagState Trigger",
            _ => "UNK"
        };
    }

    public static GagType GetGagFromAlias(this string alias) => alias switch
    {
        "Ball Gag" => GagType.BallGag,
        "Ball Gag Mask" => GagType.BallGagMask,
        "Bamboo Gag" => GagType.BambooGag,
        "Belt Strap Gag" => GagType.BeltStrapGag,
        "Bit Gag" => GagType.BitGag,
        "Bit Gag Padded" => GagType.BitGagPadded,
        "Bone Gag" => GagType.BoneGag,
        "Bone Gag (XL)" => GagType.BoneGagXL,
        "Candle Gag" => GagType.CandleGag,
        "Cage Muzzle" => GagType.CageMuzzle,
        "Cleave Gag" => GagType.CleaveGag,
        "Chloroform Gag" => GagType.ChloroformGag,
        "Chopstick Gag" => GagType.ChopStickGag,
        "Cloth Wrap Gag" => GagType.ClothWrapGag,
        "Cloth Stuffing Gag" => GagType.ClothStuffingGag,
        "Crop Gag" => GagType.CropGag,
        "Cup Holder Gag" => GagType.CupHolderGag,
        "Deepthroat Penis Gag" => GagType.DeepthroatPenisGag,
        "Dental Gag" => GagType.DentalGag,
        "Dildo Gag" => GagType.DildoGag,
        "Duct Tape Gag" => GagType.DuctTapeGag,
        "Duster Gag" => GagType.DusterGag,
        "Funnel Gag" => GagType.FunnelGag,
        "Futuristic Harness Ball Gag" => GagType.FuturisticHarnessBallGag,
        "Futuristic Harness Panel Gag" => GagType.FuturisticHarnessPanelGag,
        "Gas Mask" => GagType.GasMask,
        "Harness Ball Gag" => GagType.HarnessBallGag,
        "Harness Ball Gag XL" => GagType.HarnessBallGagXL,
        "Harness Panel Gag" => GagType.HarnessPanelGag,
        "Hook Gag Mask" => GagType.HookGagMask,
        "Inflatable Hood" => GagType.InflatableHood,
        "Large Dildo Gag" => GagType.LargeDildoGag,
        "Latex Hood" => GagType.LatexHood,
        "Latex Ball Muzzle Gag" => GagType.LatexBallMuzzleGag,
        "Latex Posture Collar Gag" => GagType.LatexPostureCollarGag,
        "Leather Corset Collar Gag" => GagType.LeatherCorsetCollarGag,
        "Leather Hood" => GagType.LeatherHood,
        "Lip Gag" => GagType.LipGag,
        "Medical Mask" => GagType.MedicalMask,
        "Muzzle Gag" => GagType.MuzzleGag,
        "Panty Stuffing Gag" => GagType.PantyStuffingGag,
        "Plastic Wrap Gag" => GagType.PlasticWrapGag,
        "Plug Gag" => GagType.PlugGag,
        "Pony Gag" => GagType.PonyGag,
        "Pump Gag Lv.1" => GagType.PumpGaglv1,
        "Pump Gag Lv.2" => GagType.PumpGaglv2,
        "Pump Gag Lv.3" => GagType.PumpGaglv3,
        "Pump Gag Lv.4" => GagType.PumpGaglv4,
        "Ribbon Gag" => GagType.RibbonGag,
        "Ring Gag" => GagType.RingGag,
        "Rope Gag" => GagType.RopeGag,
        "Scarf Gag" => GagType.ScarfGag,
        "Sensory Deprivation Hood" => GagType.SensoryDeprivationHood,
        "Sock Stuffing Gag" => GagType.SockStuffingGag,
        "Spider Gag" => GagType.SpiderGag,
        "Tentacle Gag" => GagType.TenticleGag,
        "Web Gag" => GagType.WebGag,
        "Wiffle Gag" => GagType.WiffleGag,
        "None" => GagType.None,
        _ => GagType.None
    };
}
