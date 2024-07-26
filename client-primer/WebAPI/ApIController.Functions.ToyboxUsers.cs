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
    public async Task UserCreateNewRoom(RoomCreateDto userDto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserCreateNewRoom), userDto).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a room invite to another user.
    /// </summary>
    public async Task UserRoomInvite(RoomInviteDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserRoomInvite), dto).ConfigureAwait(false);
    }

    /// <summary>
    /// Join a room by name.
    /// </summary>
    public async Task UserJoinRoom(string roomName)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserJoinRoom), roomName).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a message to the room.
    /// </summary>
    public async Task UserSendMessageToRoom(RoomMessageDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserSendMessageToRoom), dto).ConfigureAwait(false);
    }

    /// <summary>
    /// Push the information about a device to another user in the connected private room.
    public async Task UserPushDeviceInfo(UserCharaDeviceInfoMessageDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserPushDeviceInfo), dto).ConfigureAwait(false);
    }

    public async Task UserUpdateDevice(UpdateDeviceDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserUpdateDevice), dto).ConfigureAwait(false);
    }

    public async Task UserUpdateGroupDevices(UpdateDeviceDto dto)
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserUpdateGroupDevices), dto).ConfigureAwait(false);
    }

    public async Task UserLeaveRoom()
    {
        if (!IsToyboxConnected) return;
        await _toyboxHub!.SendAsync(nameof(UserLeaveRoom)).ConfigureAwait(false);
    }
}

#pragma warning restore MA0040
