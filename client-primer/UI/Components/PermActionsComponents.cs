using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Services;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Enums;
using GagspeakAPI.Data.Interfaces;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.Components;

/// <summary>
/// A helper class to make the permission actions less repetitive and obnoxious.
/// </summary>
public class PermActionsComponents
{
    private readonly ILogger<PermActionsComponents> _logger;
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiShared;
    private readonly MoodlesService _moodlesService;

    private int _selectedGagLayer = 0;
    private string _password = "";
    private string _timer = "";
    private Dictionary<string, string> SearchStrings = new();
    private Dictionary<string, object> CachedPermActionSelections = new();
    
    public PermActionsComponents(ILogger<PermActionsComponents> logger,
        ApiController apiController, UiSharedService uiShared, 
        MoodlesService moodlesService)
    {
        _logger = logger;
        _apiController = apiController;
        _uiShared = uiShared;
        _moodlesService = moodlesService;
    }

    public int GagLayer { get => _selectedGagLayer; set => _selectedGagLayer = value; }
    public string Password { get => _password; set => _password = value;}
    public string Timer { get => _timer; set => _timer = value; }

    // helper function to set the selected action of a combo item
    public T? GetSelectedItem<T>(string identifier, string pairAliasUID)
    {
        string comboName = ComboName(identifier, pairAliasUID);
        if (CachedPermActionSelections.TryGetValue(comboName, out var selectedItem))
        {
            return selectedItem is T item ? item : default;
        }
        return default;
    }

    public void DrawGagLayerSelection(float comboWidth, string pairAliasUID)
    {
        using (var gagLayerGroup = ImRaii.Group())
        {
            ImGui.SetNextItemWidth(comboWidth);
            ImGui.Combo("##GagLayerSelection", ref _selectedGagLayer, new string[] { "Layer 1", "Layer 2", "Layer 3" }, 3);
            UiSharedService.AttachToolTip("Select the layer to apply a Gag to.");
        }
    }

    public string ComboName(string identifier, string pairAliasUID) => "##" + identifier + "-" + pairAliasUID;

    public void DrawGenericComboButton<T>(
        string pairUID, 
        string identifier, 
        string buttonLabel, 
        float totalWidth,
        IEnumerable<T> comboItems, 
        Func<T, string> itemToName, 
        bool isSearchable = false, 
        bool buttonDisabled = false, 
        bool isIconButton = false, 
        T? initialSelectedItem = default, 
        FontAwesomeIcon icon = FontAwesomeIcon.None, 
        ImGuiComboFlags flags = ImGuiComboFlags.None, 
        Action<T?>? onSelected = null, 
        Action<T?>? onButton = null,
        string comboTT = "",
        string buttonTT = "")
    {
        // formulate our conjoined combo name identifier.
        string comboName = ComboName(identifier,pairUID);
        // return blank combo if empty.
        if (!comboItems.Any())
        {
            ImGui.SetNextItemWidth(totalWidth);
            ImGui.BeginCombo(comboName, "No Items...", flags);
            ImGui.EndCombo();
            return;
        }

        // if the selected item for this list is not in the dictionary,
        // set it to the first item in the list or the initial selected item.
        if (!CachedPermActionSelections.TryGetValue(comboName, out var selectedItem) && selectedItem == null)
        {
            if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
            {
                selectedItem = initialSelectedItem;
                CachedPermActionSelections[comboName] = selectedItem!;
                if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
                    onSelected?.Invoke(initialSelectedItem);
            }
            else
            {
                selectedItem = comboItems.First();
                CachedPermActionSelections[comboName] = selectedItem!;
            }
        }

        float comboWidth = isIconButton
            ? totalWidth - _uiShared.GetIconTextButtonSize(icon, buttonLabel) - ImGui.GetStyle().ItemInnerSpacing.X
            : totalWidth - ImGuiHelpers.GetButtonSize(buttonLabel).X - ImGui.GetStyle().ItemInnerSpacing.X;
        ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.BeginCombo(comboName, selectedItem != null ? itemToName((T)selectedItem) : "Nothing Selected..", flags))
        {
            IEnumerable<T> itemsToDisplay = comboItems;

            if (isSearchable)
            {
                if (!SearchStrings.ContainsKey(comboName))
                    SearchStrings[comboName] = string.Empty;

                string searchText = SearchStrings[comboName].ToLowerInvariant();

                ImGui.SetNextItemWidth(comboWidth);
                if (ImGui.InputTextWithHint("##filter", "Filter...", ref searchText, 255))
                    SearchStrings[comboName] = searchText;

                if (!string.IsNullOrEmpty(searchText))
                    itemsToDisplay = comboItems.Where(item => itemToName(item).ToLowerInvariant().Contains(searchText));
            }

            // Display filtered content.
            foreach (var item in itemsToDisplay)
            {
                bool isSelected = EqualityComparer<T>.Default.Equals(item, (T?)selectedItem);
                if (ImGui.Selectable(itemToName(item), isSelected))
                {
                    CachedPermActionSelections[comboName] = item!;
                    onSelected?.Invoke(item!); // invoke the selected action.
                }
            }
            ImGui.EndCombo();
        }
        if(comboTT != string.Empty) UiSharedService.AttachToolTip(comboTT);

        // Check if the item was right-clicked. If so, reset to default value.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            selectedItem = comboItems.First();
            CachedPermActionSelections[comboName] = selectedItem!;
            onSelected?.Invoke((T)selectedItem!); // invoke the selected action on clear.
        }

        ImUtf8.SameLineInner();
        if (isIconButton)
        {
            if (_uiShared.IconTextButton(icon, buttonLabel, null, false, buttonDisabled, (comboName + identifier + buttonLabel)))
            {
                onButton?.Invoke((T)selectedItem!); // invoke the button action.
            }
            if(buttonTT != string.Empty) UiSharedService.AttachToolTip(buttonTT);
        }
        else
        {
            if (ImGuiUtil.DrawDisabledButton(buttonLabel, new Vector2(), string.Empty, buttonDisabled))
            {
                onButton?.Invoke((T)selectedItem!); // invoke the button action.
            }
            if (buttonTT != string.Empty) UiSharedService.AttachToolTip(buttonTT);
        }
    }

    public bool PadlockVerifyLock<T>(T item, Padlocks selectedLock, bool allowExtended, bool allowOwner) where T : IPadlockable
    {

        var result = false;
        switch (selectedLock)
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                _timer = "5m";
                return true;
            case Padlocks.CombinationPadlock:
                result = _uiShared.ValidateCombination(Password);
                if (!result) _logger.LogWarning("Invalid combination entered: {Password}", Password);
                return result;
            case Padlocks.PasswordPadlock:
                result = _uiShared.ValidatePassword(Password);
                if (!result) _logger.LogWarning("Invalid password entered: {Password}", Password);
                return result;
            case Padlocks.TimerPasswordPadlock:
                if (_uiShared.TryParseTimeSpan(Timer, out var test))
                {
                    if (test > TimeSpan.FromHours(1) && !allowExtended)
                    {
                        _logger.LogWarning("Attempted to lock for more than 1 hour without permission.");
                        return false;
                    }
                    result = _uiShared.ValidatePassword(Password) && test > TimeSpan.Zero;
                }
                if (!result) _logger.LogWarning("Invalid password or time entered: {Password} {Timer}", Password, Timer);
                return result;
            case Padlocks.OwnerPadlock:
                return allowOwner;
            case Padlocks.OwnerTimerPadlock:
                var validTime = _uiShared.TryParseTimeSpan(Timer, out var test2);
                if (!validTime)
                {
                    _logger.LogWarning("Invalid time entered: {Timer}", Timer);
                    return false;
                }
                // Check if the TimeSpan is longer than one hour and extended locks are not allowed
                if (test2 > TimeSpan.FromHours(1) && !allowExtended)
                {
                    _logger.LogWarning("Attempted to lock for more than 1 hour without permission.");
                    return false;
                }
                // return base case.
                return validTime && allowOwner;
        }
        return false;
    }

    public void ResetInputs()
    {
        _password = string.Empty;
        _timer = string.Empty;
    }

    public bool PadlockVerifyUnlock<T>(T data, Padlocks selectedPadlock, bool allowOwnerLocks) where T : IPadlockable
    {
        switch (selectedPadlock)
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                return true;
            case Padlocks.CombinationPadlock:
                var resCombo = _uiShared.ValidateCombination(Password) && Password == data.Password;
                return resCombo;
            case Padlocks.PasswordPadlock:
            case Padlocks.TimerPasswordPadlock:
                var resPass = _uiShared.ValidatePassword(Password) && Password == data.Password;
                return resPass;
            case Padlocks.OwnerPadlock:
            case Padlocks.OwnerTimerPadlock:
                var resOwner = allowOwnerLocks && ApiController.UID == data.Assigner;
                return resOwner;
        }
        return false;
    }

    public bool ExpandLockHeightCheck(Padlocks type)
        => type is Padlocks.CombinationPadlock or Padlocks.PasswordPadlock or Padlocks.TimerPasswordPadlock or Padlocks.OwnerTimerPadlock;


    public void DisplayPadlockFields(Padlocks selectedPadlock, bool unlocking = false)
    {
        float width = ImGui.GetContentRegionAvail().X;
        switch (selectedPadlock)
        {
            case Padlocks.CombinationPadlock:
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##Combination_Input", "Enter 4 digit combination...", ref _password, 4);
                break;
            case Padlocks.PasswordPadlock:
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##Password_Input", "Enter password...", ref _password, 20);
                break;
            case Padlocks.TimerPasswordPadlock:
                if(unlocking)
                {
                    ImGui.SetNextItemWidth(width);
                    ImGui.InputTextWithHint("##Password_Input", "Enter password...", ref _password, 20);
                    break;
                }
                else
                {
                    ImGui.SetNextItemWidth(width * (2 / 3f));
                    ImGui.InputTextWithHint("##Password_Input", "Enter password...", ref _password, 20);
                    ImUtf8.SameLineInner();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.InputTextWithHint("##Timer_Input", "Ex: 0h2m7s", ref _timer, 12); ;
                }
                break;
            case Padlocks.OwnerTimerPadlock:
                ImGui.SetNextItemWidth(width);
                ImGui.InputTextWithHint("##Timer_Input", "Ex: 0h2m7s", ref _timer, 12); 
                break;
        }
    }
}

