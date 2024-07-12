using FFStreamViewer.WebAPI.GagspeakConfiguration.Models;
using Gagspeak.API.Data;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration.Models;

/// <summary>
/// Contains a list of alias triggers for a spesified user
/// </summary>
[Serializable]
public class AliasStorage
{
    /// <summary> The storage of all aliases you have for the spesified user (defined in key) </summary>
    public List<AliasTrigger> AliasList { get; set; } = []; 
}
