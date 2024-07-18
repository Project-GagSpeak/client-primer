using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.Services.ConfigurationServices;
using System.CodeDom;
using Newtonsoft.Json;

namespace GagSpeak.GagspeakConfiguration;
/// <summary>
/// An abstract class that provides a base for configuration services.
/// <para> This is the part of the configurations that all configs are linked to, so it handles serialization deserialization and saving.</para>
/// </summary>
public abstract class ConfigurationServiceBase<T> : IDisposable where T : IGagspeakConfiguration
{
    private readonly CancellationTokenSource _periodicCheckCts = new(); // cancellation token source for periodic checks
    protected bool _configIsDirty = false;    // if the config is dirty
    protected DateTime _configLastWriteTime; // last write time
    private Lazy<T> _currentConfigInternal; // current config

    protected ConfigurationServiceBase(string configurationDirectory)
    {
        ConfigurationDirectory = configurationDirectory;

        _ = Task.Run(CheckForConfigUpdatesInternal, _periodicCheckCts.Token);
        _ = Task.Run(CheckForDirtyConfigInternal, _periodicCheckCts.Token);

        _currentConfigInternal = LazyConfig();
    }

    public string ConfigurationDirectory { get; init; }
    public T Current => _currentConfigInternal.Value;
    protected abstract string ConfigurationName { get; }
    protected string ConfigurationPath => Path.Combine(ConfigurationDirectory, ConfigurationName);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void Save()
    {
        _configIsDirty = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        _periodicCheckCts.Cancel();
        _periodicCheckCts.Dispose();
        if (_configIsDirty) SaveDirtyConfig();
    }

    protected virtual T LoadConfig()
    {
        T? config;
        if (!File.Exists(ConfigurationPath))
        {
            config = (T)Activator.CreateInstance(typeof(T))!;
            Save();
        }
        else
        {
            try
            {
                string json = File.ReadAllText(ConfigurationPath);
                config = JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings()
                {
                    Converters = new List<JsonConverter> { new EquipItemConverter() },
                });
            }
            catch(Exception ex)
            {
                throw new Exception($"Failed to load {ConfigurationName} configuration. {ex.StackTrace}");
                // config failed to load for some reason
                config = default;
            }
            if (config == null)
            {
                config = (T)Activator.CreateInstance(typeof(T))!;
                Save();
            }
        }

        _configLastWriteTime = GetConfigLastWriteTime();
        return config;
    }

    protected virtual void SaveDirtyConfig()
    {
        _configIsDirty = false;
        var existingConfigs = Directory.EnumerateFiles(ConfigurationDirectory, ConfigurationName + ".bak.*").Select(c => new FileInfo(c))
            .OrderByDescending(c => c.LastWriteTime).ToList();
        if (existingConfigs.Skip(3).Any())
        {
            foreach (var config in existingConfigs.Skip(3).ToList())
            {
                config.Delete();
            }
        }

        try
        {
            File.Copy(ConfigurationPath, ConfigurationPath + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: true);
        }
        catch
        {
            // ignore if file cannot be backupped once
        }
        var temp = ConfigurationPath + ".tmp";
        string json = JsonConvert.SerializeObject(Current, Formatting.Indented, new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new EquipItemConverter() }
        });
        File.WriteAllText(temp, json);
        File.Move(temp, ConfigurationPath, true);
        _configLastWriteTime = new FileInfo(ConfigurationPath).LastWriteTimeUtc;
    }

    private async Task CheckForConfigUpdatesInternal()
    {
        while (!_periodicCheckCts.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), _periodicCheckCts.Token).ConfigureAwait(false);

            var lastWriteTime = GetConfigLastWriteTime();
            if (lastWriteTime != _configLastWriteTime)
            {
                _currentConfigInternal = LazyConfig();
            }
        }
    }

    private async Task CheckForDirtyConfigInternal()
    {
        while (!_periodicCheckCts.IsCancellationRequested)
        {
            if (_configIsDirty)
            {
                SaveDirtyConfig();
            }

            await Task.Delay(TimeSpan.FromSeconds(1), _periodicCheckCts.Token).ConfigureAwait(false);
        }
    }

    protected DateTime GetConfigLastWriteTime() => new FileInfo(ConfigurationPath).LastWriteTimeUtc;

    private Lazy<T> LazyConfig()
    {
        _configLastWriteTime = GetConfigLastWriteTime();
        return new Lazy<T>(LoadConfig);
    }
}
