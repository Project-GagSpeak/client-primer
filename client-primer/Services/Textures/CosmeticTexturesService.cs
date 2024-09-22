using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GagSpeak.Services.Textures;

/// <summary>
/// GagSpeaks internal Storage for all rented images to be loaded during the plugins lifetime as a cached storage, 
/// You MUST remove these during plugin disposed.
/// 
/// MUST define as a scoped class to hold the data internally. (i think?) 
/// if not must be stored as a singleton or internal... look into later.
/// </summary>
public class CosmeticTexturesService
{
    // future Corby problem.
}
