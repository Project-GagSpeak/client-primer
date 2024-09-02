using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.VibeServer;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Text;
using System.Drawing;
using System.Numerics;
namespace GagSpeak.UI.UiToybox;

public class ToyboxTriggerManager
{
    private readonly ILogger<ToyboxTriggerManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly PairManager _pairManager;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly TriggerHandler _handler;
    private readonly PatternHandler _patternHandler;

    public ToyboxTriggerManager(ILogger<ToyboxTriggerManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        PairManager pairManager, ClientConfigurationManager clientConfigs,
        TriggerHandler handler, PatternHandler patternHandler)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _pairManager = pairManager;
        _clientConfigs = clientConfigs;
        _handler = handler;
        _patternHandler = patternHandler;
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
    private string GagSearchString = LowerString.Empty;


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
                    if(ImGui.BeginTabItem("ChatText"))
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
            }

            if (ImGui.BeginTabItem("Trigger Action"))
            {
                DrawVibeActionSettings(triggerToCreate);
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

    private void DrawSpellActionTriggerEditor(SpellActionTrigger spellActionTrigger)
    {
        UiSharedService.ColorText("Action Type: ", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("The type of action to monitor for." + Environment.NewLine
            + "Can be listening for different state results of the same action.");
        _uiShared.DrawCombo("##ActionKindCombo", 150f, Enum.GetValues<ActionType>(), (Actionkind) => Actionkind.ToString(),
            (i) => spellActionTrigger.ActionKind = i, spellActionTrigger.ActionKind);

        UiSharedService.ColorText("Action Name Text: ", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("The name of the action to listen for that causes the effect." + Environment.NewLine
            + "Can use | to listen for multiple actions.");
        ImGui.SetNextItemWidth(200f);
        string refActionNamesStr = spellActionTrigger.ActionSpellNames;
        if (ImGui.InputTextWithHint("##ActionSpellNames", "Action Name", ref refActionNamesStr, 255))
        {
            spellActionTrigger.ActionSpellNames = refActionNamesStr;
        }

        UiSharedService.ColorText("Direction: ", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("If the trigger is invoked when the action hits the client, another object, or either?");
        // create a dropdown storing the enum values of TriggerDirection
        _uiShared.DrawCombo("##DirectionSelector", 200f, Enum.GetValues<TriggerDirection>(), (direction) => direction.ToString(),
            (i) => spellActionTrigger.Direction = i, spellActionTrigger.Direction);

        UiSharedService.ColorText("Threshold Min Value: ", ImGuiColors.ParsedGold);
        _uiShared.DrawHelpText("Minimum Damage/Heal number to trigger effect.\nLeave -1 for any.");
        var minVal = spellActionTrigger.ThresholdMinValue;
        ImGui.SetNextItemWidth(200f);
        if(ImGui.InputInt("##ThresholdMinValue", ref minVal))
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
        if(ImGui.Checkbox("##Use Percentage Health", ref usePercentageHealth))
        {
            healthPercentTrigger.UsePercentageHealth = usePercentageHealth;
        }
        _uiShared.DrawHelpText("When Enabled, will watch for when health goes above or below a specific %" + 
            Environment.NewLine + "Otherwise, listens for when it goes above or below a health range.");

        UiSharedService.ColorText("Pass Kind: ", ImGuiColors.ParsedGold);
        _uiShared.DrawCombo("##PassKindCombo", 150f, Enum.GetValues<ThresholdPassType>(), (passKind) => passKind.ToString(),
            (i) => healthPercentTrigger.PassKind = i, healthPercentTrigger.PassKind);
        _uiShared.DrawHelpText("If the trigger should fire when the health passes above or below the threshold.");

        if(healthPercentTrigger.UsePercentageHealth)
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
        UiSharedService.ColorText("Restraint Set: ", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawCombo("##RestraintSetCombo", 200f, _clientConfigs.StoredRestraintSets.Select(x => x.Name).ToList(),
            (name) => name, (i) => restraintTrigger.RestraintSetName = i, restraintTrigger.RestraintSetName);
        _uiShared.DrawHelpText("The Restraint Set to listen to for this trigger.");

        UiSharedService.ColorText("Restraint Set State: ", ImGuiColors.ParsedGold);
        _uiShared.DrawCombo("##RestraintStateCombo", 200f, Enum.GetValues<NewState>(), (state) => state.ToString(),
            (i) => restraintTrigger.RestraintState = i, restraintTrigger.RestraintState);
        _uiShared.DrawHelpText("Trigger should be fired when the restraint set changes to this state.");
    }

    private void DrawGagTriggerEditor(GagTrigger gagTrigger)
    {
        UiSharedService.ColorText("Gag: ", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawComboSearchable("##GagTriggerGagType", 250, ref GagSearchString,
            Enum.GetValues<GagList.GagType>(), (gag) => gag.GetGagAlias(), false,
            (i) => gagTrigger.Gag = i, gagTrigger.Gag);
        _uiShared.DrawHelpText("The Gag to listen to for this trigger.");

        UiSharedService.ColorText("Gag State: ", ImGuiColors.ParsedGold);
        ImGui.SetNextItemWidth(200f);
        _uiShared.DrawCombo("##GagStateCombo", 200f, Enum.GetValues<NewState>(), (state) => state.ToString(),
            (i) => gagTrigger.GagState = i, gagTrigger.GagState);
        _uiShared.DrawHelpText("Trigger should be fired when the gag state changes to this.");
    }

    private void DrawVibeActionSettings(Trigger TriggerToCreate)
    {
        UiSharedService.ColorText("Trigger Execution Type: ", ImGuiColors.ParsedGold);

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
