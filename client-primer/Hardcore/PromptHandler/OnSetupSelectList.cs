/*using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Utils;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace GagSpeak.Hardcore.BaseListener;

public abstract class OnSetupSelectListFeature : BaseFeature, IDisposable
{
    private readonly ILogger<OnSetupSelectListFeature> _logger;
    private Hook<OnItemSelectedDelegate>? onItemSelectedHook = null;
    private readonly ITargetManager _targetManager;
    private readonly IGameInteropProvider _gameInteropProvider;
    private readonly HardcoreHandler _handler;
    protected OnSetupSelectListFeature(ILogger logger, ITargetManager targetManager, 
        IGameInteropProvider gameInteropProvider, HardcoreHandler handler)
    {
        _targetManager = targetManager;
        _gameInteropProvider = gameInteropProvider;
        _handler = handler;
    }

    private delegate byte OnItemSelectedDelegate(IntPtr popupMenu, uint index, IntPtr a3, IntPtr a4);

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            _logger.LogDebug("OnSetupSelectListFeature: Dispose");
            this.onItemSelectedHook?.Disable();
            this.onItemSelectedHook?.Dispose();
        }
    }

    protected unsafe void CompareNodesToEntryTexts(IntPtr addon, PopupMenu* popupMenu) {
        _logger.LogDebug("CompareNodesToEntryTexts");
        var target = _targetManager.Target;
        var targetName = target != null
            ? GS_GetSeString.GetSeStringText(target.Name)
            : string.Empty;

        var texts = this.GetEntryTexts(popupMenu);
    }

    protected abstract void SelectItemExecute(IntPtr addon, int index);

    public unsafe void Fire(AtkUnitBase* Base, bool updateState, params object[] values)
    {
        if (Base == null) throw new Exception("Null UnitBase");
        var atkValues = (AtkValue*)Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
        if (atkValues == null) return;
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                switch (v)
                {
                    case uint uintValue:
                        atkValues[i].Type = ValueType.UInt;
                        atkValues[i].UInt = uintValue;
                        break;
                    case int intValue:
                        atkValues[i].Type = ValueType.Int;
                        atkValues[i].Int = intValue;
                        break;
                    case float floatValue:
                        atkValues[i].Type = ValueType.Float;
                        atkValues[i].Float = floatValue;
                        break;
                    case bool boolValue:
                        atkValues[i].Type = ValueType.Bool;
                        atkValues[i].Byte = (byte)(boolValue ? 1 : 0);
                        break;
                    case string stringValue:
                        {
                            atkValues[i].Type = ValueType.String;
                            var stringBytes = Encoding.UTF8.GetBytes(stringValue);
                            var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                            Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length);
                            Marshal.WriteByte(stringAlloc, stringBytes.Length, 0);
                            atkValues[i].String = (byte*)stringAlloc;
                            break;
                        }
                    case AtkValue rawValue:
                        {
                            atkValues[i] = rawValue;
                            break;
                        }
                    default:
                        throw new ArgumentException($"Unable to convert type {v.GetType()} to AtkValue");
                }
            }
            List<string> CallbackValues = [];
            for (var i = 0; i < values.Length; i++)
            {
                CallbackValues.Add($"    Value {i}: [input: {values[i]}/{values[i]?.GetType().Name}] -> {DecodeValue(atkValues[i])})");
            }
            _logger.LogTrace($"Firing callback: {Base->Name.Read()}, valueCount = {values.Length}, updateStatte = {updateState}, values:\n");
            FireRaw(Base, values.Length, atkValues, (byte)(updateState ? 1 : 0));
        }
        finally
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (atkValues[i].Type == ValueType.String)
                {
                    Marshal.FreeHGlobal(new IntPtr(atkValues[i].String));
                }
            }
            Marshal.FreeHGlobal(new IntPtr(atkValues));
        }
    }

    private unsafe byte OnItemSelectedDetour(IntPtr popupMenu, uint index, IntPtr a3, IntPtr a4) {
        if (popupMenu == IntPtr.Zero)
            return this.onItemSelectedHook!.Original(popupMenu, index, a3, a4);

        try {
            var popupMenuPtr = (PopupMenu*)popupMenu;
            if (index < popupMenuPtr->EntryCount) {
                var entryPtr = popupMenuPtr->EntryNames[index];
                var entryText = _handler.LastSeenListSelection = entryPtr != null
                    ? GS_GetSeString.GetSeStringText(entryPtr)
                    : string.Empty;

                var target = _targetManager.Target;
                var targetName = _handler.LastSeenListTarget = target != null
                    ? GS_GetSeString.GetSeStringText(target.Name)
                    : string.Empty;

                _logger.LogDebug($"ItemSelected: target={targetName} text={entryText}");
            }
        } catch (Exception ex) {
            _logger.LogError($"Don't crash the game, please: {ex}");
        }
        return this.onItemSelectedHook!.Original(popupMenu, index, a3, a4);
    }

    public unsafe string?[] GetEntryTexts(PopupMenu* popupMenu) {
        var count = popupMenu->EntryCount;
        var entryTexts = new string?[count];

        _logger.LogDebug($"SelectString: Reading {count} strings");
        for (var i = 0; i < count; i++)
        {
            var textPtr = popupMenu->EntryNames[i];
            entryTexts[i] = textPtr != null
                ? GS_GetSeString.GetSeStringText(textPtr)
                : null;

            // Print out the string it finds
            if (entryTexts[i] != null)
            {
                _logger.LogDebug($"Found string: {entryTexts[i]}");
            }

        }

        return entryTexts;
    }
}
*/
