using Dalamud.Game.ClientState.Objects;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.Hardcore;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UI.MainWindow;
using GagSpeak.UpdateMonitoring.Chat;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Enum;
using System.Numerics;

namespace GagSpeak.PlayerData.Handlers;
/// <summary> Responsible for handling hardcore communication from stored data & ui to core logic. </summary>
public class HardcoreHandler : DisposableMediatorSubscriberBase
{
    private readonly GagspeakConfigService _mainConfig;
    private readonly PairManager _pairManager;
    private readonly WardrobeHandler _outfitHandler;
    private readonly ApiController _apiController; // for sending the updates.
    private readonly ITargetManager _targetManager; // for targetting pair on follows.

    public unsafe GameCameraManager* cameraManager = GameCameraManager.Instance(); // for the camera manager object
    public HardcoreHandler(ILogger<HardcoreHandler> logger, GagspeakMediator mediator,
        GagspeakConfigService mainConfig, PairManager pairManager,
        WardrobeHandler outfitHandler, ApiController apiController, 
        ITargetManager targetManager) : base(logger, mediator)
    {
        _mainConfig = mainConfig;
        _pairManager = pairManager;
        _outfitHandler = outfitHandler;
        _apiController = apiController;
        _targetManager = targetManager;

        // update the text folder
        _mainConfig.Current.StoredEntriesFolder.CheckAndInsertRequired();
        _mainConfig.Current.StoredEntriesFolder.PruneEmpty();
        _mainConfig.Save();

        Mediator.Subscribe<HardcoreForcedToFollowMessage>(this, (msg) =>
        {
            if(msg.State == UpdatedNewState.Enabled) SetForcedFollow(true, msg.Pair);
            else SetForcedFollow(false, msg.Pair);
        });
        Mediator.Subscribe<HardcoreForcedToSitMessage>(this, (msg) =>
        {
            ForcedToSitPair = msg.Pair;
        });
        Mediator.Subscribe<HardcoreForcedToStayMessage>(this, (msg) =>
        {
            ForcedToStayPair = msg.Pair;
        });
        Mediator.Subscribe<HardcoreForcedBlindfoldMessage>(this, (msg) =>
        {
            BlindfoldedByPair = msg.Pair;
        });
    }

    public Pair? ForcedToFollowPair { get; private set; }
    public Pair? ForcedToSitPair { get; private set; }
    public Pair? ForcedToStayPair { get; private set; }
    public Pair? BlindfoldedByPair { get; private set; }


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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_mainConfig.Current.UsingLegacyControls == false && GameConfig.UiControl.GetBool("MoveMode") == true)
        {
            // we have legacy on but dont normally have it on, so make sure that we set it back to normal!
            GameConfig.UiControl.Set("MoveMode", (int)MovementMode.Standard);
        }
    }


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

    public void SetForcedFollow(bool newState, Pair? pairToFollow = null)
    {
        /************** WHEN ATTEMPTING TO ENABLE *****************/
        // if we are not following anyone, and the new state is false, return
        if (ForcedToFollowPair == null && newState == false)
        {
            Logger.LogError("Attempted to disable forced follow while not following anyone.");
            return;
        }
        
        // if we are not following anyone and the new state is true, but the pairToFollow is null, return
        if (ForcedToFollowPair == null && newState == true && pairToFollow == null)
        {
            Logger.LogError("Attempted to enable forced follow while not following anyone and no pair to follow was provided.");
            return;
        }

        // if we are trying to set it to true but a pair is already being set to follow, log the error and return.
        if (ForcedToFollowPair != null && newState == true && pairToFollow != null)
        {
            Logger.LogError("Attempted to set a new pair to follow while a pair is already being followed.");
            return;
        }

        // if we are not following anyone and the new state is true, set it to the pair to follow. (THIS WORKS)
        if (ForcedToFollowPair == null && newState == true && pairToFollow != null)
        {
            ForcedToFollowPair = pairToFollow;
            // if for whatever reason this pair you dont have forced to follow set to true for, set it back to null and return.
            if (!pairToFollow.UserPairOwnUniquePairPerms.IsForcedToFollow)
            {
                ForcedToFollowPair = null;
                return;
            }
            // otherwise, handle the ForcedToFollow.
            HandleForcedFollow(true, pairToFollow.UserData);
            Logger.LogDebug("Enabled forced follow for pair.");
            return;
        }

        /************** WHEN ATTEMPTING TO DISABLE *****************/
        // if we are trying to disable the forced follow but the pair to follow is null, return.
        if (newState == false && ForcedToFollowPair == null)
        {
            Logger.LogError("Attempted to disable forced follow while not following anyone.");
            return;
        }

        // if we are trying to disable but the pairToFollow is not equal to the pair requesting us to stop following, return.
        if (newState == false && ForcedToFollowPair != null && pairToFollow != null && pairToFollow.UserData.UID != ForcedToFollowPair?.UserData.UID)
        {
            Logger.LogError("Attempted to disable forced follow for a pair that is not the pair we are following.");
            return;
        }

        // if we are trying to disable, while the pair is active, but a pair is not provided, we are auto disabling it.
        if (newState == false && ForcedToFollowPair != null && pairToFollow != null)
        {
            var userData = ForcedToFollowPair.UserData;
            ForcedToFollowPair.UserPairOwnUniquePairPerms.IsForcedToFollow = false;
            HandleForcedFollow(false, userData);
            ForcedToFollowPair = null;
            _ = _apiController.UserUpdateOwnPairPerm(new(userData, new KeyValuePair<string, object>("IsForcedToFollow", false)));
            Logger.LogDebug("Auto disabled forced follow for pair.");
            return;
        }
    }

    // handles the forced follow logic.
    public void HandleForcedFollow(bool newState, UserData? pairUserData = null)
    {
        if(newState == true)
        {
            if(ForcedToFollowPair == null) { Logger.LogError("Somehow you still haven't set the forcedToFollowPair???"); return; }
            // target our pair and follow them.
            _targetManager.Target = ForcedToFollowPair.VisiblePairGameObject;
            ChatBoxMessage.EnqueueMessage("/follow <t>");
        }

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
