using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Models;

namespace GagSpeak.PlayerData.Handlers;

/// <summary>
/// This handler should keep up to date with all of the currently set alarms. The alarms should
/// go off at their spesified times, and play their spesified patterns
/// </summary>
public class AlarmHandler : MediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly DeviceHandler _IntifaceHandler;

    public AlarmHandler(ILogger<AlarmHandler> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        DeviceHandler handler) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _IntifaceHandler = handler;

        Mediator.Subscribe<AlarmAddedMessage>(this, (msg) =>
        {
            // update the alarm refs
            AlarmListRef = _clientConfigs.AlarmsRef;
        });

        Mediator.Subscribe<AlarmRemovedMessage>(this, (msg) =>
        {
            // update the list of names
            AlarmListRef = _clientConfigs.AlarmsRef;
        });

        // probably dont need this because c# magic?
        Mediator.Subscribe<AlarmDataChanged>(this, (msg) =>
        {
            // update the list of names
            EditingAlarm = _clientConfigs.FetchAlarm(msg.AlarmIndex);
        });
    }

    // store a public accessor
    public List<Alarm> AlarmListRef { get; private set; } = null!;

    // store a accessor of the alarm being edited
    public Alarm EditingAlarm { get; private set; } = null!;

    // know if the above accessors are null through a public bool return val.
    public bool AlarmListNull => AlarmListRef == null;
    public bool EditingAlarmNull => EditingAlarm == null;


    /* -------- Handler Methods ---------- */
    /// <summary>
    /// Appends a new alarm to the client's alarm list.
    /// </summary>
    /// <param name="newAlarm"> The alarm to be added to the list. </param>
    public void AddNewAlarm(Alarm newAlarm) => _clientConfigs.AddNewAlarm(newAlarm);

    /// <summary>
    /// Removes an alarm from the client's alarm list.
    /// </summary>
    /// <param name="idxToRemove"> The index of the alarm to be removed. </param>
    public void RemoveAlarm(int idxToRemove) => _clientConfigs.RemoveAlarm(idxToRemove);

    public int AlarmListSize() => _clientConfigs.FetchAlarmCount();

    public string GetAlarmFrequencyString(List<AlarmRepeat> FrequencyOptions)
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
                case AlarmRepeat.Sunday: result += "Sun"; break;
                case AlarmRepeat.Monday: result += "Mon"; break;
                case AlarmRepeat.Tuesday: result += "Tue"; break;
                case AlarmRepeat.Wednesday: result += "Wed"; break;
                case AlarmRepeat.Thursday: result += "Thu"; break;
                case AlarmRepeat.Friday: result += "Fri"; break;
                case AlarmRepeat.Saturday: result += "Sat"; break;
            }
            result += freq.ToString() + ", ";
        }
        // remove the last comma and space.
        result = result.Remove(result.Length - 2);
        return result;
    }
}

