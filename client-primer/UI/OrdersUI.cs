using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.Utils;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI;

public class OrdersUI : WindowMediatorSubscriberBase
{
    private readonly IDalamudPluginInterface _pi;
    private readonly UiSharedService _uiSharedService;
    private readonly OrdersTabMenu _tabMenu;
    private ITextureProvider _textureProvider;
    private ISharedImmediateTexture _sharedSetupImage;

    public OrdersUI(ILogger<OrdersUI> logger, ITextureProvider textureProvider,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        IDalamudPluginInterface pi) : base(logger, mediator, "Orders UI")
    {
        _textureProvider = textureProvider;
        _pi = pi;

        _tabMenu = new OrdersTabMenu();
        // define initial size of window and to not respect the close hotkey.
        this.SizeConstraints = new WindowSizeConstraints
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
            using (var table = ImRaii.Table($"OrdersUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit))
            {
                if (!table) return;

                // define the left column, which contains an image of the component (added later), and the list of 'compartments' within the setup to view.
                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetWindowWidth() / 2);

                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();

                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child($"###OrdersLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
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
                        UtilsExtensions.ImGuiLineCentered("###OrdersLogo", () =>
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
                    _tabMenu.DrawSelectableTabMenu();
                }
                // pop pushed style variables and draw next column.
                ImGui.PopStyleVar();
                ImGui.TableNextColumn();
                // display right half viewport based on the tab selection
                using (var rightChild = ImRaii.Child($"###ArtisanRightSide", Vector2.Zero, false))
                {
                    switch (_tabMenu.SelectedTab)
                    {
                        case OrdersTabSelection.ActiveOrders: // shows the interface for inspecting or applying your own gags.
                            DrawActiveOrders();
                            break;
                        case OrdersTabSelection.CreateOrder: // shows off the gag storage configuration for the user's gags.
                            DrawOrderCreator();
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
    private void DrawActiveOrders()
    {
        ImGui.Text("Viewing Own Active Orders");
    }

    // Draw the lockpicker tab
    private void DrawOrderCreator()
    {
        ImGui.Text("Order Creation Interface");
    }
}
