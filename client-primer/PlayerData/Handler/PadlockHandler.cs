using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.UI;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using OtterGui.Text;

namespace GagSpeak.PlayerData.Handlers;
/// <summary> Handles how the information stored in padlock spaces are contained.. </summary>
public class PadlockHandler
{
    private readonly ILogger<PadlockHandler> _logger;
    private readonly UiSharedService _uiSharedService;
    private readonly PlayerCharacterData _playerCharacterManager;
    private readonly ClientConfigurationManager _clientConfigs;

    public PadlockHandler(ILogger<PadlockHandler> logger,
        UiSharedService uiSharedService, PlayerCharacterData playerCharacterManager,
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

    public bool DisplayPasswordField(int slot, bool isLocked, float totalWidth = 250)
    {
        switch (PadlockPrevs[slot])
        {
            case Padlocks.CombinationPadlock:
                Passwords[slot] = DisplayInputField($"##Combination_Input{slot}", "Enter 4 digit combination...", Passwords[slot], 4, 1f, totalWidth);
                return true;
            case Padlocks.PasswordPadlock:
                Passwords[slot] = DisplayInputField($"##Password_Input{slot}", "Enter password", Passwords[slot], 20, 1f, totalWidth);
                return true;
            case Padlocks.TimerPasswordPadlock:
                if (isLocked)
                {
                    Passwords[slot] = DisplayInputField($"##Password_Input{slot}", "Enter password", Passwords[slot], 20, 1f, totalWidth);
                }
                else
                {
                    Passwords[slot] = DisplayInputField($"##Password_Input{slot}", "Enter password", Passwords[slot], 20, 2 / 3f, totalWidth);
                    ImUtf8.SameLineInner();
                    float timerWidth = totalWidth - (totalWidth*2/3f) - ImGui.GetStyle().ItemInnerSpacing.X;
                    Timers[slot] = DisplayInputField($"##Timer_Input{slot}", "Ex: 0h2m7s", Timers[slot], 12, timerWidth / totalWidth, totalWidth);
                }
                return true;
            case Padlocks.OwnerTimerPadlock:
                Timers[slot] = DisplayInputField($"##Timer_Input{slot}", "Ex: 0h2m7s", Timers[slot], 12, totalWidth);
                return true;
            default:
                return false;
        }
    }

    private string DisplayInputField(string id, string hint, string value, uint maxLength, float widthRatio = 1f, float totalWidth = 250)
    {
        // set the result to the value
        string result = value;
        // set the width of the input field
        ImGui.SetNextItemWidth(totalWidth * widthRatio);
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
                        _logger.LogTrace($"Checking if {Passwords[3]} is equal to {set.LockPassword}");
                        return string.Equals(Passwords[3], set.LockPassword, StringComparison.Ordinal);
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
                        _logger.LogTrace($"Checking if {Passwords[3]} is equal to {set.LockPassword}");
                        return string.Equals(Passwords[3], set.LockPassword, StringComparison.Ordinal);
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
                        _logger.LogTrace($"Checking if {Passwords[3]} is equal to {set.LockPassword}");
                        return string.Equals(Passwords[3], set.LockPassword, StringComparison.Ordinal);
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
