using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.UI;
using GagspeakAPI.Data.Enum;
using ImGuiNET;

namespace GagSpeak.PlayerData.Handlers;
/// <summary> Handles how the information stored in padlock spaces are contained.. </summary>
public class PadlockHandler
{
    private readonly ILogger<PadlockHandler> _logger;
    private readonly UiSharedService _uiSharedService;
    private readonly PlayerCharacterManager _playerCharacterManager;
    private readonly ClientConfigurationManager _clientConfigs;

    public PadlockHandler(ILogger<PadlockHandler> logger,
        UiSharedService uiSharedService, PlayerCharacterManager playerCharacterManager,
        ClientConfigurationManager clientConfigs)
    {
        _logger = logger;
        _uiSharedService = uiSharedService;
        _playerCharacterManager = playerCharacterManager;
        _clientConfigs = clientConfigs;
    }

    // The final slot is used for restraint sets.
    public List<Padlocks> PadlockPrevs = new List<Padlocks>() { Padlocks.None, Padlocks.None, Padlocks.None, Padlocks.None }; // to store prior to hitting LOCK
    public string[] Passwords = new string[4] { "", "", "", "" }; // when they enter password prior to locking. 
    public string[] Timers = new string[4] { "", "", "", "" }; // when they enter a timer prior to locking.

    public bool DisplayPasswordField(int slot, bool isLocked)
    {
        switch (PadlockPrevs[slot])
        {
            case Padlocks.CombinationPadlock:
                Passwords[slot] = DisplayInputField($"##Combination_Input{slot}", "Enter 4 digit combination...", Passwords[slot], 4);
                return true;
            case Padlocks.PasswordPadlock:
                Passwords[slot] = DisplayInputField($"##Password_Input{slot}", "Enter password", Passwords[slot], 20);
                return true;
            case Padlocks.TimerPasswordPadlock:
                if (isLocked)
                {
                    Passwords[slot] = DisplayInputField($"##Password_Input{slot}", "Enter password", Passwords[slot], 20);
                }
                else
                {
                    Passwords[slot] = DisplayInputField($"##Password_Input{slot}", "Enter password", Passwords[slot], 20, 2 / 3f);
                    ImGui.SameLine(0, 3);
                    Timers[slot] = DisplayInputField($"##Timer_Input{slot}", "Ex: 0h2m7s", Timers[slot], 12, .325f);
                }
                return true;
            case Padlocks.OwnerTimerPadlock:
                Timers[slot] = DisplayInputField($"##Timer_Input{slot}", "Ex: 0h2m7s", Timers[slot], 12);
                return true;
            default:
                return false;
        }
    }

    private string DisplayInputField(string id, string hint, string value, uint maxLength, float widthRatio = 1f)
    {
        // set the result to the value
        string result = value;
        // set the width of the input field
        ImGui.SetNextItemWidth(250 * widthRatio);
        // display the input field
        if (ImGui.InputTextWithHint(id, hint, ref result, maxLength, ImGuiInputTextFlags.None))
            return result;
        return value;
    }

    public bool PasswordValidated(int slot, bool currentlyLocked)
    {
        _logger.LogDebug($"Validating Password for Slot {slot} which has padlock type {PadlockPrevs[slot]}");
        switch (PadlockPrevs[slot])
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                Timers[slot] = "0h5m0s";
                return true; 
            case Padlocks.CombinationPadlock:
                {
                    if (currentlyLocked)
                    {
                        return Passwords[slot] == _playerCharacterManager.AppearanceData?.GagSlots[slot].Password;
                    }
                    else
                    {
                        return _uiSharedService.ValidateCombination(Passwords[slot]);
                    }
                }
            case Padlocks.PasswordPadlock:
                {
                    if (currentlyLocked)
                    {
                        return Passwords[slot] == _playerCharacterManager.AppearanceData?.GagSlots[slot].Password;
                    }
                    else
                    {
                        return _uiSharedService.ValidatePassword(Passwords[slot]);
                    }
                }
            case Padlocks.TimerPasswordPadlock:
                {
                    if (currentlyLocked)
                    {
                        return Passwords[slot] == _playerCharacterManager.AppearanceData?.GagSlots[slot].Password;
                    }
                    else
                    {
                        return _uiSharedService.ValidatePassword(Passwords[slot]) && _uiSharedService.TryParseTimeSpan(Timers[slot], out TimeSpan test);
                    }
                }
        }
        return false;
    }

    public bool RestraintPasswordValidate(int setIdx, bool currentlyLocked)
    {
        var set = _clientConfigs.GetRestraintSet(setIdx);
        _logger.LogDebug($"Validating Password restraintSet {set.Name} which has padlock type preview {PadlockPrevs[3]}");
        switch (PadlockPrevs[3])
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                Timers[3] = "0h5m0s";
                return true;
            case Padlocks.CombinationPadlock:
                {
                    if (currentlyLocked)
                    {
                        return Passwords[3] == set.LockPassword;
                    }
                    else
                    {
                        return _uiSharedService.ValidateCombination(Passwords[3]);
                    }
                }
            case Padlocks.PasswordPadlock:
                {
                    if (currentlyLocked)
                    {
                        return Passwords[3] == set.LockPassword;
                    }
                    else
                    {
                        return _uiSharedService.ValidatePassword(Passwords[3]);
                    }
                }
            case Padlocks.TimerPasswordPadlock:
                {
                    if (currentlyLocked)
                    {
                        return Passwords[3] == set.LockPassword;
                    }
                    else
                    {
                        return _uiSharedService.ValidatePassword(Passwords[3]) && _uiSharedService.TryParseTimeSpan(Timers[3], out TimeSpan test);
                    }
                }
        }
        return false;
    }

}
