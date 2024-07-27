using GagspeakAPI.Data;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.UserPair;
using Microsoft.AspNetCore.SignalR.Client;
using GagspeakAPI.Dto.Toybox;

namespace GagSpeak.WebAPI;

#pragma warning disable MA0040
public partial class ApiController // Partial class for Toybox User Functions.
{
    /// <summary>
    /// Take ownership of the PrivateRoomManager by hosting your own room in it. 
    /// Send the new room creation to server.
    /// </summary>
    public async Task<bool> PrivateRoomCreate(RoomCreateDto userDto)
    {
        if (!IsToyboxConnected) return false;

        return await _toyboxHub!.InvokeAsync<bool>(nameof(PrivateRoomCreate), userDto).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a room invite to another user.
    /// </summary>
    public async Task<bool> PrivateRoomInviteUser(RoomInviteDto dto)
    {
        if (!IsToyboxConnected) return false;
        return await _toyboxHub!.InvokeAsync<bool>(nameof(PrivateRoomInviteUser), dto).ConfigureAwait(false);
    }

    /// <summary>
    /// Join a room by name.
    /// </summary>
    public async Task PrivateRoomJoin(RoomParticipantDto roomName)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomJoin), roomName).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a message to the room.
    /// </summary>
    public async Task PrivateRoomSendMessage(RoomMessageDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomSendMessage), dto).ConfigureAwait(false);
    }

    /// <summary>
    /// Push the information about a device to another user in the connected private room.
    public async Task PrivateRoomPushDevice(UserCharaDeviceInfoMessageDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomPushDevice), dto).ConfigureAwait(false);
    }

    public async Task PrivateRoomAllowVibes(string roomName)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomAllowVibes), roomName).ConfigureAwait(false);
    }

    public async Task PrivateRoomDenyVibes(string roomName)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomDenyVibes), roomName).ConfigureAwait(false);
    }

    public async Task PrivateRoomUpdateUserDevice(UpdateDeviceDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomUpdateUserDevice), dto).ConfigureAwait(false);
    }

    public async Task PrivateRoomUpdateAllUserDevices(UpdateDeviceDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomUpdateAllUserDevices), dto).ConfigureAwait(false);
    }

    public async Task PrivateRoomLeave(RoomParticipantDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomLeave), dto).ConfigureAwait(false);
    }
    public async Task PrivateRoomRemove(string roomName)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(PrivateRoomRemove), roomName).ConfigureAwait(false);
    }
}

#pragma warning restore MA0040
