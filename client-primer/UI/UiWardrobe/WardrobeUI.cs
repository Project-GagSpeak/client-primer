using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.UI.UiWardrobe;
using GagSpeak.Utils;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI;

public class ToyboxUI : WindowMediatorSubscriberBase
{
    private readonly IDalamudPluginInterface _pi;
    private readonly UiSharedService _uiSharedService;
    private readonly WardrobeTabMenu _tabMenu;
    private readonly ActiveRestraintSet _activeSet;
    private readonly ToyboxPatterns _restraintOverview;
    private readonly RestraintSetCreate _restraintSetCreate;
    private readonly ManageTriggers _restraintSetEdit;
    private readonly ToyboxCosmetics _restraintCosmetics;
    private ITextureProvider _textureProvider;
    private ISharedImmediateTexture _sharedSetupImage;

    public ToyboxUI(ILogger<ToyboxUI> logger, GagspeakMediator mediator, 
        UiSharedService uiSharedService, ActiveRestraintSet activeSet,
        ToyboxPatterns restraintOverview, RestraintSetCreate restraintSetCreate,
        ManageTriggers restraintSetEdit, ToyboxCosmetics restraintCosmetics,
        ITextureProvider textureProvider, 
        IDalamudPluginInterface pi) : base(logger, mediator, "Wardrobe UI")
    {
        _uiSharedService = uiSharedService;
        _activeSet = activeSet;
        _restraintOverview = restraintOverview;
        _restraintSetCreate = restraintSetCreate;
        _restraintSetEdit = restraintSetEdit;
        _restraintCosmetics = restraintCosmetics;
        _textureProvider = textureProvider;
        _pi = pi;

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
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f * ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f), 0));
        try
        {
            using (var table = ImRaii.Table($"WardrobeUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
            {
                if (!table) return;

                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, 200f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("##RightColumn", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();

                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child($"###WardrobeLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                {
                    // attempt to obtain an image wrap for it
                    _sharedSetupImage = _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, "icon.png"));
                    if (!(_sharedSetupImage.GetWrapOrEmpty() is { } wrap))
                    {
                        _logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        // aligns the image in the center like we want.
                        UtilsExtensions.ImGuiLineCentered("###WardrobeLogo", () =>
                        {
                            ImGui.Image(wrap.ImGuiHandle, new(125f * ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f),
                                125f * ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f)));

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
                            _activeSet.DrawActiveSet();
                            break;
                        case WardrobeTabs.Tabs.SetsOverview:
                            _restraintOverview.DrawSetsOverview();
                            break;
                        case WardrobeTabs.Tabs.CreateNewSet:
                            _restraintSetCreate.DrawRestraintSetCreate();
                            break;
                        case WardrobeTabs.Tabs.ModifySet:
                            _restraintSetEdit.DrawRestraintSetEdit();
                            break;
                        case WardrobeTabs.Tabs.Cosmetics:
                            _restraintCosmetics.DrawCosmetics();
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
