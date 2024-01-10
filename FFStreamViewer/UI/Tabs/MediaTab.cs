using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui;
using OtterGui.Widgets;
using FFStreamViewer.Services;

namespace FFStreamViewer.UI.Tabs.MediaTab;
/// <summary> This class is used to handle the general tab for the FFStreamViewer plugin. </summary>
public class MediaTab : ITab, IDisposable
{
    private readonly FFStreamViewerConfig         _config;                    // the config for the plugin
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaTab"/> class.
    /// </summary>
    public MediaTab(FFStreamViewerConfig config) {
        _config = config;

    }

    // Apply our lable for the tab
    public ReadOnlySpan<byte> Label => "General"u8;

    /// <summary>
    /// This function is called when the tab is disposed.
    /// </summary>
    public void Dispose() { 
        // Unsubscribe from any events
    }

    /// <summary>
    /// This Function draws the content for the window of the General Tab
    /// </summary>
    public void DrawContent() {
        // Definitely need to refine the ImGui code here, but this is a good start.
        // Create a child for the Main Window (not sure if we need this without a left selection panel)
        using var child = ImRaii.Child("MainWindowChild");
        if (!child)
            return;

        // Draw the child grouping for the ConfigSettings Tab
        using (var child2 = ImRaii.Child("MediaTabChild")) {
            DrawHeader();
            DrawGeneral();
        }
    }

    /// <summary>
    /// This function draws the header for the window of the General Tab
    /// </summary>
    private void DrawHeader()
        => WindowHeader.Draw("Gag Selections / Inspector", 0, ImGui.GetColorU32(ImGuiCol.FrameBg));

    /// <summary>
    /// This function draws the general tab contents
    /// </summary>
    private void DrawGeneral() {
        // let people know which gags are not working
        ImGui.Text("These Gags dont work yet! If you have any IRL, contact me to help fill in the data!");
        ImGui.TextColored(new Vector4(0,1,0,1), "Bit Gag Padded, Bone Gag, Bone Gag XL, Chopstick Gag, Dental Gag,\n"+
                                                "Harness Panel Gag, Hook Gag, Inflatable Hood, Latex/Leather Hoods, Plug Gag\n"+
                                                "Pump Gag, Sensory Deprivation Hood, Spider Gag, Tenticle Gag.");
    }
}