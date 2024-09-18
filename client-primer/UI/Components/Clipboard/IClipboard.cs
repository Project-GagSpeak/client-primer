using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
using ImGuiNET;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace GagSpeak.UI.Components.Clipboard;
/*public interface IClipboardHandler<T>
{
    void CopyToClipboard(T item);
    T ImportFromClipboard();
}


public class ClipboardHandler<T> : IClipboardHandler<T>
{
    public void CopyToClipboard(T item)
    {
        try
        {
            // Serialize the pattern to JSON
            string json = JsonConvert.SerializeObject(item);
            // Compress the JSON string
            var compressed = json.Compress(6);
            // Encode the compressed string to base64
            string base64Pattern = Convert.ToBase64String(compressed);
            // Copy the base64 string to the clipboard
            ClipboardHelper.CopyToClipboard(base64Pattern);
            _logger.LogInformation("Pattern data copied to clipboard.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to copy pattern data to clipboard: {ex.Message}");
        }
    }

    public T ImportFromClipboard()
    {
        try
        {
            // Get the JSON string from the clipboard
            string base64 = ImGui.GetClipboardText();
            // Deserialize the JSON string back to pattern data
            var bytes = Convert.FromBase64String(base64);
            // Decode the base64 string back to a regular string
            var version = bytes[0];
            version = bytes.DecompressToString(out var decompressed);
            // Deserialize the string back to pattern data
            T objectData = JsonConvert.DeserializeObject<T>(decompressed) ?? throw new Exception("Failed to deserialize pattern data from clipboard.");
            
            string baseName = _handler.EnsureUniqueName(pattern.Name);

            // Set the active pattern
            _logger.LogInformation("Set pattern data from clipboard");
            _handler.AddNewPattern(pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not set pattern data from clipboard.{ex.Message}");
        }
    }
}*/
