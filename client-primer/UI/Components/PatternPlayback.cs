using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Debouncer;
using GagSpeak.Toybox.Services;
using ImGuiNET;
using ImPlotNET;
using System.Timers;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.UI.Components;

/// <summary>
/// Manages playing back patterns to the client user's connected devices.
/// </summary>
public class PatternPlayback : DisposableMediatorSubscriberBase
{
    private readonly ToyboxRemoteService _remoteService;
    private readonly ToyboxVibeService _vibeService;
    private readonly PatternPlaybackService _playbackService;

    public Stopwatch PlaybackDuration;
    private UpdateTimer PlaybackUpdateTimer;
    // private accessors for the playback only
    private double[] CurrentPositions = new double[2];

    public PatternPlayback(ILogger<PatternPlayback> logger,
        GagspeakMediator mediator, ToyboxRemoteService remoteService, 
        ToyboxVibeService vibeService, PatternPlaybackService playbackService) 
        : base(logger, mediator)
    {
        _remoteService = remoteService;
        _vibeService = vibeService;
        _playbackService = playbackService;

        PlaybackDuration = new Stopwatch();
        PlaybackUpdateTimer = new UpdateTimer(20, ReadVibePosFromBuffer);

        Mediator.Subscribe<PlaybackStateToggled>(this, (msg) =>
        {
            if(msg.NewState == NewState.Enabled)
            {
                StartPlayback();
            }
            if(msg.NewState == NewState.Disabled)
            {
                StopPlayback(msg.PatternId);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // dispose of the timers
        PlaybackUpdateTimer.Dispose();
        PlaybackDuration.Stop();
        PlaybackDuration.Reset();
    }

    public void DrawPlaybackDisplay()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(0, 0)).Push(ImGuiStyleVar.CellPadding, new Vector2(0, 0));
        using var child = ImRaii.Child("##PatternPlaybackChild", new Vector2(ImGui.GetContentRegionAvail().X, 80), true, ImGuiWindowFlags.NoScrollbar);
        if (!child) { return; }
        try
        {
            // Draw the waveform
            float[] xs;  // x-values
            float[] ys;  // y-values
                         // if we are playing back
            if (_playbackService.PlaybackActive && _playbackService.ShouldRunPlayback)
            {
                int start = Math.Max(0, ReadBufferIdx - 150);
                int count = Math.Min(150, ReadBufferIdx - start + 1);
                int buffer = 150 - count; // The number of extra values to display at the end


                xs = Enumerable.Range(-buffer, count + buffer).Select(i => (float)i).ToArray();
                ys = _playbackService.PlaybackByteRange.Skip(_playbackService.PlaybackByteRange.Count - buffer).Take(buffer)
                    .Concat(_playbackService.PlaybackByteRange.Skip(start).Take(count))
                    .Select(pos => (float)pos).ToArray();

                // Transform the x-values so that the latest position appears at x=0
                for (int i = 0; i < xs.Length; i++)
                {
                    xs[i] -= ReadBufferIdx;
                }
            }
            else
            {
                xs = new float[0];
                ys = new float[0];
            }
            float latestX = xs.Length > 0 ? xs[xs.Length - 1] : 0; // The latest x-value
                                                                   // Transform the x-values so that the latest position appears at x=0
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] -= latestX;
            }

            // get the xpos so we can draw it back a bit to span the whole width
            var xPos = ImGui.GetCursorPosX();
            var yPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(xPos - ImGuiHelpers.GlobalScale * 10, yPos - ImGuiHelpers.GlobalScale * 10));
            var width = ImGui.GetContentRegionAvail().X + ImGuiHelpers.GlobalScale * 10;
            // set up the color map for our plots.
            ImPlot.PushStyleColor(ImPlotCol.Line, _remoteService.LushPinkLine);
            ImPlot.PushStyleColor(ImPlotCol.PlotBg, _remoteService.LovenseScrollingBG);
            // draw the waveform
            ImPlot.SetNextAxesLimits(-150, 0, -5, 110, ImPlotCond.Always);
            if (ImPlot.BeginPlot("##Waveform", new System.Numerics.Vector2(width, 100), ImPlotFlags.NoBoxSelect | ImPlotFlags.NoMenus
            | ImPlotFlags.NoLegend | ImPlotFlags.NoFrame))
            {
                ImPlot.SetupAxes("X Label", "Y Label",
                    ImPlotAxisFlags.NoGridLines | ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels | ImPlotAxisFlags.NoTickMarks | ImPlotAxisFlags.NoHighlight,
                    ImPlotAxisFlags.NoGridLines | ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels | ImPlotAxisFlags.NoTickMarks);
                if (xs.Length > 0 || ys.Length > 0)
                {
                    ImPlot.PlotLine("Recorded Positions", ref xs[0], ref ys[0], xs.Length);
                }
                ImPlot.EndPlot();
            }
            ImPlot.PopStyleColor(2);
        }
        catch (Exception e)
        {
            Logger.LogError($"{e} Error drawing the toybox workshop subtab");
        }
    }
    #region Helper Fuctions
    // When active, the circle will not fall back to the 0 coordinate on the Y axis of the plot, and remain where it is
    public void StartPlayback()
    {
        if(_playbackService.ActivePattern == null)
        {
            Logger.LogWarning("It should be impossible to reach here. if you do, "+ 
                "there is a massive issue with the code. Report it ASAP");
            return;
        }
        // start a new one
        Logger.LogDebug($"Starting playback of pattern {_playbackService.ActivePattern?.Name}");
        
        // set the playback index to the start
        ReadBufferIdx = 0;

        // iniitalize volume levels if using simulated vibe
        if (_vibeService.UsingSimulatedVibe) InitializeVolumeLevels(_playbackService.PlaybackByteRange);

        // start our timers
        PlaybackDuration.Start();
        PlaybackUpdateTimer.Start();

        // begin playing the pattern to the vibrators
        _vibeService.StartActiveVibes();
    }
    public void InitializeVolumeLevels(List<byte> intensityPattern)
    {
        volumeLevels.Clear();
        foreach (var intensity in intensityPattern)
        {
            // Assuming intensity is a value between 0 and 100
            float volume = intensity / 100f;
            volumeLevels.Add(volume);
        }
    }
    private List<float> volumeLevels = new List<float>();

    public void StopPlayback(Guid patternIdentifier)
    {
        Logger.LogDebug($"Stopping playback of pattern {_playbackService.GetPatternNameFromGuid(patternIdentifier)}");
        // clear the local variables
        ReadBufferIdx = 0;
        // reset the timers
        PlaybackUpdateTimer.Stop();
        PlaybackDuration.Stop();
        PlaybackDuration.Reset();
        // reset vibe to normal levels TODO figure out how to go back to normal levels
        _vibeService.StopActiveVibes();

    }

    private int ReadBufferIdx;  // The current index of the playback
    private void ReadVibePosFromBuffer(object? sender, ElapsedEventArgs e)
    {
        // return if the playback is no longer active.
        if (!_playbackService.PlaybackActive) return;
        
        // If the new read buffer idx >= the total length of the list, stop playback, or loop to start.
        if (ReadBufferIdx >= _playbackService.PlaybackByteRange.Count)
        {
            // If we should loop, start it over again.
            if (_playbackService.ActivePattern!.ShouldLoop)
            {
                ReadBufferIdx = 0;
                PlaybackDuration.Restart();
                return;
            }
            // otherwise, stop.
            else
            {
                _playbackService.StopPattern(_playbackService.GetGuidOfActivePattern(), true);
                return;
            }
        }

        // Convert the current stored position to a float and store it in currentPos
        CurrentPositions[1] = _playbackService.PlaybackByteRange[ReadBufferIdx];

        // Send the vibration command to the device
        _vibeService.SendNextIntensity(_playbackService.PlaybackByteRange[ReadBufferIdx]);

        // Increment the buffer index
        ReadBufferIdx++;
    }
    #endregion Helper Fuctions
}

