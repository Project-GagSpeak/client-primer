using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Lua;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Debouncer;
using GagSpeak.Toybox.Services;
using ImGuiNET;
using ImPlotNET;
using OtterGui;
using System.Numerics;
using System.Timers;

namespace GagSpeak.UI.UiRemote;

/// <summary>
/// RemoteBase is created with unique distinct variables for each instance that it is made with.
/// Any attributes or methods that should share common functionality should be managed via the 
/// toybox remote service.
/// </summary>
public abstract class RemoteBase : WindowMediatorSubscriberBase
{
    // the class includes are shared however (i think), so dont worry about that.
    private readonly UiSharedService _uiShared;
    private readonly DeviceHandler _intifaceHandler;
    private readonly ToyboxRemoteService _remoteService;

    public RemoteBase(ILogger logger,
        GagspeakMediator mediator, UiSharedService uiShared,
        ToyboxRemoteService remoteService, DeviceHandler deviceHandler,
        string windowName): base(logger, mediator, $"Lovense Remote UI " + windowName)
    {
        // grab the shared services
        _uiShared = uiShared;
        _intifaceHandler = deviceHandler;
        _remoteService = remoteService;

        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize;

        // define initial size of window and to not respect the close hotkey.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 430),
            MaximumSize = new Vector2(300, 430)
        };
        RespectCloseHotkey = false;

        // label the window with the identifier name
        WindowBaseName = $"Lovense Remote UI " + windowName;

        // create a new timer stopwatch, unique to this base class instance.
        DurationStopwatch = new Stopwatch();

        // A timer for recording graph display updates. Nessisary for smooth lines, and Unique to this base class instance
        DisplayUpdateTimer = new TimerRecorder(10, AddCirclePositionToBuffer);

        // A timer for recording intensity data. Nessisary for all types, but must remain distinct for each. (base class)
        IntensityUpdateTimer = new TimerRecorder(20, RecordData);
    }

    private string WindowBaseName;

    // Stores the duration of the recording. Must be distinct for all (base class)
    protected Stopwatch DurationStopwatch;

    // Manages the recording of the circle's Y position. Must be distinct for all (base class)
    protected TimerRecorder DisplayUpdateTimer;

    // Manages the interval at which we append handle new vibration intensity data. Must be distinct for all (base class)
    protected TimerRecorder IntensityUpdateTimer;

    // the Recorded Y positions from the circle. Must be distinct for all to ensure draws dont share (base class)
    protected List<double> RecordedPositions = new List<double>();

    // the Recorded Y positions during the latest loop segment. Must be distinct for all to ensure draws dont share (base class)
    protected List<double> StoredLoopDataBlock = new List<double>();

    // the current circle position, stores the X and Y double. Unique to each instance (Base Class)
    protected double[] CirclePosition = new double[2];

    // Must be unique for each base class so that we ensure themes are not overlapped
    private bool ThemePushed = false;

    // the buffer index telling us where in the bufferloop we are reading data from we are at. (base class)
    public int BufferLoopIndex { get; protected set; } = 0;

    // if the remote is powered on (base class)
    public bool RemoteOnline { get; protected set; } = false;

    // if we are dragging the pink circle. (base class)
    public bool IsDragging { get; protected set; } = false;

    // if we have the loop button toggled. (base class)
    public bool IsLooping { get; protected set; }

    // if we have the float button toggled. (base class)
    public bool IsFloating { get; protected set; }

    // Graph data. Does not need to be distinct, but is shared across all types. (base class)
    public const float XAxisLimit = 40;
    public const float YAxisLimitLower = 0;
    public const float YAxisLimitUpper = 100;
    public double[] Positions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }; 
    public string[] Labels = { "0%", "", "", "", "", "", "", "", "", "", "100%" };
    
    // the disposal for the base class
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // dispose of the timer recorders & stop the stopwatch
        DurationStopwatch.Stop();

        DisplayUpdateTimer.Dispose();
        IntensityUpdateTimer.Dispose();
    }

    protected override void PreDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
        // no config option yet, so it will always be active. When one is added, append "&& !_configOption.useTheme" to the if statement.
        if (!ThemePushed)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0, 0));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.1f, 0.930f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0, 0.56f, 0.09f, 0.51f));

            ThemePushed = true;
        }
    }
    protected override void PostDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
        if (ThemePushed)
        {
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
            ThemePushed = false;
        }
    }
    protected override void DrawInternal()
    {
        using (var child = ImRaii.Child("##RemoteUIChild", new Vector2(ImGui.GetContentRegionAvail().X, -1), true, ImGuiWindowFlags.NoDecoration))
        {

            if (!child) { return; }

            // get the xpos so we can draw it back a bit to span the whole width of the window.
            float xPos = ImGui.GetCursorPosX();
            float yPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(xPos - ImGuiHelpers.GlobalScale * 10, yPos - ImGuiHelpers.GlobalScale * 10));
            float width = ImGui.GetContentRegionAvail().X + ImGuiHelpers.GlobalScale * 10;

            // draw the playback display
            DrawRecordedDisplay(ref xPos, ref yPos, ref width);

            // draw the center bar for recording information and things
            DrawCenterBar(ref xPos, ref yPos, ref width);

            // draw the core of the vibe remote
            DrawVibrationRecorder(ref xPos, ref yPos, ref width);

            if(ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ProcessLoopToggle();
            }
            if(ImGui.IsMouseClicked(ImGuiMouseButton.Middle))
            {
                ProcessFloatToggle();
            }
        }

    
    }

    public void ProcessLoopToggle()
    {
        IsLooping = !IsLooping;
/*        if (IsFloating) { IsFloating = false; }*/
    }

    public void ProcessFloatToggle()
    {
        IsFloating = !IsFloating;
/*        if (IsLooping) { IsLooping = false; }*/
    }

    public void DrawRecordedDisplay(ref float xPos, ref float yPos, ref float width)
    {
        try
        {
            // Setup the waveform bounding box.
            float[] xs = Enumerable.Range(0, RecordedPositions.Count).Select(i => (float)i).ToArray();  // x-values
            float[] ys = RecordedPositions.Select(pos => (float)pos).ToArray();  // y-values
            float latestX = xs.Length > 0 ? xs[xs.Length - 1] : 0; // The latest x-value

            // Transform the x-values so that the latest position appears at x=0 (ensure it doesnt start smack dab in the middle)
            for (int i = 0; i < xs.Length; i++)
                xs[i] -= latestX;

            // set up the color map for our plots.
            ImPlot.PushStyleColor(ImPlotCol.Line, _remoteService.LushPinkLine);
            ImPlot.PushStyleColor(ImPlotCol.PlotBg, _remoteService.LovenseScrollingBG);

            // setup and draw the waveform graph axis
            ImPlot.SetNextAxesLimits(-150, 0, -5, 110, ImPlotCond.Always);

            if (ImPlot.BeginPlot("##Waveform", new Vector2(width, 125), ImPlotFlags.NoBoxSelect | ImPlotFlags.NoMenus | ImPlotFlags.NoLegend | ImPlotFlags.NoFrame))
            {
                ImPlot.SetupAxes("X Label", "Y Label",
                    ImPlotAxisFlags.NoGridLines | ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels | ImPlotAxisFlags.NoTickMarks | ImPlotAxisFlags.NoHighlight,
                    ImPlotAxisFlags.NoGridLines | ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels | ImPlotAxisFlags.NoTickMarks);
                // Draw the recorded positions line.
                if (xs.Length > 0 || ys.Length > 0)
                {
                    ImPlot.PlotLine("Recorded Positions", ref xs[0], ref ys[0], xs.Length);
                }
                ImPlot.EndPlot();
            }

            // clear the styles
            ImPlot.PopStyleColor(2);

            // shift up again
            xPos = ImGui.GetCursorPosX();
            yPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(yPos - ImGuiHelpers.GlobalScale * 13);

            // add a seperator to join us up with the middle section.
            ImGui.Separator();
            yPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(yPos - ImGuiHelpers.GlobalScale);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error drawing the waveform graph.");
        }
    }

    /// <summary>
    /// This abstract method for the center bar will change how it is drawn based on the remote type.
    /// <para> Personal will display personal devices, their motors and additional options. </para>
    /// <para> Patterns will display the device the pattern is being recorded for, which motor and type we are recording for.</para>
    /// <para> Pattern will display </para>
    /// </summary>
    public abstract void DrawCenterBar(ref float xPos, ref float yPos, ref float width);
    

    public void DrawVibrationRecorder(ref float xPos, ref float yPos, ref float width)
    {

        // grab the content region
        var region = ImGui.GetContentRegionAvail();
        using (var table2 = ImRaii.Table("ThePatternCreationTable", 2, ImGuiTableFlags.NoPadInnerX | ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.BordersV))
        {
            if (!table2) { return; } // make sure our table was made
            ImGui.TableSetupColumn("InteractivePatternDrawer", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("InteractionButtons", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 60);
            ImGui.TableNextColumn();

            // create styles for the next plot
            ImPlot.PushStyleColor(ImPlotCol.PlotBg, _remoteService.LovenseDragButtonBG);
            DrawCircleButtonGraph(ref width, ref yPos);

            // end of using disabled & first column
            ImGui.TableNextColumn();
            // create another table inside here
            DrawSideButtonsTable(region);
        }
        // end of table here.
    }

    private void DrawCircleButtonGraph(ref float width, ref float yPos)
    {
        using var disabled = ImRaii.Disabled(!RemoteOnline);
        using var color = ImRaii.PushColor(ImPlotCol.PlotBg, _remoteService.LovenseDragButtonBG);
        // Draw a thin line with a timer to show the current position of the circle
        width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetContentRegionAvail().Y + ImGui.GetTextLineHeight() + ImGuiHelpers.GlobalScale * 5;

        // go to the next line and draw the grid we can move out thing in
        yPos = ImGui.GetCursorPosY();
        ImGui.SetCursorPosY(yPos - ImGui.GetTextLineHeight());
        ImPlot.SetNextAxesLimits(-50, +50, -10, 110, ImPlotCond.Always);
        var PreviousPos = CirclePosition[1]; // store the Y position

        // now draw the actual damn thing
        if (ImPlot.BeginPlot("##Box", new Vector2(width + ImGui.GetTextLineHeight(), height), ImPlotFlags.NoBoxSelect | ImPlotFlags.NoLegend | ImPlotFlags.NoFrame))
        {
            // setup Axis's
            ImPlot.SetupAxes("X Label", "Y Label",
                ImPlotAxisFlags.NoGridLines | ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels | ImPlotAxisFlags.NoTickMarks | ImPlotAxisFlags.NoMenus | ImPlotAxisFlags.NoHighlight,
                ImPlotAxisFlags.NoGridLines | ImPlotAxisFlags.NoMenus | ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoHighlight);
            ImPlot.SetupAxisTicks(ImAxis.Y1, ref Positions[0], 11, Labels);

            // setup the drag point circle
            ImPlot.DragPoint(0, ref CirclePosition[0], ref CirclePosition[1], _remoteService.LushPinkButton, 20, ImPlotDragToolFlags.NoCursors);

            // if the mouse button is released, while we are looping and dragging turn dragging off
            if (IsDragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                IsDragging = false;
                BufferLoopIndex = 0; // reset the index
                _logger.LogTrace("Dragging Period Ended!");
            }

            // if our mouse is down...
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                // if we are not yet marked as dragging, and the positions are different, then mark that we are dragging
                if (!IsDragging && PreviousPos != CirclePosition[1])
                {
                    IsDragging = true;
                    StoredLoopDataBlock = new List<double>(); // start a new list
                    BufferLoopIndex = 0; // reset the index
                    _logger.LogTrace("Dragging Period Started!");
                }
            }

            // If looping on & Left Mouse isn't down, then we're reading off the loop buffer, so skip float accounting.
            if (!(IsLooping && !ImGui.IsMouseDown(ImGuiMouseButton.Left)))
            {
                AccountForFloating();
            }
            // end the plot
            ImPlot.EndPlot();
        }
    }

    public abstract void DrawSideButtonsTable(Vector2 region);
    

    // When active, the circle will not fall back to the 0 coordinate on the Y axis of the plot, and remain where it is
    public void AccountForFloating()
    {
        // Check if the circle's position is beyond the axis limit and set it to the limit if it is
        if (CirclePosition[0] > XAxisLimit) { CirclePosition[0] = XAxisLimit; }
        if (CirclePosition[0] < -XAxisLimit) { CirclePosition[0] = -XAxisLimit; }
        if (CirclePosition[1] > YAxisLimitUpper) { CirclePosition[1] = YAxisLimitUpper; }
        if (CirclePosition[1] < YAxisLimitLower) { CirclePosition[1] = YAxisLimitLower; }
        // if the isfloating is not active and we have let go of the circle, drop it.
        if (IsFloating == false && IsDragging == false)
        {
            // drop the circle by 10
            if (CirclePosition[1] < 10)
            {
                CirclePosition[1] = 0;
            }
            else
            {
                CirclePosition[1] -= 10;
            }
        }
    }

    public virtual void StartVibrating()
    {
        _logger.LogInformation($"Starting Recording on {WindowBaseName}!");
        // start up our timers and stopwatch
        DisplayUpdateTimer.Start();
        IntensityUpdateTimer.Start();
        DurationStopwatch.Start();
        RemoteOnline = true;
        // start up the simulated vibrator if active.
        // TODO: Spacial & Simulated Audio integration
    }

    public virtual void StopVibrating()
    {
        _logger.LogInformation($"Stopping Recording on {WindowBaseName}!");
        // halt our recorders and stopwatch
        DisplayUpdateTimer.Stop();
        IntensityUpdateTimer.Stop();
        DurationStopwatch.Stop();
        RemoteOnline = false;
        // clear our stored data (not the byte intensity block)
        RecordedPositions.Clear();
        StoredLoopDataBlock.Clear();
        // Reset vibrations on motors prior to recording back to original state.
        _intifaceHandler.StopAllDevices();
    }

    /// <summary>
    /// Because this function simply appends information to the DisplayPositionBuffer, it can be called in the base class.
    /// </summary>
    private void AddCirclePositionToBuffer(object? sender, ElapsedEventArgs e)
    {
        
        // Limit recorded position doubles to 1000. 
        // Once limit reached, reset to new list with last 200 entries. (ensures display isnt fucked up)
        if (RecordedPositions.Count > 1000)
        {
            RecordedPositions = RecordedPositions.GetRange(RecordedPositions.Count - 200, 200);
        }

        // If not looping, add to default buffer
        if (!IsLooping)
        {
            //_logger.LogTrace("Not Looping!");
            RecordedPositions.Add(CirclePosition[1]);
            return;
        }

        // If we are looping but not yet recording loop data, add to default buffer.
        if (IsLooping && !IsDragging && StoredLoopDataBlock.Count == 0)
        {
            //_logger.LogTrace("Looping but not dragging!");
            RecordedPositions.Add(CirclePosition[1]);
            return;
        }


        // If we are recording a looped buffer, add to storedLoopBlock and Recorded Positions.
        // (We add to recorded positions still because what we record for the loop is still part of recording)
        if (IsLooping && IsDragging)
        {
            //_logger.LogTrace("Looping and dragging! (Storing Loop Data)");
            RecordedPositions.Add(CirclePosition[1]);
            StoredLoopDataBlock.Add(CirclePosition[1]);
            return;
        }

        // If looping, but not dragging, & storedLoopBlock has data, add the stored data to the recorded positions.
        if (IsLooping && !IsDragging && StoredLoopDataBlock.Count > 0)
        {
            //_logger.LogTrace("Looping but not dragging! (Reading Loop Data)");
            RecordedPositions.Add(StoredLoopDataBlock[BufferLoopIndex]);
            // inc buffer index so we read the next byte properly
            BufferLoopIndex++;
            // when we reach the end of the buffer index, cycle back over and start again.
            if (BufferLoopIndex >= StoredLoopDataBlock.Count)
            {
                BufferLoopIndex = 0;
            }
            return;
        }
    }

    /// <summary>
    /// This is an abstract class, as this function determines what happens to the data after it is recorded.
    /// <para> This is unique to each kind of device calling it, so we will handle that based on who is calling it.</para>
    /// </summary>
    public abstract void RecordData(object? sender, ElapsedEventArgs e);
}
