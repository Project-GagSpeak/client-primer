using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagspeakAPI.Data;
using Microsoft.Extensions.FileSystemGlobbing.Internal;

namespace GagSpeak.PlayerData.Handlers;

/// <summary>
/// This handler should keep up to date with all of the currently set alarms. The alarms should
/// go off at their specified times, and play their specified patterns
/// </summary>
public class AlarmHandler : MediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlaybackService _playbackService;
    private DateTime _lastExecutionTime;

    public AlarmHandler(ILogger<AlarmHandler> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PlaybackService playbackService) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playbackService = playbackService;

        // subscribe to the pattern removed, so we can clear the configured patterns of any alarms they were associated with
        Mediator.Subscribe<PatternRemovedMessage>(this, (msg) =>
        {
            // if msg.Pattern.Name is a patternName in any of our alarms
            // remove the pattern from the alarm
            _clientConfigs.RemovePatternNameFromAlarms(msg.PatternId);
        });

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => DelayedFrameworkAlarmCheck());
    }

    // store a accessor of the alarm being edited
    private Alarm? _alarmBeingEdited;
    public int EditingAlarmIndex { get; private set; } = -1;
    public Alarm AlarmBeingEdited
    {
        get
        {
            if (_alarmBeingEdited == null && EditingAlarmIndex >= 0)
            {
                _alarmBeingEdited = _clientConfigs.FetchAlarm(EditingAlarmIndex);
            }
            return _alarmBeingEdited!;
        }
        private set => _alarmBeingEdited = value;
    }
    public bool EditingAlarmNull => AlarmBeingEdited == null;

    public void SetEditingAlarm(Alarm alarm, int index)
    {
        AlarmBeingEdited = alarm;
        EditingAlarmIndex = index;
    }

    public void ClearEditingAlarm()
    {
        EditingAlarmIndex = -1;
        AlarmBeingEdited = null!;
    }

    public void UpdateEditedAlarm()
    {
        // update the alarm in the client configs
        _clientConfigs.UpdateAlarm(AlarmBeingEdited, EditingAlarmIndex);
        // clear the editing alarm
        ClearEditingAlarm();
    }


    public void AddNewAlarm(Alarm newAlarm)
        => _clientConfigs.AddNewAlarm(newAlarm);

    public void RemoveAlarm(int idxToRemove)
    {
        _clientConfigs.RemoveAlarm(idxToRemove);
        ClearEditingAlarm();
    }

    public int AlarmListSize()
        => _clientConfigs.FetchAlarmCount();

    public Alarm GetAlarm(int idx)
        => _clientConfigs.FetchAlarm(idx);

    public void EnableAlarm(int idx)
    {
        _clientConfigs.SetAlarmState(idx, true);
        UnlocksEventManager.AchievementEvent(UnlocksEvent.AlarmToggled, NewState.Enabled);
    }

    public void DisableAlarm(int idx)
        => _clientConfigs.SetAlarmState(idx, false);

    public string GetPatternNameFromId(Guid id)
        => _clientConfigs.GetAlarmPatternName(id);

    public void UpdateAlarmStatesFromCallback(List<AlarmInfo> AlarmInfoList)
        => _clientConfigs.UpdateAlarmStatesFromCallback(AlarmInfoList);

    public TimeSpan GetPatternLength(Guid guid)
        => _clientConfigs.GetPatternLength(guid);

    public string GetAlarmFrequencyString(List<DayOfWeek> FrequencyOptions)
    {
        // if the alarm is empty, return "never".
        if (FrequencyOptions.Count == 0) return "Never";
        // if the alarm contains all days of the week, return "every day".
        if (FrequencyOptions.Count == 7) return "Every Day";
        // List size can contain multiple days, but cannot contain "never" or "every day".
        string result = "";
        foreach (var freq in FrequencyOptions)
        {
            switch (freq)
            {
                case DayOfWeek.Sunday: result += "Sun"; break;
                case DayOfWeek.Monday: result += "Mon"; break;
                case DayOfWeek.Tuesday: result += "Tue"; break;
                case DayOfWeek.Wednesday: result += "Wed"; break;
                case DayOfWeek.Thursday: result += "Thu"; break;
                case DayOfWeek.Friday: result += "Fri"; break;
                case DayOfWeek.Saturday: result += "Sat"; break;
            }
            result += ", ";
        }
        // remove the last comma and space.
        result = result.Remove(result.Length - 2);
        return result;
    }

    public void DelayedFrameworkAlarmCheck()
    {
        if ((DateTime.Now - _lastExecutionTime).TotalSeconds < 60)
        {
            return; // Exit if less than 60 seconds have passed since the last execution
        }

        _lastExecutionTime = DateTime.Now; // Update the last execution time

        Logger.LogTrace("Checking Alarms", LoggerType.ToyboxAlarms);

        // Iterate through each stored alarm
        int alarmCount = _clientConfigs.FetchAlarmCount();
        for (int i = 0; i < alarmCount; i++)
        {
            Alarm alarm = _clientConfigs.FetchAlarm(i);
            // if the alarm is not enabled, continue
            if (!alarm.Enabled)
            {
                continue;
            }
            // grab the current day of the week in our local timezone
            DateTime now = DateTime.Now;
            DayOfWeek currentDay = now.DayOfWeek;

            // check if current day is in our frequency list
            if (!alarm.RepeatFrequency.Contains(currentDay))
            {
                continue; // Early return if the current day is not in the frequency options
            }

            // convert execution time from UTC to our local timezone
            DateTimeOffset alarmTime = alarm.SetTimeUTC.ToLocalTime();

            // check if current time matches execution time and if so play
            if (now.Hour == alarmTime.Hour && now.Minute == alarmTime.Minute)
            {
                Logger.LogInformation("Playing Pattern : "+alarm.PatternToPlay, LoggerType.ToyboxAlarms);
                _playbackService.PlayPattern(alarm.PatternToPlay, alarm.PatternStartPoint, alarm.PatternDuration, true);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.PatternAction, PatternInteractionKind.Started, alarm.PatternToPlay, true);
            }
        }
    }
}
