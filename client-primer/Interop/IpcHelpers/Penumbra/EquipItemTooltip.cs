using System;
using Dalamud.Plugin.Services;
using GagSpeak.Wardrobe;
using ImGuiNET;
using GagSpeak.Interop.Ipc;
using OtterGui.Raii;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using GagSpeak.Services.Mediator;

namespace GagSpeak.Interop.IpcHelpers.Penumbra;

public sealed class PenumbraChangedItemTooltip : DisposableMediatorSubscriberBase
{
    private readonly IpcCallerPenumbra _penumbra;
    private readonly ItemData _itemData; 
    private readonly IClientState _clientState;
    public DateTime LastTooltip { get; private set; } = DateTime.MinValue;
    public DateTime LastClick   { get; private set; } = DateTime.MinValue;

    public PenumbraChangedItemTooltip(ILogger<PenumbraChangedItemTooltip> logger,
        GagspeakMediator mediator, IpcCallerPenumbra penumbra, IClientState clientState, 
        ItemData itemData) : base(logger, mediator)
    {
        _penumbra          = penumbra;
        _clientState       = clientState;
        _itemData         = itemData;
        _penumbra.Tooltip += OnPenumbraTooltip;
        _penumbra.Click   += OnPenumbraClick;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _penumbra.Tooltip -= OnPenumbraTooltip;
        _penumbra.Click   -= OnPenumbraClick;
    }

    public void CreateTooltip(EquipItem item, string prefix, bool openTooltip) {
        if (!_clientState.IsLoggedIn || _clientState.LocalContentId == 0) {
            return;
        }
        var slot = item.Type.ToSlot();
        switch (slot) {
            case EquipSlot.RFinger:
                using (_ = !openTooltip ? null : ImRaii.Tooltip()) {
                    ImGui.TextUnformatted($"{prefix}ALT + Left-Click to apply to selected Restraint Set (Right Finger).");
                    ImGui.TextUnformatted($"{prefix}ALT + Shift + Left-Click to apply to selected Restraint Set (Left Finger).");
                }
                break;
            default:
                using (_ = !openTooltip ? null : ImRaii.Tooltip()) {
                    ImGui.TextUnformatted($"{prefix}ALT + Left-Click to apply to selected Restraint Set.");
                }
                break;
        }
    }

    public void ApplyItem(EquipItem item)
    {
        var slot = item.Type.ToSlot();
        switch (slot) {
            case EquipSlot.RFinger:
                switch (ImGui.GetIO().KeyAlt, ImGui.GetIO().KeyShift) 
                {
                    case (true, false):
                        Logger.LogDebug($"Applying {item.Name} to Right Finger.");
                        Mediator.Publish(new TooltipSetItemToRestraintSetMessage(EquipSlot.RFinger, item));
                        break;
                    case (true, true):
                        Logger.LogDebug($"Applying {item.Name} to Left Finger.");
                        Mediator.Publish(new TooltipSetItemToRestraintSetMessage(EquipSlot.LFinger, item));
                        break;
                }
                return;
            default:
                if(ImGui.GetIO().KeyAlt) {
                    Logger.LogDebug($"Applying {item.Name} to {slot.ToName()}.");
                    Mediator.Publish(new TooltipSetItemToRestraintSetMessage(slot, item));
                }
                return;
        }
    }

    private void OnPenumbraTooltip(ChangedItemType type, uint id) {
        LastTooltip = DateTime.UtcNow;
        if (!_clientState.IsLoggedIn || _clientState.LocalContentId == 0) {
            return;
        }

        if(type == ChangedItemType.Item) {
            if (!_itemData.TryGetValue(id, type is ChangedItemType.Item ? EquipSlot.MainHand : EquipSlot.OffHand, out var item)) {
                return;
            }
            CreateTooltip(item, "[GagSpeak] ", false);
            return;
        }
    }

    private void OnPenumbraClick(MouseButton button, ChangedItemType type, uint id)
    {
        LastClick = DateTime.UtcNow;
        if (button is not MouseButton.Left)
            return;

        if (!_clientState.IsLoggedIn || _clientState.LocalContentId == 0) {
            return;
        }

        if(type == ChangedItemType.Item) {
            if (!_itemData.TryGetValue(id, type is ChangedItemType.Item ? EquipSlot.MainHand : EquipSlot.OffHand, out var item)) {
                return;
            }
            ApplyItem(item);
        }
    }
}
