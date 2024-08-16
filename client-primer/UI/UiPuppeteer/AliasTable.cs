using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.UiPuppeteer;

public class AliasTable : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly PuppeteerHandler _handler;

    public AliasTable(ILogger<AliasTable> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, PuppeteerHandler handler) : base(logger, mediator)
    {
        _uiSharedService = uiSharedService;
        _handler = handler;
    }

    private int EditableAliasIndex = -1; // Field to track the editable AliasTrigger
    private AliasTrigger NewTrigger = new AliasTrigger(); // stores data of a trigger yet to be added, modifiable in the add new alias row.

    public void DrawAliasListTable(string KeyToDrawFor, float paddingHeight)
    {
        // draw table.
        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(ImGui.GetStyle().CellPadding.X * 0.3f, paddingHeight));
        using var table = ImRaii.Table("UniqueAliasListCreator", 3, ImGuiTableFlags.RowBg);
        if (!table) { return; }
        // draw the header row
        ImGui.TableSetupColumn("##Delete", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() + (ImGuiHelpers.GlobalScale) * 10);
        ImGui.TableSetupColumn("Alias Input / Output", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Edit", ImGuiTableColumnFlags.WidthFixed, (_uiSharedService.GetIconButtonSize(FontAwesomeIcon.Pen).X));
        ImGui.TableHeadersRow();

        // create a new aliastrigger list temporarily
        try
        { 
            var aliasTriggerListShallowCopy = _handler.StorageBeingEdited.AliasList.ToList();
            foreach (var (aliasTrigger, idx) in aliasTriggerListShallowCopy.Select((value, index) => (value, index)))
            {
                using var id = ImRaii.PushId(idx);
                DrawAssociatedModRow(aliasTrigger, idx, KeyToDrawFor);
            }
            DrawNewModRow(KeyToDrawFor);
        }
        catch (Exception ex) { Logger.LogError(ex.ToString()); }
    }

    private void DrawAssociatedModRow(AliasTrigger aliasTrigger, int idx, string userID)
    {
        bool canEdit = EditableAliasIndex == idx;
        ImGui.TableNextColumn();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Delete alias from list.\nHold SHIFT in order to delete.", !UiSharedService.ShiftPressed(), true))
        {
            _handler.RemoveAlias(aliasTrigger);
        }
        ImGui.Text($"ID: {idx + 1}");
        // try to draw the rest, but if it was removed, it cant, so we should skip over and consume the error
        try
        {
            ImGui.TableNextColumn();
            using (ImRaii.Group())
            {
                string aliasInput = aliasTrigger.InputCommand;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                using (ImRaii.Disabled(!canEdit))
                    if (ImGui.InputTextWithHint($"##aliasText{idx}", "Input phrase goes here...", ref aliasInput, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        // Assuming a method to update an alias directly by index exists
                        _handler.UpdateAliasInput(idx, aliasInput);
                    }
                UiSharedService.AttachToolTip($"The string of words that {userID} would have to say to make you execute the output command");

                // next line draw output
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2*ImGuiHelpers.GlobalScale);
                _uiSharedService.IconButton(FontAwesomeIcon.LongArrowAltRight);
                UiSharedService.AttachToolTip($"The command that will be executed when the input phrase is said by {userID}");
                ImUtf8.SameLineInner();
                string aliasOutput = aliasTrigger.OutputCommand;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                using (ImRaii.Disabled(!canEdit))
                    if (ImGui.InputTextWithHint($"##command{idx}", "Output command goes here...", ref aliasOutput, 200, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        // Assuming a method to update an alias directly by index exists
                        _handler.UpdateAliasOutput(idx, aliasOutput);
                    }
                UiSharedService.AttachToolTip($"The command that will be executed when the input phrase is said by {userID}");
            }
            ImGui.TableNextColumn();
            // draw edit button.
            if (_uiSharedService.IconButton(FontAwesomeIcon.Pen))
            {
                EditableAliasIndex = (EditableAliasIndex == idx) ? -1 : idx;
            }
            UiSharedService.AttachToolTip("Edit the alias configuration.");
            // draw the enable button.
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2 * ImGuiHelpers.GlobalScale);
            var enabledRef = aliasTrigger.Enabled;
            if (ImGui.Checkbox($"##Enable{idx}{aliasTrigger.InputCommand}", ref enabledRef))
            { 
                _handler.UpdateAliasEnabled(idx, enabledRef);
            }
            UiSharedService.AttachToolTip("Enable / Disable the Alias.");

            ImGui.TableNextRow();
        }
        catch(Exception ex) { Logger.LogError(ex.StackTrace); }
    }

    private void DrawNewModRow(string userID)
    {
        ImGui.TableNextColumn();
        if (_uiSharedService.IconButton(FontAwesomeIcon.Plus))
        {
            _handler.AddAlias(NewTrigger);
            NewTrigger = new AliasTrigger();
        }
        UiSharedService.AttachToolTip("Add the alias configuration to the list.");

        ImGui.TableNextColumn();
        string newAliasText = NewTrigger.InputCommand;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint("##newAliasText", "Alias Input...", ref newAliasText, 50))
        {
            NewTrigger.InputCommand = newAliasText; // Update the new alias entry input
        }
        UiSharedService.AttachToolTip("The string of words that the player would have to say to make you execute the output field.");

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        string newAliasCommand = NewTrigger.OutputCommand;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("##newAliasCommand", "Output Command...", ref newAliasCommand, 200))
        {
            NewTrigger.OutputCommand = newAliasCommand; // Update the new alias entry output
        }
        UiSharedService.AttachToolTip("The command that will be executed when the input phrase is said by the player.");
    }
}
