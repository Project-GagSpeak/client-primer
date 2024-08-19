using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GagSpeak.Interop.Ipc;

/// <summary>
/// The IPC Provider for GagSpeak to interact with other plugins by sharing information about visible players.
/// </summary>
public class IpcProvider : IHostedService, IMediatorSubscriber
{
    private const int GagspeakApiVersion = 1;

    private readonly ILogger<IpcProvider> _logger;
    private readonly ApiController _apiController;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly IDalamudPluginInterface _pi;

    private readonly List<GameObjectHandler> VisiblePairObjects = [];

    /// <summary>
    /// Stores the list of handled players by the GagSpeak plugin.
    /// <para> String Stored is in format [Player Name@World] </para>
    /// THE ABOVE IS A TODO. For now, for the sake of sanity, use address.
    /// </summary>
    private ICallGateProvider<List<nint>>? _handledVisiblePairs;

    /// <summary>
    /// Obtains an ApplyStatusToPair message from Moodles, and invokes the update to the player if permissions allow it.
    /// <para> THIS WILL NOT WORK IF THE PAIR HAS NOT GIVEN YOU PERMISSION TO APPLY </para>
    /// </summary>
    private ICallGateProvider<string, string, List<MoodlesStatusInfo>, object?>? _applyStatusesToPairRequest;

    public GagspeakMediator Mediator { get; init; }

    public IpcProvider(ILogger<IpcProvider> logger, GagspeakMediator mediator,
        ApiController apiController, OnFrameworkService frameworkUtils,
        IDalamudPluginInterface pi)
    {
        _logger = logger;
        _apiController = apiController;
        _frameworkUtils = frameworkUtils;
        _pi = pi;
        Mediator = mediator;

        Mediator.Subscribe<GameObjectHandlerCreatedMessage>(this, (msg) =>
        {
            if (msg.OwnedObject) return;
            VisiblePairObjects.Add(msg.GameObjectHandler);
        });
        Mediator.Subscribe<GameObjectHandlerDestroyedMessage>(this, (msg) =>
        {
            if (msg.OwnedObject) return;
            VisiblePairObjects.Remove(msg.GameObjectHandler);
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting IpcProviderService");

        _handledVisiblePairs = _pi.GetIpcProvider<List<nint>>("GagSpeak.GetHandledVisiblePairs");
        _handledVisiblePairs.RegisterFunc(GetHandledAddresses);

        // Register our action.
        _applyStatusesToPairRequest = _pi.GetIpcProvider<string, string, List<MoodlesStatusInfo>, object?>("GagSpeak.ApplyStatusesToPairRequest");
        _applyStatusesToPairRequest.RegisterAction(HandleApplyStatusesToPairRequest);


        _logger.LogInformation("Started IpcProviderService");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping IpcProvider Service");

        _handledVisiblePairs?.UnregisterFunc();

        Mediator.UnsubscribeAll(this);

        return Task.CompletedTask;
    }

    private List<nint> GetHandledAddresses()
        => VisiblePairObjects.Where(g => g.Address != nint.Zero).Select(g => g.Address).Distinct().ToList();

    /// <summary>
    /// Handles the request from our clients moodles plugin to update another one of our pairs status.
    /// </summary>
    /// <param name="requester">The name of the player requesting the apply (SHOULD ALWAYS BE OUR CLIENT PLAYER) </param>
    /// <param name="recipient">The name of the player to apply the status to. (SHOULD ALWAYS BE A PAIR) </param>
    /// <param name="statuses">The list of statuses to apply to the recipient. </param>
    private void HandleApplyStatusesToPairRequest(string requester, string recipient, List<MoodlesStatusInfo> statuses)
    {
        // print a warning if the recipient is not equal to our current player

        // use linQ to iterate through the handled visible game objects to find the object that is an owned object, and compare its NameWithWorld to the recipient.
        var requesterObject = VisiblePairObjects.FirstOrDefault(g => g.IsOwnedObject && g.NameWithWorld == requester);
        if (requesterObject == null)
        {
            _logger.LogWarning("Received ApplyStatusesToPairRequest for {requester} but could not find the requester", requester);
            return;
        }

        // we should throw a warning and return if the requester is not a visible pair.
        var recipientObject = VisiblePairObjects.FirstOrDefault(g => g.NameWithWorld == recipient);
        if (recipientObject == null)
        {
            _logger.LogWarning("Received ApplyStatusesToPairRequest for {recipient} but could not find the recipient", recipient);
            return;
        }

        // finally, assuming we have these, we need to check if they have the proper permissions to apply.
        // For now, we will ignore permissions and apply regardless.

    }
}

