using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using Penumbra.GameData.Enums;
using System.Numerics;

namespace GagSpeak.UI.UiWardrobe;

public class ActiveRestraintSet
{
    private readonly ILogger<ActiveRestraintSet> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;
    private readonly WardrobeHandler _handler; // helps us access wardrobe data for viewing
    private readonly TextureService _textures; // texture display for game icons

    // private readonly CosmeticManager _cosmetics; // for profile cosmetic application

    public ActiveRestraintSet(ILogger<ActiveRestraintSet> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, WardrobeHandler handler, TextureService textures)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
        _handler = handler;
        _textures = textures;

        IconSize = ImGuiHelpers.ScaledVector2(2.5f * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
    }

    private Vector2 IconSize; // size of icons that can display
    public RestraintSet ActiveSet => _handler.ActiveSet; // accessor from wardrobe handler

    public void DrawActiveSet()
    {
        DrawActiveSetPreview();
    }

    public void DrawActiveSetPreview()
    {
        using var table = ImRaii.Table("RestraintEquipSelection", 2, ImGuiTableFlags.RowBg);
        if (!table) return;
        // Create the headers for the table
        var width = IconSize.X + ImGui.GetStyle().ItemSpacing.X;
        // setup the columns
        ImGui.TableSetupColumn("EquipmentSlots", ImGuiTableColumnFlags.WidthFixed, width);
        ImGui.TableSetupColumn("AccessorySlots", ImGuiTableColumnFlags.WidthStretch);

        // draw out the equipment slots
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        int i = 0;
        foreach (var slot in EquipSlotExtensions.EquipmentSlots)
        {
            // draw the icon display
            ActiveSet.DrawData[slot].GameItem.DrawIcon(_textures, IconSize, slot);
        }
        foreach (var slot in BonusExtensions.AllFlags)
        {
            ActiveSet.BonusDrawData[slot].GameItem.DrawIcon(_textures, IconSize, slot);
        }
        // i am dumb and dont know how to place adjustable divider lengths
        ImGui.TableNextColumn();
        //draw out the accessory slots
        foreach (var slot in EquipSlotExtensions.AccessorySlots)
        {
            ActiveSet.DrawData[slot].GameItem.DrawIcon(_textures, IconSize, slot);
        }
    }
}
