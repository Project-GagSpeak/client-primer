using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.UI.Tabs.WardrobeTab;
using GagSpeak.Utils;
using ImGuiNET;
using System.Numerics;
namespace GagSpeak.UI.UiGagSetup;

public class GagSetupUI : WindowMediatorSubscriberBase
{
    private readonly IDalamudPluginInterface _pi;
    private readonly UiSharedService _uiSharedService;
    private readonly GagSetupTabMenu _tabMenu;
    private readonly ActiveGagsPanel _activeGags;
    private readonly GagStoragePanel _gagStorage;
    private readonly PadlockHandler _lockHandler;
    private readonly PlayerCharacterManager _playerManager; // for grabbing lock data
    private ITextureProvider _textureProvider;
    private ISharedImmediateTexture _sharedSetupImage;
    // gag images

    public GagSetupUI(ILogger<GagSetupUI> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, ActiveGagsPanel activeGags,
        GagStoragePanel gagStorage, PadlockHandler padlockHandler,
        PlayerCharacterManager playerManager, ITextureProvider textureProvider,
        IDalamudPluginInterface pi) : base(logger, mediator, "Gag Setup UI")
    {
        _textureProvider = textureProvider;
        _uiSharedService = uiSharedService;
        _playerManager = playerManager;
        _lockHandler = padlockHandler;
        _activeGags = activeGags;
        _gagStorage = gagStorage;
        _pi = pi;

        _tabMenu = new GagSetupTabMenu();

        // define initial size of window and to not respect the close hotkey.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(744, 350),
            MaximumSize = new Vector2(744, 409)
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
                            _activeGags.DrawActiveGagsPanel();
                            break;
                        case GagsetupTabSelection.Lockpicker: // shows off the gag storage configuration for the user's gags.
                            DrawLockPickerPanel();
                            break;
                        case GagsetupTabSelection.GagStorage: // fancy WIP thingy to give players access to features based on achievements or unlocks.
                            _gagStorage.DrawGagStoragePanel();
                            break;
                        case GagsetupTabSelection.Cosmetics:
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

    // Draw the lockpicker tab
    private void DrawLockPickerPanel()
    {
        ImGui.Text("Lockpicker Coming Soon");
    }

    // Draw the profile display edits tab
    private void DrawGagDisplayEditsPanel()
    {
        ImGui.TextWrapped("Here you will be able to unlock some progressive based achievements to unlock cosmetic features for your profile.");

        ImGui.TextWrapped("Below are some of the following progress goals:");
    }
}
