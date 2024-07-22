/*using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Debouncer;
using ImGuiNET;
using ImPlotNET;
using OtterGui;
using System.Numerics;
using System.Timers;

namespace GagSpeak.UI.UiRemote;

/// <summary>
/// I Blame ImPlot for its messiness as a result for this abyssmal display of code here.
/// </summary>
public class RemotePersonal : RemoteBase
{
    // Colors that i will sort out later.
    Vector4 VibrantPink = new Vector4(.977f, .380f, .640f, .914f);
    Vector4 VibrantPinkHovered = new Vector4(.986f, .464f, .691f, .955f);
    Vector4 VibrantPinkPressed = new Vector4(.846f, .276f, .523f, .769f);
    Vector4 LushPinkLine = new Vector4(.806f, .102f, .407f, 1);
    Vector4 LushPinkButton = new Vector4(1, .051f, .462f, 1);
    Vector4 LovenseScrollingBG = new Vector4(0.042f, 0.042f, 0.042f, 0.930f);
    Vector4 LovenseDragButtonBG = new Vector4(0.110f, 0.110f, 0.110f, 0.930f);
    Vector4 LovenseDragButtonBGAlt = new Vector4(0.1f, 0.1f, 0.1f, 0.930f);
    Vector4 ButtonDrag = new Vector4(0.097f, 0.097f, 0.097f, 0.930f);
    Vector4 SideButton = new Vector4(0.451f, 0.451f, 0.451f, 1);
    Vector4 SideButtonBG = new Vector4(0.451f, 0.451f, 0.451f, .25f);

    private readonly UiSharedService _uiShared;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly DeviceHandler _IntifaceHandler;

    public RemotePersonal(ILogger<RemoteBase> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        DeviceHandler IntifaceHandler) : base(logger, mediator, "Lovense Remote UI")
    {
        _uiShared = uiSharedService;
        _IntifaceHandler = IntifaceHandler;

        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize;

        // define initial size of window and to not respect the close hotkey.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 430),
            MaximumSize = new Vector2(300, 430)
        };
        RespectCloseHotkey = false;

        DurationStopwatch = new Stopwatch(); // create new stopwatch for our counter.
        Recorder = new TimerRecorder(10, AddCirclePositionToBuffer);
        StoredRecordedData = new TimerRecorder(20, RecordData);

    }

    private Stopwatch DurationStopwatch;
    private TimerRecorder Recorder;
    private TimerRecorder StoredRecordedData;
    private List<double> RecordedPositions = new List<double>(); // recorded Y positions of the circle, used for realtime feedback
    private List<double> StoredLoopDataBlock = new List<double>(); // Records & Stores info about Y value of circle.
    public List<byte> StoredVibrationData = new List<byte>(); // Stores the vibration data for the pattern (converted from circle positions.
    private double[] CirclePosition = new double[2];

    // public accessors.
    private bool ThemePushed = false;
    public int BufferLoopIndex { get; private set; } = 0;
    public bool IsRecording { get; private set; } = false;
    public bool HasFinishedRecording { get; private set; } = false;
    public bool IsDragging { get; private set; } = false;
    public bool IsLooping { get; private set; }
    public bool IsFloating { get; private set; }
    public float XAxisLimit { get; private set; } = 40;
    public float YAxisLimitLower { get; private set; } = 0;
    public float YAxisLimitUpper { get; private set; } = 100;
    public double[] Positions = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }; // positions of the ticks
    public string[] Labels = { "0%", "", "", "", "", "", "", "", "", "", "100%" }; // labels of the ticks

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // dispose of the timer recorders
        StoredRecordedData.Dispose();
        Recorder.Dispose();
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
        using var child = ImRaii.Child("##RemoteUIChild", new Vector2(ImGui.GetContentRegionAvail().X, -1), true, ImGuiWindowFlags.NoDecoration);

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

    }

    #region ImPlot Hell
    public void DrawRecordedDisplay(ref float xPos, ref float yPos, ref float width)
    {
        try
        {
            // Setup the waveform bounding box.
            float[] xs = Enumerable.Range(0, RecordedPositions.Count).Select(i => (float)i).ToArray();  // x-values
            float[] ys = RecordedPositions.Select(pos => (float)pos).ToArray();  // y-values
            float latestX = xs.Length > 0 ? xs[xs.Length - 1] : 0; // The latest x-value

            // Transform the x-values so that the latest position appears at x=0
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] -= latestX;
            }

            // set up the color map for our plots.
            ImPlot.PushStyleColor(ImPlotCol.Line, LushPinkLine);
            ImPlot.PushStyleColor(ImPlotCol.PlotBg, LovenseScrollingBG);

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

    public void DrawCenterBar(ref float xPos, ref float yPos, ref float width)
    {
        // grab the content region of the current section
        var CurrentRegion = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGuiHelpers.GlobalScale * 5);
        using (var color = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.2f, 0.930f)))
        {
            // create a child for the center bar
            using (var canterBar = ImRaii.Child($"###CenterBarDraw", new Vector2(CurrentRegion.X, 40f), false))
            {
                UiSharedService.ColorText("CenterBar dummy placement", ImGuiColors.ParsedGreen);
            }
        }
    }

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
            ImPlot.PushStyleColor(ImPlotCol.PlotBg, LovenseDragButtonBG);
            DrawCircleButtonGraph(ref width, ref yPos);

            // end of using disabled & first column
            ImGui.TableNextColumn();
            // create another table inside here
            DrawSideButtonsTable(region);
        }
        // end of table here.
    }

    public void DrawCircleButtonGraph(ref float width, ref float yPos)
    {
        using var disabled = ImRaii.Disabled(!IsRecording && HasFinishedRecording);
        using var color = ImRaii.PushColor(ImPlotCol.PlotBg, LovenseDragButtonBG);
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
            ImPlot.DragPoint(0, ref CirclePosition[0], ref CirclePosition[1], LushPinkButton, 20, ImPlotDragToolFlags.NoCursors);

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

            // account for floating and boundry crossing for REALISM BABY
            AccountForFloating();
            // end the plot
            ImPlot.EndPlot();
        }
    }

    public void DrawSideButtonsTable(Vector2 region)
    {
        // push our styles
        using var styleColor = ImRaii.PushColor(ImGuiCol.Button, new Vector4(.2f, .2f, .2f, .2f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(.3f, .3f, .3f, .4f))
            .Push(ImGuiCol.ButtonActive, LushPinkButton);
        using var styleVar = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 40);

        // grab the content region of the current section
        var CurrentRegion = ImGui.GetContentRegionAvail();
        var yPos2 = ImGui.GetCursorPosY();

        // setup a child for the table cell space
        using (var leftChild = ImRaii.Child($"###ButtonsList", CurrentRegion with { Y = region.Y }, false, ImGuiWindowFlags.NoDecoration))
        {
            var InitPos = ImGui.GetCursorPosY();
            if (IsRecording)
            {
                ImGuiUtil.Center($"{Recorder.Elapsed.ToString(@"mm\:ss")}");
            }

            // move our yposition down to the top of the frame height times a .3f scale of the current region
            ImGui.SetCursorPosY(InitPos + CurrentRegion.Y * .1f);
            ImGui.Separator();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + CurrentRegion.Y * .025f);

            // attempt to obtain an image wrap for it
            var spinnyArrow = _uiShared.GetImageFromDirectoryFile("arrows-spin.png");
            if (!(spinnyArrow is { } wrap))
            {
                _logger.LogWarning("Failed to render image!");
            }
            else
            {
                Vector4 buttonColor = IsLooping ? LushPinkButton : SideButton;
                // aligns the image in the center like we want.
                if (_uiShared.DrawScaledCenterButtonImage("LoopButton", new Vector2(50, 50),
                    buttonColor, new Vector2(40, 40), wrap))
                {
                    IsLooping = !IsLooping;
                    if (IsFloating) { IsFloating = false; }
                }
            }

            // move it down from current position by another .2f scale
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + CurrentRegion.Y * .05f);

            var circlesDot = _uiShared.GetImageFromDirectoryFile("circle-dot.png");
            if (!(circlesDot is { } wrap2))
            {
                _logger.LogWarning("Failed to render image!");
            }
            else
            {
                Vector4 buttonColor2 = IsFloating ? LushPinkButton : SideButton;
                // aligns the image in the center like we want.
                if (_uiShared.DrawScaledCenterButtonImage("FloatButton", new Vector2(50, 50),
                    buttonColor2, new Vector2(40, 40), wrap2))
                {
                    IsFloating = !IsFloating;
                    if (IsLooping) { IsLooping = false; }
                }
            }

            ImGui.SetCursorPosY(CurrentRegion.Y * .775f);

            var power = _uiShared.GetImageFromDirectoryFile("power.png");
            if (!(power is { } wrap3))
            {
                _logger.LogWarning("Failed to render image!");
            }
            else
            {
                Vector4 buttonColor3 = IsRecording ? LushPinkButton : SideButton;
                // aligns the image in the center like we want.
                if (_uiShared.DrawScaledCenterButtonImage("PowerToggleButton", new Vector2(50, 50),
                    buttonColor3, new Vector2(40, 40), wrap3))
                {
                    if (!IsRecording)
                    {
                        _logger.LogTrace("Starting Recording!");
                        // invert the recording state and start recording
                        IsRecording = !IsRecording;
                        StartRecording();
                    }
                    else
                    {
                        _logger.LogTrace("Stopping Recording!");
                        // invert the recording state and stop recording
                        IsRecording = !IsRecording;
                        StopRecording();
                    }
                }
            }
        }
        // pop what we appended
        styleColor.Pop(3);
        styleVar.Pop();
    }
    #endregion ImPlot Hell

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

    public void StartRecording()
    {
        _logger.LogTrace("Starting Recording!");
        StoredVibrationData.Clear();

        Recorder.Start();
        StoredRecordedData.Start();

        DurationStopwatch.Start();  // Start the stopwatch
        IsRecording = true;
        // start up the sound audio
        // TODO: Spacial & Simulated Audio integration
    }

    public void StopRecording()
    {
        _logger.LogTrace("Stopping Recording!");
        Recorder.Stop();
        StoredRecordedData.Stop();
        RecordedPositions.Clear();
        StoredLoopDataBlock.Clear();
        DurationStopwatch.Stop();
        IsRecording = false;
        HasFinishedRecording = true;
        // TODO: Reset vibrations on motors prior to recording back to original state. unsure how to do this yet.
    }

    #region TimerResolveFuncs
    // Function to add the Circles Y-Position into the buffer of stored positions
    private void AddCirclePositionToBuffer(object? sender, ElapsedEventArgs e)
    {
        // Limit recorded position doubles to 1000. 
        // Once limit reached, reset to new list with last 200 entries.
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

    /// <summary> Sends the recorded data to the connected devices. (every 20ms) </summary>
    private void RecordData(object? sender, ElapsedEventArgs e)
    {
        if (IsLooping && !IsDragging && StoredLoopDataBlock.Count > 0)
        {
            //_logger.LogTrace($"Looping & not Dragging: {(byte)Math.Round(StoredLoopDataBlock[BufferLoopIndex])}");
            // If looping, but not dragging, and have stored LoopData, add the stored data to the vibration data.
            StoredVibrationData.Add((byte)Math.Round(StoredLoopDataBlock[BufferLoopIndex]));
        }
        else
        {
            //_logger.LogTrace($"Injecting new data: {(byte)Math.Round(CirclePosition[1])}");
            // Otherwise, add the current circle position to the vibration data.
            StoredVibrationData.Add((byte)Math.Round(CirclePosition[1]));
        }

        // if we reached passed our "capped limit", (its like 3 hours) stop recording.
        if (StoredVibrationData.Count > 270000)
        {
            //_logger.LogWarning("Capped the stored data, stopping recording!");
            StopRecording();
        }

        // _logger.LogTrace($"AnyDeviceConnected: {_IntifaceHandler.AnyDeviceConnected}, ConnectedToIntiface: {_IntifaceHandler.ConnectedToIntiface}");
        // if any devices are currently connected, and our intiface client is connected,
        if (_IntifaceHandler.AnyDeviceConnected && _IntifaceHandler.ConnectedToIntiface)
        {
            //_logger.LogTrace("Sending Vibration Data to Devices!");
            // send the vibration data to all connected devices
            if (IsLooping && !IsDragging && StoredLoopDataBlock.Count > 0)
            {
                //_logger.LogTrace($"{(byte)Math.Round(StoredLoopDataBlock[BufferLoopIndex])}");
                _IntifaceHandler.SendVibeToAllDevices((byte)Math.Round(StoredLoopDataBlock[BufferLoopIndex]));
            }
            else
            {
                //_logger.LogTrace($"{(byte)Math.Round(CirclePosition[1])}");
                _IntifaceHandler.SendVibeToAllDevices((byte)Math.Round(CirclePosition[1]));
            }
        }
    }
    #endregion TimerResolveFuncs
}
*/
