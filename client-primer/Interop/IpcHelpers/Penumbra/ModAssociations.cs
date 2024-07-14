using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using System.Numerics;

namespace GagSpeak.Interop.IpcHelpers.Penumbra;
public class ModAssociations : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientManager;
    private readonly IpcCallerPenumbra _penumbra;
    private readonly CustomModCombo _modCombo;
    private readonly IClientState _clientState;

    public ModAssociations(ILogger<ModAssociations> logger,
        ClientConfigurationManager clientManager,
        GagspeakMediator mediator, IpcCallerPenumbra penumbra,
        IClientState clientState) : base(logger, mediator)
    {
        _clientManager = clientManager;
        _penumbra = penumbra;
        _modCombo = new CustomModCombo(penumbra, logger);
        _clientState = clientState;

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) => ApplyModsOnSetToggle(msg));
    }

    /// <summary> Applies associated mods to the client when a restraint set is toggled. </summary>
    private void ApplyModsOnSetToggle(RestraintSetToggledMessage msg)
    {
        // if the set is being enabled, we should toggle on the mods
        if (_clientState.IsLoggedIn && _clientState.LocalContentId != 0)
        {
            if (msg.newSetStateActive)
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

        ImGui.TableSetupColumn("##Delete", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Mods to enable with this Set", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Toggle", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Toggle").X);
        ImGui.TableSetupColumn("##Redraw", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("##Update", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());             // update to reflect what is in
        ImGui.TableHeadersRow();

        Mod? removedMod = null;
        ModUpdateResult? updatedMod = null;

        foreach (var (associatedMod, idx) in _clientManager.GetAssociatedMods(_clientManager.GetSelectedSetIdx()).WithIndex())
        {
            using var id = ImRaii.PushId(idx);

            DrawAssociatedModRow(associatedMod, idx, out var removedModTmp, out var updatedModTmp);

            if (removedModTmp.HasValue)
            {
                removedMod = removedModTmp;
            }

            if (updatedModTmp != null && updatedModTmp.IsChanged)
            {
                updatedMod = updatedModTmp;
            }
        }

        DrawNewModRow();

        if (removedMod.HasValue)
        {
            _clientManager.RemoveAssociatedMod(_clientManager.GetSelectedSetIdx(), removedMod.Value);
        }

        if (updatedMod != null && updatedMod.IsChanged)
        {
            _clientManager.UpdateAssociatedMod(_clientManager.GetSelectedSetIdx(), updatedMod.UpdatedMod);
        }
    }

    private void DrawAssociatedModRow(AssociatedMod currentMod, int idx, out Mod? removedMod, out ModUpdateResult? updatedMod)
    {
        removedMod = null;
        // set updated mod to be set as not yet changed.
        updatedMod = new(currentMod, false);

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
        if (ImGui.IsItemHovered()) { ImGui.SetTooltip($"Mod to be enabled when restraint set it turned on.\n{currentMod.Mod.Name}"); }
        // if we should enable or disable this mod list (all buttons should sync)

        ImGui.TableNextColumn();
        // store updated mod's mod and mod setting info from current

        // set icon and help text
        var iconText = currentMod.DisableWhenInactive ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
        var helpText = currentMod.DisableWhenInactive ? "Mods are disabled when set is disabled" : "Mods will stay enabled after set is turned off";
        if (ImGuiUtil.DrawDisabledButton(iconText.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
        helpText, false, true))
        {
            updatedMod.UpdatedMod.DisableWhenInactive = !currentMod.DisableWhenInactive;
            updatedMod.IsChanged = true;
        }

        ImGui.TableNextColumn();
        // redraw button
        var iconText2 = currentMod.RedrawAfterToggle ? FontAwesomeIcon.Redo : FontAwesomeIcon.None;
        var helpText2 = currentMod.RedrawAfterToggle ? "Redraws self after set toggle (nessisary for VFX/Animation Mods)" : "Do not redraw when set is toggled (uses fast redraw)";
        if (ImGuiUtil.DrawDisabledButton(iconText2.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
        helpText2, false, true))
        {
            updatedMod.UpdatedMod.RedrawAfterToggle = !currentMod.RedrawAfterToggle;
            updatedMod.IsChanged = true;
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
                updatedMod.UpdatedMod.ModSettings = newSettings;
                updatedMod.IsChanged = true;
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
                CustomModCombo.DrawSettingsLeft(newSettings);
            }

            ImGui.SameLine(Math.Max(ImGui.GetItemRectSize().X + 3 * ImGui.GetStyle().ItemSpacing.X, 150 * ImGuiHelpers.GlobalScale));
            using (ImRaii.Group())
            {
                if (namesDifferent)
                    ImGui.TextUnformatted(currentMod.Mod.DirectoryName);

                ImGui.TextUnformatted(newSettings.Enabled.ToString());
                ImGui.TextUnformatted(newSettings.Priority.ToString());
                CustomModCombo.DrawSettingsRight(newSettings);
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

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(),
            new Vector2(ImGui.GetFrameHeight()), tt, tt.Length > 0, true))
        {
            // locate the associated mod we want to add based on what is in the mod combo's current selection
            var associatedMod = new AssociatedMod
            {
                Mod = _modCombo.CurrentSelection.Mod,
                ModSettings = _modCombo.CurrentSelection.Settings,
                DisableWhenInactive = false,
                RedrawAfterToggle = false
            };
            _clientManager.AddAssociatedMod(_clientManager.GetSelectedSetIdx(), associatedMod);
        }

        ImGui.TableNextColumn();
        _modCombo.Draw("##new", currentName.IsNullOrEmpty() ? "Select new Mod..." : currentName, string.Empty,
            ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight());
    }
}
