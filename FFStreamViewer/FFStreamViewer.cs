using System.Reflection;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Log;
using FFStreamViewer.UI;
using FFStreamViewer.Services;

// our main namespace for the FFStreamViewer plugin
namespace FFStreamViewer;

public class FFStreamViewer : IDalamudPlugin
{
  /// <summary> Gets the name of the plugin. </summary>
  public string Name => "FFStreamViewer";

  /// <summary> gets the version of our current plugin from the .csproj file </summary>
  public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
  
  /// <summary> initialize the logger for our plugin, which will display debug information to our /xllog in game </summary>
  public static readonly Logger Log = new(); // initialize the logger for our plugin

  /// <summary> the messager service for this plugin. A part of the otterGui services, and is used to display the changelog </summary>
  public static MessageService Messager { get; private set; } = null!; // initialize the messager service, part of otterGui services.
  
  /// <summary> the service provider for the plugin </summary>
  private readonly ServiceProvider _services; 

  /// <summary>
  /// Initializes a new instance of the <see cref="FFStreamViewer"/> class.
  /// <list type="bullet">
  /// <item><c>pluginInt</c><param name="pluginInt"> - The Dalamud plugin interface.</param></item>
  /// </list> </summary>
  public FFStreamViewer(DalamudPluginInterface pluginInt)
  {
      // Initialize the services in the large Service collection. (see ServiceHandler.cs)
      // if at any point this process fails, we should immidiately dispose of the services, throw an exception, and exit the plugin.
      try
      {
          _services = ServiceHandler.CreateProvider(pluginInt, Log); 
          Messager = _services.GetRequiredService<MessageService>();
          _services.GetRequiredService<FFSV_WindowManager>(); // Initialize the UI
          _services.GetRequiredService<CommandManager>(); // Initialize the command manager
          
          Log.Information($"FFStreamViewer v{Version} loaded successfully."); // Log the version to the /xllog menu
      }
      catch
      {
          Dispose();
          throw;
      }
  }

  /// <summary> Disposes the plugin and its services, will call upon the service collection so all services use their dispose function. </summary>
  public void Dispose()
      => _services?.Dispose(); // Dispose of all services. (call all of their dispose functions)
}