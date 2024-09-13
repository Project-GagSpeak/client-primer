using GagSpeak.GagspeakConfiguration;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Routes;
using System.Net;
using System.Text.Json;
using SysJsonSerializer = System.Text.Json.JsonSerializer;

namespace GagSpeak.WebAPI;

public sealed class PiShockProvider : IDisposable
{
    private readonly ILogger<PiShockProvider> Logger;
    private readonly GagspeakConfigService _mainConfig;
    private readonly HttpClient _httpClient;

    public PiShockProvider(ILogger<TokenProvider> logger, GagspeakConfigService mainConfig)
    {
        _mainConfig = mainConfig;
        _httpClient = new HttpClient();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    // grab basic information from shock collar.
    private StringContent CreateGetInfoContent(string shareCode)
    {
        StringContent content = new(SysJsonSerializer.Serialize(new
        {
            UserName = _mainConfig.Current.PiShockUsername,
            Code = shareCode,
            Apikey = _mainConfig.Current.PiShockApiKey,
        }), Encoding.UTF8, "application/json");
        return content;
    }

    // For grabbing boolean permissions from a share code.
    private StringContent CreateDummyExecuteContent(string shareCode, int opCode)
    {
        StringContent content = new(SysJsonSerializer.Serialize(new
        {
            Username = _mainConfig.Current.PiShockUsername,
            Name = "GagSpeakProvider",
            Op = opCode,
            Intensity = 0,
            Duration = 0,
            Code = shareCode,
            Apikey = _mainConfig.Current.PiShockApiKey,
        }), Encoding.UTF8, "application/json");
        return content;
    }

    // Sends operation to shock collar
    private StringContent CreateExecuteOperationContent(string shareCode, int opCode, int intensity, int duration)
    {
        StringContent content = new(SysJsonSerializer.Serialize(new
        {
            Username = _mainConfig.Current.PiShockUsername,
            Name = "GagSpeakProvider",
            Op = opCode,
            Intensity = intensity,
            Duration = duration,
            Code = shareCode,
            Apikey = _mainConfig.Current.PiShockApiKey,
        }), Encoding.UTF8, "application/json");
        return content;
    }

    public async Task<PiShockPermissions> GetPermissionsFromCode(string shareCode)
    {
        try
        {
            var jsonContent = CreateGetInfoContent(shareCode);

            Logger.LogTrace("PiShock Request Info URI Firing: {piShockUri}", GagspeakPiShock.GetInfoPath());
            var response = await _httpClient.PostAsync(GagspeakPiShock.GetInfoPath(), jsonContent).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Logger.LogError("Error getting PiShock permissions from share code. Status returned: " + response.StatusCode);
                return new(false, false, false, -1, -1);
            }
            else
            {
                Logger.LogTrace("PiShock Request Info Response: {response}", response);
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonDocument = JsonDocument.Parse(content);
                var root = jsonDocument.RootElement;

                int maxIntensity = root.GetProperty("maxIntensity").GetInt32();
                int maxShockDuration = root.GetProperty("maxDuration").GetInt32();

                Logger.LogTrace("Obtaining boolean values by passing dummy requests to share code");
                return await ConstructPermissionObject(shareCode, maxIntensity, maxShockDuration);
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Error getting PiShock permissions from share code");
            return new(false,false,false,-1,-1);
        }
    }

    private async Task<PiShockPermissions> ConstructPermissionObject(string shareCode, int intensityLimit, int durationLimit)
    {
        // Shock, Vibrate, Beep. In that order
        int[] opCodes = { 0, 1, 2 };
        bool shocks = false;
        bool vibrations = false;
        bool beeps = false;

        try
        {
            foreach (var opCode in opCodes)
            {
                var jsonContent = CreateDummyExecuteContent(shareCode, opCode);
                var response = await _httpClient.PostAsync(GagspeakPiShock.ExecuteOperationPath(), jsonContent).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK) continue;

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                // TODO: Fix how this is parsed from the return code to mark the boolean as true
                // only if the response "Operation Attempted" is returned.
                var jsonDocument = JsonDocument.Parse(content);
                var root = jsonDocument.RootElement;

                switch (opCode)
                {
                    case 0: shocks = root.GetProperty("AllowShocks").GetBoolean(); break;
                    case 1: vibrations = root.GetProperty("AllowVibrations").GetBoolean(); break;
                    case 2: beeps = root.GetProperty("AllowBeeps").GetBoolean(); break;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Error executing operation on PiShock");
        }

        return new PiShockPermissions(shocks, vibrations, beeps, intensityLimit, durationLimit);
    }


    private async void ExecuteOperation(string shareCode, int opCode, int intensity, int duration)
    {
        try
        {
            var jsonContent = CreateExecuteOperationContent(shareCode, opCode, intensity, duration);
            var response = await _httpClient.PostAsync(GagspeakPiShock.ExecuteOperationPath(), jsonContent).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Logger.LogError("Error executing operation on PiShock. Status returned: " + response.StatusCode);
                return;
            }
            var contentStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Logger.LogDebug("PiShock Request Sent to Shock Collar Successfully! Content returned was:\n" + contentStr);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Error executing operation on PiShock");
        }
    }
}
