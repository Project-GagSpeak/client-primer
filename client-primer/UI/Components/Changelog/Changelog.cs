using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Components;
public class Changelog
{
    public List<VersionEntry> Versions { get; private set; } = new List<VersionEntry>();

    public Changelog()
    {
        // append the version information here.
        AddVersionData();
    }

    public VersionEntry VersionEntry(int versionMajor, int versionMinor, int minorUpdate, int updateImprovements)
    {
        var entry = new VersionEntry(versionMajor, versionMinor, minorUpdate, updateImprovements);
        Versions.Add(entry);
        return entry;
    }

    private void AddVersionData()
    {
        // Add Version Data here.
        VersionEntry(0, 7, 2, 3)
            .RegisterMain("Im a silly goober and tried to open the changelog before it was registered.");

        VersionEntry(0, 7, 2, 2)
            .RegisterMain("Added Changelog Window")
            .RegisterFeature("New PatternHub tab is available in the main window. (Currently has no functionality)")
            .RegisterQol("Helmet and Visor toggles are implemented, but will not work until Buttplug.IO updated to 3.0.2 (somepoint this weekend)")
            .RegisterBugfix("Fixed an issue where slots outside the helmet slot would not unequip the glamourerd item when unequipped via the active slot panel.");
    }
}
