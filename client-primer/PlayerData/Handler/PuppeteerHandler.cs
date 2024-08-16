using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.UiPuppeteer;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;

namespace GagSpeak.PlayerData.Handlers;
/// <summary>
/// Should be a nice place to store a rapidly updating vibe intensity value while connecting to toybox servers to send the new intensities
/// </summary>
public class PuppeteerHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;

    public PuppeteerHandler(ILogger<PuppeteerHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;


        // subscriber to update the pair being displayed.
        Mediator.Subscribe<UpdateDisplayWithPair>(this, (msg) =>
        {
            // for firstime generations
            if (SelectedPair == null)
            {
                SelectedPair = msg.Pair;
                StorageBeingEdited = _clientConfigs.FetchAliasStorageForPair(msg.Pair.UserData.UID);
            }

            // for refreshing data once we switch pairs.
            if (SelectedPair.UserData.UID != msg.Pair.UserData.UID)
            {
                Logger.LogTrace($"Updating display to reflect pair {msg.Pair.UserData.AliasOrUID}");
                SelectedPair = msg.Pair;
                StorageBeingEdited = _clientConfigs.FetchAliasStorageForPair(msg.Pair.UserData.UID);
            }
            // log if the storage being edited is null.
            if (StorageBeingEdited == null)
            {
                Logger.LogWarning($"Storage being edited is null for pair {msg.Pair.UserData.AliasOrUID}");
            }
        });
    }

    public Pair? SelectedPair = null; // Selected Pair we are viewing for Puppeteer.

    // Store an accessor of the alarm being edited.
    private AliasStorage? _storageBeingEdited;
    public string UidOfStorage { get; private set; } = string.Empty;
    public AliasStorage StorageBeingEdited
    {
        get
        {
            if (_storageBeingEdited == null && UidOfStorage != string.Empty)
            {
                _storageBeingEdited = _clientConfigs.FetchAliasStorageForPair(UidOfStorage);
            }
            return _storageBeingEdited!;
        }
        private set => _storageBeingEdited = value;
    }
    public bool EditingListIsNull => StorageBeingEdited == null;

    public void ClearEditedAliasStorage()
    {
        UidOfStorage = string.Empty;
        StorageBeingEdited = null!;
    }

    public void UpdatedEditedStorage()
    {
        // update the set in the client configs
        _clientConfigs.UpdateAliasStorage(UidOfStorage, StorageBeingEdited);
        // clear the editing set
        ClearEditedAliasStorage();
    }

    // Only intended to be called via the AliasStorage Callback dto.
    public void UpdatePlayerInfoForUID(string uid, string charaName, string charaWorld)
        => _clientConfigs.UpdateAliasStoragePlayerInfo(uid, charaName, charaWorld);


    public AliasStorage GetAliasStorage(string pairUID)
        => _clientConfigs.FetchAliasStorageForPair(pairUID);

    public void UpdateAliasStorage(string pairUID, AliasStorage storageToUpdate)
    => _clientConfigs.UpdateAliasStorage(pairUID, storageToUpdate);

    public void AddAlias(AliasTrigger alias)
        => StorageBeingEdited.AliasList.Add(alias);
    public void RemoveAlias(AliasTrigger alias)
        => StorageBeingEdited.AliasList.Remove(alias);

    public void UpdateAliasInput(int aliasIndex, string input)
        => StorageBeingEdited.AliasList[aliasIndex].InputCommand = input;

    public void UpdateAliasOutput(int aliasIndex, string output)
        => StorageBeingEdited.AliasList[aliasIndex].OutputCommand = output;
}
