using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using System.Numerics;
namespace GagSpeak.UI.UiGagSetup;

public class ActiveGagsPanel
{
    private readonly ILogger<ActiveGagsPanel> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;
    private readonly PlayerCharacterManager _playerManager; // for grabbing lock data
    private readonly PadlockHandler _lockHandler;

    public ActiveGagsPanel(ILogger<ActiveGagsPanel> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        PadlockHandler padlockHandler, PlayerCharacterManager playerManager)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
        _playerManager = playerManager;
        _lockHandler = padlockHandler;
    }

    private string GagTypeOnePath => $"ItemMouth\\{_playerManager.AppearanceData.SlotOneGagType}.png" ?? $"ItemMouth\\None.png";
    private string GagTypeTwoPath => $"ItemMouth\\{_playerManager.AppearanceData.SlotTwoGagType}.png" ?? $"ItemMouth\\None.png";
    private string GagTypeThreePath => $"ItemMouth\\{_playerManager.AppearanceData.SlotThreeGagType}.png" ?? $"ItemMouth\\None.png";
    private string GagPadlockOnePath => $"Padlocks\\{_playerManager.AppearanceData.SlotOneGagPadlock}.png" ?? $"Padlocks\\None.png";
    private string GagPadlockTwoPath => $"Padlocks\\{_playerManager.AppearanceData.SlotTwoGagPadlock}.png" ?? $"Padlocks\\None.png";
    private string GagPadlockThreePath => $"Padlocks\\{_playerManager.AppearanceData.SlotThreeGagPadlock}.png" ?? $"Padlocks\\None.png";
    private string GagTypeOne => _playerManager.AppearanceData.SlotOneGagType;
    private string GagTypeTwo => _playerManager.AppearanceData.SlotTwoGagType;
    private string GagTypeThree => _playerManager.AppearanceData.SlotThreeGagType;
    private string PadlockOne => _playerManager.AppearanceData.SlotOneGagPadlock;
    private string PadlockTwo => _playerManager.AppearanceData.SlotTwoGagPadlock;
    private string PadlockThree => _playerManager.AppearanceData.SlotThreeGagPadlock;

    // Draw the active gags tab
    public void DrawActiveGagsPanel()
    {
        var region = ImGui.GetContentRegionAvail();
        try
        {
            _uiSharedService.BigText("Inner Gag:");

            var lock1 = _uiSharedService.GetPadlock(PadlockOne);
            var lock2 = _uiSharedService.GetPadlock(PadlockTwo);
            var lock3 = _uiSharedService.GetPadlock(PadlockThree);

            // Selection 1
            DrawGagAndLockSection(0, GagTypeOnePath, GagPadlockOnePath, (lock1 != Padlocks.None),
                GagList.AliasToGagTypeMap[GagTypeOne], (lock1 == Padlocks.None ? _lockHandler.PadlockPrevs[0] : lock1));

            // Selection 2
            _uiSharedService.BigText("Central Gag:");

            DrawGagAndLockSection(1, GagTypeTwoPath, GagPadlockTwoPath, (lock2 != Padlocks.None),
                GagList.AliasToGagTypeMap[GagTypeTwo], (lock2 == Padlocks.None ? _lockHandler.PadlockPrevs[1] : lock2));

            // Selection 3
            _uiSharedService.BigText("Outermost Gag:");

            DrawGagAndLockSection(2, GagTypeThreePath, GagPadlockThreePath, (lock3 != Padlocks.None),
                GagList.AliasToGagTypeMap[GagTypeThree], (lock3 == Padlocks.None ? _lockHandler.PadlockPrevs[2] : lock3));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex}");
        }
    }



    private void DrawGagAndLockSection(int slotNumber, string gagTypePath, string lockTypePath, bool currentlyLocked, GagList.GagType gagType, Padlocks padlockType)
    {
        using (var gagAndLockOuterGroup = ImRaii.Group())
        {
            // Display gag image
            var gagOneTexture = _uiSharedService.GetImageFromDirectoryFile(gagTypePath);
            if (!(gagOneTexture is { } wrapGag))
            {
                _logger.LogWarning("Failed to render image!");
            }
            else
            {
                ImGui.Image(wrapGag.ImGuiHandle, new Vector2(80, 80));
            }
            ImGui.SameLine();

            // Display combo for gag type and lock
            using (var gagAndLockInnerGroup = ImRaii.Group())
            {
                bool isLocked = padlockType != Padlocks.None;
                _uiSharedService.DrawComboSearchable($"Gag Type {slotNumber}", 250, ref _lockHandler.Filters[slotNumber],
                    Enum.GetValues<GagList.GagType>(), (gag) => gag.GetGagAlias(), false,
                    (i) =>
                    {
                        // locate the GagData that matches the alias of i
                        var SelectedGag = GagList.AliasToGagTypeMap[i.GetGagAlias()];
                        _mediator.Publish(new GagTypeChanged(SelectedGag, (GagLayer)slotNumber));
                        _mediator.Publish(new UpdateGlamourGagsMessage(SelectedGag == GagList.GagType.None ? UpdatedNewState.Disabled : UpdatedNewState.Enabled,
                            (GagLayer)slotNumber, SelectedGag, "SelfApplied"));
                        // Update gag type based on selection
                    }, gagType);
                // draw the padlock dropdown
                using (ImRaii.Disabled(gagType == GagList.GagType.None || currentlyLocked))
                {
                    _uiSharedService.DrawCombo($"##Lock Type {slotNumber}", (248 - _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Lock).X),
                    Enum.GetValues<Padlocks>(), (padlock) => padlock.ToString(),
                    (i) =>
                    {
                        _lockHandler.PadlockPrevs[slotNumber] = i;
                    }, padlockType);
                }
                ImGui.SameLine(0, 2);

                using (ImRaii.Disabled(padlockType == Padlocks.None))
                {
                    // draw the lock button
                    if (_uiSharedService.IconButton(currentlyLocked ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock))
                    {
                        if (_lockHandler.PasswordValidated(slotNumber, currentlyLocked))
                        {
                            _mediator.Publish(new GagLockToggle(new PadlockData(
                                (GagLayer)slotNumber,
                                _lockHandler.PadlockPrevs[slotNumber],
                                _lockHandler.Passwords[slotNumber],
                                UiSharedService.GetEndTime(_lockHandler.Timers[slotNumber]), "SelfApplied"), currentlyLocked));
                        }
                        // reset the password and timer
                        _lockHandler.Passwords[slotNumber] = string.Empty;
                        _lockHandler.Timers[slotNumber] = string.Empty;
                    }
                }
                // display associated password field for padlock type.
                _lockHandler.DisplayPasswordField(slotNumber);
            }

            // Display lock image if we should
            if (_uiSharedService.GetPadlock(PadlockTwo) != Padlocks.None)
            {
                ImGui.SameLine();
                using (var lockGroup = ImRaii.Group())
                {
                    var lockTexture = _uiSharedService.GetImageFromDirectoryFile(lockTypePath);
                    if (!(lockTexture is { } wrapLock))
                    {
                        _logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        ImGui.Image(wrapLock.ImGuiHandle, new Vector2(80, 80));
                    }
                }
            }

            // ImGui.SameLine();
            // display the lock timer if we should


        } // end of the USING IMRAII.GROUP here
    }
}