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

public class GagSetupUI : WindowMediatorSubscriberBase
{
    private readonly IDalamudPluginInterface _pi;
    private readonly UiSharedService _uiSharedService;
    private readonly GagSetupTabMenu _tabMenu;
    private readonly ActiveGags _activeGags;
    private readonly PadlockHandler _lockHandler;
    private readonly PlayerCharacterManager _playerManager; // for grabbing lock data
    private ITextureProvider _textureProvider;
    private ISharedImmediateTexture _sharedSetupImage;
    // gag images

    public GagSetupUI(ILogger<GagSetupUI> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, ActiveGags activeGags, 
        PadlockHandler padlockHandler, PlayerCharacterManager playerManager, 
        ITextureProvider textureProvider, IDalamudPluginInterface pi) : base(logger, mediator, "Gag Setup UI")
    {
        _textureProvider = textureProvider;
        _uiSharedService = uiSharedService;
        _playerManager = playerManager;
        _lockHandler = padlockHandler;
        _pi = pi;

        _tabMenu = new GagSetupTabMenu();

        // define initial size of window and to not respect the close hotkey.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        RespectCloseHotkey = false;
    }
    // perhaps migrate the opened selectable for the UIShared service so that other trackers can determine if they should refresh / update it or not.
    // (this is not yet implemented, but we can modify it later when we need to adapt)

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
        // get information about the window region, its item spacing, and the topleftside height.
        var region = ImGui.GetContentRegionAvail();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        var topLeftSideHeight = region.Y;

        // create the draw-table for the selectable and viewport displays
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f * ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f), 0));
        try
        {
            using (var table = ImRaii.Table($"GagSetupUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
            {
                if (!table) return;

                // define the left column, which contains an image of the component (added later), and the list of 'compartments' within the setup to view.
                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, 200f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("##RightColumn", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();

                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child($"###GagSetupLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                {
                    // attempt to obtain an image wrap for it
                    _sharedSetupImage = _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "icon.png"));

                    // if the image was valid, display it (at rescaled size
                    if (!(_sharedSetupImage.GetWrapOrEmpty() is { } wrap))
                    {
                        _logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        // aligns the image in the center like we want.
                        UtilsExtensions.ImGuiLineCentered("###GagSetupLogo", () =>
                        {
                            ImGui.Image(wrap.ImGuiHandle, 
                                        new(125f * ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f),
                                            125f * ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f)
                                        ));

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text($"You found a wild easter egg, Y I P P E E !!!");
                                ImGui.EndTooltip();
                            }
                        });
                    }
                    // add separator
                    ImGui.Spacing();
                    ImGui.Separator();
                    // add the tab menu for the left side.
                    using (_uiSharedService.UidFont.Push())
                    {
                        _tabMenu.DrawSelectableTabMenu();
                    }
                }
                // pop pushed style variables and draw next column.
                ImGui.PopStyleVar();
                ImGui.TableNextColumn();
                // display right half viewport based on the tab selection
                using (var rightChild = ImRaii.Child($"###GagSetupRight", Vector2.Zero, false))
                {
                    switch (_tabMenu.SelectedTab)
                    {
                        case GagsetupTabSelection.ActiveGags: // shows the interface for inspecting or applying your own gags.
                            DrawActiveGagsPanel();
                            break;
                        case GagsetupTabSelection.Lockpicker: // shows off the gag storage configuration for the user's gags.
                            DrawLockPickerPanel();
                            break;
                        case GagsetupTabSelection.GagStorage: // fancy WIP thingy to give players access to features based on achievements or unlocks.
                            DrawGagStoragePanel();
                            break;
                        case GagsetupTabSelection.ProfileCosmetics:
                            DrawGagDisplayEditsPanel();
                            break;
                        default:
                            break;
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex}");
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }

    // Draw the active gags tab
    private void DrawActiveGagsPanel()
    {
        try
        {
            var region = ImGui.GetContentRegionAvail();
            using (_uiSharedService.UidFont.Push()) { ImGui.Text("Under Layer Gag:"); }
            // create a group for the listing:
            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.SlotOneGagPadlock, true, out var Padlock);
            DrawGagAndLockSection(0, $"ItemMouth\\{_playerManager.AppearanceData.SlotOneGagType}.png",
                $"Padlocks\\{_playerManager.AppearanceData.SlotOneGagPadlock}.png",
                Padlock != Padlocks.None,
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.SlotOneGagType],
                Padlock == Padlocks.None ? _lockHandler.PadlockPrevs[0] : Padlock);

            using (_uiSharedService.UidFont.Push()) { ImGui.Text("Middle Layer Gag:"); }
            // draw the listing
            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.SlotTwoGagPadlock, true, out var Padlock2);
            DrawGagAndLockSection(1, $"ItemMouth\\{_playerManager.AppearanceData.SlotTwoGagType}.png", 
                $"Padlocks\\{_playerManager.AppearanceData.SlotTwoGagPadlock}.png",
                Padlock2 != Padlocks.None,
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.SlotTwoGagType],
                Padlock2 == Padlocks.None ? _lockHandler.PadlockPrevs[1] : Padlock2);

            using (_uiSharedService.UidFont.Push()) { ImGui.Text("Upper Layer Gag:"); }
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
            if (!(_sharedSetupImage.GetWrapOrEmpty() is { } wrapGag)) { _logger.LogWarning("Failed to render image!"); }
            else {  ImGui.Image(wrapGag.ImGuiHandle, new Vector2(80, 80)); }
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
                        Mediator.Publish(new GagTypeChanged(SelectedGag, (GagLayer)slotNumber));
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
                            Mediator.Publish(new GagLockToggle(new PadlockData((GagLayer)slotNumber, _lockHandler.PadlockPrevs[slotNumber], 
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
            if (!(_sharedSetupImage.GetWrapOrEmpty() is { } wrapLock)) { _logger.LogWarning("Failed to render image!"); }
            else
            {
                ImGui.Image(wrapLock.ImGuiHandle, new Vector2(80, 80));
            }
        }
    }

    // Draw the lockpicker tab
    private void DrawLockPickerPanel()
    {
        ImGui.Text("Lockpicker");
    }

    // Draw the gag storage tab
    private void DrawGagStoragePanel()
    {
        ImGui.Text("Gag Storage");
    }

    // Draw the profile display edits tab
    private void DrawGagDisplayEditsPanel()
    {
        ImGui.Text("Profile Display Edits");
    }
}
