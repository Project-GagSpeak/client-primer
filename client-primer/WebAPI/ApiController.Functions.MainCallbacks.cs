using Dalamud.Interface.ImGuiNotification;
using GagspeakAPI.Enums;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.Toybox;
using GagspeakAPI.Dto.UserPair;
using GagSpeak.Services.Mediator;
using Microsoft.AspNetCore.SignalR.Client;
using GagspeakAPI.Dto.IPC;
using GagSpeak.Utils;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Common.Lua;

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
                Mediator.Publish(new NotificationMessage("Error from " +
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
                Mediator.Publish(new NotificationMessage("Error from " +
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
            _ = Task.Run(async () =>
            {
                // pause the server state
                _serverConfigs.CurrentServer.FullPause = true;
                _serverConfigs.Save();
                _doNotNotifyOnNextInfo = true;
                // create a new connection to force the disconnect.
                await CreateConnections().ConfigureAwait(false);
                // after it stops, switch the connection pause back to false and create a new connection.
                _serverConfigs.CurrentServer.FullPause = false;
                _serverConfigs.Save();
                _doNotNotifyOnNextInfo = true;
                await CreateConnections().ConfigureAwait(false);
            });
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
        Logger.LogDebug("Client_UserAddClientPair: "+dto, LoggerType.Callbacks);
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
        Logger.LogDebug("Client_UserRemoveClientPair: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.RemoveUserPair(dto));
        return Task.CompletedTask;
    }


    /// <summary>
    /// Should only be triggered if another pair is toggling on one of your existing moodles.
    /// This should be 
    /// </summary>
    public Task Client_UserApplyMoodlesByGuid(ApplyMoodlesByGuidDto dto)
    {
        // Should make us call to the moodles IPC to apply the statuses recieved
        // (permissions verified by server)
        Logger.LogDebug("Client_UserApplyMoodlesByGuid: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _clientCallbacks.ApplyStatusesByGuid(dto));
        return Task.CompletedTask;
    }


    public Task Client_UserApplyMoodlesByStatus(ApplyMoodlesByStatusDto dto)
    {
        Logger.LogDebug("Client_UserApplyMoodlesByStatus: "+dto, LoggerType.Callbacks);
        // obtain the localplayername and world
        string NameWithWorld = _frameworkUtils.GetIPlayerCharacterFromObjectTableAsync(_frameworkUtils.ClientPlayerAddress).GetAwaiter().GetResult()?.GetNameWithWorld() ?? string.Empty;
        ExecuteSafely(() => _clientCallbacks.ApplyStatusesToSelf(dto, NameWithWorld));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Intended to clear all moodles from OUR client player.
    /// Should make a call to our moodles IPC to remove the statuses listed by their GUID's
    /// </summary>
    public Task Client_UserRemoveMoodles(RemoveMoodlesDto dto)
    {
        Logger.LogDebug("Client_UserRemoveMoodles: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _clientCallbacks.RemoveStatusesFromSelf(dto));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Intended to clear all moodles from OUR client player.
    /// Should make a call to our moodles IPC to clear all statuses.
    /// </summary>
    public Task Client_UserClearMoodles(UserDto dto)
    {
        Logger.LogDebug("Client_UserClearMoodles: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _clientCallbacks.ClearStatusesFromSelf(dto));
        return Task.CompletedTask;
    }




    /// <summary>
    /// Sent to client from server informing them to update individual pair status of a client pair
    /// Status should be updated in the pair manager.
    /// </summary>
    public Task Client_UpdateUserIndividualPairStatusDto(UserIndividualPairStatusDto dto)
    {
        Logger.LogDebug("Client_UpdateUserIndividualPairStatusDto: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.UpdateIndividualPairStatus(dto));
        return Task.CompletedTask;
    }

    public Task Client_UserUpdateSelfAllGlobalPerms(UserAllGlobalPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfAllGlobalPerms: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogInformation("Updating all global permissions in bulk for self.", LoggerType.Callbacks);
            ExecuteSafely(() => _clientCallbacks.SetGlobalPerms(dto.GlobalPermissions));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogError("Don't try updating someone else's global permissions with a self global update call!");
            return Task.CompletedTask;
        }
    }

    public Task Client_UserUpdateSelfAllUniquePerms(UserPairUpdateAllUniqueDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfAllGlobalPerms: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user. Updating global permissions for them.", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.UpdatePairUpdateOwnAllUniquePermissions(dto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// Sent to client from server informing them to update their own global permissions
    /// Status should be updated in the pair manager.
    /// </summary>
    public Task Client_UserUpdateSelfPairPermsGlobal(UserGlobalPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfPairPermsGlobal: "+dto, LoggerType.Callbacks);
        if (dto.User.UID == UID)
        {
            Logger.LogTrace("Callback matched player character, updating own global permission data", LoggerType.Callbacks);
            ExecuteSafely(() => _clientCallbacks.ApplyGlobalPerm(dto));
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user, but was called by update self. This shouldn't be possible!", LoggerType.Callbacks);
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// Sent to client from server informing them to update their own permissions for a pair.
    /// the dto's UserData object should be the user pair that we are updating our own pair permissions for.
    /// </summary>
    public Task Client_UserUpdateSelfPairPerms(UserPairPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfPairPerms: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.UpdateSelfPairPermission(dto));
        return Task.CompletedTask;

    }

    /// <summary> Sent to client from server informing them to update their own permission edit access settings </summary>
    public Task Client_UserUpdateSelfPairPermAccess(UserPairAccessChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfPairPermAccess: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.UpdateSelfPairAccessPermission(dto));
        return Task.CompletedTask;
    }

    /// <summary> 
    /// Sent to client from server informing them to update their ALL permissions of a paired user.
    /// This should only be called once during the initial adding of a pair and never again.
    /// </summary>
    public Task Client_UserUpdateOtherAllPairPerms(UserPairUpdateAllPermsDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherAllPairPerms: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user. Updating all permissions for them.", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.UpdateOtherPairAllPermissions(dto));
            return Task.CompletedTask;
        }
    }

    public Task Client_UserUpdateOtherAllGlobalPerms(UserAllGlobalPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfAllGlobalPerms: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user. Updating global permissions for them.", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.UpdatePairUpdateOtherAllGlobalPermissions(dto));
            return Task.CompletedTask;
        }
    }

    public Task Client_UserUpdateOtherAllUniquePerms(UserPairUpdateAllUniqueDto dto)
    {
        Logger.LogDebug("Client_UserUpdateSelfAllGlobalPerms: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user. Updating global permissions for them.", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.UpdatePairUpdateOtherAllUniquePermissions(dto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// Sent to client from server informing them to update a user pair's global permission.
    /// </summary>
    public Task Client_UserUpdateOtherPairPermsGlobal(UserGlobalPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherPairPermsGlobal: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user. Updating global permissions for them.", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.UpdateOtherPairGlobalPermission(dto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// Sent to client from server informing them to update a user pair's permission option.
    /// </summary>
    public Task Client_UserUpdateOtherPairPerms(UserPairPermChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherPairPerms: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user. Updating permissions for them.", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.UpdateOtherPairPermission(dto));
            return Task.CompletedTask;
        }
    }

    /// <summary> 
    /// Sent to client from server informing them to update the new edit access permissions the user pair has.
    /// Status should be updated in the pair manager.
    /// 
    /// <para>
    /// (This should be called upon only when the other client pair needs to send the updated permission access
    /// into to the client. The client themselves should never be allowed to modify other user pairs edit access)
    /// </para>
    /// </summary>
    public Task Client_UserUpdateOtherPairPermAccess(UserPairAccessChangeDto dto)
    {
        Logger.LogDebug("Client_UserUpdateOtherPairPermAccess: "+dto, LoggerType.Callbacks);
        if (dto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogError("When updating permissions of otherUser, you shouldn't be calling yourself!");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogInformation("Callback matched to a paired user. Updating permissions for them.", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.UpdateOtherPairAccessPermission(dto));
            return Task.CompletedTask;
        }
    }


    /// <summary> 
    /// Should only ever get the other pairs. If getting self, something is up.
    /// </summary>
    public Task Client_UserReceiveCharacterDataComposite(OnlineUserCompositeDataDto dataDto)
    {
        Logger.LogTrace("Client_UserReceiveCharacterDataComposite:"+dataDto.User, LoggerType.Callbacks);
        if (dataDto.User.AliasOrUID == _connectionDto?.User.AliasOrUID)
        {
            Logger.LogWarning("Why are you trying to receive your own composite data? There is no need for this???");
            return Task.CompletedTask;
        }
        else
        {
            Logger.LogDebug("User "+ dataDto.User.UID+" has went online and updated you with their composite data!", LoggerType.Callbacks);
            ExecuteSafely(() => _pairManager.ReceiveCharaCompositeData(dataDto, UID));
            return Task.CompletedTask;
        }
    }


    /// <summary> Update Own Appearance Data </summary>
    public Task Client_UserReceiveOwnDataIpc(OnlineUserCharaIpcDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOwnDataIpc (not executing any functions):"+dataDto.User, LoggerType.Callbacks);
        return Task.CompletedTask;
    }

    /// <summary> Update Other UserPair Ipc Data </summary>
    public Task Client_UserReceiveOtherDataIpc(OnlineUserCharaIpcDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOtherDataIpc: "+dataDto, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.ReceiveCharaIpcData(dataDto));
        return Task.CompletedTask;
    }


    /// <summary> Update Own Appearance Data </summary>
    public Task Client_UserReceiveOwnDataAppearance(OnlineUserCharaAppearanceDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOwnDataAppearance:"+dataDto.User, LoggerType.Callbacks);
        bool callbackWasFromSelf = dataDto.User.UID == UID;
        ExecuteSafely(() => _clientCallbacks.CallbackAppearanceUpdate(dataDto, callbackWasFromSelf));
        return Task.CompletedTask;
    }

    /// <summary> Update Other UserPair Appearance Data </summary>
    public Task Client_UserReceiveOtherDataAppearance(OnlineUserCharaAppearanceDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOtherDataAppearance: {user}{updateKind}\n{data}", dataDto.User, dataDto.UpdateKind, dataDto.AppearanceData.ToString());
        ExecuteSafely(() => _pairManager.ReceiveCharaAppearanceData(dataDto));
        return Task.CompletedTask;
    }

    /// <summary> Update Own Wardrobe Data </summary>
    public Task Client_UserReceiveOwnDataWardrobe(OnlineUserCharaWardrobeDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOwnDataWardrobe:"+dataDto.User, LoggerType.Callbacks);
        bool callbackWasFromSelf = dataDto.User.UID == UID;
        ExecuteSafely(() => _clientCallbacks.CallbackWardrobeUpdate(dataDto, callbackWasFromSelf));
        return Task.CompletedTask;

    }

    /// <summary> Update Other UserPair Wardrobe Data </summary>
    public Task Client_UserReceiveOtherDataWardrobe(OnlineUserCharaWardrobeDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOtherDataWardrobe:"+dataDto.User, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.ReceiveCharaWardrobeData(dataDto));
        return Task.CompletedTask;
    }

    /// <summary> Update Own UserPair Alias Data </summary>
    public Task Client_UserReceiveOwnDataAlias(OnlineUserCharaAliasDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOwnDataAlias:"+dataDto.User, LoggerType.Callbacks);
        bool callbackWasFromSelf = dataDto.User.UID == UID;
        ExecuteSafely(() => _clientCallbacks.CallbackAliasStorageUpdate(dataDto));
        return Task.CompletedTask;
    }

    /// <summary> Update Other UserPair Alias Data </summary>
    public Task Client_UserReceiveOtherDataAlias(OnlineUserCharaAliasDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOtherDataAlias:"+dataDto.User, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.ReceiveCharaAliasData(dataDto));
        return Task.CompletedTask;
    }

    /// <summary> Update Own UserPair Toybox Data </summary>
    public Task Client_UserReceiveOwnDataToybox(OnlineUserCharaToyboxDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOwnDataToybox:"+dataDto.User, LoggerType.Callbacks);
        ExecuteSafely(() => _clientCallbacks.CallbackToyboxUpdate(dataDto));
        return Task.CompletedTask;
    }

    /// <summary> Update Other UserPair Toybox Data </summary>
    public Task Client_UserReceiveOtherDataToybox(OnlineUserCharaToyboxDataDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOtherDataToybox:"+dataDto.User, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.ReceiveCharaToyboxData(dataDto));
        return Task.CompletedTask;
    }

    public Task Client_UserReceiveDataPiShock(OnlineUserCharaPiShockPermDto dataDto)
    {
        Logger.LogDebug("Client_UserReceiveOwnDataPiShock:"+dataDto.User, LoggerType.Callbacks);
        // only case that doesn't offer feedback is updating own global, which should have been done before it was even sent out.
        ExecuteSafely(() => _pairManager.ReceiveCharaPiShockPermData(dataDto));
        return Task.CompletedTask;
    }

    /// <summary> Receive a Shock Instruction from another Pair. </summary>
    public Task Client_UserReceiveShockInstruction(ShockCollarActionDto dto)
    {
        Logger.LogInformation("Received Instruction from: {dto}" + Environment.NewLine
            + "OpCode: {opcode}, Intensity: {intensity}, Duration Value: {duration}"
            , dto.User.AliasOrUID, dto.OpCode, dto.Intensity, dto.Duration);
        ExecuteSafely(() =>
        {
            // figure out who sent the command, and see if we have a unique sharecode setup for them.
            var pairMatch = _pairManager.DirectPairs.FirstOrDefault(x => x.UserData.UID == dto.User.UID);
            if (pairMatch != null) 
            {
                if (!pairMatch.UserPairOwnUniquePairPerms.ShockCollarShareCode.IsNullOrEmpty())
                {
                    Logger.LogDebug("Executing Shock Instruction to UniquePair ShareCode", LoggerType.Callbacks);
                    _piShockProvider.ExecuteOperation(pairMatch.UserPairOwnUniquePairPerms.ShockCollarShareCode, dto.OpCode, dto.Intensity, dto.Duration);
                }
                else if (_clientCallbacks.ShockCodePresent)
                {
                    Logger.LogDebug("Executing Shock Instruction to Global ShareCode", LoggerType.Callbacks);
                    _piShockProvider.ExecuteOperation(_clientCallbacks.GlobalPiShockShareCode, dto.OpCode, dto.Intensity, dto.Duration);
                }
                else
                {
                    Logger.LogWarning("Someone Attempted to execute an instruction to you, but you don't have any share codes enabled!");
                }
            }

        });
        return Task.CompletedTask;
    }


    /// <summary> Receive a Global Chat Message. DO NOT LOG THIS. </summary>
    public Task Client_GlobalChatMessage(GlobalChatMessageDto dto)
    {
        ExecuteSafely(() => Mediator.Publish(new GlobalChatMessage(dto, (dto.MessageSender.UID == UID))));
        return Task.CompletedTask;
    }


    /// <summary> Server has sent us a UserDto has just went offline, and is notifying all connected pairs about it.
    /// <para> Use this info to update the UserDto in our pair manager so they are marked as offline.</para>
    /// </summary>
    public Task Client_UserSendOffline(UserDto dto)
    {
        Logger.LogDebug("Client_UserSendOffline: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.MarkPairOffline(dto.User));
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a OnlineUserIdentDto that is notifying all connected pairs about a user going online.
    /// <para> Use this info to update the UserDto in our pair manager so they are marked as online.</para>
    /// </summary>
    public Task Client_UserSendOnline(OnlineUserIdentDto dto)
    {
        Logger.LogDebug("Client_UserSendOnline: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => _pairManager.MarkPairOnline(dto));
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a UserDto from one of our client pairs that has just updated their profile.
    /// <para> Use this information to publish a new message to our mediator so update it.</para>
    /// </summary>
    public Task Client_UserUpdateProfile(UserDto dto)
    {
        Logger.LogDebug("Client_UserUpdateProfile: "+dto, LoggerType.Callbacks);
        ExecuteSafely(() => Mediator.Publish(new ClearProfileDataMessage(dto.User)));
        return Task.CompletedTask;
    }

    public Task Client_DisplayVerificationPopup(VerificationDto dto)
    {
        Logger.LogDebug("Client_DisplayVerificationPopup: "+dto, LoggerType.Callbacks);
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

    public void OnUserApplyMoodlesByGuid(Action<ApplyMoodlesByGuidDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserApplyMoodlesByGuid), act);
    }

    public void OnUserApplyMoodlesByStatus(Action<ApplyMoodlesByStatusDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserApplyMoodlesByStatus), act);
    }

    public void OnUserRemoveMoodles(Action<RemoveMoodlesDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserRemoveMoodles), act);
    }

    public void OnUserClearMoodles(Action<UserDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserClearMoodles), act);
    }

    public void OnUpdateUserIndividualPairStatusDto(Action<UserIndividualPairStatusDto> action)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UpdateUserIndividualPairStatusDto), action);
    }

    public void OnUserUpdateSelfAllGlobalPerms(Action<UserAllGlobalPermChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateSelfAllGlobalPerms), act);
    }

    public void OnUserUpdateSelfAllUniquePerms(Action<UserPairUpdateAllUniqueDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateSelfAllUniquePerms), act);
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

    public void OnUserUpdateOtherAllGlobalPerms(Action<UserAllGlobalPermChangeDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateOtherAllGlobalPerms), act);
    }

    public void OnUserUpdateOtherAllUniquePerms(Action<UserPairUpdateAllUniqueDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserUpdateOtherAllUniquePerms), act);
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

    public void OnUserReceiveCharacterDataComposite(Action<OnlineUserCompositeDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterDataComposite), act);
    }

    public void OnUserReceiveOwnDataIpc(Action<OnlineUserCharaIpcDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOwnDataIpc), act);
    }

    public void OnUserReceiveOtherDataIpc(Action<OnlineUserCharaIpcDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOtherDataIpc), act);
    }

    public void OnUserReceiveOwnDataAppearance(Action<OnlineUserCharaAppearanceDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOwnDataAppearance), act);
    }

    public void OnUserReceiveOtherDataAppearance(Action<OnlineUserCharaAppearanceDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOtherDataAppearance), act);
    }

    public void OnUserReceiveOwnDataWardrobe(Action<OnlineUserCharaWardrobeDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOwnDataWardrobe), act);
    }

    public void OnUserReceiveOtherDataWardrobe(Action<OnlineUserCharaWardrobeDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOtherDataWardrobe), act);
    }

    public void OnUserReceiveOwnDataAlias(Action<OnlineUserCharaAliasDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOwnDataAlias), act);
    }

    public void OnUserReceiveOtherDataAlias(Action<OnlineUserCharaAliasDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOtherDataAlias), act);
    }

    public void OnUserReceiveOwnDataToybox(Action<OnlineUserCharaToyboxDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOwnDataToybox), act);
    }

    public void OnUserReceiveOtherDataToybox(Action<OnlineUserCharaToyboxDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveOtherDataToybox), act);
    }

    public void OnUserReceiveDataPiShock(Action<OnlineUserCharaPiShockPermDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveDataPiShock), act);
    }

    public void OnUserReceiveShockInstruction(Action<ShockCollarActionDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveShockInstruction), act);
    }

    public void OnGlobalChatMessage(Action<GlobalChatMessageDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_GlobalChatMessage), act);
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
