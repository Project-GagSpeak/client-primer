using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox;
using GagSpeak.Toybox.Debouncer;
using ImGuiNET;
using System.Numerics;
using System.Timers;

namespace GagSpeak.UI;

public enum AnimType { ActivateWindow, DeactivateWindow, None }

public enum BlindfoldType { Light, Sensual }

public class BlindfoldUI : WindowMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly UiSharedService _uiSharedService;
    private readonly IDalamudPluginInterface _pi;
    private ITextureProvider _textureProvider;

    // our stored images.
    private ISharedImmediateTexture _sharedBlindfoldLightTexture;
    private ISharedImmediateTexture _sharedBlindfoldSensualTexture;

    // private variables and objects
    private UpdateTimer _TimerRecorder;
    private Stopwatch stopwatch = new Stopwatch();
    private float alpha = 0.0f; // Alpha channel for the image
    private float imageAlpha = 0.0f; // Alpha channel for the image
    private Vector2 position = new Vector2(0, -ImGui.GetIO().DisplaySize.Y); // Position of the image, start from top off the screen
    public AnimType AnimationProgress = AnimType.ActivateWindow; // Whether the image is currently animating
    public bool isShowing = false; // Whether the image is currently showing
    float progress = 0.0f;
    float easedProgress = 0.0f;
    float startY = -ImGui.GetIO().DisplaySize.Y;
    float midY = 0.2f * ImGui.GetIO().DisplaySize.Y;

    public BlindfoldUI(ILogger<BlindfoldUI> logger,
        GagspeakMediator mediator, IDalamudPluginInterface pi,
        ClientConfigurationManager clientConfigs, UiSharedService uiSharedService,
        ITextureProvider textureProvider) : base(logger, mediator, "BlindfoldWindowUI###BlindfoldWindowUI")
    {
        _clientConfigs = clientConfigs;
        _textureProvider = textureProvider;
        _pi = pi;
        _uiSharedService = uiSharedService;

        // set isopen to false
        IsOpen = false;
        IsWindowOpen = false;
        // do not respect close hotkey
        RespectCloseHotkey = false;
        // disable ability for client to hide UI when hideUI hotkey is pressed
        _pi.UiBuilder.DisableUserUiHide = true;

        // store images into shared intermediate textures
        _sharedBlindfoldLightTexture = _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "Blindfold_Light.png"));
        _sharedBlindfoldSensualTexture = _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "BlindfoldLace_Sensual.png"));
        // set the stopwatch to send an elapsed time event after 2 seconds then stop
        _TimerRecorder = new UpdateTimer(2000, ToggleWindow);

        Mediator.Subscribe<DisconnectedMessage>(this, (_) => IsOpen = false);

        Mediator.Subscribe<BlindfoldUiTypeChange>(this, (msg) =>
        {
            if (msg.NewType == BlindfoldType.Light)
            {
                _clientConfigs.GagspeakConfig.BlindfoldStyle = BlindfoldType.Light;
                _clientConfigs.Save();
            }
            else
            {
                _clientConfigs.GagspeakConfig.BlindfoldStyle = BlindfoldType.Sensual;
                _clientConfigs.Save();
            }
        });
    }

    private bool ThemePushed = false;

    public static bool IsWindowOpen;

    public void ToggleWindow(object? sender, ElapsedEventArgs e)
    {
        if (IsOpen && !isShowing)
        {
            this.Toggle();
            _TimerRecorder.Stop();
        }
        else
        {
            // just stop 
            AnimationProgress = AnimType.None;
            _TimerRecorder.Stop();
        }
    }

    public override void OnOpen()
    {
        // if an active timer is running
        if (_TimerRecorder.IsRunning)
        {
            // we were trying to deactivate the window, so stop the timer and turn off the window
            _logger.LogDebug($"BlindfoldWindow: Timer is running, stopping it");
            _TimerRecorder.Stop();
            this.Toggle();
        }
        // now turn it back on and reset all variables
        this.Toggle();
        alpha = 0.0f; // Alpha channel for the image
        imageAlpha = 0.0f; // Alpha channel for the image
        position = new Vector2(0, -ImGui.GetIO().DisplaySize.Y); // Position of the image, start from top off the screen
        progress = 0.0f;
        easedProgress = 0.0f;
        startY = -ImGui.GetIO().DisplaySize.Y;
        midY = 0.2f * ImGui.GetIO().DisplaySize.Y;
        AnimationProgress = AnimType.ActivateWindow;
        isShowing = true;
        // Start the stopwatch when the window starts showing
        _TimerRecorder.Start();
        base.OnOpen();
        IsWindowOpen = true;

    }

    public override void OnClose()
    {
        // if an active timer is running
        if (_TimerRecorder.IsRunning)
        {
            // we were trying to deactivate the window, so stop the timer and turn off the window
            _TimerRecorder.Stop();
        }
        // start the timer to deactivate the window
        _TimerRecorder.Start();
        AnimationProgress = AnimType.DeactivateWindow;
        alpha = 1.0f;
        imageAlpha = 1.0f;
        isShowing = false;

        base.OnClose();
        IsWindowOpen = false;
    }

    protected override void PreDrawInternal()
    {
        ImGui.SetNextWindowPos(Vector2.Zero); // start at top left of the screen
        ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size); // draw across the whole screen
        if (!ThemePushed)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero); // set the padding to 0
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f); // set the border size to 0
            ThemePushed = true;
        }
    }
    protected override void PostDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
        if (ThemePushed)
        {
            ImGui.PopStyleVar(2);
            ThemePushed = false;
        }
    }

    protected override void DrawInternal()
    {
        // force focus the window
        if (!ImGui.IsWindowFocused())
        {
            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
            }
            if (stopwatch.ElapsedMilliseconds >= 100)
            {
                ImGui.SetWindowFocus();
                stopwatch.Reset();
            }
        }
        else
        {
            stopwatch.Reset();
        }

        if (AnimationProgress != AnimType.None)
        {
            // see if we are playing the active animation
            if (AnimationProgress == AnimType.ActivateWindow)
            {
                progress = (float)_TimerRecorder.Elapsed.TotalMilliseconds / 2000.0f; // 2.0f is the total duration of the animation in seconds
                progress = Math.Min(progress, 1.0f); // Ensure progress does not exceed 1.0f
                // Use a sine function for the easing
                startY = -ImGui.GetIO().DisplaySize.Y;
                midY = 0.1f * ImGui.GetIO().DisplaySize.Y;
                if (progress < 0.7f)
                {
                    alpha = (1 - (float)Math.Pow(1 - (progress / 0.7f), 1.5)) / 0.7f;
                    // First 80% of the animation: ease out quint from startY to midY
                    easedProgress = 1 - (float)Math.Pow(1 - (progress / 0.7f), 1.5);
                    position.Y = startY + (midY - startY) * easedProgress;
                }
                else
                {
                    // Last 20% of the animation: ease in from midY to 0
                    easedProgress = 1 - (float)Math.Cos(((progress - 0.7f) / 0.3f) * Math.PI / 2);
                    position.Y = midY + (0 - midY) * easedProgress;
                }
                // If the animation is finished, stop the stopwatch and reset alpha
                if (progress >= 1.0f)
                {
                    AnimationProgress = AnimType.None;
                }
                imageAlpha = Math.Min(alpha, 1.0f); // Ensure the image stays at full opacity once it reaches it
            }
            // or if its the deactionation one
            else if (AnimationProgress == AnimType.DeactivateWindow)
            {
                // Calculate the progress of the animation based on the elapsed time
                progress = (float)_TimerRecorder.Elapsed.TotalMilliseconds / 2000.0f; // 2.0f is the total duration of the animation in seconds
                progress = Math.Min(progress, 1.0f); // Ensure progress does not exceed 1.0f
                // Use a sine function for the easing
                startY = -ImGui.GetIO().DisplaySize.Y;
                midY = 0.1f * ImGui.GetIO().DisplaySize.Y;
                // Reverse the animation
                if (progress < 0.3f)
                {
                    // First 30% of the animation: ease in from 0 to midY
                    easedProgress = (float)Math.Sin((progress / 0.3f) * Math.PI / 2);
                    position.Y = midY * easedProgress;
                }
                else
                {
                    alpha = (progress - 0.3f) / 0.7f;
                    // Last 70% of the animation: ease out quint from midY to startY
                    easedProgress = (float)Math.Pow((progress - 0.3f) / 0.7f, 1.5);
                    position.Y = midY + (startY - midY) * easedProgress;
                }
                // If the animation is finished, stop the stopwatch and reset alpha
                if (progress >= 1.0f)
                {
                    AnimationProgress = AnimType.None;
                }
                imageAlpha = 1 - (alpha == 1 ? 0 : alpha); // Ensure the image stays at full opacity once it reaches it
            }
        }
        else
        {
            position.Y = isShowing ? 0 : startY;
        }
        // Set the window position
        ImGui.SetWindowPos(position);
        // get the window size
        var windowSize = ImGui.GetWindowSize();
        // Draw the image with the updated alpha value
        if (_clientConfigs.GagspeakConfig.BlindfoldStyle == BlindfoldType.Light)
        {
            if (!(_sharedBlindfoldLightTexture.GetWrapOrEmpty() is { } wrapLight))
            {
                _logger.LogWarning("Failed to render image!");
            }
            else
            {
                ImGui.Image(wrapLight!.ImGuiHandle, windowSize, Vector2.Zero, Vector2.One, new Vector4(1.0f, 1.0f, 1.0f, imageAlpha));
            }
        }
        else
        {
            if (!(_sharedBlindfoldSensualTexture.GetWrapOrEmpty() is { } wrapSensual))
            {
                _logger.LogWarning("Failed to render image!");
            }
            else
            {
                ImGui.Image(wrapSensual!.ImGuiHandle, windowSize, Vector2.Zero, Vector2.One, new Vector4(1.0f, 1.0f, 1.0f, imageAlpha));
            }
        }
    }
}

