using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Enums;
using GagspeakAPI.Extensions;
using ImGuiNET;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.UiGagSetup;

public class ActiveGagsPanel : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly PlayerCharacterData _playerManager; // for grabbing lock data
    private readonly GagManager _gagManager;
    private readonly AppearanceManager _appearanceHandler;
    private readonly AppearanceService _appearanceChangeService;

    public ActiveGagsPanel(ILogger<ActiveGagsPanel> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        GagManager gagManager, PlayerCharacterData playerManager,
        AppearanceManager handler, AppearanceService appearanceChangeService)
        : base(logger, mediator)
    {
        _uiSharedService = uiSharedService;
        _playerManager = playerManager;
        _gagManager = gagManager;
        _appearanceHandler = handler;
        _appearanceChangeService = appearanceChangeService;
        Mediator.Subscribe<ActiveGagsUpdated>(this, (_) => UpdateActiveGags());
        Mediator.Subscribe<ActiveLocksUpdated>(this, (_) => UpdateActiveLocks());
    }
    private string[] Filters = new string[3] { "", "", "" };
    private static readonly string[] Labels = { "Inner Gag", "Central Gag", "Outer Gag" };

    private static readonly HashSet<Padlocks> TwoRowLocks = new HashSet<Padlocks>
    {
        Padlocks.None, Padlocks.MetalPadlock, Padlocks.FiveMinutesPadlock, Padlocks.OwnerPadlock, Padlocks.OwnerTimerPadlock,
        Padlocks.DevotionalPadlock, Padlocks.DevotionalTimerPadlock, Padlocks.MimicPadlock
    };

    private string GetGagTypePath(int index) => $"GagImages\\{_playerManager.AppearanceData!.GagSlots[index].GagType}.png" ?? $"ItemMouth\\None.png";
    private string GetGagPadlockPath(int index) => $"PadlockImages\\{_playerManager.AppearanceData!.GagSlots[index].Padlock.ToPadlock()}.png" ?? $"Padlocks\\None.png";
    private bool IsTimerLock(Padlocks padlock) => padlock is 
        Padlocks.FiveMinutesPadlock or Padlocks.TimerPasswordPadlock or Padlocks.OwnerTimerPadlock or Padlocks.DevotionalTimerPadlock or Padlocks.MimicPadlock;
    private void UpdateActiveGags()
    {
        if (_playerManager.CoreDataNull) { Logger.LogWarning("Appearance data is null, cannot update active gags."); return; }

        // update our combo items.
        for (int i = 0; i < 3; i++)
            _uiSharedService._selectedComboItems[$"GagType{i}"] = _playerManager.AppearanceData!.GagSlots[i].GagType.ToGagType();
        Logger.LogDebug("Dropdown Gags Now: " + string.Join(" || ", _playerManager.AppearanceData!.GagSlots.Select((g, i) => $"Gag {i}: {g.GagType}")), LoggerType.GagManagement);
    }

    private void UpdateActiveLocks()
    {
        if (_playerManager.CoreDataNull) { Logger.LogWarning("Appearance data is null, cannot update active gags."); return; }

        for (int i = 0; i < 3; i++)
        {
            var padlock = _playerManager.AppearanceData?.GagSlots[i].Padlock.ToPadlock() ?? Padlocks.None;
            _uiSharedService._selectedComboItems[$"LockType{i}"] = padlock;
            GagManager.ActiveSlotPadlocks[i] = padlock;
        }
        Logger.LogDebug("Dropdown Locks Now: "+ string.Join(" || ", GagManager.ActiveSlotPadlocks.Select((p, i) => $"Lock {i}: {p}")), LoggerType.PadlockManagement);
    }

    // Draw the active gags tab
    public void DrawActiveGagsPanel()
    {
        if (_playerManager.CoreDataNull)
        {
            Logger.LogWarning("Core data is null, cannot draw active gags panel.");
            return;
        }
        Vector2 bigTextSize = new Vector2(0, 0);
        using (_uiSharedService.UidFont.Push()) { bigTextSize = ImGui.CalcTextSize("HeightDummy"); }
        var region = ImGui.GetContentRegionAvail();
        
        var gagSlots = _playerManager.AppearanceData?.GagSlots ?? new GagSlot[3];
        try
        {
            for (int i = 0; i < 3; i++)
            {
                bool currentlyLocked = gagSlots[i].Padlock.ToPadlock() is not Padlocks.None;
                Padlocks currentPadlockSelection = gagSlots[i].Padlock.ToPadlock() is Padlocks.None ? GagManager.ActiveSlotPadlocks[i] : gagSlots[i].Padlock.ToPadlock();

                DrawGagSlotHeader(i, bigTextSize);
                using (ImRaii.Group())
                {
                    DrawImage(GetGagTypePath(i));
                    ImGui.SameLine();
                    // Dictate where this group is drawn.
                    var GroupCursorY = ImGui.GetCursorPosY();
                    using (ImRaii.Group())
                    {
                        if (TwoRowLocks.Contains(gagSlots[i].Padlock.ToPadlock()))
                            ImGui.SetCursorPosY(GroupCursorY + ImGui.GetFrameHeight() / 2);
                        DrawGagLockGroup(i, region, gagSlots, currentlyLocked, currentPadlockSelection);
                    }
                    if (gagSlots[i].Padlock.ToPadlock() is not Padlocks.None && currentlyLocked)
                    {
                        ImGui.SameLine();
                        DrawImage(GetGagPadlockPath(i));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error: {ex}");
        }
    }

    private void DrawGagSlotHeader(int slotNumber, Vector2 bigTextSize)
    {
        _uiSharedService.BigText(Labels[slotNumber]);
        if (_playerManager.CoreDataNull)
            return;

        if (IsTimerLock(_playerManager.AppearanceData!.GagSlots[slotNumber].Padlock.ToPadlock()))
        {
            ImGui.SameLine();
            DisplayTimeLeft(
                _playerManager.AppearanceData!.GagSlots[slotNumber].Timer,
                _playerManager.AppearanceData!.GagSlots[slotNumber].Padlock.ToPadlock(),
                _playerManager.AppearanceData!.GagSlots[slotNumber].Assigner, 
                yPos: ImGui.GetCursorPosY() + ((bigTextSize.Y - ImGui.GetTextLineHeight()) / 2) + 5f);
        }
    }

    private void DrawImage(string gagTypePath)
    {
        var gagTexture = _uiSharedService.GetImageFromDirectoryFile(gagTypePath);
        if (gagTexture is { } wrapGag)
            ImGui.Image(wrapGag.ImGuiHandle, new Vector2(80, 80));
        else
            Logger.LogWarning("Failed to render image!");
    }

    private Task? _gagTypeChangeTask;
    private Task? _gagPadlockChangeTask;

    private void DrawGagLockGroup(int idx, Vector2 region, GagSlot[] gagSlots, bool currentlyLocked, Padlocks currentPadlockSelection)
    {
        bool gagTypeIsNone = gagSlots[idx].GagType.ToGagType() is GagType.None;
        // The Gag Group
        using (ImRaii.Disabled(currentlyLocked))
        {
            _uiSharedService.DrawComboSearchable("GagType"+idx, 250f, ref Filters[idx],
            Enum.GetValues<GagType>(), (gag) => gag.GagName(), false,
            (i) =>
            {
                // obtain the previous gag prior to changing.
                var PrevGag = gagSlots[idx].GagType.ToGagType();
                // If Prev gag was none, we are applying, so equip.
                if (PrevGag is GagType.None)
                {
                    // if our task is running still, dont execute this.
                    if (!(_gagTypeChangeTask is not null && !_gagTypeChangeTask.IsCompleted))
                        _gagTypeChangeTask = _appearanceHandler.GagApplied((GagLayer)idx, i);
                }
                else if (PrevGag is not GagType.None)
                {
                    // We are swapping gags, so we need to initialize a replace call.
                    if (!(_gagTypeChangeTask is not null && !_gagTypeChangeTask.IsCompleted))
                    {
                        if(i is GagType.None)
                            _gagTypeChangeTask = _appearanceHandler.GagRemoved((GagLayer)idx, PrevGag);
                        else
                            _gagTypeChangeTask = _appearanceHandler.GagSwapped((GagLayer)idx, PrevGag, i);

                    }
                }
            }, gagSlots[idx].GagType.ToGagType());
        }

        // The Lock Group
        using (ImRaii.Disabled(currentlyLocked || gagTypeIsNone))
        {
            _uiSharedService.DrawCombo("LockType"+idx, (248 - _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Lock).X),
            GenericHelpers.NoOwnerPadlockList, (padlock) => padlock.ToName(),
            (i) => GagManager.ActiveSlotPadlocks[idx] = i, currentPadlockSelection, false);
        }
        // Scootch over and draw the lock button.
        ImGui.SameLine(0, 2);
        using (ImRaii.Disabled(currentPadlockSelection is Padlocks.None))
        {
            if (_uiSharedService.IconButton(currentlyLocked ? FontAwesomeIcon.Unlock : FontAwesomeIcon.Lock, id: "lock/unlock"+idx))
            {
                if (_gagManager.PasswordValidated(idx, currentlyLocked))
                {
                    var data = new PadlockData((GagLayer)idx, GagManager.ActiveSlotPadlocks[idx], GagManager.ActiveSlotPasswords[idx],
                        UiSharedService.GetEndTimeUTC(GagManager.ActiveSlotTimers[idx]), MainHub.UID);
                    _gagManager.OnGagLockChanged(data, currentlyLocked ? NewState.Unlocked : NewState.Locked, true, true);
                }
                else
                {
                    // Password invalid, reset inputs
                    _gagManager.ResetInputs();
                    // if the padlock was a timer padlock and we are currently locked trying to unlock, fire the event for it.
                    if (currentPadlockSelection is Padlocks.PasswordPadlock or Padlocks.TimerPasswordPadlock or Padlocks.CombinationPadlock)
                        UnlocksEventManager.AchievementEvent(UnlocksEvent.GagUnlockGuessFailed);
                }
            }
            UiSharedService.AttachToolTip(currentlyLocked ? "Attempt Unlocking " : "Lock " + "this gag.");
        }
        // display associated password field for padlock type.
        _gagManager.DisplayPadlockFields(idx, currentlyLocked);
    }
    private void DisplayTimeLeft(DateTimeOffset endTime, Padlocks padlock, string userWhoSetLock, float yPos)
    {
        var prefixText = userWhoSetLock != MainHub.UID
            ? userWhoSetLock +"'s " : (padlock is Padlocks.MimicPadlock ? "The Devious " : "Self-Applied ");
        var gagText = padlock.ToName() + " has";
        var color = ImGuiColors.ParsedGold;
        switch(padlock)
        {
            case Padlocks.MetalPadlock:
            case Padlocks.CombinationPadlock:
            case Padlocks.PasswordPadlock:
            case Padlocks.FiveMinutesPadlock:
            case Padlocks.TimerPasswordPadlock:
                color = ImGuiColors.ParsedGold; break;
            case Padlocks.OwnerPadlock:
            case Padlocks.OwnerTimerPadlock:
                color = ImGuiColors.ParsedPink; break;
            case Padlocks.DevotionalPadlock:
            case Padlocks.DevotionalTimerPadlock:
                color = ImGuiColors.TankBlue; break;
            case Padlocks.MimicPadlock:
                color = ImGuiColors.ParsedGreen; break;
        }
        ImGui.SameLine();
        ImGui.SetCursorPosY(yPos);
        UiSharedService.ColorText(prefixText + gagText, color);
        ImUtf8.SameLineInner();
        ImGui.SetCursorPosY(yPos);
        UiSharedService.ColorText(UiSharedService.TimeLeftFancy(endTime), color);
    }
}
