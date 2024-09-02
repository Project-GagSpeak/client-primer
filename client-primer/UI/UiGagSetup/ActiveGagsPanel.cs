using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using System.Numerics;
namespace GagSpeak.UI.UiGagSetup;

public class ActiveGagsPanel : DisposableMediatorSubscriberBase
{

    private readonly UiSharedService _uiSharedService;
    private readonly PlayerCharacterManager _playerManager; // for grabbing lock data
    private readonly GagManager _gagManager;

    public ActiveGagsPanel(ILogger<ActiveGagsPanel> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        GagManager gagManager, PlayerCharacterManager playerManager)
        : base(logger, mediator)
    {
        _uiSharedService = uiSharedService;
        _playerManager = playerManager;
        _gagManager = gagManager;

        Mediator.Subscribe<ActiveGagsUpdated>(this, (_) =>
        {
            if(_playerManager.AppearanceData == null)
            {
                Logger.LogWarning("Appearance data is null, cannot update active gags.");
                return;
            }
            // update our combo items.
            _uiSharedService._selectedComboItems["Gag Type 0"] = _playerManager.AppearanceData.GagSlots[0].GagType.GetGagFromAlias();
            _uiSharedService._selectedComboItems["Gag Type 1"] = _playerManager.AppearanceData.GagSlots[1].GagType.GetGagFromAlias();
            _uiSharedService._selectedComboItems["Gag Type 2"] = _playerManager.AppearanceData.GagSlots[2].GagType.GetGagFromAlias();
        });

        Mediator.Subscribe<ActiveLocksUpdated>(this, (_) =>
        {
            if(_playerManager.AppearanceData == null)
            {
                Logger.LogWarning("Appearance data is null, cannot update active locks.");
                return;
            }

            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.GagSlots[0].Padlock, out var lock1);
            Logger.LogInformation($"Lock 1: {lock1}");
            _uiSharedService._selectedComboItems["Lock Type 0"] = lock1;
            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.GagSlots[1].Padlock, out var lock2);
            Logger.LogInformation($"Lock 2: {lock2}");
            _uiSharedService._selectedComboItems["Lock Type 1"] = lock2;
            Enum.TryParse<Padlocks>(_playerManager.AppearanceData.GagSlots[2].Padlock, out var lock3);
            Logger.LogInformation($"Lock 3: {lock3}");
            _uiSharedService._selectedComboItems["Lock Type 2"] = lock3;
        });
    }

    private string GagTypeOnePath => $"ItemMouth\\{_playerManager.AppearanceData!.GagSlots[0].GagType}.png" ?? $"ItemMouth\\None.png";
    private string GagTypeTwoPath => $"ItemMouth\\{_playerManager.AppearanceData!.GagSlots[1].GagType}.png" ?? $"ItemMouth\\None.png";
    private string GagTypeThreePath => $"ItemMouth\\{_playerManager.AppearanceData!.GagSlots[2].GagType}.png" ?? $"ItemMouth\\None.png";
    private string GagPadlockOnePath => $"Padlocks\\{_playerManager.AppearanceData!.GagSlots[0].Padlock}.png" ?? $"Padlocks\\None.png";
    private string GagPadlockTwoPath => $"Padlocks\\{_playerManager.AppearanceData!.GagSlots[1].Padlock}.png" ?? $"Padlocks\\None.png";
    private string GagPadlockThreePath => $"Padlocks\\{_playerManager.AppearanceData!.GagSlots[2].Padlock}.png" ?? $"Padlocks\\None.png";

    // the search filters for our gag dropdowns.
    public string[] Filters = new string[3] { "", "", "" };

    // Draw the active gags tab
    public void DrawActiveGagsPanel()
    {
        Vector2 bigTextSize = new Vector2(0, 0);
        using (_uiSharedService.UidFont.Push()) { bigTextSize = ImGui.CalcTextSize("HeightDummy"); }

        var region = ImGui.GetContentRegionAvail();
        try
        {
            var lock1 = _uiSharedService.GetPadlock(_playerManager.AppearanceData!.GagSlots[0].Padlock);
            var lock2 = _uiSharedService.GetPadlock(_playerManager.AppearanceData!.GagSlots[1].Padlock);
            var lock3 = _uiSharedService.GetPadlock(_playerManager.AppearanceData!.GagSlots[2].Padlock);

            // Gag Label 1
            _uiSharedService.BigText("Inner Gag:");
            // Gag Timer 1
            if(lock1 == Padlocks.FiveMinutesPadlock || lock1 == Padlocks.TimerPasswordPadlock || lock1 == Padlocks.OwnerTimerPadlock)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((bigTextSize.Y - ImGui.GetTextLineHeight()) / 2) + 5f);
                UiSharedService.ColorText(GetRemainingTimeString(_playerManager.AppearanceData.GagSlots[0].Timer, 
                    _playerManager.AppearanceData.GagSlots[0].Assigner), ImGuiColors.ParsedGold);
            }
            // Selection 1
            DrawGagAndLockSection(0, GagTypeOnePath, GagPadlockOnePath, (lock1 != Padlocks.None),
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.GagSlots[0].GagType], (lock1 == Padlocks.None ? _gagManager.PadlockPrevs[0] : lock1));

            // Gag Label 2
            _uiSharedService.BigText("Central Gag:");
            // Gag Timer 2
            if (lock2 == Padlocks.FiveMinutesPadlock || lock2 == Padlocks.TimerPasswordPadlock || lock2 == Padlocks.OwnerTimerPadlock)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((bigTextSize.Y - ImGui.GetTextLineHeight()) / 2) + 5f);
                UiSharedService.ColorText(GetRemainingTimeString(_playerManager.AppearanceData.GagSlots[1].Timer, 
                    _playerManager.AppearanceData.GagSlots[1].Assigner), ImGuiColors.ParsedGold);
            }
            // Selection 2
            DrawGagAndLockSection(1, GagTypeTwoPath, GagPadlockTwoPath, (lock2 != Padlocks.None),
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.GagSlots[1].GagType], (lock2 == Padlocks.None ? _gagManager.PadlockPrevs[1] : lock2));

            // Gag Label 3
            _uiSharedService.BigText("Outer Gag:");
            // Gag Timer 3
            if (lock3 == Padlocks.FiveMinutesPadlock || lock3 == Padlocks.TimerPasswordPadlock || lock3 == Padlocks.OwnerTimerPadlock)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((bigTextSize.Y - ImGui.GetTextLineHeight()) / 2) + 5f);
                UiSharedService.ColorText(GetRemainingTimeString(_playerManager.AppearanceData.GagSlots[2].Timer,
                    _playerManager.AppearanceData.GagSlots[2].Assigner), ImGuiColors.ParsedGold);
            }
            // Selection 3
            DrawGagAndLockSection(2, GagTypeThreePath, GagPadlockThreePath, (lock3 != Padlocks.None),
                GagList.AliasToGagTypeMap[_playerManager.AppearanceData.GagSlots[2].GagType], (lock3 == Padlocks.None ? _gagManager.PadlockPrevs[2] : lock3));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error: {ex}");
        }
    }

    private static readonly HashSet<Padlocks> TwoRowLocks = new HashSet<Padlocks>
    {
        Padlocks.None, Padlocks.MetalPadlock, Padlocks.FiveMinutesPadlock, Padlocks.OwnerPadlock, Padlocks.OwnerTimerPadlock
    };


    private void DrawGagAndLockSection(int slotNumber, string gagTypePath, string lockTypePath, bool currentlyLocked, GagList.GagType gagType, Padlocks padlockType)
    {
        using (var gagAndLockOuterGroup = ImRaii.Group())
        {
            // Display gag image
            var gagOneTexture = _uiSharedService.GetImageFromDirectoryFile(gagTypePath);
            if (!(gagOneTexture is { } wrapGag))
            {
                Logger.LogWarning("Failed to render image!");
            }
            else
            {
                ImGui.Image(wrapGag.ImGuiHandle, new Vector2(80, 80));
            }
            ImGui.SameLine();

            // Display combo for gag type and lock
            var GroupCursorY = ImGui.GetCursorPosY();
            using (var gagAndLockInnerGroup = ImRaii.Group())
            {
                if (TwoRowLocks.Contains(padlockType)) ImGui.SetCursorPosY(GroupCursorY + ImGui.GetFrameHeight() / 2);

                using (ImRaii.Disabled(currentlyLocked))
                {
                    _uiSharedService.DrawComboSearchable($"Gag Type {slotNumber}", 250, ref Filters[slotNumber],
                    Enum.GetValues<GagList.GagType>(), (gag) => gag.GetGagAlias(), false,
                    (i) =>
                    {
                        // locate the GagData that matches the alias of i
                        var SelectedGag = GagList.AliasToGagTypeMap[i.GetGagAlias()];
                        Mediator.Publish(new GagTypeChanged(SelectedGag, (GagLayer)slotNumber));
                        Mediator.Publish(new UpdateGlamourGagsMessage(SelectedGag == GagList.GagType.None ? NewState.Disabled : NewState.Enabled,
                            (GagLayer)slotNumber, SelectedGag, "SelfApplied"));
                        // Update gag type based on selection
                    }, gagType);
                }

                // draw the padlock dropdown
                using (ImRaii.Disabled(currentlyLocked || gagType == GagList.GagType.None))
                {
                    _uiSharedService.DrawCombo($"Lock Type {slotNumber}", (248 - _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Lock).X),
                        Enum.GetValues<Padlocks>().Cast<Padlocks>().Where(p => p != Padlocks.OwnerPadlock && p != Padlocks.OwnerTimerPadlock).ToArray(),
                        (padlock) => padlock.ToString(),
                    (i) =>
                    {
                        _gagManager.PadlockPrevs[slotNumber] = i;
                    }, padlockType, false);
                }
                ImGui.SameLine(0, 2);

                using (var padlockDisabled = ImRaii.Disabled(padlockType == Padlocks.None))
                {
                    // draw the lock button
                    if (_uiSharedService.IconButton(currentlyLocked ? FontAwesomeIcon.Unlock : FontAwesomeIcon.Lock, null, slotNumber.ToString()))
                    {
                        if (_gagManager.ValidatePassword(slotNumber, currentlyLocked))
                        {
                            Mediator.Publish(new GagLockToggle(new PadlockData((GagLayer)slotNumber, _gagManager.PadlockPrevs[slotNumber], _gagManager.Passwords[slotNumber], 
                                UiSharedService.GetEndTimeUTC(_gagManager.Timers[slotNumber]), "SelfApplied"), currentlyLocked, true));
                        }
                        // reset the password and timer
                        _gagManager.Passwords[slotNumber] = string.Empty;
                        _gagManager.Timers[slotNumber] = string.Empty;
                    }
                    UiSharedService.AttachToolTip(currentlyLocked ? "Attempt Unlocking " : "Lock " +  "this gag.");
                }
                // display associated password field for padlock type.
                _gagManager.DisplayPasswordField(slotNumber, currentlyLocked);
            }
            // Display lock image if we should
            if (padlockType != Padlocks.None && currentlyLocked)
            {
                ImGui.SameLine();
                using (var lockGroup = ImRaii.Group())
                {
                    var lockTexture = _uiSharedService.GetImageFromDirectoryFile(lockTypePath);
                    if (!(lockTexture is { } wrapLock))
                    {
                        Logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        ImGui.Image(wrapLock.ImGuiHandle, new Vector2(80, 80));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text("Locked with a " + padlockType.ToString());
                            ImGui.EndTooltip();
                        }
                    }
                }
            }
        } 
    }

    private string GetRemainingTimeString(DateTimeOffset endTime, string userWhoSetLock)
    {
        TimeSpan remainingTime = (endTime - DateTimeOffset.UtcNow);
        string remainingTimeStr = $"{remainingTime.Days}d {remainingTime.Hours}h {remainingTime.Minutes}m {remainingTime.Seconds}s";
        return (userWhoSetLock != "SelfApplied") ? "Locked by" + userWhoSetLock + "for" + remainingTimeStr : "Self-locked for " + remainingTimeStr;
    }
}
