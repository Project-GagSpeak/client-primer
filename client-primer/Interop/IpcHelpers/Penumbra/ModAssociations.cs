using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.Events;
using GagSpeak.Wardrobe;
using ImGuiNET;
using GagSpeak.Interop.Ipc;
using OtterGui;
using OtterGui.Raii;
using GagSpeak.Services.Mediator;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.GagspeakConfiguration.Models;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace GagSpeak.Interop.IpcHelpers.Penumbra;
public class ModAssociations : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientManager;
    private readonly IpcCallerPenumbra _penumbra;
    private readonly ModCombo _modCombo;
    private readonly IClientState _clientState;

    public ModAssociations(ILogger<ModAssociations> logger,
        ClientConfigurationManager clientManager,
        GagspeakMediator mediator, IpcCallerPenumbra penumbra,
        IClientState clientState) : base(logger, mediator)
    {
        _clientManager = clientManager;
        _penumbra = penumbra;
        _modCombo = new ModCombo(penumbra, logger);
        _clientState = clientState;

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) => ApplyModsOnSetToggle(msg));
    }

    /// <summary> Applies associated mods to the client when a restraint set is toggled. </summary>
    private void ApplyModsOnSetToggle(RestraintSetToggledMessage msg)
    {
        // if the set is being enabled, we should toggle on the mods
        if(_clientState.IsLoggedIn && _clientState.LocalContentId != 0)
        {
            if(msg.newSetStateActive)
            {
                // enable the mods.
                foreach (var associatedMod in _clientManager.GetAssociatedMods(msg.RestraintSetIndex))
                    _penumbra.SetMod(associatedMod, true);
            }
            // otherwise, new set state is false, so toggle off the mods
            else
            {
                foreach (var associatedMod in _clientManager.GetAssociatedMods(msg.RestraintSetIndex))
                    _penumbra.SetMod(associatedMod, false);
            }
        }
    }

    // main draw function for the mod associations table
    public void Draw()
    {
        DrawTable();
    }

    // draw the table for constructing the associated mods.
    private void DrawTable()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(ImGui.GetStyle().CellPadding.X * 0.3f, ImGui.GetStyle().CellPadding.Y));
        using var table = ImRaii.Table("Mods", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY);
        if (!table) { return; }

        ImGui.TableSetupColumn("##Delete",      ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Mods to enable with this Set",       ImGuiTableColumnFlags.WidthStretch);        
        ImGui.TableSetupColumn("Toggle",         ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Toggle").X);
        ImGui.TableSetupColumn("##Redraw",        ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("##Update",      ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());             // update to reflect what is in
        ImGui.TableHeadersRow();

        Mod? removedMod = null;
        AssociatedMod? updatedMod = null;

        foreach (var (associatedMod, idx) in _clientManager.GetAssociatedMods(_clientManager.GetSelectedSetIdx()).WithIndex())
        {
            using var id = ImRaii.PushId(idx);

            DrawAssociatedModRow(associatedMod, idx, out var removedModTmp, out var updatedModTmp);
            
            if (removedModTmp.HasValue)
            {
                removedMod = removedModTmp;
            }

            if (updatedModTmp.HasValue)
            {
                updatedMod = updatedModTmp;
            }
        }

        DrawNewModRow();

        if (removedMod.HasValue)
        {
            _manager.RemoveMod(_manager._selectedIdx, removedMod.Value);
        }

        if (updatedMod.HasValue)
        {
            _manager.UpdateMod(_manager._selectedIdx, updatedMod.Value.mod, updatedMod.Value.settings, updatedMod.Value.disableWhenInactive, updatedMod.Value.redrawAfterToggle);
        }
    }

    private void DrawAssociatedModRow(AssociatedMod currentMod, int idx, out Mod? removedMod, out AssociatedMod? updatedMod)
    {
        removedMod = null;
        updatedMod = currentMod; // set to current mod.

        // get the index of this mod
        ImGui.TableNextColumn();
        // delete icon
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Delete this mod from associations", !ImGui.GetIO().KeyShift, true))
        {
            removedMod = currentMod.Mod;
        }
        
        // the name of the appended mod
        ImGui.TableNextColumn();
        ImGui.Selectable($"{currentMod.Mod.Name}##name");
        if(ImGui.IsItemHovered()) { ImGui.SetTooltip($"Mod to be enabled when restraint set it turned on.\n{mod.Name}"); }
        // if we should enable or disable this mod list (all buttons should sync)
        
        ImGui.TableNextColumn();
        // store updated mod's mod and mod setting info from current

        // set icon and help text
        var iconText = currentMod.DisableWhenInactive ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
        var helpText = currentMod.DisableWhenInactive ? "Mods are disabled when set is disabled" : "Mods will stay enabled after set is turned off";
        if (ImGuiUtil.DrawDisabledButton(iconText.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
        helpText, false, true))
        {
            updatedMod.DisableWhenInactive = !currentMod.DisableWhenInactive;
        }

        ImGui.TableNextColumn();
        // redraw button
        var iconText2 = currentMod.RedrawAfterToggle ? FontAwesomeIcon.Redo : FontAwesomeIcon.None;
        var helpText2 = currentMod.RedrawAfterToggle ? "Redraws self after set toggle (nessisary for VFX/Animation Mods)" : "Do not redraw when set is toggled (uses fast redraw)";
        if (ImGuiUtil.DrawDisabledButton(iconText2.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
        helpText2, false, true))
        {
            updatedMod.RedrawAfterToggle = !currentMod.RedrawAfterToggle;
        }

        // button to update the status the mod from penumbra
        ImGui.TableNextColumn();
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Search.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Inspect current mod status", false, true);
        if (ImGui.IsItemHovered())
        {
            var (_, newSettings) = _penumbra.GetMods().FirstOrDefault(m => m.Mod == currentMod.Mod);
            if (ImGui.IsItemClicked())
            {
                updatedMod.ModSettings = newSettings;
            }
            
            using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 2 * ImGuiHelpers.GlobalScale);
            using var tt = ImRaii.Tooltip();
            ImGui.Separator();
            var namesDifferent = currentMod.Mod.Name != currentMod.Mod.DirectoryName;
            ImGui.Dummy(new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
            using (ImRaii.Group())
            {
                if (namesDifferent)
                    ImGui.TextUnformatted("Directory Name");
                
                ImGui.TextUnformatted("Enabled");
                ImGui.TextUnformatted("Priority");
                ModCombo.DrawSettingsLeft(newSettings);
            }

            ImGui.SameLine(Math.Max(ImGui.GetItemRectSize().X + 3 * ImGui.GetStyle().ItemSpacing.X, 150 * ImGuiHelpers.GlobalScale));
            using (ImRaii.Group()) {
                if (namesDifferent)
                    ImGui.TextUnformatted(currentMod.Mod.DirectoryName);
                
                ImGui.TextUnformatted(newSettings.Enabled.ToString());
                ImGui.TextUnformatted(newSettings.Priority.ToString());
                ModCombo.DrawSettingsRight(newSettings);
            }
        }
    }
    
    private void DrawNewModRow()
    {
        var currentName = _modCombo.CurrentSelection.Mod.Name;
        ImGui.TableNextColumn();
        var tt = currentName.IsNullOrEmpty()
            ? "Please select a mod first."
            : _clientManager.GetAssociatedMods(_clientManager.GetSelectedSetIdx()).Any(x => x.Mod == _modCombo.CurrentSelection.Mod)
                ? "The design already contains an association with the selected mod."
                : string.Empty;

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), new Vector2(ImGui.GetFrameHeight()), tt, tt.Length > 0,
                true))
            _manager.AddMod(_manager._selectedIdx, _modCombo.CurrentSelection.Mod, _modCombo.CurrentSelection.Settings);
        ImGui.TableNextColumn();
        _modCombo.Draw("##new", currentName.IsNullOrEmpty() ? "Select new Mod..." : currentName, string.Empty,
            ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight());
    }
}
