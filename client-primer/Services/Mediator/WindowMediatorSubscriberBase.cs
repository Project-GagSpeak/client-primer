using Dalamud.Interface.Windowing;

namespace GagSpeak.Services.Mediator;

public abstract class WindowMediatorSubscriberBase : Window, IMediatorSubscriber, IDisposable
{
    // a performacne collector was here but I have removed it as i dont see the purpose for it yet.
    protected readonly ILogger _logger;

    protected WindowMediatorSubscriberBase(ILogger logger, GagspeakMediator mediator, string name) : base(name)
    {
        _logger = logger;
        Mediator = mediator;
        _logger.LogTrace("Creating {type}", GetType());

        // subscribe to the UI toggle message???? (likely dont need and is respective to gagspeak)
        Mediator.Subscribe<UiToggleMessage>(this, (msg) =>
        {
            if (msg.UiType == GetType())
            {
                Toggle();
            }
        });
    }

    // the gagspeak mediator
    public GagspeakMediator Mediator { get; }

    /// <summary>
    /// Properly dispose of the mediator object
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A draw method that logs performance, but since we have no performance logger this shouldnt be needed
    /// </summary>
    public override void Draw()
    {
        DrawInternal();
    }

    /// <summary>
    /// Same reasoning above follows here
    /// </summary>
    protected abstract void DrawInternal();

    /// <summary>
    /// All mediators require a startasync and stopasync method. This calls the stopasync method at the base.
    /// The startasync will be in the main GagspeakMediator
    /// </summary>
    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual void Dispose(bool disposing)
    {
        _logger.LogTrace("Disposing {type}", GetType());

        Mediator.UnsubscribeAll(this);
    }
}
