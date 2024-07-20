using Dalamud.Plugin.Services;
using GagSpeak.Utils;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Log;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.UI.Components.Combos;

public sealed class BonusItemCombo : CustomFilterComboCache<BonusItem>
{
    public readonly string Label;
    public BonusItemId _currentItem;
    private float _innerWidth;
    public PrimaryId CustomSetId { get; private set; }
    public Variant CustomVariant { get; private set; }
    public BonusItemCombo(IDataManager gameData, BonusItemFlag slot, DictBonusItems itemData, ILogger log)
        : base(() => GetItems(itemData, slot), MouseWheelType.Unmodified, log)
    {
        Label = GetLabel(gameData, slot);
        _currentItem = 0;
        SearchByParts = true;
    }

    protected override void DrawList(float width, float itemHeight)
    {
        base.DrawList(width, itemHeight);
        if (NewSelection != null && Items.Count > NewSelection.Value)
            CurrentSelection = Items[NewSelection.Value];
    }

    protected override int UpdateCurrentSelected(int currentSelected)
    {
        if (CurrentSelection.Id == _currentItem)
            return currentSelected;

        CurrentSelectionIdx = Items.IndexOf(i => i.Id == _currentItem);
        CurrentSelection = CurrentSelectionIdx >= 0 ? Items[CurrentSelectionIdx] : default;
        return base.UpdateCurrentSelected(CurrentSelectionIdx);
    }

    public bool Draw(string previewName, BonusItemId previewIdx, float width, float innerWidth, string labelDisp = "")
    {
        _innerWidth = innerWidth;
        _currentItem = previewIdx;
        CustomVariant = 0;
        return Draw($"{labelDisp}##Test{Label}", previewName, string.Empty, width, ImGui.GetTextLineHeightWithSpacing());
    }

    protected override float GetFilterWidth()
        => _innerWidth - 2 * ImGui.GetStyle().FramePadding.X;

    protected override bool DrawSelectable(int globalIdx, bool selected)
    {
        var obj = Items[globalIdx];
        var name = ToString(obj);
        var ret = ImGui.Selectable(name, selected);
        ImGui.SameLine();
        using var color = ImRaii.PushColor(ImGuiCol.Text, 0xFF808080);
        ImGuiUtil.RightAlign($"({obj.ModelId.Id}-{obj.Variant.Id})");
        return ret;
    }

    protected override bool IsVisible(int globalIndex, LowerString filter)
        => base.IsVisible(globalIndex, filter) || filter.IsContained(Items[globalIndex].ModelId.Id.ToString());

    protected override string ToString(BonusItem obj)
        => obj.Name;

    private static string GetLabel(IDataManager gameData, BonusItemFlag slot)
    {
        var sheet = gameData.GetExcelSheet<Addon>()!;

        return slot switch
        {
            BonusItemFlag.Glasses => sheet.GetRow(16050)?.Text.ToString() ?? "Facewear",
            BonusItemFlag.UnkSlot => sheet.GetRow(16051)?.Text.ToString() ?? "Facewear",

            _ => string.Empty,
        };
    }

    private static IReadOnlyList<BonusItem> GetItems(DictBonusItems itemData, BonusItemFlag slot)
    {
        var nothing = BonusItem.Empty(slot);
        if (slot is not BonusItemFlag.Glasses)
            return [nothing];

        return itemData.Values.OrderBy(i => i.Name).Prepend(nothing).ToList();
    }

    protected override void OnClosePopup()
    {
        // If holding control while the popup closes, try to parse the input as a full pair of set id and variant, and set a custom item for that.
        if (!ImGui.GetIO().KeyCtrl)
            return;

        var split = Filter.Text.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 2 || !ushort.TryParse(split[0], out var setId) || !byte.TryParse(split[1], out var variant))
            return;

        CustomSetId = setId;
        CustomVariant = variant;
    }
}

