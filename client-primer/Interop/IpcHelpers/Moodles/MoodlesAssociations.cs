using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Api.Enums;
using System.Numerics;

namespace GagSpeak.Interop.IpcHelpers.Moodles;
public class MoodlesAssociations : DisposableMediatorSubscriberBase
{
    private readonly WardrobeHandler _handler;
    private readonly IpcCallerMoodles _moodles;
    private readonly MoodlesService _moodlesService;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly UiSharedService _uiShared;

    public MoodlesAssociations(ILogger<MoodlesAssociations> logger,
        GagspeakMediator mediator, WardrobeHandler handler,
        IpcCallerMoodles moodles, MoodlesService moodlesService,
        OnFrameworkService frameworkUtils, UiSharedService uiShared)
        : base(logger, mediator)
    {
        _handler = handler;
        _moodles = moodles;
        _moodlesService = moodlesService;
        _frameworkUtils = frameworkUtils;
        _uiShared = uiShared;

        Mediator.Subscribe<RestraintSetToggleMoodlesMessage>(this, (msg) =>
        {
            var moodlesToApply = _handler.GetAssociatedMoodles(msg.SetIdx);
            ToggleMoodlesOnAction(moodlesToApply, msg.State, msg.MoodlesTask);
        });
    }

    public async void ToggleMoodlesOnAction(List<Guid> moodlesToToggle, NewState newState, TaskCompletionSource<bool>? Task = null)
    {
        try
        {
            if (newState == NewState.Enabled)
            { 
                await _moodles.ApplyOwnStatusByGUID(moodlesToToggle);
            }
            else
            {
                await _moodles.RemoveOwnStatusByGuid(moodlesToToggle);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error applying Moodles on set toggle.");
        }

        if(Task != null)
        {
            Task.SetResult(true);
        }
    }

    private int SelectedStatusIndex = 0;
    private int SelectedPresetIndex = 0;
    private string PresetSearchString = string.Empty;

    // main draw function for the mod associations table
    public void DrawMoodlesStatusesListForSet(RestraintSet refRestraintSet, CharacterIPCData? lastPlayerIpcData, float paddingHeight, bool isPresets)
    {
        if (lastPlayerIpcData == null) return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(ImGui.GetStyle().CellPadding.X * 0.3f, paddingHeight));
        using var table = ImRaii.Table("RestraintSetMoodlesTable-"+isPresets, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY);
        if (!table) { return; }

        string columnTitle = isPresets ? "Moodle Presets" : "Moodle Statuses";
        ImGui.TableSetupColumn("##Delete", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn(columnTitle+" to enable with set", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        Guid? removedMoodle = null;
        Guid? removedMoodlePreset = null;

        // before we fetch the list, first see if any moodles in the list are no longer present in our Moodles List. 
        if(_moodles.APIAvailable)
        {
            var moodlesStatusList = lastPlayerIpcData.MoodlesStatuses;
            refRestraintSet.AssociatedMoodles.RemoveAll(x => !moodlesStatusList.Any(y => y.GUID == x));

            var moodlesPresetList = lastPlayerIpcData.MoodlesPresets;
            refRestraintSet.AssociatedMoodlePresets.RemoveAll(x => !moodlesPresetList.Any(y => y.Item1 == x));
        }

        // Handle what we are drawing based on what the table is for.
        if (isPresets)
        {
            //////////////////// HANDLE PRESETS ////////////////////
            foreach (var (associatedPreset, idx) in refRestraintSet.AssociatedMoodlePresets.WithIndex())
            {
                using var id = ImRaii.PushId(idx + "Presets");

                DrawAssociatedMoodlePresetRow(lastPlayerIpcData, associatedPreset, idx, out var removedPresetTmp);
                if (removedPresetTmp.HasValue)
                {
                    removedMoodle = removedPresetTmp;
                }
            }

            DrawSetNewMoodlePresetRow(refRestraintSet, lastPlayerIpcData);

            if (removedMoodle.HasValue)
            {
                refRestraintSet.AssociatedMoodles.Remove(removedMoodle.Value);
            }
        }
        else
        {
            //////////////////// HANDLE STATUSES ////////////////////
            foreach (var (associatedMoodle, idx) in refRestraintSet.AssociatedMoodles.WithIndex())
            {
                using var id = ImRaii.PushId(idx+"Statuses");

                DrawAssociatedMoodleRow(lastPlayerIpcData, associatedMoodle, idx, out var removedMoodleTmp);
                if (removedMoodleTmp.HasValue)
                {
                    removedMoodlePreset = removedMoodleTmp;
                }
            }

            DrawSetNewMoodleRow(refRestraintSet, lastPlayerIpcData);

            if (removedMoodlePreset.HasValue)
            {
                refRestraintSet.AssociatedMoodlePresets.Remove(removedMoodlePreset.Value);
            }
        }
    }

    private void DrawAssociatedMoodleRow(CharacterIPCData clientIpcData, Guid moodleGuid, int idx, out Guid? removedMoodle)
    {
        removedMoodle = null;
        ImGui.TableNextColumn();
        // delete icon
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Delete this Moodle from associations (Hold Shift)", !ImGui.GetIO().KeyShift, true))
        {
            removedMoodle = moodleGuid;
        }

        // the name of the appended mod
        ImGui.TableNextColumn();
        // locate the friendly name 
        string friendlyName = clientIpcData.MoodlesStatuses.FirstOrDefault(x => x.GUID == moodleGuid).Title ?? "No Title Set";
        ImGui.Selectable($" {friendlyName}##name");
    }

    private void DrawAssociatedMoodlePresetRow(CharacterIPCData clientIpcData, Guid presetGuid, int idx, out Guid? removedPreset)
    {
        removedPreset = null;
        ImGui.TableNextColumn();
        // delete icon
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Delete this Moodle Preset's Statuses from associations (Hold Shift)", !ImGui.GetIO().KeyShift, true))
        {
            removedPreset = presetGuid;
        }

        // the name of the appended mod
        ImGui.TableNextColumn();
        // locate the friendly name 
        ImGui.Selectable($"{presetGuid} - (Hover To See Attached Statuses)##presetName");
        if (ImGui.IsItemHovered()) 
        {
            // get the list of friendly names for each guid in the preset
            var test = clientIpcData.MoodlesPresets
                .FirstOrDefault(x => x.Item1 == presetGuid).Item2
                .Select(x => clientIpcData.MoodlesStatuses
                .FirstOrDefault(y => y.GUID == x).Title ?? "No FriendlyName Set for this Moodle") ?? new List<string>();
            ImGui.SetTooltip($"This Preset Enables the Following Moodles:\n" + string.Join(Environment.NewLine, test));
        }
    }

    private void DrawSetNewMoodleRow(RestraintSet refSet, CharacterIPCData ipcData)
    {
        var displayList = ipcData.MoodlesStatuses;

        ImGui.TableNextColumn();
        var tooltip = refSet.AssociatedMoodles.Any(x => x == displayList[SelectedStatusIndex].GUID)
                ? "The Restraint Set already includes this Moodle." : "Add Moodle Status to Status List";

        bool disableCondition = refSet.AssociatedMoodles.Any(x => x == displayList[SelectedStatusIndex].GUID);

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(),
            new Vector2(ImGui.GetFrameHeight()), tooltip, disableCondition, true))
        {
            refSet.AssociatedMoodles.Add(displayList[SelectedStatusIndex].GUID);
        }

        ImGui.TableNextColumn();
        var length = ImGui.GetContentRegionAvail().X;
        _moodlesService.DrawMoodleStatusComboSearchable(ipcData.MoodlesStatuses, ipcData.MoodlesStatuses[SelectedStatusIndex].Title + "##StatusSelector",
            ref SelectedStatusIndex, length, 1.25f);
    }

    private void DrawSetNewMoodlePresetRow(RestraintSet refSet, CharacterIPCData ipcData)
    {
        var displayList = ipcData.MoodlesPresets;
        // if the size is 0, then make the default preview value "No Presets Available"
        ImGui.TableNextColumn();
        var tooltip = "Add Moodle Preset's Statuses to the status list";

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(),
            new Vector2(ImGui.GetFrameHeight()), tooltip, false, true))
        {
            // take all the GUID's in the preset list and append them to a list.
            List<Guid> GuidsToAdd = displayList[SelectedPresetIndex].Item2;
            var newGuids = GuidsToAdd.Where(guid => !refSet.AssociatedMoodles.Contains(guid)).ToList();
            refSet.AssociatedMoodles.AddRange(newGuids);
            // add the preset to the preset list.
            refSet.AssociatedMoodlePresets.Add(displayList[SelectedPresetIndex].Item1);
        }

        ImGui.TableNextColumn();
        var length = ImGui.GetContentRegionAvail().X;
        _moodlesService.DrawMoodlesPresetComboSearchable("RestraintSetPresetSelector", ref SelectedPresetIndex,
        ref PresetSearchString, ipcData.MoodlesPresets, ipcData.MoodlesStatuses, length);
    }
}
