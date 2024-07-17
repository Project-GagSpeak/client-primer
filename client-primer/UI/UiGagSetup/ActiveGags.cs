using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using Lumina.Misc;
using OtterGui;
using Penumbra.GameData.Files.Utility;
using System.Numerics;
namespace GagSpeak.UI.UiGagSetup;

public class ActiveGags
{
    private readonly ILogger<ActiveGags> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly IDalamudPluginInterface _pi;
    private readonly UiSharedService _uiSharedService;
    private readonly PlayerCharacterManager _playerManager; // for grabbing lock data
    private readonly PadlockHandler _lockHandler;
    private ITextureProvider _textureProvider;
    // gag images
    private ISharedImmediateTexture _sharedSetupImage;

    public ActiveGags(ILogger<ActiveGags> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, PadlockHandler padlockHandler,
        PlayerCharacterManager playerManager, ITextureProvider textureProvider,
        IDalamudPluginInterface pi)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
        _playerManager = playerManager;
        _lockHandler = padlockHandler;
        _pi = pi;
    }

    // Draw the active gags tab
    private void DrawActiveGagsPanel()
    {
        try
        {
            var region = ImGui.GetContentRegionAvail();
            using (_uiSharedService.UidFont.Push()) 
            { 
                ImGui.Text("Under Layer Gag:"); 
            }
            // create a group for the listing:
            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.SlotOneGagPadlock, true, out var Padlock);

            DrawGagAndLockSection(0, $"ItemMouth\\{_playerManager.AppearanceData.SlotOneGagType}.png",
                $"Padlocks\\{_playerManager.AppearanceData.SlotOneGagPadlock}.png",
                Padlock != Padlocks.None,
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.SlotOneGagType],
                Padlock == Padlocks.None ? _lockHandler.PadlockPrevs[0] : Padlock);

            using (_uiSharedService.UidFont.Push()) 
            { 
                ImGui.Text("Middle Layer Gag:"); 
            }
            // draw the listing
            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.SlotTwoGagPadlock, true, out var Padlock2);

            DrawGagAndLockSection(1, $"ItemMouth\\{_playerManager.AppearanceData.SlotTwoGagType}.png", 
                $"Padlocks\\{_playerManager.AppearanceData.SlotTwoGagPadlock}.png",
                Padlock2 != Padlocks.None,
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.SlotTwoGagType],
                Padlock2 == Padlocks.None ? _lockHandler.PadlockPrevs[1] : Padlock2);

            using (_uiSharedService.UidFont.Push()) 
            { 
                ImGui.Text("Upper Layer Gag:"); 
            }
            // Draw it
            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.SlotThreeGagPadlock, true, out var Padlock3);

            DrawGagAndLockSection(2,
                $"ItemMouth\\{_playerManager.AppearanceData.SlotThreeGagType}.png",
                $"Padlocks\\{_playerManager.AppearanceData.SlotThreeGagPadlock}.png",
                Padlock3 != Padlocks.None,
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.SlotThreeGagType],
                Padlock3 == Padlocks.None ? _lockHandler.PadlockPrevs[2] : Padlock3);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex}");
        }
    }



    private void DrawGagAndLockSection(int slotNumber, string gagTypePath, string lockTypePath, bool currentlyLocked, GagList.GagType gagType, Padlocks padlockType)
    {
        using (var gaglockOuterGroup = ImRaii.Group())
        {
            // Display gag image
            _sharedSetupImage = _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, gagTypePath));
            if (!(_sharedSetupImage.GetWrapOrEmpty() is { } wrapGag)) 
            {
                _logger.LogWarning("Failed to render image!");
            }
            else 
            { 
                ImGui.Image(wrapGag.ImGuiHandle, new Vector2(80, 80));
            }
            ImGui.SameLine();

            // Display combo for gag type and lock
            using (var gaglockInnerGroup = ImRaii.Group())
            {
                bool isLocked = padlockType != Padlocks.None;
                _uiSharedService.DrawComboSearchable($"Gag Type {slotNumber}", 250, ref _lockHandler.Filters[slotNumber],
                    Enum.GetValues<GagList.GagType>(), (gag) => gag.GetGagAlias(), false,
                    (i) =>
                    {
                        // locate the GagData that matches the alias of i
                        var SelectedGag = GagList.AliasToGagTypeMap[i.GetGagAlias()];
                        _mediator.Publish(new GagTypeChanged(SelectedGag, (GagLayer)slotNumber));
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
                            _mediator.Publish(new GagLockToggle(new PadlockData((GagLayer)slotNumber, _lockHandler.PadlockPrevs[slotNumber], 
                                _lockHandler.Passwords[slotNumber], UiSharedService.GetEndTime(_lockHandler.Timers[slotNumber]), "SelfApplied"), currentlyLocked));
                            _lockHandler.Passwords[slotNumber] = string.Empty;
                            _lockHandler.Timers[slotNumber] = string.Empty;
                        }
                        else
                        {
                            _lockHandler.Passwords[slotNumber] = string.Empty;
                            _lockHandler.Timers[slotNumber] = string.Empty;
                        }
                    }
                    _lockHandler.DisplayPasswordField(slotNumber);
                }
            }
            ImGui.SameLine();
            // Display lock image
            _sharedSetupImage = _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, lockTypePath));
            if (!(_sharedSetupImage.GetWrapOrEmpty() is { } wrapLock)) 
            {
                _logger.LogWarning("Failed to render image!");
            }
            else
            {
                ImGui.Image(wrapLock.ImGuiHandle, new Vector2(80, 80));
            }
        }
    }
}
