using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.IPC;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.IdentityModel.Tokens;
using OtterGui.Classes;
using OtterGui.Text;
using System.Numerics;
using System.Security.AccessControl;
using System.Xml.Linq;


namespace GagSpeak.UI.UiWardrobe;

public class MoodlesManager : MediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly PairManager _pairManager;
    private readonly IpcCallerMoodles _ipcCallerMoodles;
    private IDataManager _dataManager;
    private enum InspectType { Status, Preset }
    public MoodlesManager(ILogger<MoodlesManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        PairManager pairManager, IpcCallerMoodles ipcCallerMoodles,
        IDataManager dataManager) : base(logger, mediator)
    {
        _uiShared = uiSharedService;
        _pairManager = pairManager;
        _ipcCallerMoodles = ipcCallerMoodles;
        _dataManager = dataManager;

        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) => LastCreatedCharacterData = msg.CharacterIPCData);
    }

    // Private accessor vars for list management.
    private InspectType CurrentType = InspectType.Status;

    // Info related to the person we are inspecting.
    private CharacterIPCData LastCreatedCharacterData = null!;
    private string PairSearchString = string.Empty;
    private Pair? PairToInspect = null;
    private int SelectedStatusIndex = 0;
    private int SelectedPresetIndex = 0;

    private void MoodlesHeader()
    {
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        Vector2 textSize;
        using (_uiShared.UidFont.Push()) { textSize = ImGui.CalcTextSize(CurrentType == InspectType.Status ? "View Statuses" : "View Presets"); }
        var statusesSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Search, "Statuses");
        var presetsSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Search, "Presets");
        var centerYpos = (textSize.Y - ImGui.GetFrameHeight());

        using (ImRaii.Child("MoodlesManagerHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeight() + (centerYpos - startYpos) * 2)))
        {
            // now next to it we need to draw the header text
            ImGui.SameLine(ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                switch (CurrentType)
                {
                    case InspectType.Status:
                        UiSharedService.ColorText("View Statuses", ImGuiColors.ParsedPink);
                        break;
                    case InspectType.Preset:
                        UiSharedService.ColorText("View Presets", ImGuiColors.ParsedPink);
                        break;
                }
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - statusesSize - presetsSize - 175f - ImGui.GetStyle().ItemSpacing.X * 4);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var PairList = _pairManager.GetOnlineUserPairs()
                .Where(pair => pair.LastReceivedIpcData != null
                && (string.IsNullOrEmpty(PairSearchString)
                || pair.UserData.AliasOrUID.Contains(PairSearchString, StringComparison.OrdinalIgnoreCase)
                || (pair.GetNickname() != null && pair.GetNickname()!.Contains(PairSearchString, StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(p => p.IsVisible) // Prioritize users with Visible == true
                .ThenBy(p => p.GetNickname() ?? p.UserData.AliasOrUID, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Add a special option for "Client Player" at the top of the list
            PairList.Insert(0, null!);

            _uiShared.DrawComboSearchable("##InspectPair", 175f, ref PairSearchString, PairList,
                (pair) => pair == null ? "Examine Self" : pair.GetNickname() ?? pair.UserData.AliasOrUID, false,
                (pair) =>
                {
                    if (pair == null)
                    {
                        PairToInspect = null;
                    }
                    else
                    {
                        PairToInspect = pair;
                    }
                });

            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - statusesSize - presetsSize - ImGui.GetStyle().ItemSpacing.X * 3);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // draw revert button at the same location but right below that button
            if (_uiShared.IconTextButton(FontAwesomeIcon.Search, "Statuses", null, false, CurrentType == InspectType.Status))
            {
                CurrentType = InspectType.Status;
            }
            UiSharedService.AttachToolTip("View the list of Moodles Statuses");

            // draw revert button at the same location but right below that button
            ImGui.SameLine();
            ImGui.SetCursorPosY(currentYpos);
            if (_uiShared.IconTextButton(FontAwesomeIcon.Search, "Presets", null, false, CurrentType == InspectType.Preset))
            {
                CurrentType = InspectType.Preset;
            }
            UiSharedService.AttachToolTip("View the list of Moodles Presets.");
        }
    }


    public void DrawMoodlesManager(Vector2 cellPadding)
    {
        MoodlesHeader();
        ImGui.Separator();

        if (CurrentType == InspectType.Status)
        {
            DrawMoodles(cellPadding);
        }
        else
        {
            DrawPresets(cellPadding);
        }
    }

    private void DrawMoodles(Vector2 cellPadding)
    {
        var DataToDisplay = PairToInspect != null ? PairToInspect.LastReceivedIpcData : LastCreatedCharacterData;
        var length = ImGui.GetContentRegionAvail().X;
        DrawMoodleStatusComboSearchable(length);
        ImGui.Separator();

        using var child = ImRaii.Child("MoodlesInspectionWindow", -Vector2.One, false);
        if (!child) return;

        // draw the moodleInfo
        var cursorPos = new Vector2(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - StatusSize.X * 2, ImGui.GetCursorPosY()) - new Vector2(20, 5);
        _uiShared.BigText("Moodle Information");
        if (SelectedStatusIndex == -1) return;
        ImGui.Separator();
        PrintMoodleInfoExtended(DataToDisplay!.MoodlesStatuses[SelectedStatusIndex], cellPadding, cursorPos);
    }

    private void DrawPresets(Vector2 cellPadding)
    {
        var DataToDisplay = PairToInspect != null ? PairToInspect.LastReceivedIpcData : LastCreatedCharacterData;
        var length = ImGui.GetContentRegionAvail().X;
        _uiShared.DrawComboSearchable("##PresetSelector", length, ref PresetSearchString, DataToDisplay!.MoodlesPresets,
            (preset) => preset.Item1.ToString(), false,
            (preset) =>
            {
                SelectedPresetIndex = DataToDisplay.MoodlesPresets.IndexOf(preset);
            });
        ImGui.Separator();
        using var child = ImRaii.Child("PresetsInspectionWindow", -Vector2.One, false);
        if (!child) return;

        // draw the moodleInfo
        _uiShared.BigText("Preset Information");
        if (SelectedPresetIndex == -1) return;

        foreach (var moodle in DataToDisplay!.MoodlesPresets[SelectedPresetIndex].Item2)
        {
            int idx = DataToDisplay.MoodlesStatuses.FindIndex(m => m.GUID == moodle);
            if (idx == -1)
            {
                ImGui.CollapsingHeader("Moodle Not Found");
            }
            else
            {
                //var cursorPos = new Vector2(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - StatusSize.X * 2, ImGui.GetCursorPosY()) - new Vector2(20, 5);

                if (ImGui.CollapsingHeader(DataToDisplay.MoodlesStatuses[idx].Title + " (" + DataToDisplay.MoodlesStatuses[idx].GUID + ")"))
                {
                    PrintMoodleInfoExtended(DataToDisplay.MoodlesStatuses[idx], cellPadding, Vector2.Zero);
                }
            }
        }
    }

    private string StatusSearchString = string.Empty;
    private string PresetSearchString = string.Empty;
    private Vector2 DefaultItemSpacing;
    private void DrawMoodleStatusComboSearchable(float width)
    {
        CharacterIPCData ipcData = PairToInspect != null ? PairToInspect.LastReceivedIpcData! : LastCreatedCharacterData;

        ImGui.SetNextItemWidth(width - StatusSize.X);
        string comboLabel = ipcData.MoodlesStatuses[SelectedStatusIndex].Title + "##StatusSelector";
        // Button to open popup
        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.Button(comboLabel, new Vector2(width, ImGui.GetFrameHeight())))
        {
            ImGui.SetNextWindowPos(new Vector2(pos.X, pos.Y + ImGui.GetFrameHeight()));
            ImGui.OpenPopup("##StatusSelectorPopup");
        }

        // Popup
        if (ImGui.BeginPopup("##StatusSelectorPopup"))
        {
            DrawStatusComboBox(ipcData, width);
            ImGui.EndPopup();
        }
    }

    private void DrawStatusComboBox(CharacterIPCData ipcData, float width)
    {
        // Search filter
        ImGui.SetNextItemWidth(width - StatusSize.X);
        ImGui.InputTextWithHint("##filter", "Filter...", ref StatusSearchString, 255);
        var searchText = StatusSearchString.ToLowerInvariant();

        var filteredItems = string.IsNullOrEmpty(searchText)
            ? ipcData.MoodlesStatuses
            : ipcData.MoodlesStatuses.Where(item => item.Title.ToLowerInvariant().Contains(searchText));

        ImGui.SetWindowFontScale(1.25f);
        if (ImGui.BeginTable("TimeDurationTable", 2)) // 2 columns for status name and icon
        {
            // Setup columns based on the format
            ImGui.TableSetupColumn("##StatusName", ImGuiTableColumnFlags.WidthFixed, width - StatusSize.X*2 - ImGui.GetStyle().ItemSpacing.X*2);
            ImGui.TableSetupColumn("##StatusIcon", ImGuiTableColumnFlags.WidthFixed, StatusSize.X);

            // Display filtered content.
            foreach (var item in filteredItems)
            {
                // Draw out a selectable spanning the cell.
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(width);
                bool isSelected = item == ipcData.MoodlesStatuses[SelectedStatusIndex];

                if (ImGui.Selectable(item.Title, isSelected))
                {
                    SelectedStatusIndex = ipcData.MoodlesStatuses.IndexOf(item);
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

    /// <summary>
    /// This function and all others below were stripped from Moodles purely for the intent of making the visual display of a moodles details
    /// easily recognizable to the end user in a common format.
    /// https://github.com/kawaii/Moodles/blob/main/Moodles/Gui/TabMoodles.cs 
    /// </summary>
    private void PrintMoodleInfoExtended(MoodlesStatusInfo moodle, Vector2 cellPadding, Vector2 iconPos)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, cellPadding);
        //using var child = ImRaii.Child("##moodleInfo"+moodle.GUID, -Vector2.One, false);
        using (var table = ImRaii.Table("##moodles", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            if(!table) return;

            ImGui.TableSetupColumn("Status Config Item", ImGuiTableColumnFlags.WidthFixed, 135f);
            ImGui.TableSetupColumn("Value Set", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Title:");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##name", ref moodle.Title, 150, ImGuiInputTextFlags.ReadOnly);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Icon:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var selinfo = GetIconInfo((uint)moodle.IconID);
            string iconText = $"Icon: #{moodle.IconID} {selinfo?.Name}";
            ImGui.InputText("##iconNameParsed", ref iconText, 160, ImGuiInputTextFlags.ReadOnly);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Stacks:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var maxStacks = moodle.Stacks.ToString();
            ImGui.InputText("##stacks", ref maxStacks, 3, ImGuiInputTextFlags.ReadOnly);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var cpx = ImGui.GetCursorPosX();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Description:");

            ImGui.TableNextColumn();
            UiSharedService.InputTextWrapMultiline("##desc", ref moodle.Description, 500, 3, ImGui.GetContentRegionAvail().X);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Applicant:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##applier", ref moodle.Applier, 150, ImGuiInputTextFlags.ReadOnly);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Category:");

            ImGui.TableNextColumn();
            using (var disabledRadio = ImRaii.Disabled())
            {
                ImGui.RadioButton("Positive", moodle.Type == StatusType.Positive);
                ImGui.SameLine();
                ImGui.RadioButton("Negative", moodle.Type == StatusType.Negative);
                ImGui.SameLine();
                ImGui.RadioButton("Special", moodle.Type == StatusType.Special);
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Dispellable:");

            ImGui.TableNextColumn();
            using (var disabledRadio = ImRaii.Disabled())
            {
                ImGui.Checkbox("##dispel", ref moodle.Dispelable);
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Duration:");
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            if (moodle.NoExpire)
            {
                ImGui.TextUnformatted("Permanent");
            }
            else
            {
                ImGui.TextUnformatted($"{moodle.Days}d {moodle.Hours}h {moodle.Minutes}m {moodle.Seconds}s");
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Persistent (Sticky):");

            ImGui.TableNextColumn();
            using (var disabledRadio = ImRaii.Disabled())
            {
                ImGui.Checkbox("##sticky", ref moodle.AsPermanent);
            }
        }

        if (moodle.IconID != 0 && iconPos != Vector2.Zero)
        {
            var statusIcon = _uiShared.GetGameStatusIcon((uint)((uint)moodle.IconID + moodle.Stacks - 1));

            if (statusIcon is { } wrap)
            {
                ImGui.SetCursorPos(iconPos);
                ImGui.Image(statusIcon.ImGuiHandle, StatusSize * 2);
            }
        }
    }
    private static readonly Vector2 StatusSize = new(24, 32);
    private static Dictionary<uint, IconInfo?> IconInfoCache = [];
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
