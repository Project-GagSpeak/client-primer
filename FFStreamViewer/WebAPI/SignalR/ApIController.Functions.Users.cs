using Gagspeak.API.Data;
using Gagspeak.API.Dto.User;
using GagSpeak.API.Data.Character;
using GagSpeak.API.Dto.Connection;
using GagSpeak.API.Dto.Permissions;
using GagSpeak.API.Dto.UserPair;
using Microsoft.AspNetCore.SignalR.Client;

namespace FFStreamViewer.WebAPI;

#pragma warning disable MA0040
/// <summary> 
/// 
/// This partial class contains the user related functions.
/// 
/// User related functions are functions that our client calls to the server, sending it information via Dto's.
/// The server will then take those Dto's and handle the equivalent functions accordingly.
/// 
/// </summary>
public partial class ApiController
{
    /// <summary> 
    /// 
    /// Sends request to the server, asking to add the defined UserDto to the clients UserPair list.
    /// 
    /// </summary>
    /// <param name="user">the data transfer object of the User the client desires to add as a pair.</param>
    public async Task UserAddPair(UserDto user)
    {
        // if we are not connected, return
        if (!IsConnected) return;
        Logger.LogDebug("Adding pair {user} to client. Sending call to server.", user);
        // otherwise, call the UserAddPair function on the server with the user data transfer object via signalR
        await _gagspeakHub!.SendAsync(nameof(UserAddPair), user).ConfigureAwait(false); // wait for request to send.
    }

    /// <summary> 
    /// 
    /// Send a request to the server, asking it to remove the declared UserDto from the clients userPair list.
    /// 
    /// </summary>
    public async Task UserRemovePair(UserDto userDto)
    {
        // if we are not connected, return
        if (!IsConnected) return;
        // if we are connected, send the request to remove the user from the user pair list
        await _gagspeakHub!.SendAsync(nameof(UserRemovePair), userDto).ConfigureAwait(false);
    }

    /// <summary> 
    /// 
    /// Sends a request to the server, asking for the connected clients account to be deleted.
    /// 
    /// </summary>
    public async Task UserDelete()
    {
        // verify that we are connected
        CheckConnection();
        // send the account deletion request to the server
        await _gagspeakHub!.SendAsync(nameof(UserDelete)).ConfigureAwait(false);
        // perform a reconnect, because the account is no longer valid in the context of the current connection.
        await CreateConnections().ConfigureAwait(false);
    }

    /// <summary> 
    /// 
    /// Send a request to the server, asking it to return a list of all currently online users that you are paired with.
    /// 
    /// </summary>
    /// <returns>Returns a list of OnlineUserIdent Data Transfer Objects</returns>
    public async Task<List<OnlineUserIdentDto>> UserGetOnlinePairs()
    {
        return await _gagspeakHub!.InvokeAsync<List<OnlineUserIdentDto>>(nameof(UserGetOnlinePairs)).ConfigureAwait(false);
    }

    /// <summary> 
    /// 
    /// Send a request to the server, asking it to return a list of your paired clients.
    /// 
    /// </summary>
    /// <returns>Returns a list of UserPair data transfer objects</returns>
    public async Task<List<UserPairDto>> UserGetPairedClients()
    {
        return await _gagspeakHub!.InvokeAsync<List<UserPairDto>>(nameof(UserGetPairedClients)).ConfigureAwait(false);
    }

    /// <summary> 
    /// 
    /// Send a request to the server asking it to provide the profile of the user defined in the UserDto.
    /// 
    /// </summary>
    /// <returns>The user profile Dto belonging to the UserDto we sent</returns>
    public async Task<UserProfileDto> UserGetProfile(UserDto dto)
    {
        // if we are not connected, return a new user profile dto with the user data and disabled set to false
        if (!IsConnected) return new UserProfileDto(dto.User, Disabled: false, ProfilePictureBase64: null, Description: null);
        // otherwise, if we are connected, invoke the UserGetProfile function on the server with the user data transfer object
        return await _gagspeakHub!.InvokeAsync<UserProfileDto>(nameof(UserGetProfile), dto).ConfigureAwait(false);
    }

    /// <summary> 
    /// 
    /// Sets the profile of the client user, updating it to the clients paired users and the DB.
    /// 
    /// </summary>
    public async Task UserSetProfile(UserProfileDto userDescription)
    {
        if (!IsConnected) return;
        await _gagspeakHub!.InvokeAsync(nameof(UserSetProfile), userDescription).ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// Pushes the composite user data of a a character to other recipients.
    /// 
    /// (This is called upon by PushCharacterDataInternal, see bottom)
    /// 
    /// </summary>
    public async Task UserPushData(UserCharaCompositeDataMessageDto dto)
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

    /// <summary>
    /// 
    /// Pushes the IPC data from the client character to other recipients.
    /// 
    /// </summary>
    public async Task UserPushDataIpc(UserCharaIpcDataMessageDto dto)
    {
        // try and push the character data dto to the server
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserPushDataIpc), dto).ConfigureAwait(false);
        }
        // if it failed, log it
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to Push character data");
        }
    }

    /// <summary>
    /// 
    /// Pushes the composite user data of a a character to other recipients.
    /// 
    /// </summary>
    public async Task UserPushDataAppearance(UserCharaAppearanceDataMessageDto dto)
    {
        // try and push the character data dto to the server
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserPushDataAppearance), dto).ConfigureAwait(false);
        }
        // if it failed, log it
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to Push character data");
        }
    }

    /// <summary>
    /// 
    /// Pushes the wardrobe data of the client to other recipients.
    /// 
    /// </summary>
    public async Task UserPushDataWardrobe(UserCharaWardrobeDataMessageDto dto)
    {
        // try and push the character data dto to the server
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserPushDataWardrobe), dto).ConfigureAwait(false);
        }
        // if it failed, log it
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to Push character data");
        }
    }

    /// <summary>
    /// 
    /// Pushes the puppeteer alias lists of the client to other recipients.
    /// 
    /// </summary>
    public async Task UserPushDataAlias(UserCharaAliasDataMessageDto dto)
    {
        // try and push the character data dto to the server
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserPushDataAlias), dto).ConfigureAwait(false);
        }
        // if it failed, log it
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to Push character data");
        }
    }

    /// <summary>
    /// 
    /// Pushes the toybox pattern & trigger information of the client to other recipients.
    /// 
    /// </summary>
    public async Task UserPushDataPattern(UserCharaPatternDataMessageDto dto)
    {
        // try and push the character data dto to the server
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserPushDataPattern), dto).ConfigureAwait(false);
        }
        // if it failed, log it
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to Push character data");
        }
    }

    /// <summary>
    /// 
    /// Pushes to the server a request to modify the global permissions of self or a paired user.
    /// 
    /// </summary>
    public async Task UserUpdateGlobalPerms(UserGlobalPermChangeDto userPermissions)
    {
        CheckConnection();
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserUpdateGlobalPerms), userPermissions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set permissions");
        }
    }

    /// <summary>
    /// 
    /// Pushes a request to the server to modify the permission of self or a paired user.
    /// 
    /// </summary>
    public async Task UserUpdatePairPerms(UserPairPermChangeDto userPermissions)
    {
        CheckConnection();
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserUpdatePairPerms), userPermissions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set permissions");
        }
    }

    /// <summary>
    /// 
    /// pushes a request to update the access permissions of a user.
    /// (Should only work if the requested user is the client)
    /// 
    /// </summary>
    public async Task UserUpdatePairPermAccess(UserPairAccessChangeDto userPermissions)
    {
        CheckConnection();
        try
        {
            await _gagspeakHub!.InvokeAsync(nameof(UserUpdatePairPermAccess), userPermissions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set permissions");
        }
    }


    /* --------------------- Character Data Handling --------------------- */

    /// <summary> 
    /// 
    /// [[ NOTICE: THIS LIKELY WILL BE REMOVED AND PLACED WITH A BETTER HELP CLASS LATER PROBABLY ]] (but my head hurts to much to think about it now)
    /// This PushCharacterData function is not used by the API, but rather is used to condense the character data into a DTO and send it to the server.
    /// 
    /// <para>
    /// 
    /// This will call upon characterDataInternal, which will handle the assignments and the recipients, storing them into the Dto
    /// 
    /// </para>
    /// </summary>
    /// <param name="data">the character data</param>
    /// <param name="visibleCharacters">the visible characters to push it too (THIS SHOULD BE NEGATED SINCE IT SHOULD PUSH TO ALL ONLINE PLAYERS</param>
    public async Task PushCharacterData(CharacterCompositeData data, List<UserData> visibleCharacters)
    {
        // if we are not connected to the server, return
        if (!IsConnected) return;

        // if we are, then try pushing the character data to the visible characters by calling the internal function.
        try
        {
            Logger.LogDebug("Pushing Character data to {visible}", string.Join(", ", visibleCharacters.Select(v => v.AliasOrUID)));
            await UserPushData(new(visibleCharacters, data)).ConfigureAwait(false);
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
}
#pragma warning restore MA0040
