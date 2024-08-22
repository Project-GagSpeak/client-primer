using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data.VibeServer;
using ImGuiNET;
using OtterGui.Classes;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.UiToybox;

public class ToyboxTriggerManager
{
    private readonly ILogger<ToyboxTriggerManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly TriggerHandler _handler;
    private readonly PatternHandler _patternHandler;

    public ToyboxTriggerManager(ILogger<ToyboxTriggerManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        TriggerHandler handler)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _handler = handler;
    }

    private List<Trigger> FilteredTriggerList
        => _handler.GetTriggersForSearch()
            .Where(pattern => pattern.Name.Contains(TriggerSearchString, StringComparison.OrdinalIgnoreCase))
            .ToList();
    private Trigger? CreatedTrigger = new ChatTrigger();
    public bool CreatingTrigger = false;
    private List<bool> ListItemHovered = new List<bool>();
    private LowerString TriggerSearchString = LowerString.Empty;

    public void DrawTriggersPanel()
    {
        var regionSize = ImGui.GetContentRegionAvail();

        // if we are creating a pattern
        if (CreatingTrigger)
        {
            DrawTriggerCreatingHeader();
            ImGui.Separator();
            DrawTriggerTypeSelector(regionSize.X);
            ImGui.Separator();
            DrawTriggerEditor(CreatedTrigger);
            return; // perform early returns so we dont access other methods
        }

        if (_handler.EditingTriggerNull)
        {
            DrawCreateTriggerHeader();
            ImGui.Separator();
            DrawSearchFilter(regionSize.X, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.Separator();
            if (_handler.TriggerListSize() > 0)
                DrawTriggerSelectableMenu();

            return; // perform early returns so we dont access other methods
        }

        // if we are editing an trigger
        if (!_handler.EditingTriggerNull)
        {
            DrawTriggerEditorHeader();
            ImGui.Separator();
            if (_handler.TriggerListSize() > 0 && _handler.EditingTriggerIndex >= 0)
                DrawTriggerEditor(_handler.TriggerBeingEdited);
        }
    }

    private void DrawCreateTriggerHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize("New Trigger");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("CreateTriggerHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // draw out the icon button
            if (_uiShared.IconButton(FontAwesomeIcon.Plus))
            {
                // reset the createdTrigger to a new trigger, and set editing trigger to true
                CreatedTrigger = new ChatTrigger();
                CreatingTrigger = true;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            _uiShared.BigText("New Trigger");
        }
    }

    private void DrawTriggerCreatingHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"Create Trigger");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("CreatingTriggerHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdTrigger to a new trigger, and set editing trigger to true
                CreatedTrigger = new ChatTrigger();
                CreatingTrigger = false;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText("Create Trigger", ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.Save, null, null, CreatedTrigger == null))
            {
                // add the newly created trigger to the list of triggers
                _handler.AddNewTrigger(CreatedTrigger);
                // reset to default and turn off creating status.
                CreatedTrigger = new ChatTrigger();
                CreatingTrigger = false;
            }
            UiSharedService.AttachToolTip(CreatedTrigger == null ? "Must choose trigger type before saving!" : "Save Trigger");
        }
    }

    private void DrawTriggerEditorHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"{_handler.TriggerBeingEdited.Name}");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("EditTriggerHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdTrigger to a new trigger, and set editing trigger to true
                _handler.ClearEditingTrigger();
                return;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText(_handler.TriggerBeingEdited.Name, ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X * 2 - ImGui.GetStyle().ItemSpacing.X * 2);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // for saving contents
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                // reset the createdTrigger to a new trigger, and set editing trigger to true
                _handler.UpdateEditedTrigger();
            }
            UiSharedService.AttachToolTip("Save changes to Pattern & Return to Pattern List");

            // right beside it to the right, we need to draw the delete button
            using (var disableDelete = ImRaii.Disabled(!UiSharedService.CtrlPressed()))
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(currentYpos);
                if (_uiShared.IconButton(FontAwesomeIcon.Trash))
                {
                    // reset the createdPattern to a new pattern, and set editing pattern to true
                    _handler.RemoveTrigger(_handler.EditingTriggerIndex);
                }
            }
        }
    }

    public void DrawTriggerTypeSelector(float availableWidth)
    {
        ImGui.SetNextItemWidth(availableWidth);
        _uiShared.DrawCombo($"##TriggerTypeSelector", availableWidth, Enum.GetValues<TriggerKind>(), (triggerType) => triggerType.TriggerKindToString(),
        (i) =>
        {
            switch (i)
            {
                case TriggerKind.Chat: CreatedTrigger = new ChatTrigger(); break;
                case TriggerKind.SpellAction: CreatedTrigger = new SpellActionTrigger(); break;
                case TriggerKind.HealthPercent: CreatedTrigger = new HealthPercentTrigger(); break;
                case TriggerKind.RestraintSet: CreatedTrigger = new RestraintTrigger(); break;
                case TriggerKind.GagState: CreatedTrigger = new GagTrigger(); break;
            }
        }, default);
    }

    /// <summary> Draws the search filter for the triggers. </summary>
    public void DrawSearchFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = TriggerSearchString;
        if (ImGui.InputTextWithHint("##TriggerSearchStringFilter", "Search for a Trigger", ref filter, 255))
        {
            TriggerSearchString = filter;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(TriggerSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            TriggerSearchString = string.Empty;
        }
    }

    private void DrawTriggerSelectableMenu()
    {
        var region = ImGui.GetContentRegionAvail();
        var topLeftSideHeight = region.Y;
        // if list size has changed, refresh the list of hovered items
        if (ListItemHovered.Count != _handler.TriggerListSize())
        {
            ListItemHovered.Clear();
            ListItemHovered.AddRange(Enumerable.Repeat(false, FilteredTriggerList.Count));
        }

        using (var leftChild = ImRaii.Child($"###SelectableTriggerList", region with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoScrollbar))
        {
            // display the selectable for each pattern using a for loop to keep track of the index
            for (int i = 0; i < FilteredTriggerList.Count; i++)
            {
                var pattern = FilteredTriggerList[i];
                DrawTriggerSelectable(pattern, i); // Pass the index to DrawPatternSelectable
            }
        }
    }

    private void DrawTriggerSelectable(Trigger trigger, int idx)
    {
        // store the type of trigger, to be displayed as bigtext
        string triggerType = trigger.Type switch
        {
            TriggerKind.Chat => "Chat",
            TriggerKind.SpellAction => "Spell/Action",
            TriggerKind.HealthPercent => "Health%",
            TriggerKind.RestraintSet => "Restraint",
            TriggerKind.GagState => "Gag",
            _ => "UNK"
        };

        // store the trigger name to store beside it
        string triggerName = trigger.Name;

        // display priority of trigger.
        string priority = "Priority: " + trigger.Priority.ToString();

        // display the intended vibrationtypes.
        string vibrationTypes = string.Join(", ", trigger.Actions.Select(x => x.RequiredVibratorTypes.ToString()));

        // define our sizes
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var toggleSize = _uiShared.GetIconButtonSize(trigger.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff);

        Vector2 triggerTypeTextSize;
        var nameTextSize = ImGui.CalcTextSize(trigger.Name);
        var priorityTextSize = ImGui.CalcTextSize(priority);
        var requiredVibeTypesTextSize = ImGui.CalcTextSize(vibrationTypes);
        using (_uiShared.UidFont.Push()) { triggerTypeTextSize = ImGui.CalcTextSize(triggerType); }

        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), ListItemHovered[idx]);
        using (ImRaii.Child($"##EditTriggerHeader{idx}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 65f)))
        {
            // create a group for the bounding area
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                _uiShared.BigText(triggerType);
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((triggerTypeTextSize.Y - nameTextSize.Y) / 2) + 5f);
                UiSharedService.ColorText(triggerName, ImGuiColors.DalamudGrey2);
            }

            // now draw the lower section out.
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                UiSharedService.ColorText(priority, ImGuiColors.DalamudGrey3);
                ImGui.SameLine();
                UiSharedService.ColorText("| " + vibrationTypes, ImGuiColors.DalamudGrey3);
            }

            // now, head to the sameline of the full width minus the width of the button
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - toggleSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (65f - toggleSize.Y) / 2);
            // draw out the icon button
            if (_uiShared.IconButton(trigger.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff))
            {
                // set the enabled state of the trigger based on its current state so that we toggle it
                if (trigger.Enabled)
                    _handler.DisableTrigger(idx);
                else
                    _handler.EnableTrigger(idx);
                // toggle the state & early return so we dont access the childclicked button
                return;
            }
        }
        ListItemHovered[idx] = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            _handler.SetEditingTrigger(trigger, idx);
        }
    }

    // create a new timespan object that is set to 60seconds.
    private TimeSpan triggerSliderLimit = new TimeSpan(0, 0, 60);
    private void DrawTriggerEditor(Trigger? triggerToCreate)
    {
        if (triggerToCreate == null) return;

        ImGui.Spacing();

        // draw out the details for the base of the abstract type.
        string name = triggerToCreate.Name;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputText("Name#NewTriggerName", ref name, 50))
        {
            triggerToCreate.Name = name;
        }

        string desc = triggerToCreate.Description;
        if (UiSharedService.InputTextWrapMultiline("Description#NewTriggerDescription", ref desc, 100, 3, 200f))
        {
            triggerToCreate.Description = desc;
        }

        var startAfterRef = triggerToCreate.StartAfter;
        /*        _uiShared.DrawTimeSpanCombo("Start Delay (seconds)", triggerSliderLimit, ref startAfterRef, UiSharedService.GetWindowContentRegionWidth() / 2);
                triggerToCreate.StartAfter.StartPoint = newStartDuration;

                // calc the max playback length minus the start point we set to declare the max allowable duration to play
                if (newStartDuration > patternDurationTimeSpan) newStartDuration = patternDurationTimeSpan;
                var maxPlaybackDuration = patternDurationTimeSpan - newStartDuration;

                // how long to run the trigger for once it starts.
                var runFor = triggerToCreate.EndAfter;
                _uiShared.DrawTimeSpanCombo("Execute for (seconds)", , ref runFor, UiSharedService.GetWindowContentRegionWidth() / 2);
                patternToEdit.PlaybackDuration = newPlaybackDuration;

                parseSuccess = TimeSpan.TryParseExact(patternDuration, "ss\\:fff", null, out duration);*/


        // draw out the content details for the kind of trigger we are drawing.
        switch (triggerToCreate)
        {
            case ChatTrigger chatTrigger:
                DrawChatTriggerEditor(chatTrigger);
                break;
            case SpellActionTrigger spellActionTrigger:
                DrawSpellActionTriggerEditor(spellActionTrigger);
                break;
            case HealthPercentTrigger healthPercentTrigger:
                DrawHealthPercentTriggerEditor(healthPercentTrigger);
                break;
            case RestraintTrigger restraintTrigger:
                DrawRestraintTriggerEditor(restraintTrigger);
                break;
            case GagTrigger gagTrigger:
                DrawGagTriggerEditor(gagTrigger);
                break;
        }
    }


    private void DrawChatTriggerEditor(ChatTrigger chatTrigger)
    {
        // draw out the chat trigger editor
        ImGui.Text("Chat Trigger Editor");
    }

    private void DrawSpellActionTriggerEditor(SpellActionTrigger spellActionTrigger)
    {
        // draw out the spell action trigger editor
        ImGui.Text("Spell/Action Trigger Editor");
    }

    private void DrawHealthPercentTriggerEditor(HealthPercentTrigger healthPercentTrigger)
    {
        // draw out the health percent trigger editor
        ImGui.Text("Health Percent Trigger Editor");
    }

    private void DrawRestraintTriggerEditor(RestraintTrigger restraintTrigger)
    {
        // draw out the restraint trigger editor
        ImGui.Text("Restraint Trigger Editor");
    }

    private void DrawGagTriggerEditor(GagTrigger gagTrigger)
    {
        // draw out the gag trigger editor
        ImGui.Text("Gag Trigger Editor");
    }
}
