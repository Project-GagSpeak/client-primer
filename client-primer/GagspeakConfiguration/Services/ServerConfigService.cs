using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using Newtonsoft.Json.Linq;

namespace GagSpeak.GagspeakConfiguration;

public class ServerConfigService : ConfigurationServiceBase<ServerConfig>
{
    public const string ConfigName = "server.json";
    public const bool PerCharacterConfig = false;
    public ServerConfigService(string configDir) : base(configDir) { }

    protected override bool PerCharacterConfigPath => PerCharacterConfig;
    protected override string ConfigurationName => ConfigName;

    // apply an override for migrations off the baseconfigservice
    protected override JObject MigrateConfig(JObject oldConfigJson, int readVersion)
    {
        JObject newConfigJson;
        // if migrating from any version less than 2, to 2
        if (readVersion == 1)
        {
            newConfigJson = MigrateFromV1toV2(oldConfigJson);
        }
        else
        {
            // no migration needed
            newConfigJson = oldConfigJson;
        }
        return newConfigJson;
    }

    // Safely update data for new format.
    private JObject MigrateFromV1toV2(JObject oldConfigJson)
    {
        // create a new JObject to store the new config
        JObject newConfigJson = new();
        // set the version to 2
        newConfigJson["Version"] = 2;

        // grab the old config json server storage
        JObject? oldServerStorage = oldConfigJson["ServerStorage"] as JObject;

        // OldConfig's ServerStorage contained a Dictionary<int, SecretKey> of secret keys. Obtain it.
        JObject? oldSecretKeys = oldServerStorage?["SecretKeys"] as JObject;
        // also grab the old authentications
        JArray? oldAuthentications = oldServerStorage?["Authentications"] as JArray;

        // for each authentication in the old authentications, add it to the new authentications Jarray
        var newAuthentications = new JArray();
        if (oldAuthentications != null)
        {
            foreach (var oldAuthentication in oldAuthentications)
            {
                // obtain the secretkeyidx from the old auth
                int secretKeyIdx = oldAuthentication["SecretKeyIdx"]?.Value<int>() ?? 1;
                // fetch the secret key object from the old dictionary that matches it
                var secretKeyObject = oldSecretKeys?[secretKeyIdx.ToString()] as JObject;
                // construct a new JObject for the new authentication
                newAuthentications.Add(new JObject
                {
                    ["CharacterPlayerContentId"] = 0,
                    ["CharacterName"] = oldAuthentication["CharacterName"]!,
                    ["WorldId"] = oldAuthentication["WorldId"] ?? 0,
                    ["IsPrimary"] = true, // we can safely assume this list only has one item from v1 to v2
                    ["SecretKey"] = new JObject
                    {
                        ["Label"] = secretKeyObject?["FriendlyName"]?.Value<string>() ?? string.Empty,
                        ["Key"] = secretKeyObject?["Key"]?.Value<string>() ?? string.Empty
                    }
                });
            }
        }

        // create a new server storage object
        newConfigJson["ServerStorage"] = new JObject
        {
            ["Authentications"] = newAuthentications,
            ["FullPause"] = oldServerStorage?["FullPause"] ?? false,
            ["ToyboxFullPause"] = oldServerStorage?["ToyboxFullPause"] ?? false,
            ["ServerName"] = oldServerStorage?["ServerName"]!, // always initialized, always true
            ["ServiceUri"] = oldServerStorage?["ServiceUri"]! // always initialized, always true
        };

        // return the new config json
        return newConfigJson;
    }
}
