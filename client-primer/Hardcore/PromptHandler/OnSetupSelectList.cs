using Dalamud.Game.ClientState.Objects;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Utils;

namespace GagSpeak.Hardcore.BaseListener;

public abstract class OnSetupSelectListFeature : BaseFeature, IDisposable
{
    private readonly ILogger _logger;
    private readonly HardcoreHandler _handler;
    private readonly ITargetManager _targetManager;
    private readonly IGameInteropProvider _gameInteropProvider;
    protected OnSetupSelectListFeature(ILogger logger, HardcoreHandler handler,
        ITargetManager targetManager, IGameInteropProvider gameInteropProvider)
    {
        _logger = logger;
        _handler = handler;
        _targetManager = targetManager;
        _gameInteropProvider = gameInteropProvider;
    }

    public Hook<AddonReceiveEventDelegate>? onItemSelectedHook = null;
    public unsafe delegate nint AddonReceiveEventDelegate(AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logger.LogDebug("OnSetupSelectListFeature: Dispose", LoggerType.HardcorePrompt);
            onItemSelectedHook?.Disable();
            onItemSelectedHook?.Dispose();
        }
    }

    protected unsafe int? GetMatchingIndex(string[] entries)
    {
        _logger.LogDebug("CompareNodesToEntryTexts", LoggerType.HardcorePrompt);
        var target = _targetManager.Target;
        var targetName = target != null ? target.Name.ExtractText() : string.Empty;

        var nodes = _handler.GetAllNodes().OfType<TextEntryNode>();
        foreach (var node in nodes)
        {
            if (!node.Enabled || string.IsNullOrEmpty(node.Text))
                continue;

            var (matched, index) = EntryMatchesTexts(node, entries);
            if (!matched)
                continue;

            _logger.LogDebug($"OnSetupSelectListFeature: Matched on {node.Text}", LoggerType.HardcorePrompt);
            return index;
        }
        return null;
    }

    protected unsafe void SetupOnItemSelectedHook(PopupMenu* popupMenu)
    {
        if (onItemSelectedHook != null) return;

        var onItemSelectedAddress = (nint)popupMenu->VirtualTable->ReceiveEvent;
        onItemSelectedHook = _gameInteropProvider.HookFromAddress<AddonReceiveEventDelegate>(onItemSelectedAddress, OnItemSelectedDetour);
        onItemSelectedHook.Enable();
    }


    private static (bool Matched, int Index) EntryMatchesTexts(TextEntryNode node, string?[] texts)
    {
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null)
                continue;

            if (EntryMatchesText(node, text))
                return (true, i);
        }

        return (false, -1);
    }

    private static bool EntryMatchesText(TextEntryNode node, string text)
    => node.IsTextRegex && (node.TextRegex?.IsMatch(text) ?? false) || !node.IsTextRegex && text.Contains(node.Text);

    protected abstract void SelectItemExecute(IntPtr addon, int index);

    private unsafe nint OnItemSelectedDetour(AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData)
    {
        _logger.LogDebug($"PopupMenu RCV: listener={onItemSelectedHook.Address} {(nint)self:X}, type={eventType}, param={eventParam}, "+
            $"input={inputData[0]:X16} {inputData[1]:X16} {inputData[2]:X16} {(int)inputData[2]}", LoggerType.HardcorePrompt);
        try
        {
            var target = _targetManager.Target;
            var targetName = _handler.LastSeenListTarget = target != null ? target.Name.ExtractText() : string.Empty;
            _handler.LastSeenListSelection = _handler.LastSeenListEntries[(int)inputData[2]].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Don't crash the game.");
        }
        return onItemSelectedHook.Original(self, eventType, eventParam, eventData, inputData);
    }

    public unsafe string?[] GetEntryTexts(PopupMenu* popupMenu)
    {
        var count = popupMenu->EntryCount;
        var entryTexts = new string?[count];

        _logger.LogDebug($"SelectString: Reading {count} strings", LoggerType.HardcorePrompt);
        for (var i = 0; i < count; i++)
        {
            var textPtr = popupMenu->EntryNames[i];
            entryTexts[i] = textPtr != null ? new string((char*)*textPtr) : string.Empty;
        }

        return entryTexts;
    }
}

