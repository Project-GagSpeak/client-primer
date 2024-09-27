using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Extensions;
using ImGuiNET;
using OtterGui.Classes;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.UiWardrobe;

public class CursedDungeonLoot : DisposableMediatorSubscriberBase
{
    private readonly GagspeakConfigService _mainConfig;
    private readonly WardrobeHandler _handler;
    private readonly UiSharedService _uiShared;

    public CursedDungeonLoot(ILogger<CursedDungeonLoot> logger,
        GagspeakMediator mediator, GagspeakConfigService mainConfig,
        WardrobeHandler handler, UiSharedService uiSharedService)
        : base(logger, mediator)
    {
        _mainConfig = mainConfig;
        _handler = handler;
        _uiShared = uiSharedService;
    }

    private LowerString RestraintSetSearchString = LowerString.Empty;
    private int HoveredSetListIdx = -1;
    private int HoveredCursedItemIdx = -1;
    private string? LowerTimerRange = null;
    private string? UpperTimerRange = null;
    private List<RestraintSet> FilteredSetList
    {
        get
        {
            return _handler.NonCursedSetList
                .Where(set => set.Name.Contains(RestraintSetSearchString, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public void DrawCursedLootPanel()
    {
        if(!_mainConfig.Current.CursedDungeonLoot)
        {
            _uiShared.BigText("Must Enable Cursed Dungeon Loot");
            UiSharedService.ColorText("This can be found in the Global GagSpeak Settings", ImGuiColors.ParsedGold);
            return;
        }

        var region = ImGui.GetContentRegionAvail();
        var topLeftSideHeight = region.Y;
        var width = ImGui.GetContentRegionAvail().X / 2;
        using (ImRaii.Table("CursedLootSets", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
        {
            // setup the columns
            ImGui.TableSetupColumn("SetList", ImGuiTableColumnFlags.WidthFixed, width);
            ImGui.TableSetupColumn("ActiveCursedSets", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow(); ImGui.TableNextColumn();

            var regionSize = ImGui.GetContentRegionAvail();

            using (ImRaii.Child($"###ListWardrobeCursed", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
            {
                CursedLootHeader("Add Set To Pool");
                ImGui.Separator();
                DrawSearchFilter(regionSize.X, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.Separator();
                if (_handler.RestraintSetListSize() > 0)
                {
                    bool itemGotHovered = false;
                    for (int i = 0; i < FilteredSetList.Count; i++)
                    {
                        var set = FilteredSetList[i];
                        bool isHovered = i == HoveredSetListIdx;
                        if (CustomSelectable(set.RestraintId, set.Name, isHovered, drawRightArrow: true))
                        {
                            _handler.AddSetToCursedLootList(set.RestraintId);
                        }
                        if (ImGui.IsItemHovered())
                        {
                            itemGotHovered = true;
                            HoveredSetListIdx = i;
                        }
                    }
                    if (!itemGotHovered)
                    {
                        HoveredSetListIdx = -1;
                    }
                }
            }
            ImGui.TableNextColumn();

            regionSize = ImGui.GetContentRegionAvail();
            using (ImRaii.Child($"###ActiveCursedSets", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
            {
                CursedLootHeader("Cursed Set Pool");
                ImGui.Separator();
                DrawLockRangesAndChance(regionSize.X);
                ImGui.Separator();
                if (_handler.GetCursedLootItems().Count > 0)
                {
                    bool activeCursedItemGotHovered = false;
                    for (int i = 0; i < _handler.ActiveCursedSetList.Count; i++)
                    {
                        var set = _handler.ActiveCursedSetList[i];
                        bool isHovered = i == HoveredCursedItemIdx;
                        var gag = _handler.GetCursedLootItems()[i].AttachedGag;
                        string label = set.Name;
                        if (gag is not GagType.None) label = label + " <+Gag>";

                        if (CustomSelectable(set.RestraintId, label, isHovered, drawLeftArrow: true))
                        {
                            _handler.RemoveSetFromCursedLootList(set.RestraintId);
                        }
                        if (ImGui.IsItemHovered())
                        {
                            if (gag is not GagType.None)
                                UiSharedService.AttachToolTip("Has a " + gag.GagName() + " attached to it.");
                            activeCursedItemGotHovered = true;
                            HoveredCursedItemIdx = i;
                        }
                        // if the item is right clicked, open the popup
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && HoveredCursedItemIdx == i)
                        {
                            ImGui.OpenPopup($"CursedSetPopup{i}");
                        }
                    }
                    if (HoveredCursedItemIdx != -1 && HoveredCursedItemIdx < _handler.ActiveCursedSetList.Count)
                    {
                        // get the cursed item first.
                        var cursedItem = _handler.GetCursedLootItems()[HoveredCursedItemIdx];

                        if (ImGui.BeginPopup($"CursedSetPopup{HoveredCursedItemIdx}"))
                        {
                            _uiShared.DrawCombo("##Attach to Set", 200f, Enum.GetValues<GagType>(), (gag) => gag.GagName(), (i) =>
                            {
                                _handler.AddGagToCursedItem(HoveredCursedItemIdx, i);
                            }, GagType.None);

                            if (cursedItem.AttachedGag is not GagType.None)
                            {
                                if (ImGui.Button("Unattach Gag", new Vector2(200f, ImGui.GetFrameHeightWithSpacing())))
                                {
                                    _handler.RemoveGagFromCursedItem(HoveredCursedItemIdx);
                                    ImGui.CloseCurrentPopup();
                                }
                            }

                            ImGui.EndPopup();
                        }
                    }
                    if (!activeCursedItemGotHovered && !ImGui.IsPopupOpen($"CursedSetPopup{HoveredCursedItemIdx}"))
                    {
                        HoveredCursedItemIdx = -1;
                    }
                }
            }
        }
    }
    public bool CustomSelectable(Guid id, string label, bool isHovered, bool drawLeftArrow = false, bool drawRightArrow = false)
    {
        using var borderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 4f);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        using var borderCol = ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink);
        // push a less transparent very dark grey background color.
        using var bgColor = isHovered
            ? ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered))
            : ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.25f, 0.2f, 0.2f, 0.4f));

        var leftArrowSize = _uiShared.GetIconData(FontAwesomeIcon.ArrowLeft);
        var rightArrowSize = _uiShared.GetIconData(FontAwesomeIcon.ArrowRight);
        var nameTextSize = ImGui.CalcTextSize(label);

        using (ImRaii.Child($"##CustomSelectable" + label + id, new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeightWithSpacing()), true))
        {
            // if we are drawing the left arrow, draw that in first.
            if (drawLeftArrow)
            {
                using (ImRaii.Group())
                {
                    var yPos = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().ItemSpacing.X);
                    // center the icon text to be vertically centered by comparing ImGui.GetFrameHeightWithSpacing() to the leftArrowSize.Y
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetFrameHeightWithSpacing() - leftArrowSize.Y) / 2);
                    _uiShared.IconText(FontAwesomeIcon.ArrowLeft, ImGuiColors.ParsedPink);
                    ImGui.SameLine();
                    ImGui.SetCursorPosY(yPos + (ImGui.GetFrameHeightWithSpacing() - nameTextSize.Y) / 2);
                    ImGui.TextUnformatted(label);
                    ImGui.SameLine(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetStyle().ItemSpacing.X - rightArrowSize.X);
                    ImGui.SetCursorPosY(yPos + (ImGui.GetFrameHeightWithSpacing() - rightArrowSize.Y) / 2);
                    _uiShared.IconText(FontAwesomeIcon.Plus, ImGuiColors.ParsedGold);
                }
            }

            // if we are drawing the right arrow, draw that in last.
            if (drawRightArrow)
            {
                using (ImRaii.Group())
                {
                    var yPos = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().ItemSpacing.X);
                    // center the icon text to be vertically centered by comparing ImGui.GetFrameHeightWithSpacing() to the leftArrowSize.Y
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetFrameHeightWithSpacing() - nameTextSize.Y) / 2);
                    ImGui.TextUnformatted(label);
                    // span across to the end of the content width and subtract the ItemSpacing.X + rightArrowIconData
                    ImGui.SameLine(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetStyle().ItemSpacing.X - rightArrowSize.X);
                    ImGui.SetCursorPosY(yPos + (ImGui.GetFrameHeightWithSpacing() - rightArrowSize.Y) / 2);
                    _uiShared.IconText(FontAwesomeIcon.ArrowRight, ImGuiColors.ParsedPink);
                }
            }
        }
        if (ImGui.IsItemClicked())
            return true;

        return false;
    }

    private void CursedLootHeader(string text)
    {
        var startYpos = ImGui.GetCursorPosY();
        Vector2 textSize;
        using (_uiShared.UidFont.Push()) { textSize = ImGui.CalcTextSize(text); }
        using (ImRaii.Child("CursedSetsHeader" + text, new Vector2(UiSharedService.GetWindowContentRegionWidth(), 40)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X / 2 - textSize.X / 2));
            ImGui.SetCursorPosY(startYpos + 3f);
            _uiShared.BigText(text);
        }
    }

    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawSearchFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = RestraintSetSearchString;
        if (ImGui.InputTextWithHint("##RestraintFilter", "Search for Restraint Set", ref filter, 255))
        {
            RestraintSetSearchString = filter;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(RestraintSetSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            RestraintSetSearchString = string.Empty;
        }
    }

    public void DrawLockRangesAndChance(float availableWidth)
    {
        // Define the widths for input fields and the slider
        float inputWidth = (availableWidth - _uiShared.GetIconData(FontAwesomeIcon.HourglassHalf).X - ImGui.GetStyle().ItemInnerSpacing.X * 2 - ImGui.CalcTextSize("100.9%  ").X) / 2;

        // Input Field for the first range
        ImGui.SetNextItemWidth(inputWidth);
        var spanLow = _handler.GetCursedLootModel().LockRangeLower;
        LowerTimerRange = spanLow == TimeSpan.Zero ? string.Empty : _uiShared.TimeSpanToString(spanLow);
        if (ImGui.InputTextWithHint("##Timer_Input_Lower", "Ex: 0h2m7s", ref LowerTimerRange, 12, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (_uiShared.TryParseTimeSpan(LowerTimerRange, out var timeSpan))
            {
                _handler.SetCursedLootLowerRange(timeSpan);
            }
        }
        UiSharedService.AttachToolTip("Min Cursed Lock Time.");

        ImUtf8.SameLineInner();
        _uiShared.IconText(FontAwesomeIcon.HourglassHalf, ImGuiColors.ParsedGold);
        ImUtf8.SameLineInner();
        // Input Field for the second range
        ImGui.SetNextItemWidth(inputWidth);
        var spanHigh = _handler.GetCursedLootModel().LockRangeUpper;
        UpperTimerRange = spanHigh == TimeSpan.Zero ? string.Empty : _uiShared.TimeSpanToString(spanHigh);
        if (ImGui.InputTextWithHint("##Timer_Input_Upper", "Ex: 0h2m7s", ref UpperTimerRange, 12, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (_uiShared.TryParseTimeSpan(UpperTimerRange, out var timeSpan))
            {
                _handler.SetCursedLootUpperRange(timeSpan);
            }
        }
        UiSharedService.AttachToolTip("Max Cursed lock Time.");

        ImUtf8.SameLineInner();
        // Slider for percentage adjustment
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var percentage = _handler.GetCursedLootModel().LockChance;
        if (ImGui.DragInt("##Percentage", ref percentage, 0.1f, 0, 100, "%d%%"))
        {
            _handler.SetCursedLootLockChance(percentage);
        }
        UiSharedService.AttachToolTip("The % Chance that opening Dungeon Loot will contain Cursed Bondage Loot.");
    }
}
