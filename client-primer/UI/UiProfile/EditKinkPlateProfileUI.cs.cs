using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps; // This is discouraged, try and look into better way to do it later.
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Dto.User;
using ImGuiNET;
using Microsoft.IdentityModel.Tokens;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics;

namespace GagSpeak.UI.Profile;

public class EditProfileUi : WindowMediatorSubscriberBase
{
    private readonly MainHub _apiHubMain;
    private readonly FileDialogManager _fileDialogManager;
    private readonly ProfileService _gagspeakProfileManager;
    private readonly UiSharedService _uiSharedService;
    private bool _adjustedForScollBarsLocalProfile = false;
    private bool _adjustedForScollBarsOnlineProfile = false;
    private bool _showFileDialogError = false;
    private bool _wasOpen;

    public EditProfileUi(ILogger<EditProfileUi> logger, GagspeakMediator mediator,
        MainHub apiHubMain, UiSharedService uiSharedService,
        FileDialogManager fileDialogManager, ProfileService gagspeakProfileManager)
        : base(logger, mediator, "Edit Avatar###GagSpeakEditProfileUI")
    {
        IsOpen = false;
        this.SizeConstraints = new()
        {
            MinimumSize = new(768, 512),
            MaximumSize = new(768, 2000)
        };
        _apiHubMain = apiHubMain;
        _uiSharedService = uiSharedService;
        _fileDialogManager = fileDialogManager;
        _gagspeakProfileManager = gagspeakProfileManager;

        Mediator.Subscribe<MainHubDisconnectedMessage>(this, (_) => IsOpen = false);
    }

    protected override void PreDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }
    protected override void PostDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }

    // wrap of existing pfp.
    private byte[] _uploadedImageData;
    private bool _useCompressedImage = false; // Default to using compressed image
    private byte[] _compressedImageData;
    private IDalamudTextureWrap? _compressedImageToShow;
    private byte[] _scopedData = null!;
    private byte[] _croppedImageData;
    private IDalamudTextureWrap? _uploadedImageToShow;
    private IDalamudTextureWrap? _croppedImageToShow;
    private bool HideWidth = false;
    private float _cropX = 0.5f; // Center by default
    private float _cropY = 0.5f; // Center by default
    private float _rotationAngle = 0.0f; // Rotation angle in degrees
    private float _zoomFactor = 1.0f; // Zoom factor, 1.0 means no zoom
    private float _minZoomFactor = 1.0f;
    private float _maxZoomFactor = 3.0f;
    public string OriginalFileSize { get; private set; } = string.Empty;
    public string ScaledFileSize { get; private set; } = string.Empty;
    public string CroppedFileSize { get; private set; } = string.Empty;

    protected override void DrawInternal()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        // grab our profile.
        var profile = _gagspeakProfileManager.GetGagspeakProfile(new UserData(MainHub.UID));

        // check if flagged
        if (profile.Flagged)
        {
            UiSharedService.ColorTextWrapped(profile.Description, ImGuiColors.DalamudRed);
            return;
        }

        var pfpWrap = profile.GetCurrentProfileOrDefault();

        if (pfpWrap != null)
        {
            ImGui.Image(pfpWrap.ImGuiHandle, ImGuiHelpers.ScaledVector2(pfpWrap.Width, pfpWrap.Height));
        }
        ImGuiHelpers.ScaledRelativeSameLine(256, spacing);
        if (pfpWrap != null)
        {
            var currentPosition = ImGui.GetCursorPos();
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddImageRounded(pfpWrap.ImGuiHandle, pos, pos + pfpWrap.Size, Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 128f);
            ImGui.SetCursorPos(new Vector2(currentPosition.X, currentPosition.Y + pfpWrap.Height));
        }
        ImGuiHelpers.ScaledRelativeSameLine(256, spacing);

        ImGui.TextWrapped("Base Import & Rounded Variant of ProfileImage." +
            Environment.NewLine + "The Rounded variant is as seen in the account page and in other profile inspections." +
            Environment.NewLine + "At the moment non-rounded serves no current purpose, but could in the future");
        // move down to the newline and draw the buttons for adding and removing images
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.FileUpload, "Upload new profile picture", 256f, false))
        {
            _fileDialogManager.OpenFileDialog("Select new Profile picture", ".png", (success, file) =>
            {
                if (!success)
                {
                    _logger.LogWarning("Failed to open file dialog.");
                    return;
                }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Attempting to upload new profile picture.");
                        var fileContent = File.ReadAllBytes(file);
                        using (MemoryStream ms = new(fileContent))
                        {
                            var format = await Image.DetectFormatAsync(ms).ConfigureAwait(false);
                            if (!format.FileExtensions.Contains("png", StringComparer.OrdinalIgnoreCase))
                            {
                                _showFileDialogError = true;
                                return;
                            }
                            // store the original file size
                            OriginalFileSize = $"{fileContent.Length / 1024.0:F2} KB";
                            _uploadedImageData = ms.ToArray();
                        }

                        // Load and process the image
                        using (var image = Image.Load<Rgba32>(fileContent))
                        {
                            // Calculate the scale factor to ensure the smallest dimension is 256 pixels
                            var scale = 256f / Math.Min(image.Width, image.Height);
                            var scaledSize = new Size((int)(image.Width * scale), (int)(image.Height * scale));

                            InitializeZoomFactors(image.Width, image.Height);

                            // Resize the image while maintaining the aspect ratio
                            var resizedImage = image.Clone(ctx => ctx.Resize(new ResizeOptions
                            {
                                Size = scaledSize,
                                Mode = ResizeMode.Max
                            }));

                            // Convert the processed image to byte array
                            using (var ms = new MemoryStream())
                            {
                                resizedImage.SaveAsPng(ms);
                                _scopedData = ms.ToArray();

                                // Initialize cropping parameters
                                _cropX = 0.5f;
                                _cropY = 0.5f;

                                _uploadedImageToShow = _uiSharedService.LoadImage(_uploadedImageData);
                                ScaledFileSize = $"{_scopedData.Length / 1024.0:F2} KB";

                                // Update the preview image
                                UpdateCroppedImagePreview();
                            }
                        }

                        _showFileDialogError = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload new profile picture.");
                    }
                });
            });
        }
        UiSharedService.AttachToolTip("Select and upload a new profile picture");

        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Trash, "Clear uploaded profile picture", 256f, false, !KeyMonitor.CtrlPressed()))
        {
            _uploadedImageData = null!;
            _croppedImageData = null!;
            _uploadedImageToShow = null;
            _croppedImageToShow = null;
            _useCompressedImage = false;
            _ = _apiHubMain.UserSetProfile(new UserProfileDto(new UserData(MainHub.UID), Disabled: false, "", Description: null));
        }
        UiSharedService.AttachToolTip("Clear your currently uploaded profile picture");
        if (_showFileDialogError)
        {
            UiSharedService.ColorTextWrapped("The profile picture must be a PNG file", ImGuiColors.DalamudRed);
        }

        ImGui.Separator();
        if (_uploadedImageData != null)
        {
            DrawNewProfileDisplay();
        }

        _uiSharedService.BigText("Profile Settings");
        var refText = profile.Description;
        ImGui.InputTextMultiline("##pfpDescription", ref refText, 1000, ImGuiHelpers.ScaledVector2(
            ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeightWithSpacing()*3));
        if(ImGui.IsItemDeactivatedAfterEdit())
        {
            profile.Description = refText;
        }
        /*
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Save, "Save Profile"))
        {
            // _ = _apiHubMain.UserSetProfile(new UserProfileDto(new UserData(MainHub.UID), Disabled: false, profile.Base64ProfilePicture, profile.Description));
        }*/
        UiSharedService.AttachToolTip("Updated your stored profile with latest information");
    }

    private void DrawNewProfileDisplay()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        _uiSharedService.BigText("Setup Profile Image");

        if (_uploadedImageData != null)
        {
            // display the respective info on the line below.
            if (_croppedImageData != null)
            {
                // ensure the wrap for the data is not yet null
                if (_croppedImageToShow != null)
                {
                    ImGui.Image(_croppedImageToShow.ImGuiHandle, ImGuiHelpers.ScaledVector2(_croppedImageToShow.Width, _croppedImageToShow.Height), Vector2.Zero, Vector2.One, ImGuiColors.DalamudWhite, ImGuiColors.DalamudWhite);

                    ImGuiHelpers.ScaledRelativeSameLine(256, spacing);
                    var currentPosition = ImGui.GetCursorPos();
                    var pos = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddImageRounded(_croppedImageToShow.ImGuiHandle, pos, pos + _croppedImageToShow.Size, Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 128f);
                    ImGui.SetCursorPos(new Vector2(currentPosition.X, currentPosition.Y + _croppedImageToShow.Height));
                }
           }
        }
        // draw the slider and the update buttons
        ImGuiHelpers.ScaledRelativeSameLine(256, spacing);
        using (var sizeDispGroup = ImRaii.Group())
        {
            if (_croppedImageData != null)
            {
                if (_uiSharedService.IconTextButton(FontAwesomeIcon.Compress, "Compress Image", 150f))
                {
                    CompressImage();
                }
                UiSharedService.AttachToolTip("Shrinks the image to a 512x512 ratio for better performance");

                var cropXref = _cropX;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Width", ref _cropX, 0.0f, 1.0f, "%.2f"))
                {
                    if (cropXref != _cropX)
                    {
                        UpdateCroppedImagePreview();
                    }
                }
                var cropYref = _cropY;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Height", ref _cropY, 0.0f, 1.0f, "%.2f"))
                {
                    if (cropYref != _cropY)
                    {
                        UpdateCroppedImagePreview();
                    }
                }

                // Add rotation slider
                var rotationRef = _rotationAngle;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Rotation", ref _rotationAngle, 0.0f, 360.0f, "%.2f"))
                {
                    if (rotationRef != _rotationAngle)
                    {
                        UpdateCroppedImagePreview();
                    }
                }
                UiSharedService.AttachToolTip("DOES NOT WORK YET!");

                // Add zoom slider
                var zoomRef = _zoomFactor;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.SliderFloat("Zoom", ref _zoomFactor, _minZoomFactor, _maxZoomFactor, "%.2f"))
                {
                    if (zoomRef != _zoomFactor)
                    {
                        UpdateCroppedImagePreview();
                    }
                }
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Original Image Size: " + OriginalFileSize);

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Scaled Image Size: " + ScaledFileSize);

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Cropped Image Size: " + CroppedFileSize);

            if (_uiSharedService.IconTextButton(FontAwesomeIcon.Upload, "Upload Profile Pic to Server", 200f, false, _croppedImageData == null))
            {
                _ = Task.Run(async () =>
                {
                    // grab the _croppedImageData and upload it to the server.
                    if (_croppedImageData == null) return;

                    // update the cropped image data to the 256x256 standard it should be.
                    using (var image = Image.Load<Rgba32>(_uploadedImageData))
                    {
                        // Resize the zoomed area to 512x512
                        var resizedImage = image.Clone(ctx => ctx.Resize(new ResizeOptions
                        {
                            Size = new Size(256, 256),
                            Mode = ResizeMode.Max
                        }));


                        // Convert the processed image to byte array
                        using (var ms = new MemoryStream())
                        {
                            resizedImage.SaveAsPng(ms);
                            _compressedImageData = ms.ToArray();
                            _logger.LogTrace("New Image File Size: " + _compressedImageData.Length / 1024.0 + " KB");
                            _logger.LogDebug($"Sending Image to server with: {resizedImage.Width}x{resizedImage.Height} [Original: {image.Width}x{image.Height}]");
                        }
                    }
                    try
                    {
                        await _apiHubMain.UserSetProfile(new UserProfileDto(new UserData(MainHub.UID), Disabled: false, Convert.ToBase64String(_croppedImageData!), Description: null)).ConfigureAwait(false);
                        _logger.LogInformation("Image Sent to server successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send image to server.");
                    }
                });
            }


        }
        ImGui.Separator();
    }

    private void InitializeZoomFactors(int width, int height)
    {
        var lesserDimension = Math.Min(width, height);
        _minZoomFactor = 1.0f;
        _maxZoomFactor = lesserDimension / 256.0f; // Ensure the minimum zoomed area is 256x256
    }

    public void CompressImage()
    {
        if (_uploadedImageData == null) return;

        using (var image = Image.Load<Rgba32>(_uploadedImageData))
        {
            // Calculate the lesser dimension of the original image
            var lesserDimension = Math.Min(image.Width, image.Height);

            // Calculate the cropping rectangle to make the image square
            var cropRectangle = new Rectangle(0, 0, lesserDimension, lesserDimension);
            cropRectangle.X = (image.Width - lesserDimension) / 2;
            cropRectangle.Y = (image.Height - lesserDimension) / 2;

            // Crop the image to the lesser dimension
            var croppedImage = image.Clone(ctx => ctx.Crop(cropRectangle));

            var desiredSize = new Size(512, 512);

            // Resize the zoomed area to 512x512
            var resizedImage = croppedImage.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = desiredSize,
                Mode = ResizeMode.Max
            }));


            // Convert the processed image to byte array
            using (var ms = new MemoryStream())
            {
                resizedImage.SaveAsPng(ms);
                _compressedImageData = ms.ToArray();

                // Load the cropped image for preview
                _compressedImageToShow = _uiSharedService.LoadImage(_compressedImageData);
                _useCompressedImage = true;
                // _logger.LogDebug($"New Image width and height is: {resizedImage.Width}x{resizedImage.Height} from {croppedImage.Width}x{croppedImage.Height}");
                CroppedFileSize = $"{_croppedImageData.Length / 1024.0:F2} KB";

                InitializeZoomFactors(resizedImage.Width, resizedImage.Height);
            }
        }
    }

    private void UpdateCroppedImagePreview()
    {
        // Ensure the image data is not 
        if (_uploadedImageData == null) return;


        using (var image = Image.Load<Rgba32>(_useCompressedImage ? _compressedImageData : _uploadedImageData))
        {
            var desiredSize = new Size(256, 256);
            // Calculate the lesser dimension of the original image
            var lesserDimension = Math.Min(image.Width, image.Height);

            // Calculate the size of the zoomed area based on the zoom factor
            var zoomedWidth = (int)(lesserDimension / _zoomFactor);
            var zoomedHeight = (int)(lesserDimension / _zoomFactor);

            // Ensure the zoomed area is at least 256x256
            zoomedWidth = Math.Max(zoomedWidth, 256);
            zoomedHeight = Math.Max(zoomedHeight, 256);

            // Ensure the zoomed area does not exceed the lesser dimension of the original image
            zoomedWidth = Math.Min(zoomedWidth, lesserDimension);
            zoomedHeight = Math.Min(zoomedHeight, lesserDimension);

            // Calculate the cropping rectangle based on the user's alignment selection
            var cropRectangle = new Rectangle(0, 0, zoomedWidth, zoomedHeight);
            cropRectangle.X = Math.Max(0, Math.Min((int)((image.Width - zoomedWidth) * _cropX), image.Width - zoomedWidth));
            cropRectangle.Y = Math.Max(0, Math.Min((int)((image.Height - zoomedHeight) * _cropY), image.Height - zoomedHeight));

            // Ensure the crop rectangle is within the image bounds
            cropRectangle.Width = Math.Min(cropRectangle.Width, image.Width - cropRectangle.X);
            cropRectangle.Height = Math.Min(cropRectangle.Height, image.Height - cropRectangle.Y);

            var zoomedImage = image.Clone(ctx => ctx.Crop(cropRectangle));

            // Resize the zoomed area to 256x256
            var croppedImage = zoomedImage.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = desiredSize,
                Mode = ResizeMode.Max
            }));


            // Convert the processed image to byte array
            using (var ms = new MemoryStream())
            {
                croppedImage.SaveAsPng(ms);
                _croppedImageData = ms.ToArray();

                // Load the cropped image for preview
                _croppedImageToShow = _uiSharedService.LoadImage(_croppedImageData);
                CroppedFileSize = $"{_croppedImageData.Length / 1024.0:F2} KB";
                // _logger.LogInformation($"Cropped image to {cropRectangle}");
            }
        }
    }

}
