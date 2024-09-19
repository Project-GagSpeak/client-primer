using GagspeakAPI.Data;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public record Alarm
{
    public Guid Identifier { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = false;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset SetTimeUTC { get; set; } = DateTimeOffset.MinValue;
    public Guid PatternToPlay { get; set; } = Guid.Empty;
    public TimeSpan PatternStartPoint { get; set; } = TimeSpan.Zero;
    public TimeSpan PatternDuration { get; set; } = TimeSpan.Zero;
    public List<DayOfWeek> RepeatFrequency { get; set; } = [];
}
