using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.Utils;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.UiToybox;

public class ToyboxUI : WindowMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly ToyboxTabMenu _tabMenu;
    private readonly ToyboxOverview _toysOverview;
    private readonly ToyboxVibeServer _vibeServer;
    private readonly ToyboxPatterns _patterns;
    private readonly ToyboxTriggerManager _triggerManager;
    private readonly ToyboxAlarmManager _alarmManager;
    private readonly ToyboxCosmetics _cosmetics;

    public ToyboxUI(ILogger<ToyboxUI> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, ToyboxOverview toysOverview,
        ToyboxVibeServer vibeServer, ToyboxPatterns patterns,
        ToyboxTriggerManager triggerManager, ToyboxAlarmManager alarmManager,
        ToyboxCosmetics cosmetics) : base(logger, mediator, "Toybox UI")
    {
        _uiShared = uiSharedService;
        _toysOverview = toysOverview;
        _vibeServer = vibeServer;
        _patterns = patterns;
        _triggerManager = triggerManager;
        _alarmManager = alarmManager;
        _cosmetics = cosmetics;

        _tabMenu = new ToyboxTabMenu();

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
        var cellPadding = ImGui.GetStyle().CellPadding;
        var topLeftSideHeight = region.Y;

        // create the draw-table for the selectable and viewport displays
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiShared.GetFontScalerFloat(), 0));
        try
        {
            using (var table = ImRaii.Table($"ToyboxUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
            {
                if (!table) return;

                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, 200f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("##RightColumn", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();

                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child($"###ToyboxLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                {
                    // attempt to obtain an image wrap for it
                    var iconTexture = _uiShared.GetImageFromDirectoryFile("icon.png");
                    if (!(iconTexture is { } wrap))
                    {
                        _logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        // aligns the image in the center like we want.
                        UtilsExtensions.ImGuiLineCentered("###ToyboxLogo", () =>
                        {
                            ImGui.Image(wrap.ImGuiHandle, new(125f * _uiShared.GetFontScalerFloat(),
                                125f * _uiShared.GetFontScalerFloat()));
                        });
                    }
                    // add separator
                    ImGui.Spacing();
                    ImGui.Separator();
                    // add the tab menu for the left side.
                    using (_uiShared.UidFont.Push())
                    {
                        _tabMenu.DrawSelectableTabMenu();
                    }
                }
                // pop pushed style variables and draw next column.
                ImGui.PopStyleVar();
                ImGui.TableNextColumn();

                // display right half viewport based on the tab selection
                using (var rightChild = ImRaii.Child($"###ToyboxRight", Vector2.Zero, false))
                {
                    switch (_tabMenu.SelectedTab)
                    {
                        case ToyboxTabs.Tabs.ToyOverview:
                            _toysOverview.DrawOverviewPanel();
                            break;
                        case ToyboxTabs.Tabs.VibeServer:
                            _vibeServer.DrawVibeServerPanel();
                            break;
                        case ToyboxTabs.Tabs.PatternManager:
                            _patterns.DrawPatternManagerPanel();
                            break;
                        case ToyboxTabs.Tabs.TriggerManager:
                            _triggerManager.DrawTriggersPanel();
                            break;
                        case ToyboxTabs.Tabs.AlarmManager:
                            _alarmManager.DrawAlarmManagerPanel();
                            break;
                        case ToyboxTabs.Tabs.ToyboxCosmetics:
                            _cosmetics.DrawCosmeticsPanel();
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
