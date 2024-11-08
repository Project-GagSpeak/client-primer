using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Localization;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagSpeak.WebAPI.Utils;
using Microsoft.Extensions.Hosting;

namespace GagSpeak.UpdateMonitoring;

/// <summary>
/// The service responsible for handling framework updates and other Dalamud related services.
/// </summary>
public class GagSpeakLoc : IDisposable, IHostedService
{
    private readonly ILogger<GagSpeakLoc> _logger;
    private readonly Dalamud.Localization _localization;
    private readonly IDalamudPluginInterface _pi;

    public GagSpeakLoc(ILogger<GagSpeakLoc> logger, Dalamud.Localization localization, 
        IDalamudPluginInterface pi)
    {
        _logger = logger;
        _localization = localization;
        _pi = pi;

        // set up initial localization
        _localization.SetupWithLangCode(_pi.UiLanguage);

        // subscribe to any localization changes.
        _pi.LanguageChanged += LoadLocalization;
    }

    public void Dispose()
    {
        _pi.LanguageChanged -= LoadLocalization;
    }

    private void LoadLocalization(string languageCode)
    {
        _logger.LogInformation($"Loading Localization for {languageCode}");
        _localization.SetupWithLangCode(languageCode);

        GSLoc.ReInitialize();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting GagSpeak Localization Service.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping GagSpeak Localization Service.");
        return Task.CompletedTask;
    }
}
