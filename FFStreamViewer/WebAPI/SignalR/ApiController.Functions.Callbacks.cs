using System;
using System.Threading.Tasks;
using Dalamud.Interface.Internal.Notifications;
using Gagspeak.API.Data.Enum;
using Gagspeak.API.Dto;
using Gagspeak.API.Dto.User;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FFStreamViewer.WebAPI;

public partial class ApiController
{
    public Task Client_RecieveServerMessage(MessageSeverity messageSeverity, string message)
    {
        switch (messageSeverity)
        {
            case MessageSeverity.Error:
                //Mediator.Publish(new NotificationMessage("Warning from " + _serverManager.CurrentServer!.ServerName, message, NotificationType.Error, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Warning:
                //Mediator.Publish(new NotificationMessage("Warning from " + _serverManager.CurrentServer!.ServerName, message, NotificationType.Warning, TimeSpan.FromSeconds(7.5)));
                break;

            case MessageSeverity.Information:
                if (_doNotNotifyOnNextInfo)
                {
                    _doNotNotifyOnNextInfo = false;
                    break;
                }
                //Mediator.Publish(new NotificationMessage("Info from " + _serverManager.CurrentServer!.ServerName, message, NotificationType.Info, TimeSpan.FromSeconds(5)));
                break;
        }

        return Task.CompletedTask;
    }

    public Task Client_UpdateSystemInfo(SystemInfoDto systemInfo)
    {
        SystemInfoDto = systemInfo;
        return Task.CompletedTask;
    }

    public void OnReceiveServerMessage(Action<MessageSeverity, string> act)
    {
        if (_initialized) return;
        _mareHub!.On(nameof(Client_RecieveServerMessage), act);
    }

    public void OnUpdateSystemInfo(Action<SystemInfoDto> act)
    {
        if (_initialized) return;
        _mareHub!.On(nameof(Client_UpdateSystemInfo), act);
    }
}