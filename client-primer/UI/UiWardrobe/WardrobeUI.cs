using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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
    private readonly ActiveRestraintSet _activePanel;
    private readonly RestraintSetsOverview _overviewPanel;
    private readonly RestraintSetCreator _creatorPanel;
    private readonly RestraintSetEditor _editorPanel;
    private readonly RestraintCosmetics _cosmeticsPanel;

    public WardrobeUI(ILogger<WardrobeUI> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        ActiveRestraintSet activeSet, RestraintSetsOverview restraintOverview,
        RestraintSetCreator restraintSetCreate, RestraintSetEditor restraintSetEdit,
        RestraintCosmetics restraintCosmetics) : base(logger, mediator, "Wardrobe UI")
    {
        _uiSharedService = uiSharedService;
        _activePanel = activeSet;
        _overviewPanel = restraintOverview;
        _creatorPanel = restraintSetCreate;
        _editorPanel = restraintSetEdit;
        _cosmeticsPanel = restraintCosmetics;

        _tabMenu = new WardrobeTabMenu();

        // define initial size of window and to not respect the close hotkey.
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        RespectCloseHotkey = false;
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
        // get information about the window region, its item spacing, and the topleftside height.
        var region = ImGui.GetContentRegionAvail();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        var topLeftSideHeight = region.Y;

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
                    var iconTexture = _uiSharedService.GetImageFromDirectoryFile("icon.png");
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
                        case WardrobeTabs.Tabs.SetsOverview:
                            _overviewPanel.DrawSetsOverview();
                            break;
                        case WardrobeTabs.Tabs.CreateNewSet:
                            _creatorPanel.DrawRestraintSetCreator();
                            break;
                        case WardrobeTabs.Tabs.ModifySet:
                            _editorPanel.DrawRestraintSetEditor();
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
