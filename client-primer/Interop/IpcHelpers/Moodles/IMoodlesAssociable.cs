namespace GagSpeak.Interop.IpcHelpers.Moodles;

public interface IMoodlesAssociable
{
    List<Guid> AssociatedMoodles { get; }
    List<Guid> AssociatedMoodlePresets { get; }
}
