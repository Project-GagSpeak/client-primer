using Dalamud.Interface.Internal.Notifications;
using FFStreamViewer.WebAPI.Services.Mediator;
using Gagspeak.API.Data.Enum;
using Gagspeak.API.Dto;
using Gagspeak.API.Dto.User;
using Microsoft.AspNetCore.SignalR.Client;

namespace FFStreamViewer.WebAPI;

/// <summary> This partial class contains the callback functions.
/// <para>
/// The Callback functions are the function calls that the server sends to us.
/// Meaning, the server is sending us information, and we need to take that information and handle it accordingly.
/// </para>
/// </summary>
public partial class ApiController
{
    /// <summary> This function is called when the server sends a message to the client.</summary>
    /// <param name="messageSeverity">the severity level of the message</param>
    /// <param name="message">the content of the message</param>
    public Task Client_ReceiveServerMessage(MessageSeverity messageSeverity, string message)
    {
        switch (messageSeverity)
        {
            case MessageSeverity.Error:
                Mediator.Publish(new NotificationMessage("Warning from " + 
                    _serverConfigManager.CurrentServer!.ServerName, message, NotificationType.Error, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Warning:
                Mediator.Publish(new NotificationMessage("Warning from " + 
                    _serverConfigManager.CurrentServer!.ServerName, message, NotificationType.Warning, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Information:
                if (_doNotNotifyOnNextInfo)
                {
                    _doNotNotifyOnNextInfo = false;
                    break;
                }
                Mediator.Publish(new NotificationMessage("Info from " + 
                    _serverConfigManager.CurrentServer!.ServerName, message, NotificationType.Info, TimeSpan.FromSeconds(5)));
                break;
        }
        // return it as a completed task.
        return Task.CompletedTask;
    }

    /// <summary> The server has just sent the client a Dto of its SystemInfo.
    /// <para> We should use this Dto to update the SystemInfoDto variable on our client.</para>
    /// </summary>
    public Task Client_UpdateSystemInfo(SystemInfoDto systemInfo)
    {
        SystemInfoDto = systemInfo;
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a UserPairDto from one of our connected client pairs.
    /// <para> We should use this Dto to update the UserPairDto in our pair manager.</para>
    /// </summary>
    /// <param name="dto">the Data transfer object containing the information about the UserPair</param>
    public Task Client_UserAddClientPair(UserPairDto dto)
    {
        Logger.LogDebug("Client_UserAddClientPair: {dto}", dto);
        ExecuteSafely(() => _pairManager.AddUserPair(dto, addToLastAddedUser: true));
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a UserDto that is requesting to be removed from our client pairs.
    /// <para> Use this info to remove this user from our pairs inside the clients pair manager</para>
    /// </summary>
    public Task Client_UserRemoveClientPair(UserDto dto)
    {
        Logger.LogDebug("Client_UserRemoveClientPair: {dto}", dto);
        ExecuteSafely(() => _pairManager.RemoveUserPair(dto));
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a OnlineUserCharaDataDto that is requesting to apply the recieved characterData to our client pairs.
    /// <para> Use this info to update the UserPairDto in our pair manager.</para>
    /// </summary>
    public Task Client_UserReceiveCharacterData(OnlineUserCharaDataDto dataDto)
    {
        Logger.LogTrace("Client_UserReceiveCharacterData: {user}", dataDto.User);
        ExecuteSafely(() => _pairManager.ReceiveCharaData(dataDto));
        return Task.CompletedTask;
    }

    /// <summary> Server has sent us a UserIndividualPairStatusDto from one of our connected client pairs.
    /// <para> We should use this Dto to update the UserIndividualPairStatusDto in our pair manager.</para>
    /// </summary>
    /// <param name="dto">the Data transfer object containing the information</param>
    public Task Client_UpdateUserIndividualPairStatusDto(UserIndividualPairStatusDto dto)
    {
        Logger.LogDebug("Client_UpdateUserIndividualPairStatusDto: {dto}", dto);
        ExecuteSafely(() => _pairManager.UpdateIndividualPairStatus(dto));
        return Task.CompletedTask;
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

    public void OnUserReceiveCharacterData(Action<OnlineUserCharaDataDto> act)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UserReceiveCharacterData), act);
    }

    public void OnUpdateUserIndividualPairStatusDto(Action<UserIndividualPairStatusDto> action)
    {
        if (_initialized) return;
        _gagspeakHub!.On(nameof(Client_UpdateUserIndividualPairStatusDto), action);
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
