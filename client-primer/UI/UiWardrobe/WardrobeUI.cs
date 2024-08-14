using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.Utils;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.UiWardrobe;

public class WardrobeUI : WindowMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly WardrobeTabMenu _tabMenu;
    private readonly WardrobeHandler _handler;
    private readonly ActiveRestraintSet _activePanel;
    private readonly RestraintSetManager _overviewPanel;
    private readonly RestraintStruggleSim _struggleSimPanel;
    private readonly MoodlesManager _moodlesPanel;
    private readonly RestraintCosmetics _cosmeticsPanel;
    public WardrobeUI(ILogger<WardrobeUI> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        WardrobeHandler handler, ActiveRestraintSet activeSet, 
        RestraintSetManager restraintOverview, RestraintStruggleSim struggleSim,
        MoodlesManager moodlesManager, RestraintCosmetics restraintCosmetics) 
        : base(logger, mediator, "Wardrobe UI")
    {
        _uiSharedService = uiSharedService;
        _handler = handler;
        _activePanel = activeSet;
        _overviewPanel = restraintOverview;
        _struggleSimPanel = struggleSim;
        _moodlesPanel = moodlesManager;
        _cosmeticsPanel = restraintCosmetics;

        AllowPinning = false;
        AllowClickthrough = false;

        _tabMenu = new WardrobeTabMenu(_handler);

        // define initial size of window and to not respect the close hotkey.
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 445),
            MaximumSize = new Vector2(760*1.5f, 445*1.5f)
        };
        RespectCloseHotkey = false;
    }

    private bool IsAnySetActive => _activePanel.ActiveSet != null;

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
        //_logger.LogInformation(ImGui.GetWindowSize().ToString()); // <-- USE FOR DEBUGGING ONLY.
        // get information about the window region, its item spacing, and the topleftside height.
        var region = ImGui.GetContentRegionAvail();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        var topLeftSideHeight = region.Y;
        var cellPadding = ImGui.GetStyle().CellPadding;

        // create the draw-table for the selectable and viewport displays
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiSharedService.GetFontScalerFloat(), 0));
        try
        {
            using (var table = ImRaii.Table($"WardrobeUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
            {
                if (!table) return;
                // setup the columns for the table
                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, 200f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("##RightColumn", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child($"###WardrobeLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                {
                    var iconTexture = _uiSharedService.GetLogo();
                    if (!(iconTexture is { } wrap))
                    {
                        _logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        UtilsExtensions.ImGuiLineCentered("###WardrobeLogo", () =>
                        {
                            ImGui.Image(wrap.ImGuiHandle, new(125f * _uiSharedService.GetFontScalerFloat(), 125f * _uiSharedService.GetFontScalerFloat()));
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
                using (var rightChild = ImRaii.Child($"###WardrobeSetupRight", Vector2.Zero, false))
                {
                    switch (_tabMenu.SelectedTab)
                    {
                        case WardrobeTabs.Tabs.ActiveSet:
                            _activePanel.DrawActiveSet();
                            break;
                        case WardrobeTabs.Tabs.ManageSets:
                            _overviewPanel.DrawManageSets(cellPadding);
                            break;
                        case WardrobeTabs.Tabs.StruggleSim:
                            _struggleSimPanel.DrawStruggleSim();
                            break;
                        case WardrobeTabs.Tabs.ManageMoodles:
                            _moodlesPanel.DrawMoodlesManager();
                            break;
                        case WardrobeTabs.Tabs.Cosmetics:
                            _cosmeticsPanel.DrawCosmetics();
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
}
