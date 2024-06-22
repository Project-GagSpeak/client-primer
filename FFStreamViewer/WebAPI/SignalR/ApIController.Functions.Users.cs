using Gagspeak.API.Data;
using Gagspeak.API.Data.CharacterData;
using Gagspeak.API.Dto.User;
using Microsoft.AspNetCore.SignalR.Client;

namespace FFStreamViewer.WebAPI;

#pragma warning disable MA0040
/// <summary> This partial class contains the user related functions.
/// <para>
/// User related functions are functions that our client calls to the server, sending it information via Dto's.
/// The server will then take those Dto's and handle the equivalent functions accordingly.
/// </para>
/// </summary>
public partial class ApiController
{
    /// <summary> Sends request to the server, asking to add the defined UserDto to the clients UserPair list.</summary>
    /// <param name="user">the data transfer object of the User the client desires to add as a pair.</param>
    public async Task UserAddPair(UserDto user)
    {
        // if we are not connected, return
        if (!IsConnected) return;
        // otherwise, call the UserAddPair function on the server with the user data transfer object via signalR
        await _gagspeakHub!.SendAsync(nameof(UserAddPair), user).ConfigureAwait(false); // wait for request to send.
    }

    /// <summary> Sends a request to the server, asking for the connected clients account to be deleted.</summary>
    public async Task UserDelete()
    {
        // verify that we are connected
        CheckConnection();
        // send the account deletion request to the server
        await _gagspeakHub!.SendAsync(nameof(UserDelete)).ConfigureAwait(false);
        // perform a reconnect, because the account is no longer valid in the context of the current connection.
        await CreateConnections().ConfigureAwait(false);
    }

    /// <summary> Send a request to the server, asking it to return a list of all currently online users that you are paired with.</summary>
    /// <returns>Returns a list of OnlineUserIdent Data Transfer Objects</returns>
    public async Task<List<OnlineUserIdentDto>> UserGetOnlinePairs()
    {
        return await _gagspeakHub!.InvokeAsync<List<OnlineUserIdentDto>>(nameof(UserGetOnlinePairs)).ConfigureAwait(false);
    }

    /// <summary> Send a request to the server, asking it to return a list of your paired clients.</summary>
    /// <returns>Returns a list of UserPair data transfer objects</returns>
    public async Task<List<UserPairDto>> UserGetPairedClients()
    {
        return await _gagspeakHub!.InvokeAsync<List<UserPairDto>>(nameof(UserGetPairedClients)).ConfigureAwait(false);
    }

    /// <summary> Send a request to the server asking it to provide the profile of the user defined in the UserDto.</summary>
    /// <returns>The user profile Dto belonging to the UserDto we sent</returns>
    public async Task<UserProfileDto> UserGetProfile(UserDto dto)
    {
        // if we are not connected, return a new user profile dto with the user data and disabled set to false
        if (!IsConnected) return new UserProfileDto(dto.User, Disabled: false, ProfilePictureBase64: null, Description: null);
        // otherwise, if we are connected, invoke the UserGetProfile function on the server with the user data transfer object
        return await _gagspeakHub!.InvokeAsync<UserProfileDto>(nameof(UserGetProfile), dto).ConfigureAwait(false);
    }

    /// <summary>
    /// Using a DTO constructed of your client's character data, and a list of visible characters to 
    /// push them to, send this to the server for it to handle. (This is called upon by PushCharacterDataInternal, see bott
    /// </summary>
    public async Task UserPushData(UserCharaDataMessageDto dto)
    {
        // try and push the character data dto to the server
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserPushData), dto).ConfigureAwait(false);
        }
        // if it failed, log it
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to Push character data");
        }
    }

    /// <summary> Send a request to the server, asking it to remove the declared UserDto from the clients userPair list.</summary>
    public async Task UserRemovePair(UserDto userDto)
    {
        // if we are not connected, return
        if (!IsConnected) return;
        // if we are connected, send the request to remove the user from the user pair list
        await _gagspeakHub!.SendAsync(nameof(UserRemovePair), userDto).ConfigureAwait(false);
    }

    public async Task UserSetProfile(UserProfileDto userDescription)
    {
        if (!IsConnected) return;
        await _gagspeakHub!.InvokeAsync(nameof(UserSetProfile), userDescription).ConfigureAwait(false);
    }

    /* --------------------- Character Data Handling --------------------- */

    /// <summary> This PushCharacterData function is not used by the API, but rather is used to condense the character data into a DTO and send it to the server.
    /// <para>This will call upon characterDataInternal, which will handle the assignments and the recipients, storing them into the Dto</para>
    /// </summary>
    /// <param name="data">the character data</param>
    /// <param name="visibleCharacters">the visible characters to push it too (THIS SHOULD BE NEGATED SINCE IT SHOULD PUSH TO ALL ONLINE PLAYERS</param>
    /// <returns></returns>
    public async Task PushCharacterData(CharacterData data, List<UserData> visibleCharacters)
    {
        // if we are not connected to the server, return
        if (!IsConnected) return;

        // if we are, then try pushing the character data to the visible characters by calling the internal function.
        try
        {
            Logger.LogDebug("Pushing Character data to {visible}", string.Join(", ", visibleCharacters.Select(v => v.AliasOrUID)));
            await PushCharacterDataInternal(data, [.. visibleCharacters]).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Upload operation was cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during upload of files");
        }
    }

    private async Task PushCharacterDataInternal(CharacterData character, List<UserData> visibleCharacters)
    {
        Logger.LogInformation("Pushing clients character data to {charas}", string.Join(", ", visibleCharacters.Select(c => c.AliasOrUID)));
        // perform the push call to the server
        await UserPushData(new(visibleCharacters, character)).ConfigureAwait(false);
    }
}
#pragma warning restore MA0040
