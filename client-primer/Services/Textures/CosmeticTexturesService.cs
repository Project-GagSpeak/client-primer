using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using ImGuiNET;

//using ImGuiScene;
using OtterGui;
using OtterGui.Classes;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Numerics;

// pulled from glamourer pretty much 1:1... optimize later.
namespace GagSpeak.Services.Textures;

/// <summary>
/// For handling the retrieval and display of profile textures & cosmetics for the plugin.
/// </summary>
public sealed class CosmeticTexturesService(ITextureProvider TextureProvider, IDalamudPluginInterface Pi)
{

    /// <summary> Fetch TextureWrap from downloaded image file path. </summary>
    public IDalamudTextureWrap? GetImageFromDirectoryFile(string path)
    {
        var texture = TextureProvider.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!, path));

        if (!texture.TryGetWrap(out var wrap, out _))
            return null;

        return texture.GetWrapOrDefault();
    }


    /// <summary> Obtain the profile image from the byte array. </summary>
    public IDalamudTextureWrap GetProfileImageFromData(byte[] imageData)
    {
        return TextureProvider.CreateFromImageAsync(imageData).Result;
    }
}
