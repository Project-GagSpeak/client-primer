using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI.Profile;

public class PopoutKinkPlateUi : WindowMediatorSubscriberBase
{
    private readonly ProfileService _gagspeakProfileManager;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly UiSharedService _uiShared;
    private Pair? _pair; // pair to display the profile of.

    public PopoutKinkPlateUi(ILogger<PopoutKinkPlateUi> logger, GagspeakMediator mediator,
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
            var mainPos = msg.Position == Vector2.Zero ? _uiShared.LastMainUIWindowPosition : msg.Position;
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

        // create a child window to house this information.
        using (ImRaii.Child($"PopoutWindow{_pair.UserData.AliasOrUID}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 0), false, ImGuiWindowFlags.NoScrollbar))
        {

            var spacing = ImGui.GetStyle().ItemSpacing;
            // grab our profile.
            var pairProfile = _gagspeakProfileManager.GetGagspeakProfile(_pair.UserData);

            // check if flagged
            if (pairProfile.Flagged)
            {
                UiSharedService.ColorTextWrapped(pairProfile.Description, ImGuiColors.DalamudRed);
                return;
            }

            var pfpWrap = pairProfile.GetCurrentProfileOrDefault();

            if (!(pfpWrap is { } wrap))
            {
                /* Consume Wrap until Generated */
            }
            else
            {
                var region = ImGui.GetContentRegionAvail();
                ImGui.Spacing();
                // move the x position so that it centeres the image to the center of the window.
                _uiShared.SetCursorXtoCenter(pfpWrap.Width);
                var currentPosition = ImGui.GetCursorPos();

                var pos = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddImageRounded(wrap.ImGuiHandle, pos, pos + pfpWrap.Size, Vector2.Zero, Vector2.One,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 128f);
                ImGui.SetCursorPos(new Vector2(currentPosition.X, currentPosition.Y + pfpWrap.Height));
            }
        }
    }
}
