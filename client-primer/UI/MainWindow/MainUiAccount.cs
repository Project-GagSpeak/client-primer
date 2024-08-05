using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using ImGuiScene;
using System.Numerics;

namespace GagSpeak.UI.MainWindow;

/// <summary> 
/// Sub-class of the main UI window. Handling displaying personal account information.
/// </summary>
public class MainUiAccount : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiShared;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly ProfileService _profileManager;
    private readonly FileDialogManager _fileDialogManager; // remove this if we dont set images here.

    public MainUiAccount(ILogger<MainUiAccount> logger,
        GagspeakMediator mediator, ApiController apiController,
        UiSharedService uiShared, OnFrameworkService frameworkUtils,
        ProfileService profileManager,
        FileDialogManager fileDialogManager) : base(logger, mediator)
    {
        _apiController = apiController;
        _uiShared = uiShared;
        _frameworkUtils = frameworkUtils;
        _profileManager = profileManager;
        _fileDialogManager = fileDialogManager;

        Mediator.Subscribe<DisconnectedMessage>(this, (_) =>
        {
            // do something to the image i guess?
        });
    }

    // until we find a way to inject image data byte arrays into a shared ImmediateTexture, we need to aquire it via this route.
    // This is basically was the sharedImmediateTexture does already, but if we can convert it, it would be cleaner.
    private IDalamudTextureWrap? _profileImageWrap = null;
    private byte[] _profileImageData = [];
    TextureWrap? wrapImage { get; set; }


    /// <summary> Main Draw function for this tab </summary>
    public void DrawAccountSection()
    {
        // get the width of the window content region we set earlier
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        var _spacingX = ImGui.GetStyle().ItemSpacing.X;
        /*
        - Profile Image display
        - UID Display (centered, under profile image)
        - Player name (centered, small-text, under UID)
        */
        try
        {
            // fetch own profile data to store / display.
            // This function itself does no API calls unless the requested UID is different.
            var profileData = _profileManager.GetGagspeakProfile(new UserData(_apiController.UID));

            // if the profile is flagged, say so.
            if (profileData.Flagged)
            {
                UiSharedService.ColorTextWrapped("Your profile has been flagged for inappropriate content. Please review your profile.", ImGuiColors.DalamudRed);
            }

            // ensure we only update the texture wrap when the data is changed.
            if(profileData.Base64ProfilePicture.IsNullOrEmpty())
            {
                _profileImageWrap?.Dispose();
                _profileImageWrap = _uiShared.GetGagspeakLogoNoRadial();
            }
            else if (!_profileImageData.SequenceEqual(profileData.ImageData.Value) && !string.IsNullOrEmpty(profileData.Base64ProfilePicture))
            {
                _profileImageData = profileData.ImageData.Value;
                _profileImageWrap?.Dispose();
                _profileImageWrap = _uiShared.LoadImage(_profileImageData);
            }


            if (!(_profileImageWrap is { } wrap))
            {
                Logger.LogWarning("Player Image was null, replacing with default image until new image data is made!");
            }
            else
            {
                var region = ImGui.GetContentRegionAvail();
                var currentPosition = ImGui.GetCursorPos();

                var pos = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddImageRounded(wrap.ImGuiHandle, pos, pos + wrap.Size, Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 90f);
                ImGui.SetCursorPos(new Vector2(currentPosition.X, currentPosition.Y + wrap.Height));

            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error: {ex}");
        }

        // draw the UID header below this.
        DrawUIDHeader();

        // draw out the player name.


        // below this, draw a separator. (temp)
        ImGui.Separator();
        /*
        -Safeword text field
        -Open Account Settings
        -Open Profile Editor
        -Help Button
        - About Button
        - Buttons for opening plugin config ext. (if applicable)
        */
    }

    /// <summary>
    /// Draws the UID header for the currently connected client (you)
    /// </summary>
    private void DrawUIDHeader()
    {
        // fetch the Uid Text of yourself
        var uidText = _uiShared.GetUidText();

        // push the big boi font for the UID
        using (_uiShared.UidFont.Push())
        {
            var uidTextSize = ImGui.CalcTextSize(uidText);
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - uidTextSize.X / 2);
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(_uiShared.GetUidColor(), uidText);
        }

        // if we are connected
        if (_apiController.ServerState is ServerState.Connected)
        {
            UiSharedService.CopyableDisplayText(_apiController.DisplayName);

            // if the UID does not equal the display name
            if (!string.Equals(_apiController.DisplayName, _apiController.UID, StringComparison.Ordinal))
            {
                // grab the original text size for the UID in the api controller
                var origTextSize = ImGui.CalcTextSize(_apiController.UID);
                // adjust the cursor and redraw the UID (really not sure why this is here but we can trial and error later.
                ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - origTextSize.X / 2);
                ImGui.TextColored(_uiShared.GetUidColor(), _apiController.UID);
                // give it the same functionality.
                UiSharedService.CopyableDisplayText(_apiController.UID);
            }
        }
    }
}
