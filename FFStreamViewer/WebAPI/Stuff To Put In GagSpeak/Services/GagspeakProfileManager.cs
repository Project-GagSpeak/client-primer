using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.Services.Mediator;
using Gagspeak.API.Data;
using Gagspeak.API.Data.Comparer;

namespace FFStreamViewer.WebAPI.Services;

public class GagspeakProfileManager : MediatorSubscriberBase
{
    private const string _gagspeakLogo = "";            // the string for the gagspeak logo image
    private const string _gagspeakLogoLoading = "";     // the string for the gagspeak logo loading image
    private const string _gagspeakSupporter = "";       // the string for the gagspeak supporter image overlay
    private const string _noDescription = "-- User has no description set --";  // the string for the description of the profile that is set
    private readonly ApiController _apiController;      // the api controller so we can make calls to the server and receive calls from them
    private readonly GagspeakConfigService _gagspeakConfigService; // the gagspeak config service (for the client side config)
    private readonly ConcurrentDictionary<UserData, GagspeakProfileData> _gagspeakProfiles; // the dictionary of gagspeak profiles, with the keys being the UserData they belong to.
    // the default profile data, for when we don't have a profile for a user
    private readonly GagspeakProfileData _defaultProfileData = new(IsFlagged: false, _gagspeakLogo, string.Empty, _noDescription);
    // the loading profile data, for when we are waiting for the profile to be fetched from the server
    private readonly GagspeakProfileData _loadingProfileData = new(IsFlagged: false, _gagspeakLogoLoading, string.Empty, "Loading Data from server...");

    public GagspeakProfileManager(ILogger<GagspeakProfileManager> logger, GagspeakConfigService gagspeakConfigService,
        GagspeakMediator mediator, ApiController apiController) : base(logger, mediator)
    {
        _gagspeakConfigService = gagspeakConfigService;
        _apiController = apiController;
        _gagspeakProfiles = new(UserDataComparer.Instance);

        // subscribe to the mediator listening to whenever a profile should be cleared
        Mediator.Subscribe<ClearProfileDataMessage>(this, (msg) =>
        {
            // if the message has a user data, then we should remove that user's profile from the dictionary
            if (msg.UserData != null)
                // remove the profile from the dictionary
                _gagspeakProfiles.Remove(msg.UserData, out _);
            else
                // if it is null we have a faulty storage, so clear all profiles
                _gagspeakProfiles.Clear();
        });

        // subscribe to the disconnected message so that we know when to clear all the stored gagspeak profiles.
        Mediator.Subscribe<DisconnectedMessage>(this, (_) => _gagspeakProfiles.Clear());
    }

    /// <summary> Method to get the gagspeak profile of a paired user by their Dto </summary>
    /// <param name="data">the data of the user we want to fetch the profile from</param>
    /// <returns>A GagspeakProfileData object</returns>
    public GagspeakProfileData GetGagspeakProfile(UserData data)
    {
        // if the profile is not in the dictionary, then we need to get it from the service
        if (!_gagspeakProfiles.TryGetValue(data, out var profile))
        {
            // fetch the profile from the server, and return loading profile data display while we wait
            _ = Task.Run(() => GetGagspeakProfileFromService(data));
            // return the loading profile data
            return (_loadingProfileData);
        }

        // otherwise, we have it stored, so return it.
        return (profile);
    }

    /// <summary> Method to get the gagspeak profile of a paired user by their Dto from the server </summary>
    private async Task GetGagspeakProfileFromService(UserData data)
    {
        try
        {
            // set the profile to loading profile data
            _gagspeakProfiles[data] = _loadingProfileData;
            // fetch the user's prifile from the server
            var profile = await _apiController.UserGetProfile(new Gagspeak.API.Dto.User.UserDto(data)).ConfigureAwait(false);
            // we will create a new gagspeakprofile data object from the profile data Dto we received.
            GagspeakProfileData profileData = new(profile.Disabled,
                string.IsNullOrEmpty(profile.ProfilePictureBase64) ? _gagspeakLogo : profile.ProfilePictureBase64,
                !string.IsNullOrEmpty(data.Alias) && !string.Equals(data.Alias, data.UID, StringComparison.Ordinal) ? _gagspeakSupporter : string.Empty,
                string.IsNullOrEmpty(profile.Description) ? _noDescription : profile.Description);

            // append the profile data to replace the loading profile one.
            _gagspeakProfiles[data] = profileData;
        }
        catch (Exception ex)
        {
            // if fails save DefaultProfileData to dict
            Logger.LogWarning(ex, "Failed to get Profile from service for user {user}", data);
            _gagspeakProfiles[data] = _defaultProfileData;
        }
    }
}
