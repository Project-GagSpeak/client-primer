using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.Utils;
using GagspeakAPI.Data.IPC;
using GagspeakAPI.Data.Permissions;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using System.Numerics;
using System.Security.AccessControl;
namespace GagSpeak.Services;

// helps out with locating and parsing moodles icons and drawing custom dropdowns.
public class MoodlesService
{
    private readonly ILogger<MoodlesService> _logger;
    private readonly IDataManager _dataManager;
    private readonly ITextureProvider _textures;

    private static Dictionary<uint, IconInfo?> IconInfoCache = [];
    private string StatusSearchString = string.Empty;

    public MoodlesService(ILogger<MoodlesService> logger, IDataManager dataManager, ITextureProvider textures)
    {
        _logger = logger;
        _dataManager = dataManager;
        _textures = textures;
        SelectedMoodleComboGuids = new Dictionary<string, Guid>();
    }

    public static readonly Vector2 StatusSize = new(24, 32);
    public Dictionary<string, Guid> SelectedMoodleComboGuids;    // the selected combo items

    // helper function to extract the GUID from the combo label key in the dictionary
    public Guid? GetSelectedItem(string comboLabel)
    {
        if (SelectedMoodleComboGuids.TryGetValue(comboLabel, out var guid))
            return guid;
        return null;
    }


    // Helper function to handle the combo box logic
    private void DrawCombo(string comboLabel, float width, List<(Guid, List<Guid>)> MoodlesPresets, 
        List<MoodlesStatusInfo> RelatedStatuses, Action<Guid?>? onSelected = null)
    {
        if (!MoodlesPresets.Any())
        {
            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo("##" + comboLabel, "No Presets Available..."))
                ImGui.EndCombo();
            return;
        }

        // Get the currently selected item or default to the first preset
        if (!SelectedMoodleComboGuids.TryGetValue(comboLabel, out var selectedItem) || selectedItem == Guid.Empty)
        {
            selectedItem = MoodlesPresets.First().Item1;
            SelectedMoodleComboGuids[comboLabel] = selectedItem;
        }

        // Ensure the selected item still exists in the list, otherwise reset it
        if (!MoodlesPresets.Any(item => item.Item1 == selectedItem))
        {
            selectedItem = MoodlesPresets.First().Item1;
            SelectedMoodleComboGuids[comboLabel] = selectedItem;
        }

        // Draw the combo box
        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo(comboLabel, selectedItem.ToString()))
        {
            foreach (var item in MoodlesPresets)
            {
                bool isSelected = item.Item1 == selectedItem;
                if (ImGui.Selectable(item.Item1.ToString(), isSelected))
                {
                    SelectedMoodleComboGuids[comboLabel] = item.Item1;
                    onSelected?.Invoke(selectedItem);
                }

                // Tooltip for moodle names
                if (ImGui.IsItemHovered())
                {
                    var moodleNames = RelatedStatuses
                        .Where(x => item.Item2.Contains(x.GUID))
                        .Select(x => x.Title)
                        .ToList();
                    ImGui.SetTooltip($"This Preset Enables the Following Moodles:\n" + string.Join(Environment.NewLine, moodleNames));
                }
            }
            ImGui.EndCombo();
        }
        // Check if the item was right-clicked. If so, reset to default value.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _logger.LogTrace("Right-clicked on "+comboLabel+". Resetting to default value.", LoggerType.IpcMoodles);
            selectedItem = MoodlesPresets.First().Item1;
            SelectedMoodleComboGuids[comboLabel] = selectedItem!;
            onSelected?.Invoke(selectedItem!);
        }

        return;
    }

    public void DrawMoodlesPresetCombo(string comboLabel, float width, List<(Guid, List<Guid>)> MoodlesPresets,
        List<MoodlesStatusInfo> RelatedMoodlesStatuses, Action<Guid?>? onSelected = null)
    {
        DrawCombo(comboLabel, width, MoodlesPresets, RelatedMoodlesStatuses, onSelected);
    }

    public void DrawMoodlesPresetComboButton(string comboLabel, string buttonLabel, float width,
        List<(Guid, List<Guid>)> MoodlesPresets,
        List<MoodlesStatusInfo> RelatedMoodlesStatuses,
        bool buttonDisabled = false,
        Action<Guid?>? onSelected = null,
        Action<Guid?>? onButton = null,
        string buttonTT = "")
    {
        var comboWidth = width - ImGuiHelpers.GetButtonSize(buttonLabel).X - ImGui.GetStyle().ItemInnerSpacing.X;
        
        DrawCombo(comboLabel, comboWidth, MoodlesPresets, RelatedMoodlesStatuses, onSelected);

        ImUtf8.SameLineInner();

        if (ImGuiUtil.DrawDisabledButton(buttonLabel, new Vector2(), string.Empty, buttonDisabled))
            onButton?.Invoke(SelectedMoodleComboGuids[comboLabel]);

        if (!string.IsNullOrEmpty(buttonTT))
            UiSharedService.AttachToolTip(buttonTT);
    }

    #region MoodlesStatusCombo
    public void DrawMoodleStatusCombo(string comboLabel, float width, List<MoodlesStatusInfo> statusList, 
        Action<Guid?>? onSelected = null, float sizeScaler = 1f)
    {
        DrawStatusComboBox(statusList, comboLabel, width, onSelected, sizeScaler);
    }


    public void DrawMoodleStatusComboButton(
    string comboLabel,
    string buttonLabel,
    float width,
    List<MoodlesStatusInfo> statusList,
    bool buttonDisabled = false,
    Action<Guid?>? onSelected = null,
    Action<Guid?>? onButton = null,
    float sizeScaler = 1f,
    string buttonTT = "")
    {
        // Calculate the available width for the combo box
        var comboWidth = width - ImGui.GetStyle().ItemInnerSpacing.X - ImGuiHelpers.GetButtonSize(buttonLabel).X;

        // Draw the status combo box and get the selected item's GUID
        DrawStatusComboBox(statusList, comboLabel, comboWidth, onSelected, sizeScaler);

        ImUtf8.SameLineInner();
        if (ImGuiUtil.DrawDisabledButton(buttonLabel, new Vector2(), string.Empty, buttonDisabled))
            onButton?.Invoke(SelectedMoodleComboGuids[comboLabel]);

        // Optional: Attach a tooltip to the button
        if (!string.IsNullOrEmpty(buttonTT))
            UiSharedService.AttachToolTip(buttonTT);
    }


    private void DrawStatusComboBox(List<MoodlesStatusInfo> statuses, string comboLabel, float width, 
        Action<Guid?>? onSelected = null, float sizeScaler = 1f)
    {
        var height = ImGui.GetTextLineHeightWithSpacing() * 10 - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y;

        // if the statuses list is empty, display an empty status combo box.
        if (!statuses.Any())
        {
            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo("##DummyEmptyComboBox", "No Statuses Available..."))
                ImGui.EndCombo();
            return;
        }

        // Get the currently selected item or default to the first preset
        if (!SelectedMoodleComboGuids.TryGetValue(comboLabel, out var selectedItem) || selectedItem == Guid.Empty)
        {
            selectedItem = statuses.First().GUID;
            SelectedMoodleComboGuids[comboLabel] = selectedItem;
        }

        // Ensure the selected item still exists in the list, otherwise reset it
        if (!statuses.Any(item => item.GUID == selectedItem))
        {
            selectedItem = statuses.First().Item1;
            SelectedMoodleComboGuids[comboLabel] = selectedItem;
        }


        string selectedLabel = statuses.FirstOrDefault(x => x.GUID == selectedItem).Title ?? "None Selected";

        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo(comboLabel, selectedLabel))
        {
            // Set up Search filter
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
                    bool isSelected = item.GUID == selectedItem;
                    if (ImGui.Selectable(item.Title, isSelected))
                    {
                        SelectedMoodleComboGuids[comboLabel] = item.GUID;
                        onSelected?.Invoke(item.GUID);
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
                        var statusIcon = _textures.GetFromGameIcon(new GameIconLookup((uint)((uint)item.IconID + item.Stacks - 1))).GetWrapOrEmpty();
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
            ImGui.EndCombo();
        }
        // Check if the item was right-clicked. If so, reset to default value.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _logger.LogTrace("Right-clicked on " + comboLabel + ". Resetting to default value.", LoggerType.IpcMoodles);
            selectedItem = statuses.First().GUID;
            SelectedMoodleComboGuids[comboLabel] = selectedItem!;
            onSelected?.Invoke(selectedItem!);
        }

        ImGui.SetWindowFontScale(1f);
        return;
    }
    #endregion MoodlesStatusCombo

    public bool ValidatePermissionForApplication(UserPairPermissions pairPermsForClient, List<MoodlesStatusInfo> statuses)
    {

        if (!pairPermsForClient.AllowPositiveStatusTypes && statuses.Any(statuses => statuses.Type == StatusType.Positive))
        {
            _logger.LogWarning("Client Attempted to apply status(s) with at least one containing a positive status, but they are not allowed to.");
            return false;
        }
        if (!pairPermsForClient.AllowNegativeStatusTypes && statuses.Any(statuses => statuses.Type == StatusType.Negative))
        {
            _logger.LogWarning("Client Attempted to apply status(s) with at least one containing a negative status, but they are not allowed to.");
            return false;
        }
        if (!pairPermsForClient.AllowSpecialStatusTypes && statuses.Any(statuses => statuses.Type == StatusType.Special))
        {
            _logger.LogWarning("Client Attempted to apply status(s) with at least one containing a special status, but they are not allowed to.");
            return false;
        }

        if (!pairPermsForClient.AllowPermanentMoodles && statuses.Any(statuses => statuses.NoExpire))
        {
            _logger.LogWarning("Client Attempted to apply status(s) with at least one containing a permanent status, but they are not allowed to.");
            return false;
        }

        // check the max moodle time exceeding
        if (statuses.Any(status => status.NoExpire == false && // if the status is not permanent, and the time its set for is longer than max allowed time.
            new TimeSpan(status.Days, status.Hours, status.Minutes, status.Seconds) > pairPermsForClient.MaxMoodleTime))
        {
            _logger.LogWarning("Client Attempted to apply status(s) with at least one containing a time exceeding the max allowed time.");
            return false;
        }
        // return true if reached here.
        return true;
    }

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
