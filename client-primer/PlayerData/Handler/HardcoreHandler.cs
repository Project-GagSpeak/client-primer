using Dalamud.Plugin;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagspeakAPI.Data.Enum;
using System.Numerics;

namespace GagSpeak.PlayerData.Handlers;
/// <summary> Responsible for handling hardcore communication from stored data & ui to core logic. </summary>
public class HardcoreHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly WardrobeHandler _outfitHandler;

    // for camera manager
    public unsafe GameCameraManager* cameraManager = GameCameraManager.Instance(); // for the camera manager object
    private readonly BlindfoldUI _blindfoldWindowRef;
    public HardcoreHandler(ILogger<GagDataHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, 
        PlayerCharacterManager playerManager, WardrobeHandler outfitHandler, 
        BlindfoldUI blindfoldRef) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _playerManager = playerManager;
        _outfitHandler = outfitHandler;
        _blindfoldWindowRef = blindfoldRef;
    }

    // our publicly accessible variables.
    public Tuple<string, List<string>> LastSeenDialogText { get; set; }
    public bool DisablePromptHooks { get; set; } = false;
    public TextFolderNode StoredEntriesFolder { get; private set; } = new TextFolderNode { Name = "ForcedDeclineList" };
    public double StimulationMultiplier = 1.0;
    public string LastSeenListTarget { get; set; } = string.Empty;
    public string LastSeenListSelection { get; set; } = string.Empty;
    public DateTimeOffset LastMovementTime { get; set; } = DateTimeOffset.Now;
    public Vector3 LastPosition { get; set; } = Vector3.Zero;

    // Diversify this handler so that it can store many public access variables that help prevent us
    // from needing to iterate over pair manager every time something happens or is checked upon.

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

    // handles the forced follow logic.
    public void HandleForcedFollow(bool newState)
    {
        // toggle movement type to legacy if we are not on legacy
        if (_clientConfigs.GagspeakConfig.UsingLegacyControls == false)
        {
            // if forced follow is still on, dont switch it back to false
            uint mode = newState ? (uint)MovementMode.Legacy : (uint)MovementMode.Standard;
            GameConfig.UiControl.Set("MoveMode", mode);
        }
    }

    public async Task HandleBlindfoldLogic(UpdatedNewState newState, string applierUID)
    {
        // toggle our window based on conditions
        if (newState == UpdatedNewState.Enabled && !_blindfoldWindowRef.IsOpen)
        {
            _blindfoldWindowRef.ActivateWindow();
        }
        if (newState == UpdatedNewState.Disabled && _blindfoldWindowRef.IsOpen)
        {
            _blindfoldWindowRef.DeactivateWindow();
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
