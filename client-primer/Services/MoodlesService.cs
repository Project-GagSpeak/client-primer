using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.Interop.Ipc;
using GagSpeak.UI;
using GagSpeak.Utils;
using GagspeakAPI.Data.IPC;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui.Raii;
using System.Numerics;
namespace GagSpeak.Services;

// helps out with locating and parsing moodles icons and drawing custom dropdowns.
public class MoodlesService
{
    private readonly ILogger<MoodlesService> _logger;
    private readonly UiSharedService _uiShared;
    private IDataManager _dataManager;

    private readonly HashSet<uint> _popupState = [];
    private int _lastSelection = -1;
    protected int? NewSelection;
    private bool _closePopup = false;
    private static Dictionary<uint, IconInfo?> IconInfoCache = [];

    public MoodlesService(ILogger<MoodlesService> logger, 
        UiSharedService uiShared, IDataManager dataManager)
    {
        _logger = logger;
        _uiShared = uiShared;
        _dataManager = dataManager;
        SelectedPresetComboGuids = new Dictionary<string, Guid>();
    }

    public static readonly Vector2 StatusSize = new(24, 32);
    public Dictionary<string, Guid> SelectedPresetComboGuids;    // the selected combo items


    public bool DrawMoodlesPresetCombo(string comboLabel, ref int selectedIdx, List<(Guid, List<Guid>)> MoodlesPresets, 
        List<MoodlesStatusInfo> MoodlesStatuses, float width)
    {
        bool itemSelected = false;
        // Return if there are no items to display in the combo box.
        if (!MoodlesPresets.Any())
        {
            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo("##" + comboLabel, "No Presets Available..."))
            {
                ImGui.EndCombo();
            }
            return false;
        }
        // Get the currently selected item from the dictionary storing selections for each combo box.
        if (!SelectedPresetComboGuids.TryGetValue(comboLabel, out var selectedItem) || selectedItem == Guid.Empty)
        {
            selectedItem = MoodlesPresets.First().Item1;
            SelectedPresetComboGuids[comboLabel] = selectedItem!;
        }

        // If the selected item is not in the list of items being passed in, update it to the first item in the comboItems list.
        if (!MoodlesPresets.Any(item => item.Equals((Guid)selectedItem!)))
        {
            selectedItem = MoodlesPresets.First().Item1;
            SelectedPresetComboGuids[comboLabel] = selectedItem!;
        }

        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo(comboLabel, selectedItem.ToString()))
        {
            // Display filtered content.
            foreach (var item in MoodlesPresets)
            {
                bool isSelected = item.Equals((Guid)selectedItem!);
                if (ImGui.Selectable(item.Item1.ToString(), isSelected))
                {
                    SelectedPresetComboGuids[comboLabel] = item.Item1;
                    selectedIdx = MoodlesPresets.IndexOf(item);
                    itemSelected = true;
                }
                if (ImGui.IsItemHovered())
                {
                    var moodleNames = MoodlesStatuses
                        .Where(x => item.Item2.Contains(x.GUID))
                        .Select(x => x.Title)
                        .ToList();
                    ImGui.SetTooltip($"This Preset Enables the Following Moodles:\n" + string.Join(Environment.NewLine, moodleNames));
                }
            }
            ImGui.EndCombo();
        }
        return itemSelected;
    }

    #region MoodlesStatusCombo
    private string StatusSearchString = string.Empty;
    protected virtual void PostCombo(float previewWidth) { }

    protected virtual void OnClosePopup() { }
    public bool DrawMoodleStatusComboSearchable(List<MoodlesStatusInfo> statusList, string comboLabel, ref int selectedIdx, float width, float sizeScaler)
    {
        DrawComboActual(statusList, comboLabel, ref selectedIdx, width, sizeScaler);

        if (NewSelection == null) return false;

        selectedIdx = NewSelection.Value;
        NewSelection = null;
        return true;
    }

    private void DrawComboActual(List<MoodlesStatusInfo> statusList, string comboLabel, ref int selectedIdx, float width, float sizeScaler)
    {
        ImGui.SetNextItemWidth(width);
        // Button to open popup
        var pos = ImGui.GetCursorScreenPos();
        var id = ImGui.GetID(comboLabel);

        using var combo = ImRaii.Combo(comboLabel, statusList[selectedIdx].Title, ImGuiComboFlags.HeightLarge);
        PostCombo(width);

        if (combo)
        {
            _popupState.Add(id);

            DrawStatusComboBox(statusList, ref selectedIdx, width, sizeScaler);
            ClosePopup(id, comboLabel);
        }
        else if (_popupState.Remove(id))
        {
            _logger.LogTrace("Popup closed");
        }
    }

    protected void ClosePopup(uint id, string label)
    {
        if (!_closePopup) return;

        _logger.LogTrace("Cleaning up Filter Combo Cache for {Label}.", label);
        ImGui.CloseCurrentPopup();
        _popupState.Remove(id);
        OnClosePopup();
        ClearStorage(label);
        _closePopup = false;
    }

    protected void ClearStorage(string label)
    {
        _lastSelection = -1;
        StatusSearchString = string.Empty;
    }

    private void DrawStatusComboBox(List<MoodlesStatusInfo> statuses, ref int selectedIdx, float width, float sizeScaler)
    {
        var height = ImGui.GetTextLineHeightWithSpacing() * 10 - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y;
        using var _ = ImRaii.Child("ChildL", new Vector2(width, height), false, ImGuiWindowFlags.NoScrollbar);
        // Search filter
        ImGui.SetNextItemWidth(width - 5f);
        ImGui.InputTextWithHint("##filter", "Filter...", ref StatusSearchString, 100);
        var searchText = StatusSearchString.ToLowerInvariant();

        var filteredItems = string.IsNullOrEmpty(searchText)
            ? statuses : statuses.Where(item => item.Title.ToLowerInvariant().Contains(searchText));

        ImGui.SetWindowFontScale(sizeScaler);
        if (ImGui.BeginTable("StatusSelectableTable", 2)) // 2 columns for status name and icon
        {
            // Setup columns based on the format
            ImGui.TableSetupColumn("##StatusName", ImGuiTableColumnFlags.WidthFixed, width - StatusSize.X - ImGui.GetStyle().ItemSpacing.X * 2);
            ImGui.TableSetupColumn("##StatusIcon", ImGuiTableColumnFlags.WidthFixed, StatusSize.X);

            // Display filtered content.
            foreach (var item in filteredItems)
            {
                // Draw out a selectable spanning the cell.
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(width);
                bool isSelected = item == statuses[selectedIdx];

                if (ImGui.Selectable(item.Title, isSelected))
                {
                    NewSelection = statuses.IndexOf(item);
                    _closePopup = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(item.GUID.ToString());
                }

                // Move to the next column.
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();

                // Draw out the image.
                if (item.IconID != 0)
                {
                    var statusIcon = _uiShared.GetGameStatusIcon((uint)((uint)item.IconID + item.Stacks - 1));

                    if (statusIcon is { } wrap)
                    {
                        ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() - new Vector2(0, 5));
                        ImGui.Image(statusIcon.ImGuiHandle, StatusSize);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(item.Description);
                        }
                    }
                }
            }
            ImGui.EndTable();
        }
        ImGui.SetWindowFontScale(1f);
    }
    #endregion MoodlesStatusCombo

    public IconInfo? GetIconInfo(uint iconID)
    {
        if (IconInfoCache.TryGetValue(iconID, out var iconInfo))
        {
            return iconInfo;
        }
        else
        {
            var data = _dataManager.GetExcelSheet<Status>()?.FirstOrDefault(x => x.Icon == iconID);
            if (data == null)
            {
                IconInfoCache[iconID] = null;
                return null;
            }
            var info = new IconInfo()
            {
                Name = data.Name.ToDalamudString().ExtractText(),
                IconID = iconID,
                Type = data.CanIncreaseRewards == 1 ? StatusType.Special : (data.StatusCategory == 2 ? StatusType.Negative : StatusType.Positive),
                ClassJobCategory = data.ClassJobCategory.Value,
                IsFCBuff = data.IsFcBuff,
                IsStackable = data.MaxStacks > 1,
                Description = data.Description.ToDalamudString().ExtractText()

            };
            IconInfoCache[iconID] = info;
            return info;
        }
    }

    public struct IconInfo
    {
        public string Name;
        public uint IconID;
        public StatusType Type;
        public bool IsStackable;
        public ClassJobCategory ClassJobCategory;
        public bool IsFCBuff;
        public string Description;
    }
}
