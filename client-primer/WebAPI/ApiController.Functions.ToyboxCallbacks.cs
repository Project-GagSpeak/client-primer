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

public partial class ApiController // Partial cloass for ToyboxHub Callbacks.
{
    /// <summary> Called when the toybox server sends a message to the client. </summary>
    /// <param name="messageSeverity">the severity level of the message</param>
    /// <param name="message">the content of the message</param>
    public Task Client_ReceiveToyboxServerMessage(MessageSeverity messageSeverity, string message)
    {
        switch (messageSeverity)
        {
            case MessageSeverity.Error:
                Mediator.Publish(new NotificationMessage("Warning from Toybox Server", message, NotificationType.Error, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Warning:
                Mediator.Publish(new NotificationMessage("Warning from Toybox Server", message, NotificationType.Warning, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Information:
                if (_doNotNotifyOnNextInfo)
                {
                    _doNotNotifyOnNextInfo = false;
                    break;
                }
                Mediator.Publish(new NotificationMessage("Info from Toybox Server", message, NotificationType.Info, TimeSpan.FromSeconds(5)));
                break;
        }
        // return it as a completed task.
        return Task.CompletedTask;
    }

    public Task Client_UserReceiveRoomInvite(RoomInviteDto dto)
    {
        Logger.LogDebug("Client_UserReceiveRoomInvite: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.InviteRecieved(dto));
        return Task.CompletedTask;
    }

    /// <summary> For whenever you join a new room. </summary>
    public Task Client_UserJoinedRoom(RoomInfoDto dto)
    {
        Logger.LogDebug("Client_UserJoinedRoom: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ClientJoinRoom(dto));
        return Task.CompletedTask;
    }

    /// <summary> 
    /// Adds another participant who has joined the room you are in.
    /// </summary>
    public Task Client_OtherUserJoinedRoom(RoomParticipantDto dto)
    {
        Logger.LogDebug("Client_OtherUserJoinedRoom: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.AddParticipantToRoom(dto));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a participant who has left the room you are in.
    /// </summary>
    public Task Client_OtherUserLeftRoom(RoomParticipantDto dto)
    {
        Logger.LogDebug("Client_OtherUserLeftRoom: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ParticipantLeftRoom(dto));
        return Task.CompletedTask;
    }

    public Task Client_UserReceiveRoomMessage(RoomMessageDto dto)
    {
        Logger.LogDebug("Client_UserReceiveRoomMessage: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.AddChatMessage(dto));
        return Task.CompletedTask;
    }

    public Task Client_UserReceiveDeviceInfo(UserCharaDeviceInfoMessageDto dto)
    {
        Logger.LogDebug("Client_UserReceiveDeviceInfo: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ReceiveParticipantDeviceData(dto));
        return Task.CompletedTask;
    }

    public Task Client_UserDeviceUpdate(UpdateDeviceDto dto)
    {
        Logger.LogDebug("Client_UserDeviceUpdate: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ApplyDeviceUpdate(dto));
        return Task.CompletedTask;
    }

    public Task Client_ReceiveRoomClosedMessage(string roomName)
    {
        Logger.LogDebug("Client_ReceiveRoomClosedMessage: {roomName}", roomName);
        ExecuteSafely(() => _privateRoomManager.RoomClosedByHost(roomName));
        return Task.CompletedTask;
    }


    /* --------------------------------- void methods from the API to call the hooks --------------------------------- */
    public void OnReceiveToyboxServerMessage(Action<MessageSeverity, string> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_ReceiveToyboxServerMessage), act);
    }

    public void OnUserReceiveRoomInvite(Action<RoomInviteDto> act)
    {
       if (_initialized) return;
        _toyboxHub!.On(nameof(Client_UserReceiveRoomInvite), act);
    }

    public void OnUserJoinedRoom(Action<RoomInfoDto> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_UserJoinedRoom), act);
    }

    public void OnOtherUserJoinedRoom(Action<RoomParticipantDto> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_OtherUserJoinedRoom), act);
    }

    public void OnOtherUserLeftRoom(Action<RoomParticipantDto> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_OtherUserLeftRoom), act);
    }

    public void OnUserReceiveRoomMessage(Action<RoomMessageDto> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_UserReceiveRoomMessage), act);
    }

    public void OnUserReceiveDeviceInfo(Action<UserCharaDeviceInfoMessageDto> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_UserReceiveDeviceInfo), act);
    }

    public void OnUserDeviceUpdate(Action<UpdateDeviceDto> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_UserDeviceUpdate), act);
    }

    public void OnReceiveRoomClosedMessage(Action<string> act)
    {
        if (_initialized) return;
        _toyboxHub!.On(nameof(Client_ReceiveRoomClosedMessage), act);
    }
}
