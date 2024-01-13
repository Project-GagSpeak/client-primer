﻿using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using FFStreamViewer.Services;
using FFStreamViewer.Utils;

namespace FFStreamViewer.UI;
/// <summary> This class is used to show the debug menu in its own window. </summary>
public class DebugWindow : Window //, IDisposable
{
    private readonly FFSV_Config         _config;                        // for retrieving the config data to display to the window

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryWindow"/> class.
    /// </summary>
    public DebugWindow(DalamudPluginInterface pluginInt, FFSV_Config config) : base(GetLabel()) {
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
        ImGui.Text($"Version: {_config.Version}");
        ImGui.Text($"Fresh Install?: {_config.FreshInstall}");
        ImGui.Separator();
        ImGui.Text($"Watch Link: {_config.WatchLink}");
        ImGui.Text($"IsStreamPlaying: {_config.IsStreamPlaying}");
        ImGui.Text($"Stream Resolution: {_config.StreamResolution}");
        ImGui.Text($"Last Stream URL: {_config.LastStreamURL}");
        ImGui.Text($"FPS Count: {_config.FPSCount}");
        ImGui.Text($"Counted Frames: {_config.CountedFrames}");
        ImGui.Text($"Was Streaming: {_config.WasStreaming}");
        ImGui.Separator();
        ImGui.Text($"ChangeLogDisplayType: {_config.ChangeLogDisplayType}");
        ImGui.Text($"LastSeenVersion: {_config.LastSeenVersion}");
        ImGui.Text($"Enabled: {_config.Enabled}");
        ImGui.Separator();
        ImGui.Text($"MainPlayerVolume: {_config.MainPlayerVolume}");
        ImGui.Text($"OtherPlayerVolume: {_config.OtherPlayerVolume}");
        ImGui.Text($"UnfocusedPlayerVolume: {_config.UnfocusedPlayerVolume}");
        ImGui.Text($"SfxVolume: {_config.SfxVolume}");
        ImGui.Text($"LiveStreamVolume: {_config.LiveStreamVolume}");
        ImGui.Text($"LowPerformanceMode: {_config.LowPerformanceMode}");
        ImGui.Text($"LibVLCPath: {_config.LibVLCPath}");
        ImGui.Separator();
        ImGui.Text($"SoundPath: {_config.SoundPath}");
        ImGui.Text($"OffsetVolume: {_config.OffsetVolume}");
    }
}

