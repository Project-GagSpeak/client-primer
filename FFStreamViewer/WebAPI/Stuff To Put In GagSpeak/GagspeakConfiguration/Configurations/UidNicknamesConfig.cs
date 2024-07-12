using FFStreamViewer.WebAPI.GagspeakConfiguration.Models;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

public class UidNicknamesConfig : IGagspeakConfiguration
{
    public ServerNicknamesStorage ServerNicknames { get; set; }
    public int Version { get; set; } = 0;
}
