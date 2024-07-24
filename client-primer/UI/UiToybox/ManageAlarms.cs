using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Models;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using System.Numerics;
using System.Linq;
using System.Runtime.CompilerServices;
namespace GagSpeak.UI.UiToybox;

public class ToyboxAlarmManager
{
    private readonly ILogger<ToyboxAlarmManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly AlarmHandler _handler;
    private readonly PatternHandler _patternHandler;

    private bool HeaderHovered = false;

    public ToyboxAlarmManager(ILogger<ToyboxAlarmManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        AlarmHandler handler, PatternHandler patternHandler)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _handler = handler;
        _patternHandler = patternHandler;
    }

    private Alarm CreatedAlarm = new Alarm();
    public bool CreatingAlarm = false;
    private List<bool> ListItemHovered = new List<bool>();
    private LowerString PatternSearchString = LowerString.Empty;

    public void DrawAlarmManagerPanel()
    {
        // if we are creating a pattern
        if (CreatingAlarm)
        {
            DrawAlarmCreatingHeader();
            ImGui.Separator();
            DrawAlarmEditor(CreatedAlarm);
            return; // perform early returns so we dont access other methods
        }

        // if we are simply viewing the main page       
        if (_handler.EditingAlarmNull)
        {
            DrawCreateAlarmHeader();
            ImGui.Separator();
            if (!_handler.AlarmListNull)
                DrawAlarmSelectableMenu();

            return; // perform early returns so we dont access other methods
        }

        // if we are editing an alarm
        if (!_handler.EditingAlarmNull)
        {
            DrawAlarmEditorHeader();
            ImGui.Separator();
            if (!_handler.AlarmListNull)
                DrawAlarmEditor(_handler.EditingAlarm);
        }
    }

    private void DrawCreateAlarmHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize("New Alarm");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), HeaderHovered);
        using (ImRaii.Child("CreateAlarmHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // draw out the icon button
            if (_uiShared.IconButton(FontAwesomeIcon.Plus))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                CreatedAlarm = new Alarm();
                CreatingAlarm = true;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            _uiShared.BigText("New Alarm");
        }
    }

    private void DrawAlarmCreatingHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"Creating Alarm: {CreatedAlarm.Name}");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), HeaderHovered);
        using (ImRaii.Child("EditAlarmHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                CreatedAlarm = new Alarm();
                CreatingAlarm = false;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                ImGui.Text("Editing");
                ImGui.SameLine();
                UiSharedService.ColorText(CreatedAlarm.Name, ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                // add the newly created alarm to the list of alarms
                _handler.AddNewAlarm(CreatedAlarm);
                // reset to default and turn off creating status.
                CreatedAlarm = new Alarm();
                CreatingAlarm = false;
            }
        }
    }

    private void DrawAlarmEditorHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize($"Editing {_handler.EditingAlarm.Name}");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), HeaderHovered);
        using (ImRaii.Child("EditAlarmHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                CreatedAlarm = new Alarm();
                CreatingAlarm = false;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                ImGui.Text("Editing");
                ImGui.SameLine();
                UiSharedService.ColorText(_handler.EditingAlarm.Name, ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                CreatedAlarm = new Alarm();
                CreatingAlarm = false;
            }
        }
    }

    private void DrawAlarmSelectableMenu()
    {
        // if list size has changed, refresh the list of hovered items
        if (ListItemHovered.Count != _handler.AlarmListSize())
        {
            ListItemHovered.Clear();
            ListItemHovered.AddRange(Enumerable.Repeat(false, _handler.AlarmListSize()));
        }

        // display the selectable for each alarm using a for loop to keep track of the index
        for (int i = 0; i < _handler.AlarmListRef.Count; i++)
        {
            var alarm = _handler.AlarmListRef[i];
            DrawAlarmSelectable(alarm, i); // Pass the index to DrawAlarmSelectable
        }
    }

    private void DrawAlarmSelectable(Alarm alarm, int idx)
    {
        // define our sizes
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var toggleOnSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.ToggleOn);
        var toggleOffSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.ToggleOff);
        var nameTextSize = ImGui.CalcTextSize(alarm.Name);
        Vector2 alarmTextSize;
        var frequencyTextSize = ImGui.CalcTextSize(_handler.GetAlarmFrequencyString(alarm.RepeatFrequency));
        var patternNameSize = ImGui.CalcTextSize(alarm.PatternToPlay);
        using (_uiShared.UidFont.Push())
        {
            alarmTextSize = ImGui.CalcTextSize($"{alarm.SetTimeUTC.LocalDateTime}");
        }
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), ListItemHovered[idx]);
        using (ImRaii.Child("EditAlarmHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 150f)))
        {
            // create a group for the bounding area
            using (var group = ImRaii.Group())
            {
                // Draw out the alarm time
                _uiShared.BigText($"{alarm.SetTimeUTC.LocalDateTime}");
                // on the same line
                ImGui.SameLine();
                // move the Y cursor down.
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((alarmTextSize.Y - nameTextSize.Y) / 2));
                // draw out the alarm name
                UiSharedService.ColorText(alarm.Name, ImGuiColors.DalamudGrey2);
            }

            // now draw the lower section out.
            using (var group = ImRaii.Group())
            {
                // draw the frequency text
                UiSharedService.ColorText(_handler.GetAlarmFrequencyString(alarm.RepeatFrequency), ImGuiColors.DalamudGrey3);
                // on the same line
                ImGui.SameLine();
                // draw out the pattern name
                UiSharedService.ColorText("| " + alarm.PatternToPlay, ImGuiColors.DalamudGrey3);
            }

            // now, head to the sameline of the full width minus the width of the button
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - toggleOnSize.X);

            // draw out the icon button
            if (_uiShared.IconButton(FontAwesomeIcon.ToggleOn))
            {
                // toggle the state
            }
        }
    }

    private void DrawAlarmEditor(Alarm alarmToCreate)
    {
        // Display the local time zone
        var textlength = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Clock, TimeZoneInfo.Local.StandardName);
        int hour = alarmToCreate.SetTimeUTC.Hour;
        int minute = alarmToCreate.SetTimeUTC.Minute;
        // set the x position to center the icontext button
        ImGui.Spacing();
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - textlength) / 2);
        using (var timezoneDisabled = ImRaii.Disabled())
        {
            _uiShared.IconTextButton(FontAwesomeIcon.Clock, TimeZoneInfo.Local.StandardName);
        }

        // Draw out using the big pushed font, a large, blank button canvas
        using (ImRaii.Child("TimezoneFancyUI", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 90f)))
        {
            // define the scales 
            Vector2 hourTextSize;
            Vector2 minuteTextSize;
            using (_uiShared.UidFont.Push())
            {
                hourTextSize = ImGui.CalcTextSize($"{hour:00}");
                minuteTextSize = ImGui.CalcTextSize($"{minute:00}");
            }

            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - (minuteTextSize.X + ImGui.GetStyle().ItemSpacing.X * 2));
            using (ImRaii.Child("FancyHourDisplay", new Vector2(hourTextSize.X + ImGui.GetStyle().ItemSpacing.X * 2, ImGui.GetContentRegionAvail().Y)))
            {
                string prevHour = $"{(hour - 1 + 24) % 24:00}";
                string currentHour = $"{hour:00}";
                string nextHour = $"{(hour + 1) % 24:00}";

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (hourTextSize.X - ImGui.CalcTextSize(prevHour).X) / 2);
                ImGui.TextDisabled(prevHour); // Previous hour (centered)

                _uiShared.BigText(currentHour);
                // adjust the hour with the mouse wheel
                if (ImGui.IsItemHovered() && ImGui.GetIO().MouseWheel != 0)
                {
                    hour -= (int)ImGui.GetIO().MouseWheel;
                    hour = (hour + 24) % 24;  // hour = Math.Clamp(hour, 0, 23); <-- If we need to clamp.
                    alarmToCreate.SetTimeUTC = new DateTimeOffset(
                        alarmToCreate.SetTimeUTC.Year, alarmToCreate.SetTimeUTC.Month, alarmToCreate.SetTimeUTC.Day,
                        hour, alarmToCreate.SetTimeUTC.Minute, 0, alarmToCreate.SetTimeUTC.Offset);
                }

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (hourTextSize.X - ImGui.CalcTextSize(prevHour).X) / 2);
                ImGui.TextDisabled(nextHour); // Next hour (centered)
            }

            ImGui.SameLine((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.Y);
            _uiShared.BigText(":");
            ImGui.SameLine();

            using (ImRaii.Child("FancyMinuteDisplay", new Vector2(minuteTextSize.X + ImGui.GetStyle().ItemSpacing.X * 2, ImGui.GetContentRegionAvail().Y)))
            {
                string prevMinute = $"{(minute - 1 + 60) % 60:00}";
                string currentMinute = $"{minute:00}";
                string nextMinute = $"{(minute + 1) % 60:00}";

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (minuteTextSize.X - ImGui.CalcTextSize(prevMinute).X) / 2);
                ImGui.TextDisabled(prevMinute); // Previous hour (centered)

                _uiShared.BigText(currentMinute);
                // adjust the hour with the mouse wheel
                if (ImGui.IsItemHovered() && ImGui.GetIO().MouseWheel != 0)
                {
                    minute -= (int)ImGui.GetIO().MouseWheel;
                    minute = (minute + 60) % 60; // minute = Math.Clamp(minute, 0, 59); <-- If we need to clamp.
                    alarmToCreate.SetTimeUTC = new DateTimeOffset(
                        alarmToCreate.SetTimeUTC.Year, alarmToCreate.SetTimeUTC.Month, alarmToCreate.SetTimeUTC.Day,
                        alarmToCreate.SetTimeUTC.Hour, minute, 0, alarmToCreate.SetTimeUTC.Offset);
                }

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (minuteTextSize.X - ImGui.CalcTextSize(nextMinute).X) / 2);
                ImGui.TextDisabled(nextMinute); // Next hour (centered)
            }
        }
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 5f);
        ImGui.Separator();
        ImGui.Spacing();

        // Input field for the Alarm name
        var name = alarmToCreate.Name;
        ImGui.SetNextItemWidth(UiSharedService.GetWindowContentRegionWidth() / 2);
        ImGui.InputText("Alarm Name", ref name, 32);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            alarmToCreate.Name = name;
        }

        // Input field for the pattern the alarm will play
        var pattern = alarmToCreate.PatternToPlay;
        var searchString = PatternSearchString.Lower;
        // draw the selector on the left
        _uiShared.DrawComboSearchable("Alarm Pattern", UiSharedService.GetWindowContentRegionWidth() / 2,
        ref searchString, _patternHandler.PatternNames, (i) => i, true,
        (i) =>
        {
            var foundMatch = _patternHandler.GetPatternIdxByName(i);
            if (_patternHandler.IsIndexInBounds(foundMatch))
            {
                alarmToCreate.PatternToPlay = i;
            }
        }, alarmToCreate.PatternToPlay ?? default);

        TimeSpan newpatternMaxDuration = _patternHandler.GetPatternLength(alarmToCreate.PatternToPlay!);
        var newduration = alarmToCreate.PatternDuration;

        _uiShared.DrawTimeSpanCombo("Alarm Duration", newpatternMaxDuration, ref newduration, UiSharedService.GetWindowContentRegionWidth() / 2);
        alarmToCreate.PatternDuration = newduration;

        // Frequency of occurrence
        ImGui.Text("Alarm Frequency Per Week");
        var alarmRepeatValues = Enum.GetValues(typeof(AlarmRepeat)).Cast<AlarmRepeat>().ToArray();
        int totalValues = alarmRepeatValues.Length;
        int splitIndex = 4; // Index to split the groups

        // Group 1: First four
        using (ImRaii.Group())
        {
            for (int i = 0; i < splitIndex && i < totalValues; i++)
            {
                AlarmRepeat day = alarmRepeatValues[i];
                bool isSelected = alarmToCreate.RepeatFrequency.Contains(day);
                if (ImGui.Checkbox(day.ToString(), ref isSelected))
                {
                    if (isSelected)
                        alarmToCreate.RepeatFrequency.Add(day);
                    else
                        alarmToCreate.RepeatFrequency.Remove(day);
                }
            }
        }
        ImGui.SameLine();

        // Group 2: Last three
        using (ImRaii.Group())
        {
            for (int i = splitIndex; i < totalValues; i++)
            {
                AlarmRepeat day = alarmRepeatValues[i];
                bool isSelected = alarmToCreate.RepeatFrequency.Contains(day);
                if (ImGui.Checkbox(day.ToString(), ref isSelected))
                {
                    if (isSelected)
                        alarmToCreate.RepeatFrequency.Add(day);
                    else
                        alarmToCreate.RepeatFrequency.Remove(day);
                }
            }
        }
    }
}
