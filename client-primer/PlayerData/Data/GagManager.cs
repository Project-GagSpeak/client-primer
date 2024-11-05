using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.MufflerCore.Handler;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Interfaces;
using GagspeakAPI.Extensions;
using ImGuiNET;
using OtterGui.Text;
using System.Text.RegularExpressions;

namespace GagSpeak.PlayerData.Data;

public partial class GagManager : DisposableMediatorSubscriberBase
{
    private readonly PlayerCharacterData _characterManager;
    private readonly GagDataHandler _gagDataHandler;
    private readonly Ipa_EN_FR_JP_SP_Handler _IPAParser;

    public List<GagData> _activeGags;

    // The items currently present in the active Gags UI Panel.
    private static string[] GagSearchFilters = new string[3] { "", "", "" };
    public static GagType[] ComboGags { get; private set; } = new GagType[3] { GagType.None, GagType.None, GagType.None };
    public static Padlocks[] ComboPadlocks { get; set; } = new Padlocks[4] { Padlocks.None, Padlocks.None, Padlocks.None, Padlocks.None };

    public GagManager(ILogger<GagManager> logger, GagspeakMediator mediator,
        PlayerCharacterData characterManager, GagDataHandler gagDataHandler,
        Ipa_EN_FR_JP_SP_Handler IPAParser) : base(logger, mediator)
    {
        _characterManager = characterManager;
        _gagDataHandler = gagDataHandler;
        _IPAParser = IPAParser;

        // check for any locked gags on delayed framework to see if their timers expired.
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => CheckForExpiredTimers());
    }

    public bool AnyGagActive => _activeGags.Any(gag => gag.Name != "None");
    public bool AnyGagLocked => _characterManager.AppearanceData?.GagSlots.Any(x => x.Padlock != "None") ?? false;
    // This is a preview of the padlock type that will be applied to the restraint set.
    public static Padlocks[] ActiveSlotPadlocks { get; set; } = new Padlocks[4] { Padlocks.None, Padlocks.None, Padlocks.None, Padlocks.None };
    public static string[] ActiveSlotPasswords { get; set; } = new string[4] { "", "", "", "" };
    public static string[] ActiveSlotTimers { get; set; } = new string[4] { "", "", "", "" };

    /// <summary> ONLY UPDATES THE LOGIC CONTROLLING GARBLE SPEECH, NOT APPEARNACE DATA </summary>
    public void UpdateGagGarblerLogic()
    {
        Logger.LogTrace("GagTypeOne: " + _characterManager.AppearanceData?.GagSlots[0].GagType ?? "UNK"
            + " || GagTypeTwo: " + _characterManager.AppearanceData?.GagSlots[1].GagType ?? "UNK"
            + " || GagTypeThree: " + _characterManager.AppearanceData?.GagSlots[2].GagType ?? "UNK", LoggerType.GagManagement);

        // compile the strings into a list of strings, then locate the names in the handler storage that match it.
        _activeGags = new List<string>
        {
            _characterManager.AppearanceData?.GagSlots[0].GagType ?? GagType.None.GagName(),
            _characterManager.AppearanceData?.GagSlots[1].GagType ?? GagType.None.GagName(),
            _characterManager.AppearanceData?.GagSlots[2].GagType ?? GagType.None.GagName(),
        }
        .Where(gagType => _gagDataHandler._gagTypes.Any(gag => gag.Name == gagType))
        .Select(gagType => _gagDataHandler._gagTypes.First(gag => gag.Name == gagType))
        .ToList();
    }

    /// <summary>
    /// Handles the GagTypeChanged event, updating the active gags list accordingly.
    /// </summary>
    public void OnGagTypeChanged(GagLayer Layer, GagType NewGagType, bool publish)
    {
        if (_characterManager.CoreDataNull) return;

        Logger.LogTrace("GagTypeChanged event received.", LoggerType.GagManagement);
        bool IsApplying = (NewGagType is not GagType.None);

        // Update the corresponding slot in CharaAppearanceData based on the GagLayer
        if (Layer is GagLayer.UnderLayer)
        {
            _characterManager.AppearanceData!.GagSlots[0].GagType = NewGagType.GagName();
            if (publish) Mediator.Publish(new PlayerCharAppearanceChanged(IsApplying ? DataUpdateKind.AppearanceGagAppliedLayerOne : DataUpdateKind.AppearanceGagRemovedLayerOne));
        }
        if (Layer is GagLayer.MiddleLayer)
        {
            _characterManager.AppearanceData!.GagSlots[1].GagType = NewGagType.GagName();
            if (publish) Mediator.Publish(new PlayerCharAppearanceChanged(IsApplying ? DataUpdateKind.AppearanceGagAppliedLayerTwo : DataUpdateKind.AppearanceGagRemovedLayerTwo));
        }
        if (Layer is GagLayer.TopLayer)
        {
            _characterManager.AppearanceData!.GagSlots[2].GagType = NewGagType.GagName();
            if (publish) Mediator.Publish(new PlayerCharAppearanceChanged(IsApplying ? DataUpdateKind.AppearanceGagAppliedLayerThree : DataUpdateKind.AppearanceGagRemovedLayerThree));
        }

        UpdateGagLockComboSelections();
        UpdateGagGarblerLogic();
    }

    public void OnGagLockChanged(PadlockData padlockInfo, NewState gagLockNewState, bool publish, bool SelfApplied = false)
    {
        if (_characterManager.CoreDataNull) return;

        int layerIndex = (int)padlockInfo.Layer;

        if (gagLockNewState is NewState.Unlocked)
        {
            DisableLock(layerIndex);
            if (publish)
                PublishAppearanceChange(layerIndex, isUnlocked: true);
        }
        else
        {
            UpdateGagSlot(layerIndex, padlockInfo);
            if (publish)
                PublishAppearanceChange(layerIndex, isUnlocked: false);
        }
    }

    public void SafewordWasUsed()
    {
        if (_characterManager.AppearanceData == null)
        {
            Logger.LogWarning("AppearanceData is null, cannot apply safeword.");
            return;
        }

        // Disable all locks and clear the gags
        for (int i = 0; i < 3; i++)
        {
            DisableLock(i);
            _characterManager.AppearanceData.GagSlots[i].GagType = GagType.None.GagName();
        }

        // Update the list of active gags and notify mediator
        UpdateGagGarblerLogic();
        Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.Safeword));
    }

    /// <summary>
    /// Does not push changes to mediator, only use this when in a safeword operation.
    /// </summary>
    public void DisableLock(int layerIndex)
    {
        var gagSlot = _characterManager.AppearanceData!.GagSlots[layerIndex];
        gagSlot.Padlock = Padlocks.None.ToName();
        gagSlot.Password = string.Empty;
        gagSlot.Timer = DateTimeOffset.MinValue;
        gagSlot.Assigner = string.Empty;
        // update the shown lock type in the UI to none.
        ActiveSlotPadlocks[layerIndex] = Padlocks.None;
        // update the lock combos.
        UpdateGagLockComboSelections();
    }

    private void UpdateGagSlot(int layerIndex, PadlockData padlockInfo)
    {
        Logger.LogDebug("Updating Gag Slotw with Padlock Data: " + padlockInfo.PadlockType.ToName() +
            " || " + padlockInfo.Password + " || " + padlockInfo.Timer + " || " + padlockInfo.Assigner, LoggerType.PadlockManagement);
        var gagSlot = _characterManager.AppearanceData!.GagSlots[layerIndex];
        gagSlot.Padlock = padlockInfo.PadlockType.ToName();
        gagSlot.Password = padlockInfo.Password;
        gagSlot.Timer = padlockInfo.Timer;
        gagSlot.Assigner = padlockInfo.Assigner;
        // update the lock combos.
        UpdateGagLockComboSelections();
    }

    public void UpdateGagLockComboSelections()
    {
        if (_characterManager.AppearanceData == null)
        {
            Logger.LogWarning("AppearanceData is null, cannot update combo selections.");
            return;
        }

        ComboGags = new GagType[3]
        {
            _characterManager.AppearanceData.GagSlots[0].GagType.ToGagType(),
            _characterManager.AppearanceData.GagSlots[1].GagType.ToGagType(),
            _characterManager.AppearanceData.GagSlots[2].GagType.ToGagType()
        };
        ComboPadlocks[0] = _characterManager.AppearanceData.GagSlots[0].Padlock.ToPadlock();
        ComboPadlocks[1] = _characterManager.AppearanceData.GagSlots[1].Padlock.ToPadlock();
        ComboPadlocks[2] = _characterManager.AppearanceData.GagSlots[2].Padlock.ToPadlock();
        Logger.LogDebug("Dropdown Gags Now: " + string.Join(" || ", _characterManager.AppearanceData!.GagSlots.Select((g, i) => $"Gag {i}: {g.GagType}")), LoggerType.GagManagement);
        Logger.LogDebug("Dropdown ActiveSlotPadlocks Now: " + string.Join(" || ", ActiveSlotPadlocks.Select((p, i) => $"Lock {i}: {p}")), LoggerType.PadlockManagement);
        Logger.LogDebug("Dropdown Appearance Padlocks Now: " + string.Join(" || ", _characterManager.AppearanceData.GagSlots.Select((p, i) => $"Lock {i}: {p.Padlock}")), LoggerType.PadlockManagement);

    }

    private void PublishAppearanceChange(int layerIndex, bool isUnlocked)
    {
        DataUpdateKind updateKind = layerIndex switch
        {
            0 => isUnlocked ? DataUpdateKind.AppearanceGagUnlockedLayerOne : DataUpdateKind.AppearanceGagLockedLayerOne,
            1 => isUnlocked ? DataUpdateKind.AppearanceGagUnlockedLayerTwo : DataUpdateKind.AppearanceGagLockedLayerTwo,
            2 => isUnlocked ? DataUpdateKind.AppearanceGagUnlockedLayerThree : DataUpdateKind.AppearanceGagLockedLayerThree,
            _ => throw new ArgumentOutOfRangeException(nameof(layerIndex), "Invalid layer index")
        };

        Mediator.Publish(new PlayerCharAppearanceChanged(updateKind));
    }

    public bool PadlockVerifyLock<T>(T item, GagLayer layer, bool extended, bool owner, bool devotional, TimeSpan maxTime) where T : IPadlockable
    {

        var result = false;
        switch (ActiveSlotPadlocks[(int)layer])
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
                return true;
            case Padlocks.FiveMinutesPadlock:
                ActiveSlotTimers[(int)layer] = "5m";
                return true;
            case Padlocks.CombinationPadlock:
                result = ValidateCombination(ActiveSlotPasswords[(int)layer]);
                if (!result) Logger.LogWarning("Invalid combination entered: {Password}", ActiveSlotPasswords[(int)layer]);
                return result;
            case Padlocks.PasswordPadlock:
                result = ValidatePassword(ActiveSlotPasswords[(int)layer]);
                if (!result) Logger.LogWarning("Invalid password entered: {Password}", ActiveSlotPasswords[(int)layer]);
                return result;
            case Padlocks.MimicPadlock:
                if (!TryParseTimeSpan(ActiveSlotTimers[(int)layer], out var mimicTime))
                {
                    Logger.LogWarning("Invalid time entered: {Timer}", ActiveSlotTimers[(int)layer]);
                    return false;
                }
                return true;
            case Padlocks.TimerPasswordPadlock:
                if (TryParseTimeSpan(ActiveSlotTimers[(int)layer], out var pwdTimer))
                {
                    if ((pwdTimer > TimeSpan.FromHours(1) && !extended) || pwdTimer > maxTime)
                    {
                        Logger.LogWarning("Attempted to lock for more than 1 hour without permission.");
                        return false;
                    }
                    result = ValidatePassword(ActiveSlotPasswords[(int)layer]) && pwdTimer > TimeSpan.Zero;
                }
                if (!result) Logger.LogWarning("Invalid password or time entered: {Password} {Timer}", ActiveSlotPasswords[(int)layer], ActiveSlotTimers[(int)layer]);
                return result;
            case Padlocks.OwnerPadlock:
                return owner;
            case Padlocks.OwnerTimerPadlock:
                if (!TryParseTimeSpan(ActiveSlotTimers[(int)layer], out var ownerTime))
                {
                    Logger.LogWarning("Invalid time entered: {Timer}", ActiveSlotTimers[(int)layer]);
                    return false;
                }
                if ((ownerTime > TimeSpan.FromHours(1) && !extended) || ownerTime > maxTime)
                {
                    Logger.LogWarning("Attempted to lock for more than 1 hour without permission.");
                    return false;
                }
                return owner;
            case Padlocks.DevotionalPadlock:
                return devotional;
            case Padlocks.DevotionalTimerPadlock:
                if (!TryParseTimeSpan(ActiveSlotTimers[(int)layer], out var devotionalTime))
                {
                    Logger.LogWarning("Invalid time entered: {Timer}", ActiveSlotTimers[(int)layer]);
                    return false;
                }
                // Check if the TimeSpan is longer than one hour and extended locks are not allowed
                if ((devotionalTime > TimeSpan.FromHours(1) && !extended) || devotionalTime > maxTime)
                {
                    Logger.LogWarning("Attempted to lock for longer than you were allowed access for!");
                    return false;
                }
                // return base case.
                return devotional;
        }
        return false;
    }

    public void ResetInputs()
    {
        ActiveSlotTimers = new string[4] { "", "", "", "" };
        ActiveSlotPasswords = new string[4] { "", "", "", "" };
    }

    public void DisplayPadlockFields(Padlocks padlock, int layer, bool unlocking = false, float totalWidth = 250)
    {
        float width = totalWidth;
        switch (padlock)
        {
            case Padlocks.CombinationPadlock:
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##Combination_Input", "Enter 4 digit combination...", ref ActiveSlotPasswords[layer], 4);
                break;
            case Padlocks.PasswordPadlock:
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##Password_Input", "Enter password...", ref ActiveSlotPasswords[layer], 20);
                break;
            case Padlocks.TimerPasswordPadlock:
                if (unlocking)
                {
                    ImGui.SetNextItemWidth(width);
                    ImGui.InputTextWithHint("##Password_Input", "Enter password...", ref ActiveSlotPasswords[layer], 20);
                    break;
                }
                else
                {
                    ImGui.SetNextItemWidth(width * .65f);
                    ImGui.InputTextWithHint("##Password_Input", "Enter password...", ref ActiveSlotPasswords[layer], 20);
                    ImUtf8.SameLineInner();
                    ImGui.SetNextItemWidth(width * .35f - ImGui.GetStyle().ItemInnerSpacing.X);
                    ImGui.InputTextWithHint("##Timer_Input", "Ex: 0h2m7s", ref ActiveSlotTimers[layer], 12); ;
                }
                break;
            case Padlocks.OwnerTimerPadlock:
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##Timer_Input", "Ex: 0h2m7s", ref ActiveSlotTimers[layer], 12);
                break;
        }
    }

    public bool PasswordValidated(Padlocks padlock, int slot, bool currentlyLocked)
    {
        Logger.LogDebug($"Validating Password for GagSlot {slot} which has padlock type "+padlock.ToName(), LoggerType.PadlockManagement);
        switch (padlock)
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                ActiveSlotTimers[slot] = "0h5m0s";
                return true;
            case Padlocks.CombinationPadlock:
                if (currentlyLocked)
                    return ActiveSlotPasswords[slot] == _characterManager.AppearanceData?.GagSlots[slot].Password;
                else
                    return ValidateCombination(ActiveSlotPasswords[slot]);
            case Padlocks.PasswordPadlock:
                if (currentlyLocked)
                    return ActiveSlotPasswords[slot] == _characterManager.AppearanceData?.GagSlots[slot].Password;
                else
                    return ValidatePassword(ActiveSlotPasswords[slot]);
            case Padlocks.TimerPasswordPadlock:
                if (currentlyLocked)
                    return ActiveSlotPasswords[slot] == _characterManager.AppearanceData?.GagSlots[slot].Password;
                else
                    return ValidatePassword(ActiveSlotPasswords[slot]) && TryParseTimeSpan(ActiveSlotTimers[slot], out TimeSpan test);
        }
        return false;
    }

    public bool RestraintPasswordValidate(RestraintSet set, bool currentlyLocked)
    {
        Logger.LogDebug($"Validating Password restraintSet {set.Name} which has padlock type preview {ActiveSlotPadlocks[3]}", LoggerType.PadlockManagement);
        switch (ActiveSlotPadlocks[3])
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                ActiveSlotTimers[3] = "5m";
                return true;
            case Padlocks.CombinationPadlock:
                if (currentlyLocked)
                {
                    Logger.LogTrace($"Checking if {ActiveSlotPasswords[3]} is equal to {set.LockPassword}", LoggerType.PadlockManagement);
                    return string.Equals(ActiveSlotPasswords[3], set.LockPassword, StringComparison.Ordinal);
                }
                else
                    return ValidateCombination(ActiveSlotPasswords[3]);
            case Padlocks.PasswordPadlock:
                if (currentlyLocked)
                {
                    Logger.LogTrace($"Checking if {ActiveSlotPasswords[3]} is equal to {set.LockPassword}", LoggerType.PadlockManagement);
                    return string.Equals(ActiveSlotPasswords[3], set.LockPassword, StringComparison.Ordinal);
                }
                else
                    return ValidatePassword(ActiveSlotPasswords[3]);
            case Padlocks.TimerPasswordPadlock:
                if (currentlyLocked)
                {
                    Logger.LogTrace($"Checking if {ActiveSlotPasswords[3]} is equal to {set.LockPassword}", LoggerType.PadlockManagement);
                    return string.Equals(ActiveSlotPasswords[3], set.LockPassword, StringComparison.Ordinal);
                }
                else
                    return ValidatePassword(ActiveSlotPasswords[3]) && TryParseTimeSpan(ActiveSlotTimers[3], out TimeSpan test);
        }
        return false;
    }

    /// <summary> Validates a password </summary>
    private bool ValidatePassword(string password)
    {
        Logger.LogDebug($"Validating Password {password}", LoggerType.PadlockManagement);
        return !string.IsNullOrWhiteSpace(password) && password.Length <= 20 && !password.Contains(" ");
    }

    /// <summary> Validates a 4 digit combination </summary>
    private bool ValidateCombination(string combination)
    {
        Logger.LogDebug($"Validating Combination {combination}", LoggerType.PadlockManagement);
        return int.TryParse(combination, out _) && combination.Length == 4;
    }

    private bool TryParseTimeSpan(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        var regex = new Regex(@"(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?");
        var match = regex.Match(input);

        if (!match.Success)
        {
            return false;
        }

        int days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        int hours = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        int minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        int seconds = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;

        result = new TimeSpan(days, hours, minutes, seconds);
        return true;
    }

    private void CheckForExpiredTimers()
    {
        // return if characterManager not valid
        if (_characterManager == null) return;

        // return if appearance data not present.
        if (_characterManager.AppearanceData == null) return;

        // return if none of our gags have padlocks.
        if (!AnyGagLocked) return;

        // If a gag does have a padlock, ensure it is a timer padlock
        for (int i = 0; i < _characterManager.AppearanceData.GagSlots.Length; i++)
        {
            var gagSlot = _characterManager.AppearanceData.GagSlots[i];
            if (GenericHelpers.TimerPadlocks.Contains(gagSlot.Padlock) && gagSlot.Timer - DateTimeOffset.UtcNow <= TimeSpan.Zero)
            {
                var padlockType = gagSlot.Padlock.ToPadlock();
                DisableLock(i);
                PublishAppearanceChange(i, isUnlocked: true);
            }
        }
    }

    // Below are special combos for the gags and padlocks:
    public void DrawGagCombo(GagLayer layer, float width, Action<GagType> onSelected)
    {
        try
        {
            // Return default if there are no items to display in the combo box.
            string comboLabel = "##ActiveGagsSelection" + layer.ToString();
            string displayText = ComboGags[(int)layer].GagName();
            
            var comboItems = Enum.GetValues<GagType>();
            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo(comboLabel, displayText))
            {
                // Search filter
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##filter", "Filter...", ref GagSearchFilters[(int)layer], 255);
                var searchText = GagSearchFilters[(int)layer].ToLowerInvariant();

                var filteredItems = string.IsNullOrEmpty(searchText)
                    ? comboItems
                    : comboItems.Where(item => item.GagName().ToLowerInvariant().Contains(searchText));

                // display filtered content.
                foreach (var item in filteredItems)
                {
                    bool isSelected = item.GagName() == ComboGags[(int)layer].GagName();
                    if (ImGui.Selectable(item.GagName(), isSelected))
                    {
                        Logger.LogTrace("Selected {item} from {comboName}", item.GagName(), comboLabel);
                        ComboGags[(int)layer] = item;
                        onSelected?.Invoke(item);
                    }
                }
                ImGui.EndCombo();
            }
            // Check if the item was right-clicked. If so, reset to default value.
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                Logger.LogTrace("Right-clicked on {comboName}. Resetting to default value.", comboLabel);
                ComboGags[(int)layer] = comboItems.First();
                onSelected?.Invoke(comboItems.First());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in DrawComboSearchable");
        }
    }

    public void DrawPadlockCombo(int idValue, float width, IEnumerable<Padlocks> comboItems, Action<Padlocks> onSelected)
    {
        try
        {
            // Return default if there are no items to display in the combo box.
            string comboLabel = "##ActivePadlocksSelection" + idValue.ToString();
            string displayText = ComboPadlocks[idValue].ToName();

            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo(comboLabel, displayText))
            {
                // display filtered content.
                foreach (var item in comboItems)
                {
                    bool isSelected = item.ToName() == ComboPadlocks[idValue].ToName();
                    if (ImGui.Selectable(item.ToName(), isSelected))
                    {
                        Logger.LogTrace("Selected {item} from {comboName}", item.ToName(), comboLabel);
                        ComboPadlocks[idValue] = item;
                        onSelected?.Invoke(item);
                    }
                }
                ImGui.EndCombo();
            }
            // Check if the item was right-clicked. If so, reset to default value.
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                Logger.LogTrace("Right-clicked on {comboName}. Resetting to default value.", comboLabel);
                ComboPadlocks[idValue] = comboItems.First();
                onSelected?.Invoke(comboItems.First());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in DrawPadlocks");
        }
    }

}
