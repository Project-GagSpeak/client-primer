using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI.Profile;

public class PopoutKinkPlateUi : WindowMediatorSubscriberBase
{
    private readonly KinkPlateLightUI _lightUI;
    private readonly KinkPlateService _KinkPlateManager;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly UiSharedService _uiShared;
    private UserData? _userDataToDisplay;

    private bool ThemePushed = false;

    public PopoutKinkPlateUi(ILogger<PopoutKinkPlateUi> logger, GagspeakMediator mediator,
        UiSharedService uiBuilder, ServerConfigurationManager serverManager,
        GagspeakConfigService gagspeakConfigService, KinkPlateLightUI plateLightUi,
        KinkPlateService KinkPlateManager, PairManager pairManager)
        : base(logger, mediator, "###GagSpeakPopoutProfileUI")
    {
        _lightUI = plateLightUi;
        _uiShared = uiBuilder;
        _serverConfigs = serverManager;
        _KinkPlateManager = KinkPlateManager;
        _pairManager = pairManager;
        Flags = ImGuiWindowFlags.NoDecoration;

        Mediator.Subscribe<ProfilePopoutToggle>(this, (msg) =>
        {
            _logger.LogDebug("Profile Popout Toggle Message Received.");
            IsOpen = msg.PairUserData != null; // only open if the pair sent is not null
            _userDataToDisplay = msg.PairUserData; // set the pair to display the popout profile for.
        });

        IsOpen = false;
    }

    protected override void PreDrawInternal()
    {
        if (!ThemePushed)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 35f);

            ThemePushed = true;
        }

        var position = _uiShared.LastMainUIWindowPosition;
        position.X -= 256;
        ImGui.SetNextWindowPos(position);

        Flags |= ImGuiWindowFlags.NoMove;

        var size = new Vector2(256, 512);

        ImGui.SetNextWindowSize(size);
    }
    protected override void PostDrawInternal()
    {
        if (ThemePushed)
        {
            ImGui.PopStyleVar(2);
            ThemePushed = false;
        }
    }

    protected override void DrawInternal()
    {
        // do not display if pair is null.
        if (_userDataToDisplay is null)
            return;

        // obtain the profile for this userPair.
        var KinkPlate = _KinkPlateManager.GetKinkPlate(_userDataToDisplay);
        if (KinkPlate.KinkPlateInfo.Flagged)
        {
            ImGui.TextUnformatted("This profile is flagged by moderation.");
            return;
        }

        string DisplayName = KinkPlate.KinkPlateInfo.PublicPlate
            ? _userDataToDisplay.AliasOrUID
            : "Anon.Kinkster-" + _userDataToDisplay.UID.Substring(_userDataToDisplay.UID.Length - 3);

        // draw the plate.
        _lightUI.DrawKinkPlateLight(KinkPlate, DisplayName, _userDataToDisplay, false, () => this.IsOpen = false);
    }
}
