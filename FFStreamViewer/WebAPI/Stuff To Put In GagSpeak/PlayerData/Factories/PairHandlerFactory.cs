using FFStreamViewer.WebAPI.Services;
using Gagspeak.API.Dto.User;
using FFStreamViewer.WebAPI.Interop.Ipc;
using FFStreamViewer.WebAPI.PlayerData.Handlers;
using FFStreamViewer.WebAPI.Services.Mediator;
using Microsoft.Extensions.Hosting;

namespace FFStreamViewer.WebAPI.PlayerData.Factories;

public class PairHandlerFactory
{
    private readonly OnFrameworkService _frameworkUtilService;
    private readonly GameObjectHandlerFactory _gameObjectHandlerFactory;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IpcManager _ipcManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly GagspeakMediator _GagspeakMediator;

    public PairHandlerFactory(ILoggerFactory loggerFactory, GameObjectHandlerFactory gameObjectHandlerFactory, 
        IpcManager ipcManager, OnFrameworkService OnFrameworkService, IHostApplicationLifetime hostApplicationLifetime, 
        GagspeakMediator GagspeakMediator)
    {
        _loggerFactory = loggerFactory;
        _gameObjectHandlerFactory = gameObjectHandlerFactory;
        _ipcManager = ipcManager;
        _frameworkUtilService = OnFrameworkService;
        _hostApplicationLifetime = hostApplicationLifetime;
        _GagspeakMediator = GagspeakMediator;
    }

    /// <summary> This create method in the pair handler factory will create a new pair handler object.</summary>
    /// <param name="onlineUserIdentDto">The online user to create a pair handler for</param>
    /// <returns> A new PairHandler object </returns>
    public PairHandler Create(OnlineUserIdentDto onlineUserIdentDto)
    {
        return new PairHandler(_loggerFactory.CreateLogger<PairHandler>(), onlineUserIdentDto, _gameObjectHandlerFactory,
            _ipcManager, _frameworkUtilService, _hostApplicationLifetime, _GagspeakMediator);
    }
}
