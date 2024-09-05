namespace GagSpeak.UI.Components;

public interface IMoodlesAssociable
{
    List<Guid> AssociatedMoodles { get; }
    List<Guid> AssociatedMoodlePresets { get; }
}
