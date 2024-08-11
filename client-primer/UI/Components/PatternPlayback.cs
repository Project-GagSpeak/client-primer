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

namespace GagSpeak.UI.Components;

/// <summary>
/// Manages playing back patterns to the client user's connected devices.
/// </summary>
public class PatternPlayback : DisposableMediatorSubscriberBase
{
    private readonly ToyboxRemoteService _remoteService;
    private readonly ToyboxVibeService _vibeService;
    private readonly PatternHandler _pHandler;

    public Stopwatch PlaybackDuration;
    private UpdateTimer PlaybackUpdateTimer;
    // private accessors for the playback only
    private double[] CurrentPositions = new double[2];

    public PatternPlayback(ILogger<PatternPlayback> logger,
        GagspeakMediator mediator, ToyboxRemoteService remoteService, 
        ToyboxVibeService vibeService, PatternHandler patternHandler) 
        : base(logger, mediator)
    {
        _remoteService = remoteService;
        _vibeService = vibeService;
        _pHandler = patternHandler;

        PlaybackDuration = new Stopwatch();
        PlaybackUpdateTimer = new UpdateTimer(20, ReadVibePosFromBuffer);

        Mediator.Subscribe<PatternActivedMessage>(this, (msg) =>
        {
            StartPlayback(msg.PatternIndex);
        });

        Mediator.Subscribe<PatternDeactivedMessage>(this, (msg) =>
        {
            StopPlayback(msg.PatternIndex);
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
            if (_pHandler.PlaybackRunning && _pHandler.ActivePattern != null)
            {
                int start = Math.Max(0, ReadBufferIdx - 150);
                int count = Math.Min(150, ReadBufferIdx - start + 1);
                int buffer = 150 - count; // The number of extra values to display at the end


                xs = Enumerable.Range(-buffer, count + buffer).Select(i => (float)i).ToArray();
                ys = _pHandler.ActivePattern.PatternByteData.Skip(_pHandler.ActivePattern.PatternByteData.Count - buffer).Take(buffer)
                    .Concat(_pHandler.ActivePattern.PatternByteData.Skip(start).Take(count))
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
    public void StartPlayback(int PatternIdx)
    {
        // see if a pattern is already active. And it is not our pattern
        if (_pHandler.IsAnyPatternPlaying())
        {
            var activeIdx = _pHandler.GetActivePatternIdx();
            if (activeIdx != PatternIdx)
            {
                // stop the active pattern
                StopPlayback(activeIdx);
            }
        }
        // start a new one
        Logger.LogDebug($"Starting playback of pattern {_pHandler.ActivePattern.Name}");
        // set the playback index to the start
        ReadBufferIdx = 0;

        // iniitalize volume levels if using simulated vibe
        if (_vibeService.UsingSimulatedVibe)
        {
            InitializeVolumeLevels(_pHandler.ActivePattern.PatternByteData);
        }

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

    public void StopPlayback(int PatternIdx)
    {
        Logger.LogDebug($"Stopping playback of pattern {_pHandler.PatternNames[PatternIdx]}");
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
        // If we're playing back the stored positions
        if (_pHandler.PlaybackRunning && _pHandler.ActivePattern != null)
        {
            // If we've reached the end of the stored positions, stop playback
            if (ReadBufferIdx >= _pHandler.ActivePattern.PatternByteData.Count)
            {
                // If we should loop, start it over again.
                if (_pHandler.ActivePattern.ShouldLoop)
                {
                    ReadBufferIdx = 0;
                    PlaybackDuration.Restart();
                    return;
                }
                // otherwise, stop.
                else
                {
                    _pHandler.StopPattern(_pHandler.GetPatternIdxByName(_pHandler.ActivePattern.Name));
                    return;
                }
            }

            // Convert the current stored position to a float and store it in currentPos
            CurrentPositions[1] = _pHandler.ActivePattern.PatternByteData[ReadBufferIdx];

            // Send the vibration command to the device
            _vibeService.SendNextIntensity(_pHandler.ActivePattern.PatternByteData[ReadBufferIdx]);

            // Increment the buffer index
            ReadBufferIdx++;
        }
    }
    #endregion Helper Fuctions
}

