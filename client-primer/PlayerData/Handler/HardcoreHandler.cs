using Dalamud.Plugin;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UI.MainWindow;
using GagspeakAPI.Data.Enum;
using System.Numerics;

namespace GagSpeak.PlayerData.Handlers;
/// <summary> Responsible for handling hardcore communication from stored data & ui to core logic. </summary>
public class HardcoreHandler : DisposableMediatorSubscriberBase
{
    private readonly GagspeakConfigService _mainConfig;
    private readonly PairManager _pairManager;
    private readonly WardrobeHandler _outfitHandler;

    // for camera manager
    public unsafe GameCameraManager* cameraManager = GameCameraManager.Instance(); // for the camera manager object
    public HardcoreHandler(ILogger<GagDataHandler> logger, GagspeakMediator mediator,
        GagspeakConfigService mainConfig, PairManager pairManager, 
        WardrobeHandler outfitHandler) : base(logger, mediator)
    {
        _mainConfig = mainConfig;
        _pairManager = pairManager;
        _outfitHandler = outfitHandler;

        // update the textfolder
        _mainConfig.Current.StoredEntriesFolder.CheckAndInsertRequired();
        _mainConfig.Current.StoredEntriesFolder.PruneEmpty();
        _mainConfig.Save();
    }

    public bool DisablePromptHooks => _mainConfig.Current.DisablePromptHooks;
    public TextFolderNode StoredEntriesFolder => _mainConfig.Current.StoredEntriesFolder;

    public Tuple<string, List<string>> LastSeenDialogText { get; set; }
    public TextEntryNode? LastSelectedListNode { get; set; } = null;
    public string LastSeenListTarget { get; set; } = string.Empty;
    public string LastSeenListSelection { get; set; } = string.Empty;
    public (int Index, string Text)[] LastSeenListEntries { get; set; } = [];
    public int LastSeenListIndex { get; set; } = -1;
    public DateTimeOffset LastMovementTime { get; set; } = DateTimeOffset.Now;
    public Vector3 LastPosition { get; set; } = Vector3.Zero;
    public double StimulationMultiplier { get; set; } = 1.0;

    public IEnumerable<ITextNode> GetAllNodes()
    {
        return new ITextNode[] { StoredEntriesFolder }.Concat(GetAllNodes(StoredEntriesFolder.Children));
    }

    public IEnumerable<ITextNode> GetAllNodes(IEnumerable<ITextNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            if (node is TextFolderNode folder)
            {
                var children = GetAllNodes(folder.Children);
                foreach (var childNode in children)
                {
                    yield return childNode;
                }
            }
        }
    }

    public bool TryFindParent(ITextNode node, out TextFolderNode? parent)
    {
        foreach (var candidate in GetAllNodes())
        {
            if (candidate is TextFolderNode folder && folder.Children.Contains(node))
            {
                parent = folder;
                return true;
            }
        }

        parent = null;
        return false;
    }

    public void CreateTextNode(TextFolderNode folder)
    {
        var newNode = new TextEntryNode()
        {
            Enabled = true,
            Text = LastSeenDialogText.Item1,
            Options = LastSeenDialogText.Item2.ToArray(),
        };
        folder.Children.Add(newNode);
    }

    public void SetPromptHooksState(bool newState)
    {
        _mainConfig.Current.DisablePromptHooks = newState;
        _mainConfig.Save();
    }

    // handles the forced follow logic.
    public void HandleForcedFollow(bool newState)
    {
        // toggle movement type to legacy if we are not on legacy
        if (_mainConfig.Current.UsingLegacyControls == false)
        {
            // if forced follow is still on, dont switch it back to false
            uint mode = newState ? (uint)MovementMode.Legacy : (uint)MovementMode.Standard;
            GameConfig.UiControl.Set("MoveMode", mode);
        }
    }

    public bool IsCurrentlyForcedToFollow() // return if we are currently forced to follow by anyone
        => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow);

    public bool IsCurrentlyForcedToSit() // return if we are currently forced to move by anyone
        => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToSit);

    public bool IsCurrentlyForcedToStay() // return if we are currently forced to stay by anyone
        => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToStay);



    public async Task HandleBlindfoldLogic(UpdatedNewState newState, string applierUID)
    {
        // toggle our window based on conditions
        if (newState == UpdatedNewState.Enabled && !BlindfoldUI.IsWindowOpen)
        {
            Mediator.Publish(new UiToggleMessage(typeof(BlindfoldUI), ToggleType.Show));
        }
        if (newState == UpdatedNewState.Disabled && BlindfoldUI.IsWindowOpen)
        {
            Mediator.Publish(new UiToggleMessage(typeof(MainWindowUI), ToggleType.Hide));
        }
        if (UpdatedNewState.Enabled == newState)
        {
            // go in right away
            DoCameraVoodoo(newState);
            // apply the blindfold
            Mediator.Publish(new UpdateGlamourBlindfoldMessage(UpdatedNewState.Enabled, applierUID));

        }
        else
        {
            // wait a bit before doing the camera voodoo
            await Task.Delay(2000);
            DoCameraVoodoo(newState);
            // call a refresh all
            Mediator.Publish(new UpdateGlamourBlindfoldMessage(UpdatedNewState.Disabled, applierUID));
        }
    }

    private unsafe void DoCameraVoodoo(UpdatedNewState newValue)
    {
        // force the camera to first person, but dont loop the force
        if (UpdatedNewState.Enabled == newValue)
        {
            if (cameraManager != null && cameraManager->Camera != null
            && cameraManager->Camera->Mode != (int)CameraControlMode.FirstPerson)
            {
                cameraManager->Camera->Mode = (int)CameraControlMode.FirstPerson;
            }
        }
        else
        {
            if (cameraManager != null && cameraManager->Camera != null
            && cameraManager->Camera->Mode == (int)CameraControlMode.FirstPerson)
            {
                cameraManager->Camera->Mode = (int)CameraControlMode.ThirdPerson;
            }
        }
    }

    public void ApplyMultiplier()
    {
        if (_outfitHandler.ActiveSet.SetProperties[_outfitHandler.ActiveSet.EnabledBy].LightStimulation)
        {
            Logger.LogDebug($"Light Stimulation Multiplier applied from set with factor of 1.125x!");
            StimulationMultiplier = 1.125;
        }
        else if (_outfitHandler.ActiveSet.SetProperties[_outfitHandler.ActiveSet.EnabledBy].MildStimulation)
        {
            Logger.LogDebug($"Mild Stimulation Multiplier applied from set with factor of 1.25x!");
            StimulationMultiplier = 1.25;
        }
        else if (_outfitHandler.ActiveSet.SetProperties[_outfitHandler.ActiveSet.EnabledBy].HeavyStimulation)
        {
            Logger.LogDebug($"Heavy Stimulation Multiplier applied from set with factor of 1.5x!");
            StimulationMultiplier = 1.5;
        }
        else
        {
            Logger.LogDebug($"No Stimulation Multiplier applied from set, defaulting to 1.0x!");
            StimulationMultiplier = 1.0;
        }
    }
}
