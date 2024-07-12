using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class UidNicknamesConfig : IGagspeakConfiguration
{
    public ServerNicknamesStorage ServerNicknames { get; set; }
    public int Version { get; set; } = 0;
}
