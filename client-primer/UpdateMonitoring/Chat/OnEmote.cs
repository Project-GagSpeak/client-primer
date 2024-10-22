using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using GagSpeak.Utils;

// References for Knowledge
// https://github.com/MgAl2O4/PatMeDalamud/blob/42385a92d1a9c3f043f35128ee68dc623cfc6a20/plugin/EmoteReaderHooks.cs#L21C87-L21C130

namespace GagSpeak.UpdateMonitoring.Triggers;
public class OnEmote : IDisposable
{
    private readonly ILogger<OnEmote> _logger;
    private readonly OnFrameworkService _frameworkUtils;
    private static class Signatures
    {
        public const string OnEmoteDetour = "40 53 56 41 54 41 57 48 83 EC ?? 48 8B 02";
    }

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong emoteCallerAddr, ushort emoteId, ulong targetId, ulong unk2);
    internal static Hook<OnEmoteFuncDelegate> ProcessEmoteHook = null!;
    public OnEmote(ILogger<OnEmote> logger, OnFrameworkService frameworkUtils,
        ISigScanner sigScanner, IGameInteropProvider interopProvider)
    {
        _logger = logger;
        _frameworkUtils = frameworkUtils;
        interopProvider.InitializeFromAttributes(this);

        ProcessEmoteHook = interopProvider.HookFromSignature<OnEmoteFuncDelegate>(Signatures.OnEmoteDetour, OnEmoteDetour);
        EnableHook();
        _logger.LogInformation("Started EmoteDetour", LoggerType.ChatDetours);
    }

    public void EnableHook()
    {
        if (ProcessEmoteHook.IsEnabled) return;
        _logger.LogInformation("Enabling EmoteDetour", LoggerType.ChatDetours);
        ProcessEmoteHook.Enable();
    }

    public void DisableHook()
    {
        if (!ProcessEmoteHook.IsEnabled) return;
        _logger.LogInformation("Disabling EmoteDetour", LoggerType.ChatDetours);
        ProcessEmoteHook.Disable();
    }

    public void Dispose()
    {
        _logger.LogInformation("Stopping EmoteDetour", LoggerType.ChatDetours);
        try
        {
            if (ProcessEmoteHook.IsEnabled) DisableHook();
            ProcessEmoteHook.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error disposing of EmoteDetour");
        }
        _logger.LogInformation("Stopped EmoteDetour", LoggerType.ChatDetours);
    }

    async void OnEmoteDetour(ulong unk, ulong emoteCallerAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            await _frameworkUtils.RunOnFrameworkThread(() =>
            {
                var emoteCaller = _frameworkUtils.CreateGameObject((nint)emoteCallerAddr);
                var emoteCallerName = (emoteCaller as IPlayerCharacter)?.GetNameWithWorld() ?? "No Player Was Emote Caller";
                var emoteName = _frameworkUtils.GetEmoteName(emoteId);
                var targetObj = (_frameworkUtils.SearchObjectTableById((uint)targetId));
                var targetName = (targetObj as IPlayerCharacter)?.GetNameWithWorld() ?? "No Player Was Target";
                _logger.LogTrace("OnEmote >> [" + emoteCallerName + "] used Emote [" + emoteName + "](ID:"+emoteId+") on Target: [" + targetName+"]", LoggerType.ChatDetours);

                UnlocksEventManager.AchievementEvent(UnlocksEvent.EmoteExecuted, emoteCaller, emoteId, targetObj);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in EmoteDetour");
        }
        ProcessEmoteHook.Original(unk, emoteCallerAddr, emoteId, targetId, unk2);
    }
}
