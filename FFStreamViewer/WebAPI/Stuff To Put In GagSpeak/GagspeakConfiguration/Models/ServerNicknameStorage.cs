namespace FFStreamViewer.WebAPI.GagspeakConfiguration.Models;
public class ServerNicknamesStorage
{
    public Dictionary<string, string> UidServerComments { get; set; } = new(StringComparer.Ordinal);
}
