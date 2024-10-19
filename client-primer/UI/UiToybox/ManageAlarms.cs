using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using ImGuiNET;
using OtterGui.Classes;
using System.Globalization;
using System.Numerics;
namespace GagSpeak.UI.UiToybox;

public class ToyboxAlarmManager
{
    private readonly ILogger<ToyboxAlarmManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly AlarmHandler _handler;
    private readonly PatternHandler _patternHandler;

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
            if (_handler.AlarmListSize() > 0)
                DrawAlarmSelectableMenu();

            return; // perform early returns so we dont access other methods
        }

        // if we are editing an alarm
        if (!_handler.EditingAlarmNull)
        {
            DrawAlarmEditorHeader();
            ImGui.Separator();
            if (_handler.AlarmListSize() > 0 && _handler.EditingAlarmIndex >= 0)
                DrawAlarmEditor(_handler.AlarmBeingEdited);
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
            textSize = ImGui.CalcTextSize($"{_handler.AlarmBeingEdited.Name}");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("EditAlarmHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // the "fuck go back" button.
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                _handler.ClearEditingAlarm();
                return;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText(_handler.AlarmBeingEdited.Name, ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - iconSize.X * 2 - ImGui.GetStyle().ItemSpacing.X * 2);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // for saving contents
            if (_uiShared.IconButton(FontAwesomeIcon.Save))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                _handler.UpdateEditedAlarm();
            }
            UiSharedService.AttachToolTip("Save changes to Pattern & Return to Pattern List");

            // right beside it to the right, we need to draw the delete button
            using (var disableDelete = ImRaii.Disabled(!KeyMonitor.CtrlPressed()))
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(currentYpos);
                if (_uiShared.IconButton(FontAwesomeIcon.Trash))
                {
                    // reset the createdPattern to a new pattern, and set editing pattern to true
                    _handler.RemoveAlarm(_handler.EditingAlarmIndex);
                }
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
        for (int i = 0; i < _handler.AlarmListSize(); i++)
        {
            DrawAlarmSelectable(i); // Pass the index to DrawAlarmSelectable
        }
    }

    private void DrawAlarmSelectable(int idx)
    {
        // grab the temp alarm
        var tmpAlarm = _handler.GetAlarm(idx);
        //  automatically handle whether to use a 12-hour or 24-hour clock.
        var localTime = tmpAlarm.SetTimeUTC.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

        string patternName = _handler.GetPatternNameFromId(tmpAlarm.PatternToPlay);

        // define our sizes
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var toggleSize = _uiShared.GetIconButtonSize(tmpAlarm.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff);
        var nameTextSize = ImGui.CalcTextSize(tmpAlarm.Name);
        Vector2 tmpAlarmTextSize;
        var frequencyTextSize = ImGui.CalcTextSize(_handler.GetAlarmFrequencyString(tmpAlarm.RepeatFrequency));
        var patternNameSize = ImGui.CalcTextSize(patternName);
        string patternToPlayName = _handler.GetPatternNameFromId(tmpAlarm.PatternToPlay);
        using (_uiShared.UidFont.Push())
        {
            tmpAlarmTextSize = ImGui.CalcTextSize($"{localTime}");
        }
        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), ListItemHovered[idx]);
        using (ImRaii.Child($"##EditAlarmHeader{idx}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 65f)))
        {
            // create a group for the bounding area
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                _uiShared.BigText($"{localTime}");
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((tmpAlarmTextSize.Y - nameTextSize.Y) / 2) + 5f);
                UiSharedService.ColorText(tmpAlarm.Name, ImGuiColors.DalamudGrey2);
            }

            // now draw the lower section out.
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                UiSharedService.ColorText(_handler.GetAlarmFrequencyString(tmpAlarm.RepeatFrequency), ImGuiColors.DalamudGrey3);
                ImGui.SameLine();
                UiSharedService.ColorText("| " + patternToPlayName, ImGuiColors.DalamudGrey3);
            }

            // now, head to the sameline of the full width minus the width of the button
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - toggleSize.X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (65f - toggleSize.Y) / 2);
            // draw out the icon button
            if (_uiShared.IconButton(tmpAlarm.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff))
            {
                // set the enabled state of the alarm based on its current state so that we toggle it
                if (tmpAlarm.Enabled)
                    _handler.DisableAlarm(idx);
                else
                    _handler.EnableAlarm(idx);
                // toggle the state & early return so we dont access the childclicked button
                return;
            }
        }
        ListItemHovered[idx] = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            _handler.SetEditingAlarm(tmpAlarm, idx);
        }
    }

    private void DrawAlarmEditor(Alarm alarmToCreate)
    {
        // Display the local time zone
        var textlength = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Clock, TimeZoneInfo.Local.StandardName);
        var localTime = alarmToCreate.SetTimeUTC.ToLocalTime();
        int hour = localTime.Hour;
        int minute = localTime.Minute;
        // set the x position to center the icontext button
        ImGui.Spacing();
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - textlength) / 2);
        _uiShared.IconTextButton(FontAwesomeIcon.Clock, TimeZoneInfo.Local.StandardName, null!, true, true);

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
                    hour = (hour - (int)ImGui.GetIO().MouseWheel + 24) % 24;
                    var newLocalTime = new DateTime(localTime.Year, localTime.Month, localTime.Day, hour, localTime.Minute, 0);
                    alarmToCreate.SetTimeUTC = new DateTimeOffset(newLocalTime, TimeZoneInfo.Local.GetUtcOffset(newLocalTime)).ToUniversalTime();
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
                    minute = (minute - (int)ImGui.GetIO().MouseWheel + 60) % 60;
                    var newLocalTime = new DateTime(localTime.Year, localTime.Month, localTime.Day, localTime.Hour, minute, 0);
                    alarmToCreate.SetTimeUTC = new DateTimeOffset(newLocalTime, TimeZoneInfo.Local.GetUtcOffset(newLocalTime)).ToUniversalTime();
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
        ref searchString, _patternHandler.GetPatternsForSearch(), (i) => i.Name, true,
        (i) =>
        {
            if(i == null) return;
            alarmToCreate.PatternToPlay = i.UniqueIdentifier;
        });

        // if the pattern is not the alarmTocreate.PatternToPlay, it has changed, so update newPatternMaxDuration
        TimeSpan durationTotal = _handler.GetPatternLength(alarmToCreate.PatternToPlay);
        TimeSpan StartPointTimeSpan = alarmToCreate.PatternStartPoint;
        TimeSpan PlaybackDuration = alarmToCreate.PatternDuration;

        string formatStart = durationTotal.Hours > 0 ? "hh\\:mm\\:ss" : "mm\\:ss";
        _uiShared.DrawTimeSpanCombo("Playback Start-Point", durationTotal, ref StartPointTimeSpan, UiSharedService.GetWindowContentRegionWidth()/2, formatStart, true);
        alarmToCreate.PatternStartPoint = StartPointTimeSpan;

        // time difference calculation.
        if (alarmToCreate.PatternStartPoint > durationTotal) alarmToCreate.PatternStartPoint = durationTotal;
        TimeSpan maxPlaybackDuration = durationTotal - alarmToCreate.PatternStartPoint;

        // playback duration
        string formatDuration = PlaybackDuration.Hours > 0 ? "hh\\:mm\\:ss" : "mm\\:ss";
        _uiShared.DrawTimeSpanCombo("Playback Duration", maxPlaybackDuration, ref PlaybackDuration, UiSharedService.GetWindowContentRegionWidth()/2, formatDuration, true);
        alarmToCreate.PatternDuration = PlaybackDuration;

        ImGui.Separator();

        // Frequency of occurrence
        ImGui.Text("Alarm Frequency Per Week");
        var alarmRepeatValues = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToArray();
        int totalValues = alarmRepeatValues.Length;
        int splitIndex = 4; // Index to split the groups

        // Group 1: First four
        using (ImRaii.Group())
        {
            for (int i = 0; i < splitIndex && i < totalValues; i++)
            {
                DayOfWeek day = alarmRepeatValues[i];
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
                DayOfWeek day = alarmRepeatValues[i];
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
