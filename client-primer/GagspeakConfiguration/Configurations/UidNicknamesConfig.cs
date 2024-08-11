using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class UidNicknamesConfig : IGagspeakConfiguration
{
    public ServerNicknamesStorage ServerNicknames { get; set; }
    public static int CurrentVersion => 1;
    public int Version { get; set; } = CurrentVersion;
}
