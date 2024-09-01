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

        Mediator.Subscribe<HardcoreForcedToFollowMessage>(this, (msg) => SetForcedFollow(msg.State, msg.Pair));
        Mediator.Subscribe<HardcoreForcedToSitMessage>(this, (msg) => SetForcedSitState(msg.State, false, msg.Pair));
        Mediator.Subscribe<HardcoreForcedToKneelMessage>(this, (msg) => SetForcedSitState(msg.State, true, msg.Pair));
        Mediator.Subscribe<HardcoreForcedToStayMessage>(this, (msg) => SetForcedStayState(msg.State, msg.Pair));
        Mediator.Subscribe<HardcoreForcedBlindfoldMessage>(this, (msg) => SetBlindfoldState(msg.State, msg.Pair));
    }

    private bool _isFollowingForAnyPair;
    private bool _isSittingForAnyPair;
    private bool _isStayingForAnyPair;
    private bool _isBlindfoldedByAnyPair;
    public bool IsForcedFollow => _isFollowingForAnyPair;
    public Pair? ForceFollowedPair => _pairManager.DirectPairs.FirstOrDefault(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow) ?? null;
    public bool IsCurrentlyForcedToFollow() => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow);
    public bool IsForcedSit => _isSittingForAnyPair;
    public Pair? ForceSitPair => _pairManager.DirectPairs.FirstOrDefault(x => x.UserPairOwnUniquePairPerms.IsForcedToSit || x.UserPairOwnUniquePairPerms.IsForcedToGroundSit) ?? null;
    public bool IsCurrentlyForcedToSit() => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToSit || x.UserPairOwnUniquePairPerms.IsForcedToGroundSit);
    public bool IsForcedStay => _isStayingForAnyPair;
    public Pair? ForceStayPair => _pairManager.DirectPairs.FirstOrDefault(x => x.UserPairOwnUniquePairPerms.IsForcedToStay) ?? null;
    public bool IsCurrentlyForcedToStay() => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToStay);
    public bool IsBlindfolded => _isBlindfoldedByAnyPair;
    public Pair? BlindfoldPair => _pairManager.DirectPairs.FirstOrDefault(x => x.UserPairOwnUniquePairPerms.IsBlindfolded) ?? null;
    public bool IsCurrentlyBlindfolded() => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded);

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

    // when called for an enable, it will already be set to enabled, so we just need to update the state.
    // for disable, it can be auto, so we have to call it.
    public void SetForcedFollow(UpdatedNewState newState, Pair? pairToFollow = null)
    {
        // if the new state is true and we are already following any pairs, return.
        if (IsForcedFollow && newState == UpdatedNewState.Enabled) { Logger.LogError("Already Following Someone, Cannot Enable!"); return; }
        // if the new state is false and we are not following anyone, return.
        if (!IsForcedFollow && newState == UpdatedNewState.Disabled) { Logger.LogError("Not Following Anyone, Cannot Disable!"); return; }

        // if forced to follow is false, and we are setting it to true, begin setting.
        if (newState == UpdatedNewState.Enabled && !IsForcedFollow)
        {
            if (pairToFollow == null) { Logger.LogError("Cannot follow nothing."); return; }
            // update the isFollowingForAnyPair to true. This should always be true since its switched to enabled from the call.
            _isFollowingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow);
            HandleForcedFollow(true, pairToFollow);
            Logger.LogDebug("Enabled forced follow for pair.");
            return;
        }

        // if forced to follow is true, and we are setting it to false, begin setting.
        if (newState == UpdatedNewState.Disabled && IsForcedFollow)
        {
            if (pairToFollow == null)
            {
                Logger.LogWarning("ForceFollow Disable was triggered manually before it naturally disabled. Forcibly shutting down.");
                _isFollowingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow);
                HandleForcedFollow(false);
                Mediator.Publish(new MovementRestrictionChangedMessage(MovementRestrictionType.ForcedFollow, UpdatedNewState.Disabled));
                Logger.LogDebug("Disabled forced follow for pair.");
                return;
            }

            // if this is a natural falloff, we must naturally disable it.
            if (pairToFollow.UserData.UID != ForceFollowedPair?.UserData.UID)
            {
                Logger.LogError("Cannot unfollow a pair that is not the pair we are following.");
                return;
            }
            pairToFollow.UserPairOwnUniquePairPerms.IsForcedToFollow = false;
            _isFollowingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow);
            _ = _apiController.UserUpdateOwnPairPerm(new(pairToFollow.UserData, new KeyValuePair<string, object>("IsForcedToFollow", false)));
            HandleForcedFollow(false, pairToFollow);
            Mediator.Publish(new MovementRestrictionChangedMessage(MovementRestrictionType.ForcedFollow, UpdatedNewState.Disabled));
            // log success.
            Logger.LogDebug("Disabled forced follow for pair.");
            return;
        }
    }

    public void SetForcedSitState(UpdatedNewState newState, bool isGroundsit, Pair? pairToSitFor = null)
    {
        if (IsForcedSit && newState == UpdatedNewState.Enabled) { Logger.LogError("Already Forced to Sit by someone else, Cannot Enable!"); return; }
        if (!IsForcedSit && newState == UpdatedNewState.Disabled) { Logger.LogError("Not sitting for anyone. Cannot Disable!"); return; }
        if (pairToSitFor == null) { Logger.LogError("Cannot update forced sit status when pair is nothing."); return; }

        if (newState == UpdatedNewState.Enabled && !IsForcedSit)
        {
            if (isGroundsit)
            {
                // value is already updated, so simply update the boolean and enqueue the message.
                _isSittingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToGroundSit);
                ChatBoxMessage.EnqueueMessage("/groundsit");
            }
            else
            {
                _isSittingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToSit);
                ChatBoxMessage.EnqueueMessage("/sit");
            }
            // log success.
            Logger.LogDebug("Enabled forced kneeling for pair.");
            return;
        }

        if (newState == UpdatedNewState.Disabled && IsForcedSit)
        {
            if (pairToSitFor.UserData.UID != ForceSitPair?.UserData.UID)
            {
                Logger.LogError("Cannot force kneel a pair that is not the pair we are following.");
                return;
            }

            if (isGroundsit)
            {
                _isSittingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToGroundSit);
                Mediator.Publish(new MovementRestrictionChangedMessage(MovementRestrictionType.ForcedGroundSit, UpdatedNewState.Disabled));
            }
            else
            {
                _isSittingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToSit);
                Mediator.Publish(new MovementRestrictionChangedMessage(MovementRestrictionType.ForcedSit, UpdatedNewState.Disabled));
            }
            // log success.
            Logger.LogDebug("Enabled forced follow for pair.");
            return;
        }
    }

    public void SetForcedStayState(UpdatedNewState newState, Pair? pairToStayFor = null)
    {
        if (IsForcedStay && newState == UpdatedNewState.Enabled) { Logger.LogError("Already Forced to Stay by someone else. Cannot Enable!"); return; }
        if (!IsForcedStay && newState == UpdatedNewState.Disabled) { Logger.LogError("Not Forced to Stay by Anyone, Cannot Disable!"); return; }
        if (pairToStayFor == null) { Logger.LogError("Cannot follow nothing."); return; }

        // updates in either state are already set for the pair before its called, so we just need to update the boolean.
        if (newState == UpdatedNewState.Enabled && !IsForcedFollow)
        {
            _isStayingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToStay);
            Logger.LogDebug("Enabled forced follow for pair.");
            return;
        }

        if (newState == UpdatedNewState.Disabled && IsForcedFollow)
        {
            if (pairToStayFor.UserData.UID != ForceStayPair?.UserData.UID)
            {
                Logger.LogError("Cannot unfollow a pair that is not the pair we are following.");
                return;
            }
            _isStayingForAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToStay);
            Logger.LogDebug("Disabled forced follow for pair.");
            return;
        }
    }

    public async void SetBlindfoldState(UpdatedNewState newState, Pair? pairBlindfolding = null)
    {
        if (IsBlindfolded && newState == UpdatedNewState.Enabled) { Logger.LogError("Already Blindfolded by someone else. Cannot Enable!"); return; }
        if (!IsBlindfolded && newState == UpdatedNewState.Disabled) { Logger.LogError("Not Blindfolded by Anyone, Cannot Disable!"); return; }
        if (pairBlindfolding == null) { Logger.LogError("Cannot follow nothing."); return; }

        if (newState == UpdatedNewState.Enabled && !IsBlindfolded)
        {
            _isBlindfoldedByAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded);
            await HandleBlindfoldLogic(UpdatedNewState.Enabled, pairBlindfolding.UserData.UID);
            Logger.LogDebug("Enabled forced follow for pair.");
            return;
        }

        if (newState == UpdatedNewState.Disabled && IsForcedFollow)
        {
            if (pairBlindfolding.UserData.UID != BlindfoldPair?.UserData.UID)
            {
                Logger.LogError("Cannot unfollow a pair that is not the pair we are following.");
                return;
            }
            _isBlindfoldedByAnyPair = _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded);
            await HandleBlindfoldLogic(UpdatedNewState.Disabled, pairBlindfolding.UserData.UID);
            Logger.LogDebug("Disabled forced follow for pair.");
            return;
        }
    }

    // handles the forced follow logic.
    public void HandleForcedFollow(bool newState, Pair? pairUserData = null)
    {
        if(newState == true)
        {
            if(pairUserData == null) { Logger.LogError("Somehow you still haven't set the forcedToFollowPair???"); return; }
            // target our pair and follow them.
            _targetManager.Target = pairUserData.VisiblePairGameObject;
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
