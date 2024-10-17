using Dalamud.Game.ClientState.Objects;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Utils;

namespace GagSpeak.Hardcore.ForcedStay;
public abstract class SetupSelectListPrompt : BasePrompt, IDisposable
{
    protected readonly ILogger _logger;
    protected readonly ClientConfigurationManager _clientConfigs;
    protected readonly IAddonLifecycle _addonLifecycle;
    private readonly IGameInteropProvider _gameInterop;
    protected readonly ITargetManager _targets;

    internal SetupSelectListPrompt(ILogger logger, ClientConfigurationManager clientConfigs,
        IAddonLifecycle addonLifecycle, IGameInteropProvider gameInterop, ITargetManager targets)
    {
        _logger = logger;
        _clientConfigs = clientConfigs;
        _addonLifecycle = addonLifecycle;
        _gameInterop = gameInterop;
        _targets = targets;
    }

    public Hook<AddonReceiveEventDelegate>? onItemSelectedHook = null;
    public unsafe delegate nint AddonReceiveEventDelegate(AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData);
    public void Dispose()
    {
        onItemSelectedHook?.Disable();
        onItemSelectedHook?.Dispose();
    }

    protected unsafe int? GetMatchingIndex(string[] entries, string nodeLabel)
    {
        var nodes = _clientConfigs.GetAllNodes().OfType<TextEntryNode>();
        foreach (var node in nodes)
        {
            if (!node.Enabled || string.IsNullOrEmpty(node.SelectedOptionText))
                continue;
            
            var (matched, index) = EntryMatchesTexts(node, entries);
            if (!matched)
                continue;

            // If the node is target restricted and TargetNodeLabel is provided, use it for matching.
            // Otherwise, fall back to matching based on the node name.
            if (node.TargetRestricted)
            {
                // Use TargetNodeLabel if it's not null or empty
                if (!string.IsNullOrEmpty(node.TargetNodeLabel))
                {
                    if (EntryMatchesTargetName(node, nodeLabel))
                    {
                        _logger.LogDebug($"SelectListPrompt: Matched on {node.TargetNodeLabel} for option ({node.SelectedOptionText})", LoggerType.HardcorePrompt);
                        _clientConfigs.LastSelectedListNode = node;
                        return index;
                    }
                }
                // Fallback to using LastSeenNodeName if TargetNodeLabel is not provided
                else if (!string.IsNullOrEmpty(_clientConfigs.LastSeenNodeName))
                {
                    if (string.Equals(node.TargetNodeName, _clientConfigs.LastSeenNodeName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug($"SelectListPrompt: Matched on node name for option ({node.SelectedOptionText})", LoggerType.HardcorePrompt);
                        _clientConfigs.LastSelectedListNode = node;
                        return index;
                    }
                }
            }
            else
            {
                _logger.LogDebug("SelectListPrompt: Matched on " + node.TargetNodeLabel + " for option (" + node.SelectedOptionText + ")", LoggerType.HardcorePrompt);
                _clientConfigs.LastSelectedListNode = node;
                return index;
            }
        }
        return null;
    }
    protected unsafe void SetupOnItemSelectedHook(PopupMenu* popupMenu)
    {
        if (onItemSelectedHook != null) return;

        var onItemSelectedAddress = (nint)popupMenu->VirtualTable->ReceiveEvent;
        onItemSelectedHook = _gameInterop.HookFromAddress<AddonReceiveEventDelegate>(onItemSelectedAddress, OnItemSelectedDetour);
        onItemSelectedHook.Enable();
    }

    // We dont technically need this if we dont need to store the list selection in memory but yeah idk its whatever.
    private unsafe nint OnItemSelectedDetour(AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData)
    {
        _logger.LogDebug($"PopupMenu RCV: listener={onItemSelectedHook?.Address} " +
            $"{(nint)self:X}, type={eventType}, param={eventParam}, input={inputData[0]:X16} {inputData[1]:X16} {inputData[2]:X16} {(int)inputData[2]}");
        try
        {
            var target = _targets.Target;
            var targetName = _clientConfigs.LastSeenNodeName = target != null ? target.Name.ExtractText() : string.Empty;
            _clientConfigs.LastSeenListSelection = _clientConfigs.LastSeenListEntries[(int)inputData[2]].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Don't crash the game.\n{ex}");
        }
        return onItemSelectedHook.Original(self, eventType, eventParam, eventData, inputData);
    }

    private unsafe string?[] GetEntryTexts(PopupMenu* popupMenu)
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

    #region Matching

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
        => node.IsTextRegex && (node.TextRegex?.IsMatch(text) ?? false) 
        || !node.IsTextRegex && text.Contains(node.SelectedOptionText);

    private static bool EntryMatchesTargetName(TextEntryNode node, string targetNodeLabel)
        => node.TargetNodeLabelIsRegex && (node.TargetNodeTextRegex?.IsMatch(targetNodeLabel) ?? false) 
        || !node.TargetNodeLabelIsRegex && targetNodeLabel.Contains(node.TargetNodeLabel);

    #endregion
}
