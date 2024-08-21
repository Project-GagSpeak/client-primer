using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public record Alarm
{
    public bool Enabled { get; set; } = false;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset SetTimeUTC { get; set; } = DateTimeOffset.MinValue;
    public string PatternToPlay { get; set; } = string.Empty;
    public TimeSpan PatternStartPoint { get; set; } = TimeSpan.Zero;
    public TimeSpan PatternDuration { get; set; } = TimeSpan.Zero;
    public List<DayOfWeek> RepeatFrequency { get; set; } = [];
}
