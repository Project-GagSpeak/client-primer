using GagspeakAPI.Data.IPC;

namespace GagSpeak.Services.Textures;

public static class CosmeticLabels
{
    public static readonly Dictionary<CorePluginTexture, string> NecessaryImages = new()
    {
        { CorePluginTexture.Logo256, "RequiredImages\\icon256.png" },
        { CorePluginTexture.Logo256bg, "RequiredImages\\icon256bg.png" },
        { CorePluginTexture.SupporterBooster, "RequiredImages\\BoosterIcon.png" },
        { CorePluginTexture.SupporterTier1, "RequiredImages\\Tier1Icon.png" },
        { CorePluginTexture.SupporterTier2, "RequiredImages\\Tier2Icon.png" },
        { CorePluginTexture.SupporterTier3, "RequiredImages\\Tier3Icon.png" },
        { CorePluginTexture.SupporterTier4, "RequiredImages\\Tier4Icon.png" },
        { CorePluginTexture.AchievementLineSplit, "RequiredImages\\achievementlinesplit.png" },
        { CorePluginTexture.Achievement, "RequiredImages\\achievement.png" },
        { CorePluginTexture.Blindfolded, "RequiredImages\\blindfolded.png" },
        { CorePluginTexture.ChatBlocked, "RequiredImages\\chatblocked.png" },
        { CorePluginTexture.Clock, "RequiredImages\\clock.png" },
        { CorePluginTexture.CursedLoot, "RequiredImages\\cursedloot.png" },
        { CorePluginTexture.ForcedEmote, "RequiredImages\\forcedemote.png" },
        { CorePluginTexture.ForcedStay, "RequiredImages\\forcedstay.png" },
        { CorePluginTexture.Gagged, "RequiredImages\\gagged.png" },
        { CorePluginTexture.Leash, "RequiredImages\\leash.png" },
        { CorePluginTexture.Restrained, "RequiredImages\\restrained.png" },
        { CorePluginTexture.RestrainedArmsLegs, "RequiredImages\\restrainedarmslegs.png" },
        { CorePluginTexture.ShockCollar, "RequiredImages\\shockcollar.png" },
        { CorePluginTexture.SightLoss, "RequiredImages\\sightloss.png" },
        { CorePluginTexture.Stimulated, "RequiredImages\\stimulated.png" },
        { CorePluginTexture.Vibrator, "RequiredImages\\vibrator.png" },
        { CorePluginTexture.Weighty, "RequiredImages\\weighty.png" },
        { CorePluginTexture.ArrowSpin, "RequiredImages\\arrowspin.png" },
        { CorePluginTexture.CircleDot, "RequiredImages\\circledot.png" },
        { CorePluginTexture.Power, "RequiredImages\\power.png" },
        { CorePluginTexture.Play, "RequiredImages\\play.png" },
        { CorePluginTexture.Stop, "RequiredImages\\stop.png" },
    };

    public static readonly Dictionary<string, string> CosmeticTextures = InitializeCosmeticTextures();

    private static Dictionary<string, string> InitializeCosmeticTextures()
    {
        var dictionary = new Dictionary<string, string>
        {
            { "DummyTest", "RequiredImages\\icon256bg.png" } // Dummy File

        };

        AddEntriesForComponent(dictionary, ProfileComponent.Plate, hasBackground: true, hasBorder: true, hasOverlay: false);
        AddEntriesForComponent(dictionary, ProfileComponent.PlateLight, hasBackground: true, hasBorder: true, hasOverlay: false);
        AddEntriesForComponent(dictionary, ProfileComponent.ProfilePicture, hasBackground: false, hasBorder: true, hasOverlay: true);
        AddEntriesForComponent(dictionary, ProfileComponent.Description, hasBackground: true, hasBorder: true, hasOverlay: true);
        AddEntriesForComponent(dictionary, ProfileComponent.DescriptionLight, hasBackground: true, hasBorder: true, hasOverlay: true);
        AddEntriesForComponent(dictionary, ProfileComponent.GagSlot, hasBackground: true, hasBorder: true, hasOverlay: true);
        AddEntriesForComponent(dictionary, ProfileComponent.Padlock, hasBackground: true, hasBorder: true, hasOverlay: true);
        AddEntriesForComponent(dictionary, ProfileComponent.BlockedSlots, hasBackground: true, hasBorder: true, hasOverlay: true);
        AddEntriesForComponent(dictionary, ProfileComponent.BlockedSlot, hasBackground: false, hasBorder: true, hasOverlay: true);

        return dictionary;
    }

    private static void AddEntriesForComponent(Dictionary<string, string> dictionary, ProfileComponent component, bool hasBackground, bool hasBorder, bool hasOverlay)
    {
        if (hasBackground)
        {
            foreach (ProfileStyleBG styleBG in Enum.GetValues<ProfileStyleBG>())
            {
                string key = component.ToString() + "_Background_" + styleBG.ToString();
                string value = $"CosmeticImages\\{component}\\Background_{styleBG}.png";
                dictionary[key] = value;
            }
        }

        if (hasBorder)
        {
            foreach (ProfileStyleBorder styleBorder in Enum.GetValues<ProfileStyleBorder>())
            {
                string key = component.ToString() + "_Border_" + styleBorder.ToString();
                string value = $"CosmeticImages\\{component}\\Border_{styleBorder}.png";
                dictionary[key] = value;
            }
        }

        if (hasOverlay)
        {
            foreach (ProfileStyleOverlay styleOverlay in Enum.GetValues<ProfileStyleOverlay>())
            {
                string key = component.ToString() + "_Overlay_" + styleOverlay.ToString();
                string value = $"CosmeticImages\\{component}\\Overlay_{styleOverlay}.png";
                dictionary[key] = value;
            }
        }
    }

    #region Mapping Dictionaries
    // Plate
    public static readonly Dictionary<uint, ProfileStyleBG> PlateBackgroundMap = new()
    {
        { 0, ProfileStyleBG.Default },
        { 301, ProfileStyleBG.Ropes },
        { 302, ProfileStyleBG.Chains },
        { 303, ProfileStyleBG.LatexBlack },
        { 304, ProfileStyleBG.Slime },
        { 305, ProfileStyleBG.Tentacles },
        { 306, ProfileStyleBG.Tape },
        { 307, ProfileStyleBG.Belts },
        { 308, ProfileStyleBG.Prison },
        { 309, ProfileStyleBG.Hypnotic },
        { 3010, ProfileStyleBG.MedicalNurse },
        { 3011, ProfileStyleBG.Asylum },
        { 3012, ProfileStyleBG.RedRoom },
        { 3013, ProfileStyleBG.Office },
        { 3014, ProfileStyleBG.Church },
        { 3015, ProfileStyleBG.School },
        { 3016, ProfileStyleBG.Demonic },
    };

    public static readonly Dictionary<uint, ProfileStyleBorder> PlateBorderMap = new()
    {
        { 0, ProfileStyleBorder.Default },
        { 301, ProfileStyleBorder.Ropes },
        { 302, ProfileStyleBorder.Chains },
        { 303, ProfileStyleBorder.LatexBlack },
        { 304, ProfileStyleBorder.Slime },
        { 305, ProfileStyleBorder.Tape },
        { 306, ProfileStyleBorder.Belts },
        { 307, ProfileStyleBorder.Prison },
        { 308, ProfileStyleBorder.MedicalNurse },
        { 309, ProfileStyleBorder.Asylum },
        { 3010, ProfileStyleBorder.RedRoom },
        { 3011, ProfileStyleBorder.Office },
        { 3012, ProfileStyleBorder.School },
    };

    // Profile Picture
    public static readonly Dictionary<uint, ProfileStyleBorder> ProfilePictureBorderMap = new()
    {
        { 0, ProfileStyleBorder.Default },
        { 301, ProfileStyleBorder.Ropes },
        { 302, ProfileStyleBorder.Chains },
        { 303, ProfileStyleBorder.LatexBlack },
        { 304, ProfileStyleBorder.Slime },
        { 305, ProfileStyleBorder.Tape },
        { 306, ProfileStyleBorder.Belts },
        { 307, ProfileStyleBorder.Prison },
        { 308, ProfileStyleBorder.MedicalNurse },
        { 309, ProfileStyleBorder.Asylum },
        { 3010, ProfileStyleBorder.RedRoom },
        { 3011, ProfileStyleBorder.Office },
        { 3012, ProfileStyleBorder.School },
    };

    public static readonly Dictionary<uint, ProfileStyleOverlay> ProfilePictureOverlayMap = new()
    {
        { 0, ProfileStyleOverlay.Default },
        { 301, ProfileStyleOverlay.Ropes },
        { 302, ProfileStyleOverlay.Chains },
        { 303, ProfileStyleOverlay.LatexBlack },
        { 304, ProfileStyleOverlay.Slime },
        { 305, ProfileStyleOverlay.Prison },
        { 306, ProfileStyleOverlay.Hypnotic },
        { 307, ProfileStyleOverlay.MedicalNurse },
        { 308, ProfileStyleOverlay.School },
        { 309, ProfileStyleOverlay.Demonic },
        { 3010, ProfileStyleOverlay.Drool },
    };

    // DescriptionMap
    public static readonly Dictionary<uint, ProfileStyleBG> DescriptionBackgroundMap = new()
    {
        { 0, ProfileStyleBG.Default },
        { 301, ProfileStyleBG.Ropes },
        { 302, ProfileStyleBG.Chains },
        { 303, ProfileStyleBG.LatexBlack },
        { 304, ProfileStyleBG.Slime },
        { 305, ProfileStyleBG.Tentacles },
        { 306, ProfileStyleBG.Tape },
        { 307, ProfileStyleBG.Belts },
        { 308, ProfileStyleBG.Prison },
        { 309, ProfileStyleBG.Hypnotic },
        { 3010, ProfileStyleBG.MedicalNurse },
        { 3011, ProfileStyleBG.Asylum },
        { 3012, ProfileStyleBG.RedRoom },
        { 3013, ProfileStyleBG.Office },
        { 3014, ProfileStyleBG.Church },
        { 3015, ProfileStyleBG.School },
        { 3016, ProfileStyleBG.Demonic },
    };

    public static readonly Dictionary<uint, ProfileStyleBorder> DescriptionBorderMap = new()
    {
        { 0, ProfileStyleBorder.Default },
        { 301, ProfileStyleBorder.Ropes },
        { 302, ProfileStyleBorder.Chains },
        { 303, ProfileStyleBorder.LatexBlack },
        { 304, ProfileStyleBorder.Slime },
        { 305, ProfileStyleBorder.Tape },
        { 306, ProfileStyleBorder.Belts },
        { 307, ProfileStyleBorder.Prison },
        { 308, ProfileStyleBorder.MedicalNurse },
        { 309, ProfileStyleBorder.Asylum },
        { 3010, ProfileStyleBorder.RedRoom },
        { 3011, ProfileStyleBorder.Office },
        { 3012, ProfileStyleBorder.School },
    };

    public static readonly Dictionary<uint, ProfileStyleOverlay> DescriptionOverlayMap = new()
    {
        { 0, ProfileStyleOverlay.Default },
        { 301, ProfileStyleOverlay.Ropes },
        { 302, ProfileStyleOverlay.Chains },
        { 303, ProfileStyleOverlay.LatexBlack },
        { 304, ProfileStyleOverlay.Slime },
        { 305, ProfileStyleOverlay.Prison },
        { 306, ProfileStyleOverlay.Hypnotic },
        { 307, ProfileStyleOverlay.MedicalNurse },
        { 308, ProfileStyleOverlay.School },
        { 309, ProfileStyleOverlay.Demonic },
        { 3010, ProfileStyleOverlay.Drool },
    };

    // GagSlot
    public static readonly Dictionary<uint, ProfileStyleBG> GagSlotBackgroundMap = new()
    {
        { 0, ProfileStyleBG.Default},
        { 301, ProfileStyleBG.Ropes },
        { 302, ProfileStyleBG.Chains },
        { 303, ProfileStyleBG.LatexBlack },
        { 304, ProfileStyleBG.Slime },
        { 305, ProfileStyleBG.Tentacles },
        { 306, ProfileStyleBG.Tape },
        { 307, ProfileStyleBG.Belts },
        { 308, ProfileStyleBG.Prison },
        { 309, ProfileStyleBG.Hypnotic },
        { 3010, ProfileStyleBG.MedicalNurse },
        { 3011, ProfileStyleBG.Asylum },
        { 3012, ProfileStyleBG.RedRoom },
        { 3013, ProfileStyleBG.Office },
        { 3014, ProfileStyleBG.Church },
        { 3015, ProfileStyleBG.School },
        { 3016, ProfileStyleBG.Demonic },
    };

    public static readonly Dictionary<uint, ProfileStyleBorder> GagSlotBorderMap = new()
    {
        { 0, ProfileStyleBorder.Default },
        { 301, ProfileStyleBorder.Ropes },
        { 302, ProfileStyleBorder.Chains },
        { 303, ProfileStyleBorder.LatexBlack },
        { 304, ProfileStyleBorder.Slime },
        { 305, ProfileStyleBorder.Tape },
        { 306, ProfileStyleBorder.Belts },
        { 307, ProfileStyleBorder.Prison },
        { 308, ProfileStyleBorder.MedicalNurse },
        { 309, ProfileStyleBorder.Asylum },
        { 3010, ProfileStyleBorder.RedRoom },
        { 3011, ProfileStyleBorder.Office },
        { 3012, ProfileStyleBorder.School },
    };

    public static readonly Dictionary<uint, ProfileStyleOverlay> GagSlotOverlayMap = new()
    {
        { 0, ProfileStyleOverlay.Default },
        { 301, ProfileStyleOverlay.Ropes },
        { 302, ProfileStyleOverlay.Chains },
        { 303, ProfileStyleOverlay.LatexBlack },
        { 304, ProfileStyleOverlay.Slime },
        { 305, ProfileStyleOverlay.Prison },
        { 306, ProfileStyleOverlay.Hypnotic },
        { 307, ProfileStyleOverlay.MedicalNurse },
        { 308, ProfileStyleOverlay.School },
        { 309, ProfileStyleOverlay.Demonic },
        { 3010, ProfileStyleOverlay.Drool },
    };

    // Padlock
    public static readonly Dictionary<uint, ProfileStyleBG> PadlockBackgroundMap = new()
    {
        { 0, ProfileStyleBG.Default},
        { 301, ProfileStyleBG.Ropes },
        { 302, ProfileStyleBG.Chains },
        { 303, ProfileStyleBG.LatexBlack },
        { 304, ProfileStyleBG.Slime },
        { 305, ProfileStyleBG.Tentacles },
        { 306, ProfileStyleBG.Tape },
        { 307, ProfileStyleBG.Belts },
        { 308, ProfileStyleBG.Prison },
        { 309, ProfileStyleBG.Hypnotic },
        { 3010, ProfileStyleBG.MedicalNurse },
        { 3011, ProfileStyleBG.Asylum },
        { 3012, ProfileStyleBG.RedRoom },
        { 3013, ProfileStyleBG.Office },
        { 3014, ProfileStyleBG.Church },
        { 3015, ProfileStyleBG.School },
        { 3016, ProfileStyleBG.Demonic },
    };

    public static readonly Dictionary<uint, ProfileStyleBorder> PadlockBorderMap = new()
    {
        { 0, ProfileStyleBorder.Default },
        { 301, ProfileStyleBorder.Ropes },
        { 302, ProfileStyleBorder.Chains },
        { 303, ProfileStyleBorder.LatexBlack },
        { 304, ProfileStyleBorder.Slime },
        { 305, ProfileStyleBorder.Tape },
        { 306, ProfileStyleBorder.Belts },
        { 307, ProfileStyleBorder.Prison },
        { 308, ProfileStyleBorder.MedicalNurse },
        { 309, ProfileStyleBorder.Asylum },
        { 3010, ProfileStyleBorder.RedRoom },
        { 3011, ProfileStyleBorder.Office },
        { 3012, ProfileStyleBorder.School },
    };

    public static readonly Dictionary<uint, ProfileStyleOverlay> PadlockOverlayMap = new()
    {
        { 0, ProfileStyleOverlay.Default },
        { 301, ProfileStyleOverlay.Ropes },
        { 302, ProfileStyleOverlay.Chains },
        { 303, ProfileStyleOverlay.LatexBlack },
        { 304, ProfileStyleOverlay.Slime },
        { 305, ProfileStyleOverlay.Prison },
        { 306, ProfileStyleOverlay.Hypnotic },
        { 307, ProfileStyleOverlay.MedicalNurse },
        { 308, ProfileStyleOverlay.School },
        { 309, ProfileStyleOverlay.Demonic },
        { 3010, ProfileStyleOverlay.Drool },
    };

    // BlockedSlots
    public static readonly Dictionary<uint, ProfileStyleBG> BlockedSlotsBackgroundMap = new()
    {
        { 0, ProfileStyleBG.Default},
        { 301, ProfileStyleBG.Ropes },
        { 302, ProfileStyleBG.Chains },
        { 303, ProfileStyleBG.LatexBlack },
        { 304, ProfileStyleBG.Slime },
        { 305, ProfileStyleBG.Tentacles },
        { 306, ProfileStyleBG.Tape },
        { 307, ProfileStyleBG.Belts },
        { 308, ProfileStyleBG.Prison },
        { 309, ProfileStyleBG.Hypnotic },
        { 3010, ProfileStyleBG.MedicalNurse },
        { 3011, ProfileStyleBG.Asylum },
        { 3012, ProfileStyleBG.RedRoom },
        { 3013, ProfileStyleBG.Office },
        { 3014, ProfileStyleBG.Church },
        { 3015, ProfileStyleBG.School },
        { 3016, ProfileStyleBG.Demonic },
    };

    public static readonly Dictionary<uint, ProfileStyleBorder> BlockedSlotsBorderMap = new()
    {
        { 0, ProfileStyleBorder.Default },
        { 301, ProfileStyleBorder.Ropes },
        { 302, ProfileStyleBorder.Chains },
        { 303, ProfileStyleBorder.LatexBlack },
        { 304, ProfileStyleBorder.Slime },
        { 305, ProfileStyleBorder.Tape },
        { 306, ProfileStyleBorder.Belts },
        { 307, ProfileStyleBorder.Prison },
        { 308, ProfileStyleBorder.MedicalNurse },
        { 309, ProfileStyleBorder.Asylum },
        { 3010, ProfileStyleBorder.RedRoom },
        { 3011, ProfileStyleBorder.Office },
        { 3012, ProfileStyleBorder.School },
    };

    public static readonly Dictionary<uint, ProfileStyleOverlay> BlockedSlotsOverlayMap = new()
    {
        { 0, ProfileStyleOverlay.Default },
        { 301, ProfileStyleOverlay.Ropes },
        { 302, ProfileStyleOverlay.Chains },
        { 303, ProfileStyleOverlay.LatexBlack },
        { 304, ProfileStyleOverlay.Slime },
        { 305, ProfileStyleOverlay.Prison },
        { 306, ProfileStyleOverlay.Hypnotic },
        { 307, ProfileStyleOverlay.MedicalNurse },
        { 308, ProfileStyleOverlay.School },
        { 309, ProfileStyleOverlay.Demonic },
        { 3010, ProfileStyleOverlay.Drool },
    };

    // BlockedSlot
    public static readonly Dictionary<uint, ProfileStyleBorder> BlockedSlotBorderMap = new()
    {
        { 0, ProfileStyleBorder.Default },
        { 301, ProfileStyleBorder.Ropes },
        { 302, ProfileStyleBorder.Chains },
        { 303, ProfileStyleBorder.LatexBlack },
        { 304, ProfileStyleBorder.Slime },
        { 305, ProfileStyleBorder.Tape },
        { 306, ProfileStyleBorder.Belts },
        { 307, ProfileStyleBorder.Prison },
        { 308, ProfileStyleBorder.MedicalNurse },
        { 309, ProfileStyleBorder.Asylum },
        { 3010, ProfileStyleBorder.RedRoom },
        { 3011, ProfileStyleBorder.Office },
        { 3012, ProfileStyleBorder.School },
    };

    public static readonly Dictionary<uint, ProfileStyleOverlay> BlockedSlotOverlayMap = new()
    {
        { 0, ProfileStyleOverlay.Default },
        { 301, ProfileStyleOverlay.Ropes },
        { 302, ProfileStyleOverlay.Chains },
        { 303, ProfileStyleOverlay.LatexBlack },
        { 304, ProfileStyleOverlay.Slime },
        { 305, ProfileStyleOverlay.Prison },
        { 306, ProfileStyleOverlay.Hypnotic },
        { 307, ProfileStyleOverlay.MedicalNurse },
        { 308, ProfileStyleOverlay.School },
        { 309, ProfileStyleOverlay.Demonic },
        { 3010, ProfileStyleOverlay.Drool },
    };
    #endregion
}
