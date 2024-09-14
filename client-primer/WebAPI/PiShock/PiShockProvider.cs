using GagSpeak.GagspeakConfiguration;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Routes;
using System.Net;
using System.Text.Json;
using SysJsonSerializer = System.Text.Json.JsonSerializer;

namespace GagSpeak.WebAPI;

public sealed class PiShockProvider : IDisposable
{
    private readonly ILogger<PiShockProvider> _logger;
    private readonly GagspeakConfigService _mainConfig;
    private readonly HttpClient _httpClient;

    public PiShockProvider(ILogger<PiShockProvider> logger, GagspeakConfigService mainConfig)
    {
        _logger = logger;
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

            _logger.LogTrace("PiShock Request Info URI Firing: {piShockUri}", GagspeakPiShock.GetInfoPath());
            var response = await _httpClient.PostAsync(GagspeakPiShock.GetInfoPath(), jsonContent).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _logger.LogTrace("PiShock Request Info Response: {response}", response);
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonDocument = JsonDocument.Parse(content);
                var root = jsonDocument.RootElement;

                int maxIntensity = root.GetProperty("maxIntensity").GetInt32();
                int maxShockDuration = root.GetProperty("maxDuration").GetInt32();

                _logger.LogTrace("Obtaining boolean values by passing dummy requests to share code");
                var result = await ConstructPermissionObject(shareCode, maxIntensity, maxShockDuration);
                _logger.LogTrace("PiShock Permissions obtained: {result}", result);
                return result;
            }
            else if(response.StatusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogWarning("The Credentials for your API Key and Username do not match any profile in PiShock");
                return new(false, false, false, -1, -1);
            }
            else
            {
                _logger.LogError("The ShareCode for this profile does not exist, or this is a simple error 404: {statusCode}", response.StatusCode);
                return new(false, false, false, -1, -1);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error getting PiShock permissions from share code");
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

                switch (opCode)
                {
                    case 0:
                        shocks = content! == "Operation Attempted."; break;
                    case 1:
                        vibrations = content! == "Operation Attempted."; break;
                    case 2:
                        beeps = content! == "Operation Attempted."; break;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error executing operation on PiShock");
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
                _logger.LogError("Error executing operation on PiShock. Status returned: " + response.StatusCode);
                return;
            }
            var contentStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogDebug("PiShock Request Sent to Shock Collar Successfully! Content returned was:\n" + contentStr);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error executing operation on PiShock");
        }
    }
}
