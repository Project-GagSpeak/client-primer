using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Controllers;
using GagSpeak.Toybox.Services;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.VibeServer;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Text;
using System.Numerics;
using GameAction = Lumina.Excel.GeneratedSheets.Action;

namespace GagSpeak.UI.UiToybox;

public class ToyboxTriggerManager
{
    private readonly ILogger<ToyboxTriggerManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly PairManager _pairManager;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly DeviceController _deviceController;
    private readonly TriggerHandler _handler;
    private readonly PatternHandler _patternHandler;
    private readonly TriggerService _triggerService;
    private readonly MoodlesService _moodlesService;

    public ToyboxTriggerManager(ILogger<ToyboxTriggerManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        PairManager pairManager, ClientConfigurationManager clientConfigs,
        PlayerCharacterManager playerManager, DeviceController deviceController,
        TriggerHandler handler, PatternHandler patternHandler,
        TriggerService triggerService, MoodlesService moodlesService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _pairManager = pairManager;
        _clientConfigs = clientConfigs;
        _playerManager = playerManager;
        _deviceController = deviceController;
        _handler = handler;
        _patternHandler = patternHandler;
        _triggerService = triggerService;
        _moodlesService = moodlesService;
    }

    private List<Trigger> FilteredTriggerList
        => _handler.GetTriggersForSearch()
            .Where(pattern => pattern.Name.Contains(TriggerSearchString, StringComparison.OrdinalIgnoreCase))
            .ToList();
    private Trigger? CreatedTrigger = new ChatTrigger();
    public bool CreatingTrigger = false;
    private List<bool> ListItemHovered = new List<bool>();
    private LowerString TriggerSearchString = LowerString.Empty;
    private LowerString PairSearchString = LowerString.Empty;
    private string PatternSearchString = LowerString.Empty;
    private string GagSearchString = LowerString.Empty;
    private string SelectedDeviceName = LowerString.Empty;
    private int SelectedMoodleIdx = 0;
    private int SelectedMoodlePresetIdx = 0;

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
        using (_uiShared.UidFont.Push()) { textSize = ImGui.CalcTextSize("Edit Trigger"); }
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
                UiSharedService.ColorText("Edit Trigger", ImGuiColors.ParsedPink);
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
                case TriggerKind.SocialAction: CreatedTrigger = new SocialTrigger(); break;
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
            TriggerKind.SpellAction => "Action",
            TriggerKind.HealthPercent => "Health%",
            TriggerKind.RestraintSet => "Restraint",
            TriggerKind.GagState => "Gag",
            TriggerKind.SocialAction => "Social",
            _ => "UNK"
        };

        // store the trigger name to store beside it
        string triggerName = trigger.Name;

        // display priority of trigger.
        string priority = "Priority: " + trigger.Priority.ToString();

        // display the intended vibrationtypes.
        string devicesUsed = string.Join(", ", trigger.TriggerAction.Select(x => x.DeviceName.ToString()));

        // define our sizes
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var toggleSize = _uiShared.GetIconButtonSize(trigger.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff);

        Vector2 triggerTypeTextSize;
        var nameTextSize = ImGui.CalcTextSize(trigger.Name);
        var priorityTextSize = ImGui.CalcTextSize(priority);
        var devicesUsedTextSize = ImGui.CalcTextSize(devicesUsed);
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
                UiSharedService.ColorText("| " + devicesUsed, ImGuiColors.DalamudGrey3);
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
                // toggle the state & early return so we dont access the child clicked button
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
    private TimeSpan triggerSliderLimit = new TimeSpan(0, 0, 0, 59, 999);
    private void DrawTriggerEditor(Trigger? triggerToCreate)
    {
        if (triggerToCreate == null) return;

        ImGui.Spacing();

        // draw out the content details for the kind of trigger we are drawing.
        if (ImGui.BeginTabBar("TriggerEditorTabBar"))
        {
            if (ImGui.BeginTabItem("Info"))
            {
                DrawInfoSettings(triggerToCreate);
                ImGui.EndTabItem();
            }

            // Draw the options relative to the type we are interacting with.
            switch (triggerToCreate)
            {
                case ChatTrigger chatTrigger:
                    if (ImGui.BeginTabItem("ChatText"))
                    {
                        DrawChatTriggerEditor(chatTrigger);
                        ImGui.EndTabItem();
                    }
                    break;
                case SpellActionTrigger spellActionTrigger:
                    if (ImGui.BeginTabItem("Spells/Action"))
                    {
                        DrawSpellActionTriggerEditor(spellActionTrigger);
                        ImGui.EndTabItem();
                    }
                    break;
                case HealthPercentTrigger healthPercentTrigger:
                    if (ImGui.BeginTabItem("Health %"))
                    {
                        DrawHealthPercentTriggerEditor(healthPercentTrigger);
                        ImGui.EndTabItem();
                    }
                    break;
                case RestraintTrigger restraintTrigger:
                    if (ImGui.BeginTabItem("RestraintState"))
                    {
                        DrawRestraintTriggerEditor(restraintTrigger);
                        ImGui.EndTabItem();
                    }
                    break;
                case GagTrigger gagTrigger:
                    if (ImGui.BeginTabItem("GagState"))
                    {
                        DrawGagTriggerEditor(gagTrigger);
                        ImGui.EndTabItem();
                    }
                    break;
                case SocialTrigger socialTrigger:
                    if (ImGui.BeginTabItem("Social"))
                    {
                        DrawSocialTriggerEditor(socialTrigger);
                        ImGui.EndTabItem();
                    }
                    break;
            }

            if (ImGui.BeginTabItem("Trigger Action"))
            {
                DrawTriggerActions(triggerToCreate);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Access"))
            {
                DrawAccessControl(triggerToCreate);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawInfoSettings(Trigger triggerToCreate)
    {
        // draw out the details for the base of the abstract type.
        string name = triggerToCreate.Name;
        UiSharedService.ColorText("Name", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(225f);
        if (ImGui.InputTextWithHint("##NewTriggerName", "Enter Trigger Name", ref name, 40))
        {
            triggerToCreate.Name = name;
        }

        string desc = triggerToCreate.Description;
        UiSharedService.ColorText("Description", ImGuiColors.ParsedGold);
        if (UiSharedService.InputTextWrapMultiline("##NewTriggerDescription", ref desc, 100, 3, 225f))
        {
            triggerToCreate.Description = desc;
        }

        var startAfterRef = triggerToCreate.StartAfter;
        UiSharedService.ColorText("Start After (seconds : Milliseconds)", ImGuiColors.ParsedGold);
        _uiShared.DrawTimeSpanCombo("##Start Delay (seconds)", triggerSliderLimit, ref startAfterRef, UiSharedService.GetWindowContentRegionWidth() / 2, "ss\\:fff", false);
        triggerToCreate.StartAfter = startAfterRef;

        var runFor = triggerToCreate.EndAfter;
        UiSharedService.ColorText("Run For (seconds : Milliseconds)", ImGuiColors.ParsedGold);
        _uiShared.DrawTimeSpanCombo("##Execute for (seconds)", triggerSliderLimit, ref runFor, UiSharedService.GetWindowContentRegionWidth() / 2, "ss\\:fff", false);
        triggerToCreate.EndAfter = runFor;
    }


    private void DrawChatTriggerEditor(ChatTrigger chatTrigger)
    {
        string playerName = chatTrigger.FromPlayerName;
        UiSharedService.ColorText("Triggered By", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Must follow the format Player Name@World." + Environment.NewLine + "Example: Y'shtola Rhul@Mateus");
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputTextWithHint("##FromPlayerName", "Player Name@World", ref playerName, 72))
        {
            chatTrigger.FromPlayerName = playerName;
        }
        string triggerText = chatTrigger.ChatText;
        UiSharedService.ColorText("Trigger Text", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("What the above player must say to activate this trigger.");

        if (UiSharedService.InputTextWrapMultiline("##TriggerTextString", ref triggerText, 100, 3, 200f))
        {
            chatTrigger.ChatText = triggerText;
        }


        UiSharedService.ColorText("Can be Triggered in the following Channels:", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("If none are selected, it will accept the trigger text from any channel.");
        DrawChatTriggerChannels(chatTrigger);
    }


    private uint SelectedJobId = uint.MaxValue;
    private List<GameAction> SelectedActions = new List<GameAction>();
    private string JobTypeSearchString = string.Empty;
    private string ActionSearchString = string.Empty;
    private void DrawSpellActionTriggerEditor(SpellActionTrigger spellActionTrigger)
    {
        if (!CanDrawSpellActionTriggerUI()) return;

        UiSharedService.ColorText("Action Type", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("The type of action to monitor for.");

        _uiShared.DrawCombo("##ActionKindCombo", 150f, Enum.GetValues<LimitedActionEffectType>(), (ActionKind) => ActionKind.EffectTypeToString(),
        (i) => spellActionTrigger.ActionKind = i, spellActionTrigger.ActionKind);

        // the name of the action to listen to.
        UiSharedService.ColorText("Action Name", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Action To listen for." + Environment.NewLine + Environment.NewLine
            + "NOTE: Effects Divine Benison or regen, that cast no heal value, so not count as heals.");

        bool anyChecked = spellActionTrigger.ActionID == uint.MaxValue;
        if (ImGui.Checkbox("Any", ref anyChecked))
        {
            spellActionTrigger.ActionID = anyChecked ? uint.MaxValue : 0;
        }
        _uiShared.DrawHelpText("If checked, will listen for any action from any class for this type.");

        using (var disabled = ImRaii.Disabled(anyChecked))
        {
            _uiShared.DrawComboSearchable("##ActionJobSelectionCombo", 85f, ref JobTypeSearchString, _triggerService.BattleClassJobs,
            (job) => job.Name, false, (i) =>
            {
                _logger.LogTrace($"Selected Job ID for Trigger: {SelectedJobId}");
                SelectedJobId = i?.RowId ?? uint.MaxValue;
                _triggerService.CacheJobActionList(SelectedJobId);
            }, _triggerService.GetClientClassJob() ?? default, "Job..", ImGuiComboFlags.NoArrowButton);

            ImUtf8.SameLineInner();
            var loadedActions = _triggerService.LoadedActions[SelectedJobId];
            _uiShared.DrawComboSearchable("##ActionToListenTo", 150f, ref ActionSearchString, loadedActions, (action) => action.Name,
            false, (i) => spellActionTrigger.ActionID = i?.RowId ?? uint.MaxValue, null, "Select Job Action..");
        }

        // Determine how we draw out the rest of this based on the action type:
        switch (spellActionTrigger.ActionKind)
        {
            case LimitedActionEffectType.Miss:
            case LimitedActionEffectType.Interrupt:
            case LimitedActionEffectType.Attract1:
            case LimitedActionEffectType.Knockback:
                DrawDirection(spellActionTrigger);
                return;
            case LimitedActionEffectType.BlockedDamage:
            case LimitedActionEffectType.ParriedDamage:
            case LimitedActionEffectType.Damage:
            case LimitedActionEffectType.Heal:
                DrawDirection(spellActionTrigger);
                DrawThresholds(spellActionTrigger);
                return;
        }
    }

    private void DrawDirection(SpellActionTrigger spellActionTrigger)
    {
        UiSharedService.ColorText("Direction", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Determines how the trigger is fired.");
        // create a dropdown storing the enum values of TriggerDirection
        _uiShared.DrawCombo("##DirectionSelector", 150f, Enum.GetValues<TriggerDirection>(),
        (direction) => direction.DirectionToString(), (i) => spellActionTrigger.Direction = i, spellActionTrigger.Direction);
    }

    private void DrawThresholds(SpellActionTrigger spellActionTrigger)
    {
        UiSharedService.ColorText("Threshold Min Value: ", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Minimum Damage/Heal number to trigger effect.\nLeave -1 for any.");
        var minVal = spellActionTrigger.ThresholdMinValue;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputInt("##ThresholdMinValue", ref minVal))
        {
            spellActionTrigger.ThresholdMinValue = minVal;
        }

        UiSharedService.ColorText("Threshold Max Value: ", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Maximum Damage/Heal number to trigger effect.");
        var maxVal = spellActionTrigger.ThresholdMaxValue;
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputInt("##ThresholdMaxValue", ref maxVal))
        {
            spellActionTrigger.ThresholdMaxValue = maxVal;
        }
    }

    private void DrawHealthPercentTriggerEditor(HealthPercentTrigger healthPercentTrigger)
    {
        string playerName = healthPercentTrigger.PlayerToMonitor;
        UiSharedService.ColorText("Track Health % of:", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputTextWithHint("##PlayerToTrackHealthOf", "Player Name@World", ref playerName, 72))
        {
            healthPercentTrigger.PlayerToMonitor = playerName;
        }
        _uiShared.DrawHelpText("Must follow the format Player Name@World." + Environment.NewLine + "Example: Y'shtola Rhul@Mateus");

        UiSharedService.ColorText("Use % Threshold: ", ImGuiColors.ParsedGold);
        var usePercentageHealth = healthPercentTrigger.UsePercentageHealth;
        if (ImGui.Checkbox("##Use Percentage Health", ref usePercentageHealth))
        {
            healthPercentTrigger.UsePercentageHealth = usePercentageHealth;
        }
        _uiShared.DrawHelpText("When Enabled, will watch for when health goes above or below a specific %" +
            Environment.NewLine + "Otherwise, listens for when it goes above or below a health range.");

        UiSharedService.ColorText("Pass Kind: ", ImGuiColors.ParsedGold);
        _uiShared.DrawCombo("##PassKindCombo", 150f, Enum.GetValues<ThresholdPassType>(), (passKind) => passKind.ToString(),
            (i) => healthPercentTrigger.PassKind = i, healthPercentTrigger.PassKind);
        _uiShared.DrawHelpText("If the trigger should fire when the health passes above or below the threshold.");

        if (healthPercentTrigger.UsePercentageHealth)
        {
            UiSharedService.ColorText("Health % Threshold: ", ImGuiColors.ParsedGold);
            int minHealth = healthPercentTrigger.MinHealthValue;
            if (ImGui.SliderInt("##HealthPercentage", ref minHealth, 0, 100, "%d%%"))
            {
                healthPercentTrigger.MinHealthValue = minHealth;
            }
            _uiShared.DrawHelpText("The Health % that must be crossed to activate the trigger.");
        }
        else
        {
            UiSharedService.ColorText("Min Health Range Threshold: ", ImGuiColors.ParsedGold);
            int minHealth = healthPercentTrigger.MinHealthValue;
            if (ImGui.InputInt("##MinHealthValue", ref minHealth))
            {
                healthPercentTrigger.MinHealthValue = minHealth;
            }
            _uiShared.DrawHelpText("Lowest HP Value the health should be if triggered upon going below");

            UiSharedService.ColorText("Max Health Range Threshold: ", ImGuiColors.ParsedGold);
            int maxHealth = healthPercentTrigger.MaxHealthValue;
            if (ImGui.InputInt("##MaxHealthValue", ref maxHealth))
            {
                healthPercentTrigger.MaxHealthValue = maxHealth;
            }
            _uiShared.DrawHelpText("Highest HP Value the health should be if triggered upon going above");
        }
    }

    private void DrawRestraintTriggerEditor(RestraintTrigger restraintTrigger)
    {
        UiSharedService.ColorText("Restraint Set to Monitor", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawCombo("EditRestraintSetCombo" + restraintTrigger.TriggerIdentifier, 200f, _clientConfigs.StoredRestraintSets.Select(x => x.Name).ToList(),
            (name) => name, (i) => restraintTrigger.RestraintSetName = i!, restraintTrigger.RestraintSetName, false, ImGuiComboFlags.None, "No Set Selected...");
        _uiShared.DrawHelpText("The Restraint Set to listen to for this trigger.");

        UiSharedService.ColorText("Restraint State that fires Trigger", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawCombo("RestraintStateToMonitor" + restraintTrigger.TriggerIdentifier, 200f, Enum.GetValues<NewState>(), (state) => state.ToString(),
            (i) => restraintTrigger.RestraintState = i, restraintTrigger.RestraintState, false, ImGuiComboFlags.None, "No State Selected");
    }

    private void DrawGagTriggerEditor(GagTrigger gagTrigger)
    {
        UiSharedService.ColorText("Gag to Monitor", ImGuiColors.ParsedGold);
        var gagTypes = Enum.GetValues<GagList.GagType>().Where(gag => gag != GagList.GagType.None).ToArray();
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawComboSearchable("GagTriggerGagType" + gagTrigger.TriggerIdentifier, 250, ref GagSearchString, gagTypes,
            (gag) => gag.GetGagAlias(), false, (i) => gagTrigger.Gag = i, gagTrigger.Gag);
        _uiShared.DrawHelpText("The Gag to listen to for this trigger.");

        UiSharedService.ColorText("Gag State that fires Trigger", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawCombo("GagStateToMonitor" + gagTrigger.TriggerIdentifier, 200f, Enum.GetValues<NewState>(), (state) => state.ToString(),
            (i) => gagTrigger.GagState = i, gagTrigger.GagState, false, ImGuiComboFlags.None, "No Layer Selected");
        _uiShared.DrawHelpText("Trigger should be fired when the gag state changes to this.");
    }

    private void DrawSocialTriggerEditor(SocialTrigger socialTrigger)
    {
        UiSharedService.ColorText("Social Action to Monitor", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawCombo("SocialActionToMonitor", 200f, Enum.GetValues<SocialActionType>(), (action) => action.ToString(),
            (i) => socialTrigger.SocialType = i, socialTrigger.SocialType, false, ImGuiComboFlags.None, "Select a Social Type..");

    }

    private void DrawTriggerActions(Trigger trigger)
    {
        UiSharedService.ColorText("Trigger Action Kind", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("The kind of action to perform when the trigger is activated.");

        _uiShared.DrawCombo("##TriggerActionTypeCombo" + trigger.TriggerIdentifier, 175f, Enum.GetValues<TriggerActionKind>(),
        (triggerActionKind) => triggerActionKind.ToName(), (i) => trigger.TriggerActionKind = i,
        trigger.TriggerActionKind, false);
        ImGui.Separator();

        switch (trigger.TriggerActionKind)
        {
            case TriggerActionKind.SexToy:
                DrawVibeActionSettings(trigger);
                break;
            case TriggerActionKind.ShockCollar:
                DrawShockCollarActionSettings(trigger);
                break;
            case TriggerActionKind.Restraint:
                DrawRestraintActionSettings(trigger);
                break;
            case TriggerActionKind.Gag:
                DrawGagActionSettings(trigger);
                break;
            case TriggerActionKind.Moodle:
                DrawMoodleStatusActionSettings(trigger);
                break;
            case TriggerActionKind.MoodlePreset:
                DrawMoodlePresetActionSettings(trigger);
                break;
        }
    }

    private void DrawVibeActionSettings(Trigger trigger)
    {
        try
        {
            float width = ImGui.GetContentRegionAvail().X - _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus).X - ImGui.GetStyle().ItemInnerSpacing.X;
            // had to call it this way instead of just via connectedDevices, because otherwise, it would cause a crash due to referencing from values that are null.
            var deviceNames = _deviceController.ConnectedDevices?
                .Where(device => device != null && !string.IsNullOrEmpty(device.DeviceName))
                .Select(device => device.DeviceName)
                .ToList() ?? new List<string>();

            UiSharedService.ColorText("Select and Add a Device", ImGuiColors.ParsedGold);

            _uiShared.DrawCombo("VibeDeviceTriggerSelector" + trigger.TriggerIdentifier, width, deviceNames, (device) => device,
            (i) => { SelectedDeviceName = i ?? string.Empty; }, default, false, ImGuiComboFlags.None, "No Devices Connected");
            ImUtf8.SameLineInner();
            if (_uiShared.IconButton(FontAwesomeIcon.Plus, null, null, SelectedDeviceName == string.Empty))
            {
                // attempt to find the device by its name.
                var connectedDevice = _deviceController.GetDeviceByName(SelectedDeviceName);
                if (connectedDevice == null)
                {
                    _logger.LogWarning("Could not find device by name: " + SelectedDeviceName);
                }
                else
                {
                    trigger.TriggerAction.Add(new(connectedDevice.DeviceName, connectedDevice.VibeMotors, connectedDevice.RotateMotors));
                }
            }

            ImGui.Separator();

            if (trigger.TriggerAction.Count == 0) return;

            // draw a collapsible header for each of the selected devices.
            for (var i = 0; i < trigger.TriggerAction.Count; i++)
            {
                if (ImGui.CollapsingHeader("Settings for Device: " + trigger.TriggerAction[i].DeviceName))
                {
                    DrawDeviceActions(trigger.TriggerAction[i], i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error drawing VibeActionSettings");
        }
    }

    private void DrawShockCollarActionSettings(Trigger trigger)
    {
        // determine the opCode
        UiSharedService.ColorText("Shock Collar Action", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("What kind of action to inflict on the shock collar.");

        _uiShared.DrawCombo("ShockCollarActionType" + trigger.TriggerIdentifier, 100f, Enum.GetValues<ShockMode>(), (shockMode) => shockMode.ToString(),
            (i) => trigger.ShockTriggerAction.OpCode = i, trigger.ShockTriggerAction.OpCode, false, ImGuiComboFlags.None, "Select Action...");

        if (trigger.ShockTriggerAction.OpCode != ShockMode.Beep)
        {
            ImGui.Spacing();
            // draw the intensity slider
            UiSharedService.ColorText(trigger.ShockTriggerAction.OpCode + " Intensity", ImGuiColors.ParsedGold);
            _uiShared.DrawHelpText("Adjust the intensity level that will be sent to the shock collar.");

            int intensity = trigger.ShockTriggerAction.Intensity;
            if (ImGui.SliderInt("##ShockCollarIntensity" + trigger.TriggerIdentifier, ref intensity, 0, 100))
            {
                trigger.ShockTriggerAction.Intensity = intensity;
            }
        }

        ImGui.Spacing();
        // draw the duration slider
        UiSharedService.ColorText(trigger.ShockTriggerAction.OpCode + " Duration", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Adjust the Duration the action is played for on the shock collar.");

        var duration = trigger.ShockTriggerAction.Duration;
        TimeSpan timeSpanFormat = (duration > 15 && duration < 100)
            ? TimeSpan.Zero // invalid range.
            : (duration >= 100 && duration <= 15000)
                ? TimeSpan.FromMilliseconds(duration) // convert to milliseconds
                : TimeSpan.FromSeconds(duration); // convert to seconds
        float value = (float)timeSpanFormat.TotalSeconds + (float)timeSpanFormat.Milliseconds / 1000;
        if (ImGui.SliderFloat("##ShockCollarDuration" + trigger.TriggerIdentifier, ref value, 0.016f, 15f))
        {
            int newMaxDuration;
            if (value % 1 == 0 && value >= 1 && value <= 15) { newMaxDuration = (int)value; }
            else { newMaxDuration = (int)(value * 1000); }
            trigger.ShockTriggerAction.Duration = newMaxDuration;
        }
    }

    private void DrawRestraintActionSettings(Trigger trigger)
    {
        UiSharedService.ColorText("Apply Restraint Set", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        var selected = !trigger.RestraintNameAction.IsNullOrEmpty() ? trigger.RestraintNameAction : null;
        _uiShared.DrawCombo("ApplyRestraintSetActionCombo" + trigger.TriggerIdentifier, 200f, _clientConfigs.StoredRestraintSets.Select(x => x.Name).ToList(),
            (name) => name, (i) => trigger.RestraintNameAction = i, selected, false, ImGuiComboFlags.None, "No Set Selected...");
        _uiShared.DrawHelpText("Apply restraint set to your character when the trigger is fired.");
    }

    private void DrawGagActionSettings(Trigger trigger)
    {
        UiSharedService.ColorText("Apply to Layer", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawCombo("ApplyGagLayerActionCombo" + trigger.TriggerIdentifier, 200f, Enum.GetValues<GagLayer>(), (state) => state.ToString(),
            (i) => trigger.GagLayerAction = i, trigger.GagLayerAction, false, ImGuiComboFlags.None, "No Layer Selected");
        _uiShared.DrawHelpText("Apply Gag to the selected layer when the trigger is fired.");

        UiSharedService.ColorText("Apply Gag Type", ImGuiColors.ParsedGold);
        var gagTypes = Enum.GetValues<GagList.GagType>().Where(gag => gag != GagList.GagType.None).ToArray();
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawComboSearchable("GagActionGagType" + trigger.TriggerIdentifier, 250, ref GagSearchString, gagTypes,
            (gag) => gag.GetGagAlias(), false, (i) => trigger.GagTypeAction = i, trigger.GagTypeAction, "No Gag Type Selected");
        _uiShared.DrawHelpText("Apply this Gag to your character when the trigger is fired.");
    }

    private void DrawMoodleStatusActionSettings(Trigger trigger)
    {
        if (!IpcCallerMoodles.APIAvailable || _playerManager.LastIpcData == null)
        {
            UiSharedService.ColorText("Moodles is not currently active!", ImGuiColors.DalamudRed);
            return;
        }

        // reset the index if its out of bounds.
        if (SelectedMoodleIdx >= _playerManager.LastIpcData.MoodlesStatuses.Count) SelectedMoodleIdx = 0;

        UiSharedService.ColorText("Moodle Status to Apply", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _moodlesService.DrawMoodleStatusComboSearchable(_playerManager.LastIpcData.MoodlesStatuses, "##MoodleStatusTriggerAction"
            + trigger.TriggerIdentifier, ref SelectedMoodleIdx, ImGui.GetContentRegionAvail().X, 1f);
        _uiShared.DrawHelpText("This Moodle will be applied when the trigger is fired.");
    }

    private void DrawMoodlePresetActionSettings(Trigger trigger)
    {
        if (!IpcCallerMoodles.APIAvailable || _playerManager.LastIpcData == null)
        {
            UiSharedService.ColorText("Moodles is not currently active!", ImGuiColors.DalamudRed);
            return;
        }

        // reset the index if its out of bounds.
        if (SelectedMoodlePresetIdx >= _playerManager.LastIpcData.MoodlesPresets.Count) SelectedMoodlePresetIdx = 0;

        UiSharedService.ColorText("Moodle Preset to Apply", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _moodlesService.DrawMoodlesPresetCombo("##MoodlePresetTriggerAction" + trigger.TriggerIdentifier, ref SelectedMoodlePresetIdx,
            _playerManager.LastIpcData.MoodlesPresets, _playerManager.LastIpcData.MoodlesStatuses, ImGui.GetContentRegionAvail().X);
        _uiShared.DrawHelpText("This Moodle Preset will be applied when the trigger is fired.");
    }

    private void DrawDeviceActions(DeviceTriggerAction deviceAction, int idx)
    {
        if (deviceAction.VibrateMotorCount == 0) return;

        bool vibrates = deviceAction.Vibrate;
        if (ImGui.Checkbox("##Vibrate Device" + deviceAction.DeviceName, ref vibrates))
        {
            deviceAction.Vibrate = vibrates;
        }
        ImUtf8.SameLineInner();
        UiSharedService.ColorText("Vibrate Device", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Determines if this device will have its vibration motors activated.");

        using (ImRaii.Disabled(!vibrates))
            for (var i = 0; i < deviceAction.VibrateMotorCount; i++)
            {
                DrawMotorAction(deviceAction, i);
            }
    }

    private void DrawMotorAction(DeviceTriggerAction deviceAction, int motorIndex)
    {
        var motor = deviceAction.VibrateActions.FirstOrDefault(x => x.MotorIndex == motorIndex);
        bool enabled = motor != null;

        ImGui.AlignTextToFramePadding();
        UiSharedService.ColorText("Motor " + (motorIndex + 1), ImGuiColors.ParsedGold);
        ImGui.SameLine();

        ImGui.AlignTextToFramePadding();
        if (ImGui.Checkbox("##Motor" + motorIndex + deviceAction.DeviceName, ref enabled))
        {
            if (enabled)
            {
                deviceAction.VibrateActions.Add(new MotorAction((uint)motorIndex));
            }
            else
            {
                deviceAction.VibrateActions.RemoveAll(x => x.MotorIndex == motorIndex);
            }
        }
        UiSharedService.AttachToolTip("Enable/Disable Motor Activation on trigger execution");

        if (motor == null)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Motor not Enabled");
            return;
        }

        ImUtf8.SameLineInner();
        _uiShared.DrawCombo("##ActionType" + deviceAction.DeviceName + motorIndex, ImGui.CalcTextSize("Vibration").X + ImGui.GetStyle().FramePadding.X * 2,
            Enum.GetValues<TriggerActionType>(), type => type.ToName(), (i) => motor.ExecuteType = i, motor.ExecuteType, false, ImGuiComboFlags.NoArrowButton);
        UiSharedService.AttachToolTip("What should be played to this motor?");


        ImUtf8.SameLineInner();
        if (motor.ExecuteType == TriggerActionType.Vibration)
        {
            int intensity = motor.Intensity;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.SliderInt("##MotorSlider" + deviceAction.DeviceName + motorIndex, ref intensity, 0, 100))
            {
                motor.Intensity = (byte)intensity;
            }
        }
        else
        {
            _uiShared.DrawComboSearchable("PatternSelector" + deviceAction.DeviceName + motorIndex, ImGui.GetContentRegionAvail().X,
            ref PatternSearchString, _patternHandler.GetPatternsForSearch(), pattern => pattern.Name, false,
            (i) =>
            {
                motor.PatternIdentifier = i?.UniqueIdentifier ?? Guid.Empty;
                motor.StartPoint = i?.StartPoint ?? TimeSpan.Zero;
            }, default, "No Pattern Selected");
        }
    }


    private bool CanDrawSpellActionTriggerUI()
    {
        if (_triggerService.ClassJobs.Count == 0)
        {
            _logger.LogTrace("Updating ClassJob list because it was empty!");
            _triggerService.TryUpdateClassJobList();
        }
        // if the selected job id is the max value and the client is logged in, set it to the client class job.
        if (SelectedJobId == uint.MaxValue)
        {
            // try and get the client class job.
            var clientClassJob = _triggerService.GetClientClassJob();
            if (clientClassJob == null || clientClassJob == default)
            {
                _logger.LogWarning("Client Class Job is null or default. Cannot set SelectedJobId.");
                return false;
            }
            // otherwise, update job ID and cache actions for the job.
            _logger.LogTrace("Set SelectedJobId to current client jobId.");
            SelectedJobId = clientClassJob.RowId;
            _triggerService.CacheJobActionList(SelectedJobId);
        }
        if (SelectedJobId != uint.MaxValue && !_triggerService.LoadedActions.ContainsKey(SelectedJobId))
        {
            ImGui.Text("SelectedJobID: " + SelectedJobId);
            ImGui.Text("Loading Actions, please wait.");
            ImGui.Text("Current ClassJob size: " + _triggerService.ClassJobs.Count);
            ImGui.Text("If this doesnt go away, its an error. Report it!");
            return false;
        }
        return true;
    }

    private void DrawChatTriggerChannels(ChatTrigger chatTrigger)
    {
        var i = 0;
        foreach (var e in ChatChannel.GetOrderedChannels())
        {
            // See if it is already enabled by default
            var enabled = chatTrigger.AllowedChannels.Contains(e);

            // Create a new line after every 4 columns
            if (i != 0 && (i == 4 || i == 7 || i == 11 || i == 15 || i == 19)) ImGui.NewLine();

            // Move to the next row if it is LS1 or CWLS1
            if (e is ChatChannel.ChatChannels.LS1 or ChatChannel.ChatChannels.CWL1) ImGui.Separator();

            if (ImGui.Checkbox($"{e}", ref enabled))
            {
                if (enabled && !chatTrigger.AllowedChannels.Contains(e))
                {
                    // ensure that it is not already in the list first
                    chatTrigger.AllowedChannels.Add(e);
                }
                else
                {
                    chatTrigger.AllowedChannels.Remove(e);
                }
            }

            ImGui.SameLine();
            i++;
        }
    }

    private void DrawAccessControl(Trigger TriggerToCreate)
    {
        // display filterable search list
        DrawUidSearchFilter(ImGui.GetContentRegionAvail().X);
        using (var table = ImRaii.Table("userListForVisibility", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail()))
        {
            if (!table) return;

            ImGui.TableSetupColumn("Nickname/Alias/UID", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Can Toggle", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Can Toggle").X);
            ImGui.TableHeadersRow();

            var PairList = _pairManager.DirectPairs
                .Where(pair => string.IsNullOrEmpty(PairSearchString) ||
                               pair.UserData.AliasOrUID.Contains(PairSearchString, StringComparison.OrdinalIgnoreCase) ||
                               (pair.GetNickname() != null && pair.GetNickname().Contains(PairSearchString, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p.GetNickname() ?? p.UserData.AliasOrUID, StringComparer.OrdinalIgnoreCase);

            foreach (Pair pair in PairList)
            {
                using var tableId = ImRaii.PushId("userTable_" + pair.UserData.UID);

                ImGui.TableNextColumn(); // alias or UID of user.
                var nickname = pair.GetNickname();
                var text = nickname == null ? pair.UserData.AliasOrUID : nickname + " (" + pair.UserData.UID + ")";
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(text);

                ImGui.TableNextColumn();
                // display nothing if they are not in the list, otherwise display a check
                var canSeeIcon = TriggerToCreate.CanToggleTrigger.Contains(pair.UserData.UID) ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    string.Empty, false, true))
                    {
                        if (canSeeIcon == FontAwesomeIcon.Times)
                        {
                            TriggerToCreate.CanToggleTrigger.Add(pair.UserData.UID);
                        }
                        else
                        {
                            TriggerToCreate.CanToggleTrigger.Remove(pair.UserData.UID);
                        }
                    }
                }
            }
        }
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawUidSearchFilter(float availableWidth)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - ImGui.GetStyle().ItemInnerSpacing.X);
        string filter = PairSearchString;
        if (ImGui.InputTextWithHint("##filter", "Filter for UID/notes", ref filter, 255))
        {
            PairSearchString = filter;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(PairSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            PairSearchString = string.Empty;
        }
    }
}
