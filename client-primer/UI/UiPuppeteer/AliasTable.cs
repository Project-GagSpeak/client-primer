using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Data;
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
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;

    public AliasTable(ILogger<AliasTable> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, PlayerCharacterManager playerManager,
        ClientConfigurationManager clientConfigs) : base(logger, mediator)
    {
        _uiSharedService = uiSharedService;
        _playerManager = playerManager;
        _clientConfigs = clientConfigs;

        Mediator.Subscribe<AliasListUpdated>(this, (msg) =>
        {
            AliasTriggerList = _clientConfigs.FetchListForPair(msg.UserUID);
        });
    }

    public List<AliasTrigger> AliasTriggerList = null!; // Field to track the AliasTrigger list
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
            var aliasTriggerListShallowCopy = AliasTriggerList.ToList();
            foreach (var (aliasTrigger, idx) in aliasTriggerListShallowCopy.Select((value, index) => (value, index)))
            {
                using var id = ImRaii.PushId(idx);
                DrawAssociatedModRow(aliasTrigger, idx, KeyToDrawFor);
            }
            DrawNewModRow(KeyToDrawFor);
        }
        catch { /* Simple hack that consumes errors during the millisecond that the list is updating via Mediator. 
                 * Prevents need for duplicate AliasList resources in draw loop */}
    }

    private void DrawAssociatedModRow(AliasTrigger aliasTrigger, int idx, string userID)
    {
        bool canEdit = EditableAliasIndex == idx;
        ImGui.TableNextColumn();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Delete alias from list.\nHold SHIFT in order to delete.", !UiSharedService.ShiftPressed(), true))
        {
            _clientConfigs.RemoveAlias(userID, aliasTrigger);
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
                        _clientConfigs.UpdateAliasInput(userID, idx, aliasInput);
                    }
                UiSharedService.AttachToolTip($"The string of words that {userID} would have to say to make you execute the output command");

                // next line draw output
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2*ImGuiHelpers.GlobalScale);
                _uiSharedService.IconButton(FontAwesomeIcon.LongArrowAltRight);
                ImUtf8.SameLineInner();
                string aliasOutput = aliasTrigger.OutputCommand;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                using (ImRaii.Disabled(!canEdit))
                    if (ImGui.InputTextWithHint($"##command{idx}", "Output command goes here...", ref aliasOutput, 200, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        // Assuming a method to update an alias directly by index exists
                        _clientConfigs.UpdateAliasOutput(userID, idx, aliasOutput);
                    }
                UiSharedService.AttachToolTip($"The command that will be executed when the input phrase is said by {userID}");
            }
            ImGui.TableNextColumn();
            // draw edit button.
            if (_uiSharedService.IconButton(FontAwesomeIcon.Pen))
            {
                EditableAliasIndex = (EditableAliasIndex == idx) ? -1 : idx;
            }
            ImGui.TableNextRow();
        }
        catch(Exception ex) { Logger.LogError(ex.StackTrace); }
    }

    private void DrawNewModRow(string userID)
    {
        ImGui.TableNextColumn();
        if (_uiSharedService.IconButton(FontAwesomeIcon.Plus))
        {
            _clientConfigs.AddAlias(userID, NewTrigger);
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
