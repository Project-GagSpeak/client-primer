using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Configuration;
using OtterGui.Classes;
using OtterGui.Widgets;
using Dalamud.Interface.Windowing;
using FFStreamViewer.UI;
using FFStreamViewer.Services;
using Newtonsoft.Json;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace FFStreamViewer;

/// <summary> The configuration for the FFStreamViewer plugin. </summary>
public class FFStreamViewerConfig : IPluginConfiguration, ISavable
{   
    // Plugin information
    public          ChangeLogDisplayType    ChangeLogDisplayType { get; set; } = ChangeLogDisplayType.New;
    public          int                     LastSeenVersion { get; set; } = FFStreamViewerChangelog.LastChangelogVersion;   // look more into ottergui to figure out how to implement this later.
    public          int                     Version { get; set; } = 0;                                                      // Version of the plugin
    public          bool                    FreshInstall { get; set; } = true;                                              // Is user on a fresh install?
    public          bool                    Enabled { get; set; } = true;                                                   // Is plugin enabled?  
    public          MainWindow.TabType      SelectedTab { get; set; } = MainWindow.TabType.Media;                           // Default to the general tab
    // variables involved with saving and updating the config
    private readonly SaveService            _saveService;                                                                   // Save service for the FFStreamViewer plugin
    
    /// <summary> Gets or sets the colors used within our UI </summary>
    public Dictionary<ColorId, uint> Colors { get; private set; }
        = Enum.GetValues<ColorId>().ToDictionary(c => c, c => c.Data().DefaultColor);

    /// <summary> Initializes a new instance of the <see cref="FFStreamViewerConfig"/> class!
    /// <list type="bullet">
    /// <item><c>saveService</c><param name="saveService"> - The save service.</param></item>
    /// <item><c>migrator</c><param name="migrator"> - The config migrator.</param></item></list></summary>
    public FFStreamViewerConfig(SaveService saveService, ConfigMigrationService migrator) {
        _saveService = saveService;
        Load(migrator);
        FFStreamViewer.Log.Debug("[Configuration File] Constructor Finished Initializing. Previous data restored.");
    }

    /// <summary> Saves the config to our save service and updates the garble level to its new value. </summary>
    public void Save() {
        // initialize save service
        _saveService.DelaySave(this);
    }

    /// <summary> Loads the config from our save service and migrates it if necessary.
    /// <list type="bullet">
    /// <item><c>migrator</c><param name="migrator"> - The config migrator.</param></item></list></summary>
    /// <returns>The migrated config.</returns>
    public void Load(ConfigMigrationService migrator) {
        // Handle deserialization errors
        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs) {
            FFStreamViewer.Log.Error( $"[Config]: Error parsing Configuration at {errorArgs.ErrorContext.Path}, using default or migrating:\n{errorArgs.ErrorContext.Error}");
            errorArgs.ErrorContext.Handled = true;
        }
        // If the config file does not exist, return
        if (!File.Exists(_saveService.FileNames.ConfigFile))
            return;
        // Otherwise, load the config
        if (File.Exists(_saveService.FileNames.ConfigFile))
            // try to load the config
            try {
                var text = File.ReadAllText(_saveService.FileNames.ConfigFile);
                JsonConvert.PopulateObject(text, this, new JsonSerializerSettings {
                    Error = HandleDeserializationError,
                });
            }
            catch (Exception ex) {
                // If there is an error, log it and revert to default
                FFStreamViewer.Messager.NotificationMessage(ex,
                    "Error reading Configuration, reverting to default.\nYou may be able to restore your configuration using the rolling backups in the XIVLauncher/backups/FFStreamViewer directory.",
                    "Error reading Configuration", NotificationType.Error);
            }
        // Migrate the config
        migrator.Migrate(this);
    }

    /// <summary> Gets the filename for the config file.
    /// <list type="bullet">
    /// <item><c>fileNames</c><param name="fileNames"> - The file names service.</param></item></list></summary>
    public string ToFilename(FilenameService fileNames)
        => fileNames.ConfigFile;

    /// <summary> Save the config to a file.
    /// <list type="bullet">
    /// <item><c>writer</c><param name="writer"> - The writer to write to.</param></item></list></summary>
    public void Save(StreamWriter writer) {
        using var jWriter    = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        var       serializer = new JsonSerializer { Formatting         = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }

    /// <summary> a very small class that gets the current version of the config save file. </summary>
    public static class Constants {
        public const int CurrentVersion = 4;
    }
}