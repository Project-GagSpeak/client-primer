using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data.IPC;
using Microsoft.Extensions.Hosting;
using System.ComponentModel;

namespace GagSpeak.Services.Textures;

// Friendly Reminded, this is a scoped service, and IDalamudTextureWraps will only return values on the framework thread.
// Attempting to use or access this class to obtain information outside the framework draw update thread will result in a null return.
public class CosmeticService : IHostedService, IDisposable
{
    private readonly ILogger<CosmeticService> _logger;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly ITextureProvider _textures;
    private readonly IDalamudPluginInterface _pi;

    // This is shared across all states of our plugin, so should attach to the one in UISharedService
    private ISharedImmediateTexture _sharedTextures;

    public CosmeticService(ILogger<CosmeticService> logger, GagspeakMediator mediator,
        OnFrameworkService frameworkUtils, IDalamudPluginInterface pi, ITextureProvider tp)
    {
        _logger = logger;
        _frameworkUtils = frameworkUtils;
        _textures = tp;
        _pi = pi;
    }

    private Dictionary<string, IDalamudTextureWrap> InternalCosmeticCache = [];
    public Dictionary<CorePluginTexture, IDalamudTextureWrap> CorePluginTextures = [];

    // MUST ensure ALL images are disposed of or else we will leak a very large amount of memory.
    public void Dispose()
    {
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Disposing.");
        foreach (var texture in CorePluginTextures.Values)
            texture?.Dispose();
        foreach (var texture in InternalCosmeticCache.Values)
            texture?.Dispose();

        // clear the dictionary, erasing all disposed textures.
        CorePluginTextures.Clear();
        InternalCosmeticCache.Clear();
    }

    public void LoadAllCoreTextures()
    {
        foreach (var label in CosmeticLabels.NecessaryImages)
        {
            var key = label.Key;
            var path = label.Value;
            if (string.IsNullOrEmpty(path))
            {
                _logger.LogError("Cosmetic Key: " + key + " Texture Path is Empty.");
                return;
            }

            _logger.LogTrace("Renting image to store in Cache: " + key);
            if(TryRentImageFromFile(path, out var texture))
                CorePluginTextures[key] = texture;
        }
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Fetched all NecessaryImages!");
    }

    public void LoadAllCosmetics()
    {
        // load in all the images to the dictionary by iterating through all public const strings stored in the cosmetic labels and appending them as new texture wraps that should be stored into the cache.
        foreach (var label in CosmeticLabels.CosmeticTextures)
        {
            var key = label.Key;
            var path = label.Value;
            if (string.IsNullOrEmpty(path))
            {
                _logger.LogError("Cosmetic Key: " + key + " Texture Path is Empty.");
                return;
            }

            _logger.LogTrace("Renting image to store in Cache: " + key);
            if (TryRentImageFromFile(path, out var texture))
            {
                InternalCosmeticCache[key] = texture;
            }
            else
            {
                // This will make the key for the dictionary no longer exist, which is why we need the TryGetValue functions.
                _logger.LogWarning("Cosmetic Key: " + key + " Texture Failed to Load: " + path);
                continue;
            }
        }
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Fetched all Cosmetic Images!");

    }

    /// <summary>
    /// Grabs the texture from GagSpeak Cosmetic Cache Service, if it exists.
    /// </summary>
    /// <returns>True if the texture is valid, false otherwise. If returning false, the wrap WILL BE NULL. </returns>
    public bool TryGetBackground(ProfileComponent section, ProfileStyleBG style, out IDalamudTextureWrap value)
    {
        // See if the item exists in our GagSpeak Cache Service.
        if(InternalCosmeticCache.TryGetValue(section.ToString() + "_Background_" + style.ToString(), out var texture))
        {
            value = texture;
            return true;
        }
        // not valid, so return false.
        value = null!;
        return false;
    }

    /// <summary>
    /// Grabs the texture from GagSpeak Cosmetic Cache Service, if it exists.
    /// </summary>
    /// <returns>True if the texture is valid, false otherwise. If returning false, the wrap WILL BE NULL. </returns>
    public bool TryGetBorder(ProfileComponent section, ProfileStyleBorder style, out IDalamudTextureWrap value)
    {
        if(InternalCosmeticCache.TryGetValue(section.ToString() + "_Border_" + style.ToString(), out var texture))
        {
            value = texture;
            return true;
        }
        value = null!;
        return false;
    }

    /// <summary>
    /// Grabs the texture from GagSpeak Cosmetic Cache Service, if it exists.
    /// </summary>
    /// <returns>True if the texture is valid, false otherwise. If returning false, the wrap WILL BE NULL. </returns>
    public bool TryGetOverlay(ProfileComponent section, ProfileStyleOverlay style, out IDalamudTextureWrap value)
    {
        if(InternalCosmeticCache.TryGetValue(section.ToString() + "_Overlay_" + style.ToString(), out var texture))
        {
            value = texture;
            return true;
        }
        value = null!;
        return false;
    }

    public IDalamudTextureWrap GetImageFromDirectoryFile(string path)
        => _textures.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "Assets", path)).GetWrapOrEmpty();
    public IDalamudTextureWrap GetProfilePicture(byte[] imageData)
        => _textures.CreateFromImageAsync(imageData).Result;

    private bool TryRentImageFromFile(string path, out IDalamudTextureWrap fileTexture)
    {
        try
        {
            var image = _textures.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "Assets", path)).RentAsync().Result;
            fileTexture = image;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to load texture from path: {path}");
            fileTexture = null!;
            return false;
        }
    }


    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Started.");
        LoadAllCoreTextures();
        LoadAllCosmetics();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Stopped.");
        return Task.CompletedTask;
    }

}


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
        AddEntriesForComponent(dictionary, ProfileComponent.ProfilePicture, hasBackground: false, hasBorder: true, hasOverlay: true);
        AddEntriesForComponent(dictionary, ProfileComponent.Description, hasBackground: true, hasBorder: true, hasOverlay: true);
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
}
