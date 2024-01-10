using Dalamud.Plugin;              
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Log;
using FFStreamViewer.UI;
using FFStreamViewer.UI.Tabs.MediaTab;              // Contains classes for the general tab in the FFStreamViewer plugin

// following namespace naming convention
namespace FFStreamViewer.Services;

/// <summary> This class is used to handle the services for the FFStreamViewer plugin. </summary>
public static class ServiceHandler
{
    /// <summary> Initializes a new instance of the <see cref="ServiceProvider"/> class.
    /// <list type="bullet">
    /// <item><c>pi</c><param name="pi"> - The Dalamud plugin interface.</param></item>
    /// <item><c>log</c><param name="log"> - The logger instance.</param></item>
    /// </list> </summary>
    /// <returns>The created service provider.</returns>
    public static ServiceProvider CreateProvider(DalamudPluginInterface pi, Logger log) {
        // introduce the logger to log any debug messages.
        EventWrapper.ChangeLogger(log);
        // Create a service collection (see Dalamud.cs, if confused about AddDalamud, that is what AddDalamud(pi) pulls from)
        var services = new ServiceCollection()
            .AddSingleton(log)          // Adds the logger
            .AddDalamud(pi)             // adds the dalamud services
            //.AddAudio()                 // adds the audio services
            //.AddData()                  // adds the data services
            //.AddEvent()                 // adds the event services
            //.AddLivestream()            // adds the livestream services
            .AddServiceClasses()        // adds the service classes
            .AddUi()                    // adds the UI services
            .AddApi();                  // adds the API services
        // return the built services provider in the form of a instanced service collection
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    /// <summary> Adds the Dalamud services to the service collection. </summary>
    private static IServiceCollection AddDalamud(this IServiceCollection services, DalamudPluginInterface pi) {
        // Add the dalamudservices to the service collection
        new DalamudServices(pi).AddServices(services);
        return services;
    }

    /// <summary> Adds the Audio related classes to the Chat service collection. </summary>
    //private static IServiceCollection AddAudio(this IServiceCollection services) {}

    /// <summary> Adds the data related classes to the service collection </summary>
    //private static IServiceCollection AddData(this IServiceCollection services) {}

    /// <summary> Adds the event related classes to the service collection </summary>
    //private static IServiceCollection AddEvent(this IServiceCollection services) {}

    /// <summary> Adds the core of the FFStreamViewer to the service collection </summary>
    //private static IServiceCollection AddLivestream(this IServiceCollection services) {}


    /// <summary> Adds the classes identified as self-made services for the overarching service collection. </summary>
    private static IServiceCollection AddServiceClasses(this IServiceCollection services)
        => services.AddSingleton<FrameworkManager>()
                .AddSingleton<MessageService>()
                .AddSingleton<BackupService>()
                .AddSingleton<ConfigMigrationService>()
                .AddSingleton<FilenameService>()
                .AddSingleton<SaveService>();

    /// <summary> Adds the UI related classes to the service collection. </summary>
    private static IServiceCollection AddUi(this IServiceCollection services)
        => services.AddSingleton<FFSV_WindowManager>()
            .AddSingleton<MediaTab>()
            .AddSingleton<MainWindow>()
            .AddSingleton<DebugWindow>()
            .AddSingleton<FFStreamViewerChangelog>();

    /// <summary> Adds the API services to the API service collection. </summary>
    private static IServiceCollection AddApi(this IServiceCollection services)
        => services.AddSingleton<CommandManager>();
}
