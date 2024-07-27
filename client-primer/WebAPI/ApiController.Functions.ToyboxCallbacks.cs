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
    public Task Client_PrivateRoomJoined(RoomInfoDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomJoined: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ClientJoinRoom(dto));
        return Task.CompletedTask;
    }

    /// <summary> 
    /// Adds another participant who has joined the room you are in.
    /// </summary>
    public Task Client_PrivateRoomOtherUserJoined(RoomParticipantDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomOtherUserJoined: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.AddParticipantToRoom(dto));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a participant who has left the room you are in.
    /// </summary>
    public Task Client_PrivateRoomOtherUserLeft(RoomParticipantDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomOtherUserLeft: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ParticipantLeftRoom(dto));
        return Task.CompletedTask;
    }

    public Task Client_PrivateRoomRemovedUser(RoomParticipantDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomRemovedUser: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ParticipantRemovedFromRoom(dto));
        return Task.CompletedTask;
    }

    public Task Client_PrivateRoomUpdateUser(RoomParticipantDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomUpdateUser: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ParticipantUpdated(dto));
        return Task.CompletedTask;
    }

    public Task Client_PrivateRoomMessage(RoomMessageDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomMessage: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.AddChatMessage(dto));
        return Task.CompletedTask;
    }

    public Task Client_PrivateRoomReceiveUserDevice(UserCharaDeviceInfoMessageDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomReceiveUserDevice: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ReceiveParticipantDeviceData(dto));
        return Task.CompletedTask;
    }

    public Task Client_PrivateRoomDeviceUpdate(UpdateDeviceDto dto)
    {
        Logger.LogDebug("Client_PrivateRoomDeviceUpdate: {dto}", dto);
        ExecuteSafely(() => _privateRoomManager.ApplyDeviceUpdate(dto));
        return Task.CompletedTask;
    }

    public Task Client_PrivateRoomClosed(string roomName)
    {
        Logger.LogDebug("Client_PrivateRoomClosed: {roomName}", roomName);
        ExecuteSafely(() => _privateRoomManager.RoomClosedByHost(roomName));
        return Task.CompletedTask;
    }


    /* --------------------------------- void methods from the API to call the hooks --------------------------------- */
    public void OnReceiveToyboxServerMessage(Action<MessageSeverity, string> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_ReceiveToyboxServerMessage), act);
    }

    public void OnUserReceiveRoomInvite(Action<RoomInviteDto> act)
    {
       if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_UserReceiveRoomInvite), act);
    }

    public void OnPrivateRoomJoined(Action<RoomInfoDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomJoined), act);
    }

    public void OnPrivateRoomOtherUserJoined(Action<RoomParticipantDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomOtherUserJoined), act);
    }

    public void OnPrivateRoomOtherUserLeft(Action<RoomParticipantDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomOtherUserLeft), act);
    }

    public void OnPrivateRoomRemovedUser(Action<RoomParticipantDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomRemovedUser), act);
    }

    public void OnPrivateRoomUpdateUser(Action<RoomParticipantDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomUpdateUser), act);
    }

    public void OnPrivateRoomMessage(Action<RoomMessageDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomMessage), act);
    }

    public void OnPrivateRoomReceiveUserDevice(Action<UserCharaDeviceInfoMessageDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomReceiveUserDevice), act);
    }

    public void OnPrivateRoomDeviceUpdate(Action<UpdateDeviceDto> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomDeviceUpdate), act);
    }

    public void OnPrivateRoomClosed(Action<string> act)
    {
        if (_toyboxInitialized) return;
        _toyboxHub!.On(nameof(Client_PrivateRoomClosed), act);
    }
}