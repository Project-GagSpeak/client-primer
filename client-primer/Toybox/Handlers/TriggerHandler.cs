using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;

namespace GagSpeak.PlayerData.Handlers;

public class TriggerHandler : MediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    public TriggerHandler(ILogger<TriggerHandler> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs) 
        : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
    }

    private Trigger? _triggerBeingEdited;
    public int EditingTriggerIndex { get; private set; } = -1;
    public Trigger TriggerBeingEdited
    {
        get
        {
            if (_triggerBeingEdited == null && EditingTriggerIndex >= 0)
            {
                _triggerBeingEdited = _clientConfigs.FetchTrigger(EditingTriggerIndex);
            }
            return _triggerBeingEdited!;
        }
        private set => _triggerBeingEdited = value;
    }
    public bool EditingTriggerNull => TriggerBeingEdited == null;

    public void SetEditingTrigger(Trigger trigger, int index)
    {
        TriggerBeingEdited = trigger;
        EditingTriggerIndex = index;
    }

    public void ClearEditingTrigger()
    {
        EditingTriggerIndex = -1;
        TriggerBeingEdited = null!;
    }

    public void UpdateEditedTrigger()
    {
        // update the trigger in the client configs
        _clientConfigs.UpdateTrigger(TriggerBeingEdited, EditingTriggerIndex);
        // clear the editing trigger
        ClearEditingTrigger();
    }


    public void AddNewTrigger(Trigger newTrigger)
        => _clientConfigs.AddNewTrigger(newTrigger);

    public void RemoveTrigger(int idxToRemove)
    {
        _clientConfigs.RemoveTrigger(idxToRemove);
        ClearEditingTrigger();
    }

    public int TriggerListSize()
        => _clientConfigs.FetchTriggerCount();

    public List<Trigger> GetTriggersForSearch()
    => _clientConfigs.GetTriggersForSearch();

    public Trigger GetTrigger(int idx)
        => _clientConfigs.FetchTrigger(idx);

    public void EnableTrigger(int idx)
        => _clientConfigs.SetTriggerState(idx, true);

    public void DisableTrigger(int idx)
        => _clientConfigs.SetTriggerState(idx, false);
}
