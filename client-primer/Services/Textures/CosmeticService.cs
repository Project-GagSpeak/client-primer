using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data.IPC;
using Microsoft.Extensions.Hosting;

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

        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Initializing:" + Environment.StackTrace);
    }

    // we need to store a local static cache of our image data so
    // that they can load instantly whenever required.
    public Dictionary<string, IDalamudTextureWrap> InternalCosmeticCache = [];

    public Dictionary<CorePluginTexture, IDalamudTextureWrap> CorePluginTextures = [];

    // MUST ensure ALL images are disposed of or else we will leak a very large amount of memory.
    public void Dispose()
    {
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Disposing.");
        foreach (var texture in CorePluginTextures.Values)
        {
            texture?.Dispose();
            // if we run into issues with this not going to null, a null should have been here.
        }
        foreach (var texture in InternalCosmeticCache.Values)
        {
            texture?.Dispose();
            // if we run into issues with this not going to null, a null should have been here.
        }
        // clear the dictionary, erasing all disposed textures.
        CorePluginTextures.Clear();
        InternalCosmeticCache.Clear();
    }

    public async Task LoadAllCoreTextures()
    {
        foreach (var label in CosmeticLabels.NecessaryImages)
        {
            var key = label.Key;
            var path = label.Value;
            _logger.LogInformation("Cosmetic Key: " + key);

            if (string.IsNullOrEmpty(path))
            {
                _logger.LogError("Cosmetic Key: " + key + " Texture Path is Empty.");
                return;
            }

            _logger.LogInformation("Renting image to store in Cache: " + key);
            var texture = RentImageFromFile(path);
            if (texture != null)
            {
                _logger.LogInformation("Cosmetic Key: " + key + " Texture Loaded Successfully: " + path);
                CorePluginTextures[key] = texture;
            }
            else
            {
                _logger.LogError("Cosmetic Key: " + key + " Texture Failed to Load: " + path);
            }
        }
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Fetched all NecessaryImages!");
    }

    public async Task LoadAllCosmetics()
    {
        // load in all the images to the dictionary by iterating through all public const strings stored in the cosmetic labels and appending them as new texture wraps that should be stored into the cache.
        foreach (var label in CosmeticLabels.Labels)
        {
            var key = label.Key;
            var path = label.Value;
            _logger.LogInformation("Cosmetic Key: " + key);

            if (string.IsNullOrEmpty(path))
            {
                _logger.LogError("Cosmetic Key: " + key + " Texture Path is Empty.");
                return;
            }

            _logger.LogInformation("Renting image to store in Cache: " + key);
            var texture = RentImageFromFile(path);
            if (texture != null)
            {
                _logger.LogInformation("Cosmetic Key: " + key + " Texture Loaded Successfully: " + path);
                InternalCosmeticCache[key] = texture;
            }
            else
            {
                _logger.LogError("Cosmetic Key: " + key + " Texture Failed to Load: " + path);
            }
            // Corby Note: If this is too much to handle in a single thread,
            // see if there is a way to batch send requests that can be returned overtime when retrieved.
            _logger.LogInformation("GagSpeak Profile Cosmetic Cache Fetched all Cosmetic Images!");
        }
    }

    public bool isImageValid(string keyName)
    {
        var texture = _sharedTextures.GetWrapOrDefault(InternalCosmeticCache[keyName]);
        if(texture == null) return false;
        return true;
    }

    /// <summary>
    /// Obtain an image from the assets folder as wrap or empty for display. This is NOT RENTED.
    /// </summary>
    public IDalamudTextureWrap GetImageFromDirectoryFile(string path)
        => _textures.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "Assets", path)).GetWrapOrEmpty();
    public IDalamudTextureWrap GetProfilePicture(byte[] imageData)
        => _textures.CreateFromImageAsync(imageData).Result;

    private IDalamudTextureWrap RentImageFromFile(string path)
        => _textures.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "Assets", path)).RentAsync().Result;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GagSpeak Profile Cosmetic Cache Started.");
        Task.Run(async () =>
        {
            await LoadAllCoreTextures();
            await LoadAllCosmetics();
        });
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
    public static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
    {
        { "DummyTest", "RequiredImages\\icon256bg.png" }, // Dummy File
    };

    public static readonly Dictionary<CorePluginTexture, string> NecessaryImages = new()
    {
        { CorePluginTexture.Logo256, "RequiredImages\\icon256.png" },
        { CorePluginTexture.Logo256bg, "RequiredImages\\icon256bg.png" },
        { CorePluginTexture.SupporterBooster, "RequiredImages\\BoosterIcon.png" },
        { CorePluginTexture.SupporterTier1, "RequiredImages\\Tier1Icon.png" },
        { CorePluginTexture.SupporterTier2, "RequiredImages\\Tier2Icon.png" },
        { CorePluginTexture.SupporterTier3, "RequiredImages\\Tier3Icon.png" },
        { CorePluginTexture.SupporterTier4, "RequiredImages\\Tier4Icon.png" }
    };
}
