using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Widgets;

namespace FFStreamViewer.UI.Tabs.MediaTab;
/// <summary> This class is used to handle the general tab for the FFStreamViewer plugin. </summary>
public class WebAPITestingTab : ITab, IDisposable
{
    public WebAPITestingTab() { }

    /// <summary> This function is called when the tab is disposed. </summary>
    public void Dispose()
    {
        // Unsubscribe from any events
    }

    /// <summary> This Function draws the content for the Media Tab </summary>
    public void DrawContent()
    {
        // Create a child for the Main Window (not sure if we need this without a left selection panel)
        using var child = ImRaii.Child("MainWindowChild");
        if (!child)
            return;
        // Draw the child grouping for the ConfigSettings Tab
        using (var child2 = ImRaii.Child("MediaTabChild"))
        {
            DrawHeader();
            DrawGeneral();
        }
    }

    private void DrawHeader()
        => WindowHeader.Draw("Web API Test Center", 0, ImGui.GetColorU32(ImGuiCol.FrameBg));

    /// <summary> This function draws the media tab contents 
    /// <para> FUTURE CONCEPT: Have a dropdown to spesify certain stream link types.
    /// However, this leads to complications </para> 
    /// </summary>
    private void DrawGeneral()
    {
        ImGui.Text("hi");
    }

    public ReadOnlySpan<byte> Label => "WebAPITesting"u8;
}
