using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.UI.Handlers;
using GagSpeak.Utils;
using ImGuiNET;
using System.Numerics;
namespace GagSpeak.UI;

public class PuppeteerUI : WindowMediatorSubscriberBase
{
    private readonly IDalamudPluginInterface _pi;
    private readonly UiSharedService _uiSharedService;
    private readonly UserPairListHandler _userPairListHandler;
    /*private readonly PuppeteerTabMenu _tabMenu;*/ // Replace this with a lister for all users.
    private ITextureProvider _textureProvider;
    private ISharedImmediateTexture _sharedSetupImage;

    public Pair? selectedPair = null; // the selected pair we are referncing when drawing the right half.

    public PuppeteerUI(ILogger<PuppeteerUI> logger, GagspeakMediator mediator, 
        UiSharedService uiSharedService, UserPairListHandler userPairListHandler,
        ITextureProvider textureProvider, IDalamudPluginInterface pi) : base(logger, mediator, "Puppeteer UI")
    {
        _textureProvider = textureProvider;
        _pi = pi;
        _uiSharedService = uiSharedService;
        _userPairListHandler = userPairListHandler;

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
            using (var table = ImRaii.Table($"PuppeteerUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit))
            {
                if (!table) return;

                // define the left column, which contains an image of the component (added later), and the list of 'compartments' within the setup to view.
                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetWindowWidth() / 2);

                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();

                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child($"###PuppeteerLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
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
                        UtilsExtensions.ImGuiLineCentered("###PuppeteerLogo", () =>
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
                    // Assuming you have a currently selected UserPair object or similar mechanism

                    // Add the tab menu for the left side
                    foreach (var userPair in _userPairListHandler.AllPairDrawsDistinct)
                    {
                        // Generate a unique ID for each selectable based on the UserPair's unique identifier (e.g., AliasOrUID)
                        string selectableId = $"##Selectable{userPair.Pair.UserData.AliasOrUID}";

                        // Determine if this UserPair is the currently selected one
                        bool isSelected = (selectedPair != null) && (userPair.Pair.UserData.AliasOrUID == selectedPair.UserData.AliasOrUID);

                        // Draw the selectable with the AliasOrUID as the label
                        if (ImGui.Selectable(selectableId, isSelected))
                        {
                            // If this selectable is clicked, set it as the current selection
                            selectedPair = userPair.Pair;
                        }

                        // Optionally, use the same line to keep the selectables horizontally aligned or remove it for vertical alignment
                        // ImGui.SameLine();
                    }
                }
                // pop pushed style variables and draw next column.
                ImGui.PopStyleVar();
                ImGui.TableNextColumn();
                // display right half viewport based on the tab selection
                using (var rightChild = ImRaii.Child($"###ArtisanRightSide", Vector2.Zero, false))
                {
                    DrawPuppeteer();
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

    // Main Right-half Draw function for puppeteer.
    private void DrawPuppeteer()
    {
        if(selectedPair == null)
        {
            ImGui.Text("Select a pair to view their puppeteer setup.");
            return;
        }
        ImGui.Text("Viewing Puppeteer Interface for " + selectedPair.UserData.AliasOrUID);
    }
}
