using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Mediator;

namespace FFStreamViewer.WebAPI.PlayerData.Handlers;
public sealed class GameObjectHandler : DisposableMediatorSubscriberBase
{
    private readonly OnFrameworkService _frameworkUtil; // for method helpers handled on the game's framework thread.
    private readonly Func<IntPtr> _getAddress;          // for getting the address of the object.
    private readonly bool _isOwnedObject;               // for checking if the object is owned by the handler.

    public GameObjectHandler(ILogger<GameObjectHandler> logger, GagspeakMediator mediator,
        OnFrameworkService frameworkUtil, Func<IntPtr> getAddress, bool ownedObject = true) : base(logger, mediator)
    {
        _frameworkUtil = frameworkUtil;
        _getAddress = () =>
        {
            _frameworkUtil.EnsureIsOnFramework();
            return getAddress.Invoke();
        };
        _isOwnedObject = ownedObject;
        Name = string.Empty;

        // this might require a bit of expansion into the cache creation service, but only if what we have now isnt enough.
        Mediator.Publish(new GameObjectHandlerCreatedMessage(this, _isOwnedObject));

    }

    // this is very likely going to go wrong since there is an
    // address updater inside of the updateobject method we erased.
    // (im certain it will, but i just want to see SOMETHING happen right now)
    public IntPtr Address { get; private set; }
    public string Name { get; private set; }

    public void Invalidate()
    {
        Address = IntPtr.Zero;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Mediator.Publish(new GameObjectHandlerDestroyedMessage(this, _isOwnedObject));
    }
}
