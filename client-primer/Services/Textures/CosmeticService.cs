using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Achievements;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data;
using GagspeakAPI.Data.IPC;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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
    public List<ProfileStyleBG> UnlockedPlateBackgrounds { get; private set; } = new();
    public List<ProfileStyleBorder> UnlockedPlateBorders { get; private set; } = new();
    public List<ProfileStyleBorder> UnlockedProfilePictureBorder { get; private set; } = new();
    public List<ProfileStyleOverlay> UnlockedProfilePictureOverlay { get; private set; } = new();
    public List<ProfileStyleBG> UnlockedDescriptionBackground { get; private set; } = new();
    public List<ProfileStyleBorder> UnlockedDescriptionBorder { get; private set; } = new();
    public List<ProfileStyleOverlay> UnlockedDescriptionOverlay { get; private set; } = new();
    public List<ProfileStyleBG> UnlockedGagSlotBackground { get; private set; } = new();
    public List<ProfileStyleBorder> UnlockedGagSlotBorder { get; private set; } = new();
    public List<ProfileStyleOverlay> UnlockedGagSlotOverlay { get; private set; } = new();
    public List<ProfileStyleBG> UnlockedPadlockBackground { get; private set; } = new();
    public List<ProfileStyleBorder> UnlockedPadlockBorder { get; private set; } = new();
    public List<ProfileStyleOverlay> UnlockedPadlockOverlay { get; private set; } = new();
    public List<ProfileStyleBG> UnlockedBlockedSlotsBackground { get; private set; } = new();
    public List<ProfileStyleBorder> UnlockedBlockedSlotsBorder { get; private set; } = new();
    public List<ProfileStyleOverlay> UnlockedBlockedSlotsOverlay { get; private set; } = new();
    public List<ProfileStyleBorder> UnlockedBlockedSlotBorder { get; private set; } = new();
    public List<ProfileStyleOverlay> UnlockedBlockedSlotOverlay { get; private set; } = new();

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

            _logger.LogTrace("Renting image to store in Cache: " + key, LoggerType.Textures);
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

            _logger.LogTrace("Renting image to store in Cache: " + key, LoggerType.Textures);
            if (TryRentImageFromFile(path, out var texture))
                InternalCosmeticCache[key] = texture;
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

    public (IDalamudTextureWrap? SupporterWrap, string Tooltip) GetSupporterInfo(UserData userData)
    {
        IDalamudTextureWrap? supporterWrap = null;
        string tooltipString = string.Empty;

        switch (userData.SupporterTier)
        {
            case CkSupporterTier.ServerBooster:
                supporterWrap = CorePluginTextures[CorePluginTexture.SupporterBooster];
                tooltipString = userData.AliasOrUID + " is supporting the discord with a server Boost!";
                break;

            case CkSupporterTier.IllustriousSupporter:
                supporterWrap = CorePluginTextures[CorePluginTexture.SupporterTier1];
                tooltipString = userData.AliasOrUID + " is supporting CK as an Illustrious Supporter";
                break;

            case CkSupporterTier.EsteemedPatron:
                supporterWrap = CorePluginTextures[CorePluginTexture.SupporterTier2];
                tooltipString = userData.AliasOrUID + " is supporting CK as an Esteemed Patron";
                break;

            case CkSupporterTier.DistinguishedConnoisseur:
                supporterWrap = CorePluginTextures[CorePluginTexture.SupporterTier3];
                tooltipString = userData.AliasOrUID + " is supporting CK as a Distinguished Connoisseur";
                break;

            case CkSupporterTier.KinkporiumMistress:
                supporterWrap = CorePluginTextures[CorePluginTexture.SupporterTier4];
                tooltipString = userData.AliasOrUID + " is the Shop Mistress of CK, and the Dev of GagSpeak.";
                break;

            default:
                tooltipString = userData.AliasOrUID + " has an unknown supporter tier.";
                break;
        }

        return (supporterWrap, tooltipString);
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
            // TODO: Remove surpression once we have defined proper images.
            //_logger.LogWarning($"Failed to load texture from path: {path}");
            fileTexture = null!;
            return false;
        }
    }

    public void RecalculateUnlockedItems()
    {
        var completedAchievementIds = AchievementManager.CompletedAchievements.Select(x => x.AchievementId).ToHashSet();
        completedAchievementIds.Add(0); // Add the default achievement to the list.

        UnlockedPlateBackgrounds = CosmeticLabels.PlateBackgroundMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedPlateBorders = CosmeticLabels.PlateBorderMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedProfilePictureBorder = CosmeticLabels.ProfilePictureBorderMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedProfilePictureOverlay = CosmeticLabels.ProfilePictureOverlayMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedDescriptionBackground = CosmeticLabels.DescriptionBackgroundMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedDescriptionBorder = CosmeticLabels.DescriptionBorderMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedDescriptionOverlay = CosmeticLabels.DescriptionOverlayMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedGagSlotBackground = CosmeticLabels.GagSlotBackgroundMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedGagSlotBorder = CosmeticLabels.GagSlotBorderMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedGagSlotOverlay = CosmeticLabels.GagSlotOverlayMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedPadlockBackground = CosmeticLabels.PadlockBackgroundMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedPadlockBorder = CosmeticLabels.PadlockBorderMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedPadlockOverlay = CosmeticLabels.PadlockOverlayMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedBlockedSlotsBackground = CosmeticLabels.BlockedSlotsBackgroundMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedBlockedSlotsBorder = CosmeticLabels.BlockedSlotsBorderMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedBlockedSlotsOverlay = CosmeticLabels.BlockedSlotsOverlayMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedBlockedSlotBorder = CosmeticLabels.BlockedSlotBorderMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();

        UnlockedBlockedSlotOverlay = CosmeticLabels.BlockedSlotOverlayMap
            .Where(kvp => completedAchievementIds.Contains(kvp.Key)).Select(kvp => kvp.Value)
            .ToList();
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
