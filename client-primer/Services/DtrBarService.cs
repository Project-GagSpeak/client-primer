using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI.Utils;
using Microsoft.Extensions.Hosting;
using System.Formats.Tar;
using System.Net;
using GagSpeak.Utils;

namespace GagSpeak.UpdateMonitoring;

/// <summary>
/// The service responsible for handling framework updates and other Dalamud related services.
/// </summary>
public class DtrBarService : DisposableMediatorSubscriberBase
{
    private readonly PairManager _pairManager;
    private readonly IClientState _clientState;
    private readonly IDtrBar _dtrBar;
    private readonly IObjectTable _objectTable;

    public DtrBarService(ILogger<DtrBarService> logger,
        GagspeakMediator mediator, PairManager pairManager,
        IClientState clientState, IDtrBar dtrBar, 
        IObjectTable objectTable) : base(logger, mediator)
    {
        _pairManager = pairManager;
        _clientState = clientState;
        _dtrBar = dtrBar;
        _objectTable = objectTable;

        PrivacyEntry = _dtrBar.Get("GagSpeakPrivacy");
        PrivacyEntry.Shown = true;
        UpdateMessagesEntry = _dtrBar.Get("GagSpeakUpdateMessages");
        UpdateMessagesEntry.Shown = false;
        VibratorEntry = _dtrBar.Get("GagSpeakVibrator");
        UpdateMessagesEntry.Shown = false;

        //Mediator.Subscribe<ToggleDtrBarMessage>(this, (_) => DtrBarEnabled = !DtrBarEnabled);
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => UpdateDtrBar());
    }

    protected override void Dispose(bool disposing)
    {
        PrivacyEntry.Remove();
        UpdateMessagesEntry.Remove();
        VibratorEntry.Remove();
        base.Dispose(disposing);
    }

    public IDtrBarEntry PrivacyEntry { get; private set; }
    public IDtrBarEntry UpdateMessagesEntry { get; private set; }
    public IDtrBarEntry VibratorEntry { get; private set; }


    public List<IPlayerCharacter> ObjectTablePlayers()
        => _objectTable.OfType<IPlayerCharacter>().ToList();

    // Alarm == BitmapIcon for notifications.
    private void UpdateDtrBar()
    {
        if(PrivacyEntry.Shown == true)
        {
            // update the privacy dtr bar
            var visiblePairGameObjects = _pairManager.GetVisiblePairGameObjects();
            // get players not included in our gagspeak pairs.
            var playersNotInPairs = ObjectTablePlayers()
                .Where(player => player != _clientState.LocalPlayer && !visiblePairGameObjects.Contains(player))
                .ToList();

            var displayedPlayers = playersNotInPairs.Take(10).ToList();
            var remainingCount = playersNotInPairs.Count - displayedPlayers.Count;

            // set the text based on if privacy was breeched or not.
            BitmapFontIcon DisplayIcon = playersNotInPairs.Any() ? BitmapFontIcon.Warning : BitmapFontIcon.Recording;
            string TextDisplay = playersNotInPairs.Any() ? (playersNotInPairs.Count + " Others Visible") : "Only Pairs Visible";
            // Limit to 10 players and indicate if there are more
            string TooltipDisplay = playersNotInPairs.Any()
                ? "Non-GagSpeak Players:\n" + string.Join("\n", displayedPlayers.Select(player => player.Name.ToString() + "  " + player.HomeWorld.GameData!.Name)) +
                  (remainingCount > 0 ? $"\nand {remainingCount} others..." : string.Empty)
                : "Only GagSpeak Pairs Visible";
            // pair display string for tooltip.
            PrivacyEntry.Text = new SeString(new IconPayload(DisplayIcon),new TextPayload(TextDisplay));
            PrivacyEntry.Tooltip = new SeString(new TextPayload(TooltipDisplay));
        }
    }
}

