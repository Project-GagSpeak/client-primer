using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Profile;

public class PopoutProfileUi : WindowMediatorSubscriberBase
{
    private readonly ProfileService _gagspeakProfileManager;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly UiSharedService _uiShared;
    private byte[] _lastProfilePicture = []; // stores the image data byte array of the last displayed image for reference.
    private Pair? _pair; // pair to display the profile of.
    private IDalamudTextureWrap? _textureWrap;

    public PopoutProfileUi(ILogger<PopoutProfileUi> logger, GagspeakMediator mediator, 
        UiSharedService uiBuilder, ServerConfigurationManager serverManager, 
        GagspeakConfigService gagspeakConfigService, ProfileService gagspeakProfileManager, 
        PairManager pairManager) : base(logger, mediator, "###GagSpeakPopoutProfileUI")
    {
        _uiShared = uiBuilder;
        _serverConfigs = serverManager;
        _gagspeakProfileManager = gagspeakProfileManager;
        _pairManager = pairManager;
        Flags = ImGuiWindowFlags.NoDecoration;

        Mediator.Subscribe<ProfilePopoutToggle>(this, (msg) =>
        {
            IsOpen = msg.Pair != null; // only open if the pair sent is not null
            _pair = msg.Pair; // set the pair to display the popout profile for.
            _lastProfilePicture = []; 
            _textureWrap?.Dispose();
            _textureWrap = null;
        });

        // unsure how accurate this positioning is just yet.
        Mediator.Subscribe<CompactUiChange>(this, (msg) =>
        {
            if (msg.Size != Vector2.Zero)
            {
                var border = ImGui.GetStyle().WindowBorderSize;
                var padding = ImGui.GetStyle().WindowPadding;
                Size = new(256 + (padding.X * 2) + border, _uiShared.LastMainUIWindowSize.Y / ImGuiHelpers.GlobalScale);
            }
            var mainPos = _uiShared.LastMainUIWindowSize;
            if (gagspeakConfigService.Current.ProfilePopoutRight)
            {
                Position = new(mainPos.X + _uiShared.LastMainUIWindowSize.X * ImGuiHelpers.GlobalScale, mainPos.Y);
            }
            else
            {
                Position = new(mainPos.X - Size!.Value.X * ImGuiHelpers.GlobalScale, mainPos.Y);
            }
        });

        IsOpen = false;
    }

    protected override void PreDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }
    protected override void PostDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }

    protected override void DrawInternal()
    {
        // do not display if pair is null.
        if (_pair == null) return;

        try
        {
            var spacing = ImGui.GetStyle().ItemSpacing;

            // grab the profile to display.
            var gagspeakProfile = _gagspeakProfileManager.GetGagspeakProfile(_pair.UserData);

            // if the profile to display is different from the last stored image, recreate the new wrap (shouldnt need this?)
            if (_textureWrap == null || !gagspeakProfile.ImageData.Value.SequenceEqual(_lastProfilePicture))
            {
                _textureWrap?.Dispose();
                _lastProfilePicture = gagspeakProfile.ImageData.Value;
                _textureWrap = _uiShared.LoadImage(_lastProfilePicture);
            }

            var drawList = ImGui.GetWindowDrawList();
            var rectMin = drawList.GetClipRectMin();
            var rectMax = drawList.GetClipRectMax();

            using (_uiShared.UidFont.Push())
                UiSharedService.ColorText(_pair.UserData.AliasOrUID, ImGuiColors.HealerGreen);

            ImGuiHelpers.ScaledDummy(spacing.Y, spacing.Y);
            var textPos = ImGui.GetCursorPosY();
            ImGui.Separator();
            var imagePos = ImGui.GetCursorPos();
            ImGuiHelpers.ScaledDummy(256, 256 * ImGuiHelpers.GlobalScale + spacing.Y);
            var note = _serverConfigs.GetNicknameForUid(_pair.UserData.UID);
            if (!string.IsNullOrEmpty(note))
            {
                UiSharedService.ColorText(note, ImGuiColors.DalamudGrey);
            }
            string status = _pair.IsVisible ? "Visible" : (_pair.IsOnline ? "Online" : "Offline");
            UiSharedService.ColorText(status, (_pair.IsVisible || _pair.IsOnline) ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed);
            if (_pair.IsVisible)
            {
                ImGui.SameLine();
                ImGui.TextUnformatted($"({_pair.PlayerName})");
            }
            if (_pair.UserPair.IndividualPairStatus == GagspeakAPI.Data.Enum.IndividualPairStatus.Bidirectional)
            {
                ImGui.TextUnformatted("Directly paired");
            }

            ImGui.Separator();
            var font = _uiShared.GameFont.Push();
            var remaining = ImGui.GetWindowContentRegionMax().Y - ImGui.GetCursorPosY();
            var descText = gagspeakProfile.Description;
            var textSize = ImGui.CalcTextSize(descText, 256f * ImGuiHelpers.GlobalScale);
            bool trimmed = textSize.Y > remaining;
            while (textSize.Y > remaining && descText.Contains(' '))
            {
                descText = descText[..descText.LastIndexOf(' ')].TrimEnd();
                textSize = ImGui.CalcTextSize(descText + $"...{Environment.NewLine}[Open Full Profile for complete description]", 256f * ImGuiHelpers.GlobalScale);
            }
            UiSharedService.TextWrapped(trimmed ? descText + $"...{Environment.NewLine}[Open Full Profile for complete description]" : gagspeakProfile.Description);
            font.Dispose();

            var padding = ImGui.GetStyle().WindowPadding.X / 2;
            bool tallerThanWide = _textureWrap.Height >= _textureWrap.Width;
            var stretchFactor = tallerThanWide ? 256f * ImGuiHelpers.GlobalScale / _textureWrap.Height : 256f * ImGuiHelpers.GlobalScale / _textureWrap.Width;
            var newWidth = _textureWrap.Width * stretchFactor;
            var newHeight = _textureWrap.Height * stretchFactor;
            var remainingWidth = (256f * ImGuiHelpers.GlobalScale - newWidth) / 2f;
            var remainingHeight = (256f * ImGuiHelpers.GlobalScale - newHeight) / 2f;
            drawList.AddImage(_textureWrap.ImGuiHandle, new Vector2(rectMin.X + padding + remainingWidth, rectMin.Y + spacing.Y + imagePos.Y + remainingHeight),
                new Vector2(rectMin.X + padding + remainingWidth + newWidth, rectMin.Y + spacing.Y + imagePos.Y + remainingHeight + newHeight));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during draw tooltip");
        }
    }
}
