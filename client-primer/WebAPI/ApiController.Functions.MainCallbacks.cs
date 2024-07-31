using Dalamud.Interface.ImGuiNotification;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.Toybox;
using GagspeakAPI.Dto.UserPair;
using GagSpeak.Services.Mediator;
using Microsoft.AspNetCore.SignalR.Client;

namespace GagSpeak.WebAPI;

public partial class ApiController // Partial class for MainHub Callbacks
{
    /// <summary> Called when the server sends a message to the client. </summary>
    /// <param name="messageSeverity">the severity level of the message</param>
    /// <param name="message">the content of the message</param>
    public Task Client_ReceiveServerMessage(MessageSeverity messageSeverity, string message)
    {
        switch (messageSeverity)
        {
            case MessageSeverity.Error:
                Mediator.Publish(new NotificationMessage("Warning from " +
                    _serverConfigs.CurrentServer!.ServerName, message, NotificationType.Error, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Warning:
                Mediator.Publish(new NotificationMessage("Warning from " +
                    _serverConfigs.CurrentServer!.ServerName, message, NotificationType.Warning, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Information:
                if (_doNotNotifyOnNextInfo)
                {
                    _doNotNotifyOnNextInfo = false;
                    break;
                }
                Mediator.Publish(new NotificationMessage("Info from " +
                    _serverConfigs.CurrentServer!.ServerName, message, NotificationType.Info, TimeSpan.FromSeconds(7.5)));
                break;
        }
        // return it as a completed task.
        return Task.CompletedTask;
    }

    public Task Client_ReceiveHardReconnectMessage(MessageSeverity messageSeverity, string message, ServerState newServerState)
    {
        switch (messageSeverity)
        {
            case MessageSeverity.Error:
                Mediator.Publish(new NotificationMessage("Warning from " +
                    _serverConfigs.CurrentServer!.ServerName, message, NotificationType.Error, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Warning:
                Mediator.Publish(new NotificationMessage("Warning from " +
                    _serverConfigs.CurrentServer!.ServerName, message, NotificationType.Warning, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Information:
                if (_doNotNotifyOnNextInfo)
                {
                    _doNotNotifyOnNextInfo = false;
                    break;
                }
                Mediator.Publish(new NotificationMessage("Info from " +
                    _serverConfigs.CurrentServer!.ServerName, message, NotificationType.Info, TimeSpan.FromSeconds(5)));
                break;
        }
        // we need to update the api server state to be stopped if connected
        if (ServerState == ServerState.Connected)
        {
            _ = Task.Run(async () => await StopConnection(newServerState).ConfigureAwait(false));
        }
        // return completed
        return Task.CompletedTask;
    }

    /// <summary> 
    /// 
    /// The server has just sent the client a Dto of its SystemInfo.
    /// 
    /// </summary>
    public Task Client_UpdateSystemInfo(SystemInfoDto systemInfo)
    {
        SystemInfoDto = systemInfo;
        return Task.CompletedTask;
    }

    /// <summary> 
    /// 
    /// Server has sent us a UserPairDto from one of our connected client pairs.
    /// 
    /// </summary>
    public Task Client_UserAddClientPair(UserPairDto dto)
    {
        Logger.LogDebug("Client_UserAddClientPair: {dto}", dto);
        ExecuteSafely(() => _pairManager.AddUserPair(dto, addToLastAddedUser: true));
        return Task.CompletedTask;
    }

    /// <summary> 
    /// 
    /// Server has sent us a UserDto that is requesting to be removed from our client pairs.
    /// 
    /// </summary>
    public Task Client_UserRemoveClientPair(UserDto dto)
    {
        Logger.LogDebug("Client_UserRemoveClientPair: {dto}", dto);
        ExecuteSafely(() => _pairManager.RemoveUserPair(dto));
        return Task.CompletedTask;
    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update individual pair status of a client pair.
    /// 
    /// Status should be updated in the pair manager.
    /// 
    /// </summary>
    public Task Client_UpdateUserIndividualPairStatusDto(UserIndividualPairStatusDto dto)
    {
        Logger.LogDebug("Client_UpdateUserIndividualPairStatusDto: {dto}", dto);
        ExecuteSafely(() => _pairManager.UpdateIndividualPairStatus(dto));
        return Task.CompletedTask;
    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update their own global permissions
    /// 
    /// Status should be updated in the pair manager.
    /// 
    /// </summary>
    public Task Client_UserUpdateSelfPairPermsGlobal(UserGlobalPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfPairPermsGlobal: {dto}", dto);
        if (dto.User.UID == UID)
        {
            Logger.LogTrace("Callback matched player character, updating own global permission data");
            ExecuteSafely(() => _playerCharManager.ApplyGlobalPermChange(dto));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogError("Callback matched to a paired user, but was called by update self. This shouldn't be possible!");
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update their own permissions for a pair.
    /// 
    /// the dto's UserData object should be the user pair that we are updating our own pair permissions for.
    /// 
    /// </summary>
    public Task Client_UserUpdateSelfPairPerms(UserPairPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfPairPerms: {dto}", dto);
        ExecuteSafely(() => _pairManager.UpdateSelfPairPermission(dto));
        return Task.CompletedTask;

    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update their own permission edit access settings
    /// 
    /// </summary>
    public Task Client_UserUpdateSelfPairPermAccess(UserPairAccessChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfPairPermAccess: {dto}", dto);
        ExecuteSafely(() => _pairManager.UpdateSelfPairAccessPermission(dto));
        return Task.CompletedTask;
    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update their ALL permissions of a paired user.
    /// This should only be called once during the initial adding of a pair and never again.
    /// 
    /// </summary>
    public Task Client_UserUpdateOtherAllPairPerms(UserPairUpdateAllPermsDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherAllPairPerms: {dto}", dto);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogError("Callback matched to a paired user. Updating all permissions for them.");
            ExecuteSafely(() => _pairManager.UpdateOtherPairAllPermissions(dto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update a user pair's global permission.
    /// 
    /// </summary>
    public Task Client_UserUpdateOtherPairPermsGlobal(UserGlobalPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherPairPermsGlobal: {dto}", dto);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogError("Callback matched to a paired user. Updating global permissions for them.");
            ExecuteSafely(() => _pairManager.UpdateOtherPairGlobalPermission(dto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update a user pair's permission option.
    /// 
    /// </summary>
    public Task Client_UserUpdateOtherPairPerms(UserPairPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherPairPerms: {dto}", dto);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogError("Callback matched to a paired user. Updating permissions for them.");
            ExecuteSafely(() => _pairManager.UpdateOtherPairPermission(dto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server informing them to update the new edit access permissions the user pair has.
    /// Status should be updated in the pair manager.
    /// 
    /// <para>
    /// 
    /// (This should be called upon only when the other client pair needs to send the updated permission access
    /// into to the client. The client themselves should never be allowed to modify other user pairs edit access)
    /// 
    /// </para>
    /// </summary>
    public Task Client_UserUpdateOtherPairPermAccess(UserPairAccessChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherPairPermAccess: {dto}", dto);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogError("Callback matched to a paired user. Updating permissions for them.");
            ExecuteSafely(() => _pairManager.UpdateOtherPairAccessPermission(dto));
            return Task.CompletedTask;
        }
    }


    /// <summary> 
    /// 
    /// Sent to client from server, providing client with all updated character data of a user pair.
    /// 
    /// (In theory, this should also be able to send back updated information about our own character.)
    /// 
    /// </summary>
    public Task Client_UserReceiveCharacterDataComposite(OnlineUserCharaCompositeDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveCharacterDataComposite: {dataDto}", dataDto);
        if (dataDto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogTrace("Callback matched player character, updating own composite data");
            ExecuteSafely(() => _playerCharManager.UpdateCharWithCompositeData(dataDto));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogTrace("Callback matched to a paired user, updating their composite data.");
            ExecuteSafely(() => _pairManager.ReceiveCharaCompositeData(dataDto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server, providing client with the updated  IPC character data of a user pair.
    /// 
    /// (In theory, this should also be able to send back updated information about our own character.)
    /// 
    /// </summary>
    public Task Client_UserReceiveCharacterDataIpc(OnlineUserCharaIpcDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveCharacterDataIpc: {dataDto}", dataDto);
        if (dataDto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogTrace("Callback matched player character, updating own IPC data");
            ExecuteSafely(() => _playerCharManager.UpdateCharIpcData(dataDto));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogTrace("Callback matched to a paired user, updating their IPC data.");
            ExecuteSafely(() => _pairManager.ReceiveCharaIpcData(dataDto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server, providing client with the updated Appearance character data of a user pair.
    /// 
    /// (In theory, this should also be able to send back updated information about our own character.)
    /// 
    /// </summary>
    public Task Client_UserReceiveCharacterDataAppearance(OnlineUserCharaAppearanceDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveCharacterDataAppearance: {dataDto}", dataDto);
        if (dataDto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogTrace("Callback matched player character, updating own appearance data");
            ExecuteSafely(() => _playerCharManager.UpdateCharAppearanceData(dataDto));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogTrace("Callback matched to a paired user, updating their appearance data.");
            ExecuteSafely(() => _pairManager.ReceiveCharaAppearanceData(dataDto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server, providing client with the updated Wardrobe character data of a user pair.
    /// 
    /// (In theory, this should also be able to send back updated information about our own character.)
    /// 
    /// </summary>
    public Task Client_UserReceiveCharacterDataWardrobe(OnlineUserCharaWardrobeDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveCharacterDataWardrobe: {dataDto}", dataDto);
        if (dataDto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogTrace("Callback matched player character, updating own wardrobe data");
            ExecuteSafely(() => _playerCharManager.UpdateCharWardrobeData(dataDto));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogTrace("Callback matched to a paired user, updating their wardrobe data.");
            ExecuteSafely(() => _pairManager.ReceiveCharaWardrobeData(dataDto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server, providing client with the updated Alias character data of a user pair.
    /// 
    /// (In theory, this should also be able to send back updated information about our own character.)
    /// 
    /// </summary>
    public Task Client_UserReceiveCharacterDataAlias(OnlineUserCharaAliasDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveCharacterDataAlias: {dataDto}", dataDto);
        if (dataDto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            // successful parse for updating own alias data.
            Logger.LogTrace("Callback matched player character, updating own alias data");
            ExecuteSafely(() => _playerCharManager.UpdateCharAliasData(dataDto));
            return Task.CompletedTask;
        }
        else
        {
            // successfull parse for updating user pair's alias data.
            Logger.LogTrace("Callback matched to a paired user, updating their alias data.");
            ExecuteSafely(() => _pairManager.ReceiveCharaAliasData(dataDto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// 
    /// Sent to client from server, providing client with the updated PatternInfo character data of a user pair.
    /// 
    /// (In theory, this should also be able to send back updated information about our own character.)
    /// 
    /// </summary>
    public Task Client_UserReceiveCharacterDataToybox(OnlineUserCharaPatternDataDto dataDto)
    {
        Logger.LogTrace("Client_UserReceiveCharacterDataToybox: {user}", dataDto.User);
        if (dataDto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogTrace("Callback matched player character, updating own pattern data");
            ExecuteSafely(() => _playerCharManager.UpdateCharPatternData(dataDto));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogTrace("Callback matched to a paired user, updating their pattern data.");
            ExecuteSafely(() => _pairManager.ReceiveCharaPatternData(dataDto));
            return Task.CompletedTask;
        }
    }

    /// <summary> Server has sent us a UserDto has just went offline, and is notifying all connected pairs about it.
    /// <para> Use this info to update the UserDto in our pair manager so they are marked as offline.</para>
    /// </summary>
    public Task Client_UserSendOffline(UserDto dto)
    {
        Logger.LogDebug("Client_UserSendOffline: {dto}", dto);
        ExecuteSafely(() => _pairManager.MarkPairOffline(dto.User));
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a OnlineUserIdentDto that is notifying all connected pairs about a user going online.
    /// <para> Use this info to update the UserDto in our pair manager so they are marked as online.</para>
    /// </summary>
    public Task Client_UserSendOnline(OnlineUserIdentDto dto)
    {
        Logger.LogDebug("Client_UserSendOnline: {dto}", dto);
        ExecuteSafely(() => _pairManager.MarkPairOnline(dto));
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a UserDto from one of our client pairs that has just updated their profile.
    /// <para> Use this information to publish a new message to our mediator so update it.</para>
    /// </summary>
    public Task Client_UserUpdateProfile(UserDto dto)
    {
        Logger.LogDebug("Client_UserUpdateProfile: {dto}", dto);
        ExecuteSafely(() => Mediator.Publish(new ClearProfileDataMessage(dto.User)));
        return Task.CompletedTask;
    }

    public Task Client_DisplayVerificationPopup(VerificationDto dto)
    {
        Logger.LogDebug("Client_DisplayVerificationPopup: {dto}", dto);
        ExecuteSafely(() => Mediator.Publish(new VerificationPopupMessage(dto)));
        return Task.CompletedTask;
    }


    /// <summary> A helper method to ensure the action is executed safely, and if an exception is thrown, it is logged.</summary>
    /// <param name="act">the action to execute</param>
    private void ExecuteSafely(Action act)
    {
        try
        {
            act();
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Error on executing safely");
        }
    }

    /* --------------------------------- void methods from the API to call the hooks --------------------------------- */
    public void OnReceiveServerMessage(Action<MessageSeverity, string> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_ReceiveServerMessage), act);
    }

    public void OnReceiveHardReconnectMessage(Action<MessageSeverity, string, ServerState> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_ReceiveHardReconnectMessage), act);
    }

    public void OnUpdateSystemInfo(Action<SystemInfoDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UpdateSystemInfo), act);
    }

    public void OnUserAddClientPair(Action<UserPairDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserAddClientPair), act);
    }

    public void OnUserRemoveClientPair(Action<UserDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserRemoveClientPair), act);
    }

    public void OnUpdateUserIndividualPairStatusDto(Action<UserIndividualPairStatusDto> action)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UpdateUserIndividualPairStatusDto), action);
    }

    public void OnUserUpdateSelfPairPermsGlobal(Action<UserGlobalPermChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateSelfPairPermsGlobal), act);
    }

    public void OnUserUpdateSelfPairPerms(Action<UserPairPermChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateSelfPairPerms), act);
    }

    public void OnUserUpdateSelfPairPermAccess(Action<UserPairAccessChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateSelfPairPermAccess), act);
    }

    public void OnUserUpdateOtherAllPairPerms(Action<UserPairUpdateAllPermsDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateOtherAllPairPerms), act);
    }

    public void OnUserUpdateOtherPairPermsGlobal(Action<UserGlobalPermChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateOtherPairPermsGlobal), act);
    }

    public void OnUserUpdateOtherPairPerms(Action<UserPairPermChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateOtherPairPerms), act);
    }
    public void OnUserUpdateOtherPairPermAccess(Action<UserPairAccessChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateOtherPairPermAccess), act);
    }

    public void OnUserReceiveCharacterDataComposite(Action<OnlineUserCharaCompositeDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterDataComposite), act);
    }

    public void OnUserReceiveCharacterDataIpc(Action<OnlineUserCharaIpcDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterDataIpc), act);
    }
    public void OnUserReceiveCharacterDataAppearance(Action<OnlineUserCharaAppearanceDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterDataAppearance), act);
    }

    public void OnUserReceiveCharacterDataWardrobe(Action<OnlineUserCharaWardrobeDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterDataWardrobe), act);
    }
    public void OnUserReceiveCharacterDataAlias(Action<OnlineUserCharaAliasDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterDataAlias), act);
    }
    public void OnUserReceiveCharacterDataPattern(Action<OnlineUserCharaPatternDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterDataToybox), act);
    }

    public void OnUserSendOffline(Action<UserDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserSendOffline), act);
    }

    public void OnUserSendOnline(Action<OnlineUserIdentDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserSendOnline), act);
    }

    public void OnUserUpdateProfile(Action<UserDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateProfile), act);
    }

    public void OnDisplayVerificationPopup(Action<VerificationDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_DisplayVerificationPopup), act);
    }
}
