using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Comparer;
using GagspeakAPI.Dto.User;

namespace GagSpeak.Services;

public class ProfileService : MediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly ProfileFactory _profileFactory;

    // concurrent dictionary of cached profile data.
    private readonly ConcurrentDictionary<UserData, GagspeakProfile> _gagspeakProfiles = new(UserDataComparer.Instance);

    public ProfileService(ILogger<ProfileService> logger,
        GagspeakMediator mediator, ApiController apiController, 
        ProfileFactory profileFactory) : base(logger, mediator)
    {
        _apiController = apiController;
        _profileFactory = profileFactory;

        // Clear profiles when called.
        Mediator.Subscribe<ClearProfileDataMessage>(this, (msg) =>
        {
            // if UserData exists, clear the profile, otherwise, clear whole cache and reload things again.
            if (msg.UserData != null)
            {
                RemoveGagspeakProfile(msg.UserData);
            }
            else
            {
                ClearAllProfiles();
            }
        });

        // Clear all profiles on disconnect
        Mediator.Subscribe<DisconnectedMessage>(this, (_) => ClearAllProfiles());
    }

    /// <summary> Get the Gagspeak Profile data for a user. </summary>
    public GagspeakProfile GetGagspeakProfile(UserData userData)
    {
        // Locate the profile data for the pair.
        if (!_gagspeakProfiles.TryGetValue(userData, out var profile))
        {
            Logger.LogTrace("Profile for {user} not found, creating loading profile.", userData);
            // If not found, create a loading profile template for the user,
            AssignLoadingProfile(userData);
            // then run a call to the GetGagspeakProfile API call to fetch it.
            _ = Task.Run(() => GetGagspeakProfileFromService(userData));
            // in the meantime, return the loading profile data
            // (it will have the loadingProfileState at this point)
            return _gagspeakProfiles[userData]; 
        }

        // Profile found, so return it.
        return (profile);
    }

    private void AssignLoadingProfile(UserData data)
    {
        // add the user & profile data to the concurrent dictionary.
        _gagspeakProfiles[data] = _profileFactory.CreateProfileData(false, string.Empty, string.Empty);
        Logger.LogTrace("Assigned new profile for {user}", data);
    }

    public void RemoveGagspeakProfile(UserData userData)
    {
        Logger.LogInformation("Removing profile for {user}", userData);
        // remove them from the dictionary.
        _gagspeakProfiles.Remove(userData, out _);
    }

    public void ClearAllProfiles()
    {
        Logger.LogInformation("Clearing all profiles.");
        // clear the dictionary.
        _gagspeakProfiles.Clear();
    }

    // This fetches the profile data and assigns it. Only updated profiles are cleared, so this is how we grab initial data.
    private async Task GetGagspeakProfileFromService(UserData data)
    {
        try
        {
            Logger.LogTrace("Fetching profile for {user}", data);
            // Fetch userData profile info from server
            var profile = await _apiController.UserGetProfile(new UserDto(data)).ConfigureAwait(false);

            // apply the retrieved profile data to the profile object.
            _gagspeakProfiles[data].Flagged = profile.Disabled;
            _gagspeakProfiles[data].Base64ProfilePicture = profile.ProfilePictureBase64;
            _gagspeakProfiles[data].Description = (string.IsNullOrEmpty(profile.Description) ? "No Description Set." : profile.Description);
            Logger.LogDebug("Profile for {user} loaded.", data);
        }
        catch (Exception ex)
        {
            // log the failure and set default data.
            Logger.LogWarning(ex, "Failed to get Profile from service for user {user}", data);
            _gagspeakProfiles[data].Flagged = false;
            _gagspeakProfiles[data].Base64ProfilePicture = string.Empty;
            _gagspeakProfiles[data].Description = "Failed to load profile data.";
        }
    }
}
