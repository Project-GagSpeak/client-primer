﻿using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using FFStreamViewer.Services;
using FFStreamViewer.UI.Helpers;

namespace FFStreamViewer.UI;
/// <summary> This class is used to show the debug menu in its own window. </summary>
public class DebugWindow : Window //, IDisposable
{
    private readonly FFStreamViewerConfig         _config;                        // for retrieving the config data to display to the window

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryWindow"/> class.
    /// </summary>
    public DebugWindow(DalamudPluginInterface pluginInt, FFStreamViewerConfig config) : base(GetLabel()) {
        // Let's first make sure that we disable the plugin while inside of gpose.
        pluginInt.UiBuilder.DisableGposeUiHide = true;
        // Next let's set the size of the window
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(300, 400),     // Minimum size of the window
            MaximumSize = ImGui.GetIO().DisplaySize, // Maximum size of the window
        };
        _config = config;
    }

    /// <summary> This function is used to draw the history window. </summary>
    public override void Draw() {
        DrawDebugInformation();
    }

    // basic string function to get the label of title for the window
    private static string GetLabel() => "FFStreamViewerDebug###FFStreamViewerDebug";    

    /// <summary>
    /// Draws the debug information.
    /// </summary>
    public void DrawDebugInformation() {
        if(!ImGui.CollapsingHeader("DEBUG INFORMATION")) { return; }
        try
        {
            // General plugin information
            ImGui.Text($"Fresh Install?: {_config.FreshInstall}");
        } 
        catch (Exception e) {
            FFStreamViewer.Log.Error($"Error while fetching config in debug: {e}");
        }
    }
}

