using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Profile;

public class KinkPlateLightUI : WindowMediatorSubscriberBase
{
    private readonly KinkPlateLight _lightUI;
    private readonly KinkPlateService _KinkPlateManager;
    private readonly PairManager _pairManager;
    private readonly UiSharedService _uiShared;
    private bool _showFullUID;

    private bool ThemePushed = false;

    public KinkPlateLightUI(ILogger<KinkPlateLightUI> logger, GagspeakMediator mediator,
        KinkPlateLight plateLightUi, KinkPlateService KinkPlateManager,
        PairManager pairManager, UiSharedService uiShared, UserData pairUserData)
        : base(logger, mediator, "###GagSpeakKinkPlateLight" + pairUserData.UID)
    {
        _lightUI = plateLightUi;
        _KinkPlateManager = KinkPlateManager;
        _pairManager = pairManager;
        _uiShared = uiShared;

        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        Size = new(256, 512);
        IsOpen = true;

        _showFullUID = _pairManager.DirectPairs.Any(x => x.UserData.UID == pairUserData.UID);
        UserDataToDisplay = pairUserData;
    }

    public UserData UserDataToDisplay { get; init; }

    protected override void PreDrawInternal()
    {
        if (!ThemePushed)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 35f);

            ThemePushed = true;
        }
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
        if (UserDataToDisplay is null)
            return;

        // obtain the profile for this userPair.
        var KinkPlate = _KinkPlateManager.GetKinkPlate(UserDataToDisplay);
        if (KinkPlate.KinkPlateInfo.Flagged)
        {
            ImGui.TextUnformatted("This profile is flagged by moderation.");
            return;
        }

        string DisplayName = _showFullUID
            ? UserDataToDisplay.AliasOrUID
            : "Anon.Kinkster-" + UserDataToDisplay.UID.Substring(UserDataToDisplay.UID.Length - 3);

        // draw the plate.
        _lightUI.DrawKinkPlateLight(KinkPlate, DisplayName, UserDataToDisplay, _showFullUID, true, () => this.IsOpen = false);
    }

    public override void OnClose()
    {
        Mediator.Publish(new RemoveWindowMessage(this));
    }
}
