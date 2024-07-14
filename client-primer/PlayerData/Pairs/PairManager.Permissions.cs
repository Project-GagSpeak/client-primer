using Dalamud.Interface.ImGuiNotification;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Factories;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Comparer;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.UserPair;
using System.Reflection;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentNumericInput.Delegates;

namespace GagSpeak.PlayerData.Pairs;

/// <summary>
/// General note to self, pairs used to have "own permissions" and "other permissions" but they were removed.
/// <para> If down the line something like this is an answer to a problem of mine, then find a way to utilize it.</para>
/// </summary>
public sealed partial class PairManager : DisposableMediatorSubscriberBase
{
    /// <summary>
    /// 
    /// Updates all permissions of a client pair user.
    /// 
    /// Edit access is checked server-side to prevent abuse, so these should be all accurate
    /// 
    /// </summary>
    public void UpdateOtherPairAllPermissions(UserPairUpdateAllPermsDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair))
        {
            throw new InvalidOperationException("No such pair for " + dto);
        }

        if (pair.UserPair == null) throw new InvalidOperationException("No direct pair for " + dto);

        // check to see if the user just paused themselves.
        if (pair.UserPair.OtherPairPerms.IsPaused != dto.PairPermissions.IsPaused)
        {
            Mediator.Publish(new ClearProfileDataMessage(dto.User));
        }

        // set the permissions.
        pair.UserPair.OtherGlobalPerms = dto.GlobalPermissions;
        pair.UserPair.OtherPairPerms = dto.PairPermissions;
        pair.UserPair.OtherEditAccessPerms = dto.EditAccessPermissions;

        Logger.LogTrace("Fresh update >> Paused: {paused}", pair.UserPair.OtherPairPerms.IsPaused);

        RecreateLazy();
    }

    /// <summary>
    /// 
    /// Updates a global permission of a client pair user.
    /// 
    /// Edit access is checked server-side to prevent abuse, so these should be all accurate.
    /// 
    /// </summary>>
    public void UpdateOtherPairGlobalPermission(UserGlobalPermChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedPermission.Key;
        object ChangedValue = dto.ChangedPermission.Value;

        PropertyInfo? propertyInfo = typeof(UserGlobalPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            if (ChangedValue.GetType() == typeof(UInt64) && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                // property should be converted back from its Uint64 [MaxLockTime, 36000000000] to the timespan.
                propertyInfo.SetValue(pair.UserPair.OtherGlobalPerms, TimeSpan.FromTicks((long)(ulong)ChangedValue));
            }
            // char recognition. (these are converted to byte for Dto's instead of char)
            else if (ChangedValue.GetType() == typeof(byte) && propertyInfo.PropertyType == typeof(char))
            {
                propertyInfo.SetValue(pair.UserPair.OtherGlobalPerms, Convert.ToChar(ChangedValue));
            }
            else if (propertyInfo.CanWrite)
            {
                // convert the value to the appropriate type before setting.
                object value = Convert.ChangeType(ChangedValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(pair.UserPair.OtherGlobalPerms, value);
                Logger.LogDebug($"Updated global permission '{ChangedPermission}' to '{ChangedValue}'");
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy();
    }

    /// <summary>
    /// Updates one of the paired users pair permissions they have set for you. (their permission for you)
    /// </summary>>
    public void UpdateOtherPairPermission(UserPairPermChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedPermission.Key;
        object ChangedValue = dto.ChangedPermission.Value;

        // has the person just paused us.
        if (ChangedPermission == "IsPaused")
        {
            if (pair.UserPair.OtherPairPerms.IsPaused != (bool)ChangedValue)
            {
                Mediator.Publish(new ClearProfileDataMessage(dto.User));
            }
        }

        PropertyInfo? propertyInfo = typeof(UserPairPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            if (ChangedValue.GetType() == typeof(UInt64) && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                // property should be converted back from its Uint64 [MaxLockTime, 36000000000] to the timespan.
                propertyInfo.SetValue(pair.UserPair.OtherPairPerms, TimeSpan.FromTicks((long)(ulong)ChangedValue));
            }
            // char recognition. (these are converted to byte for Dto's instead of char)
            else if (ChangedValue.GetType() == typeof(byte) && propertyInfo.PropertyType == typeof(char))
            {
                propertyInfo.SetValue(pair.UserPair.OtherPairPerms, Convert.ToChar(ChangedValue));
            }
            else if (propertyInfo.CanWrite)
            {
                // convert the value to the appropriate type before setting.
                object value = Convert.ChangeType(ChangedValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(pair.UserPair.OtherPairPerms, value);
                Logger.LogDebug($"Updated global permission '{ChangedPermission}' to '{ChangedValue}'");
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy();
    }

    /// <summary>
    /// Updates an edit access permission for the paired user, reflecting what they are giving you access to.
    /// </summary>>
    public void UpdateOtherPairAccessPermission(UserPairAccessChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedAccessPermission.Key;
        object ChangedValue = dto.ChangedAccessPermission.Value;

        PropertyInfo? propertyInfo = typeof(UserEditAccessPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            if (ChangedValue.GetType() == typeof(UInt64) && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                // property should be converted back from its Uint64 [MaxLockTime, 36000000000] to the timespan.
                propertyInfo.SetValue(pair.UserPair.OtherEditAccessPerms, TimeSpan.FromTicks((long)(ulong)ChangedValue));
            }
            // char recognition. (these are converted to byte for Dto's instead of char)
            else if (ChangedValue.GetType() == typeof(byte) && propertyInfo.PropertyType == typeof(char))
            {
                propertyInfo.SetValue(pair.UserPair.OtherEditAccessPerms, Convert.ToChar(ChangedValue));
            }
            else if (propertyInfo.CanWrite)
            {
                // convert the value to the appropriate type before setting.
                object value = Convert.ChangeType(ChangedValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(pair.UserPair.OtherEditAccessPerms, value);
                Logger.LogDebug($"Updated global permission '{ChangedPermission}' to '{ChangedValue}'");
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy();
    }


    /// <summary>
    /// Updates one of your unique pair permissions you have set with the paired user.
    /// </summary>>
    public void UpdateSelfPairPermission(UserPairPermChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedPermission.Key;
        object ChangedValue = dto.ChangedPermission.Value;

        // has the person just paused us.
        if (ChangedPermission == "IsPaused" && (pair.UserPair.OwnPairPerms.IsPaused != (bool)ChangedValue))
        {
            Mediator.Publish(new ClearProfileDataMessage(dto.User));
        }

        PropertyInfo? propertyInfo = typeof(UserPairPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            if (ChangedValue.GetType() == typeof(UInt64) && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                // property should be converted back from its Uint64 [MaxLockTime, 36000000000] to the timespan.
                propertyInfo.SetValue(pair.UserPair.OwnPairPerms, TimeSpan.FromTicks((long)(ulong)ChangedValue));
            }
            // char recognition. (these are converted to byte for Dto's instead of char)
            else if (ChangedValue.GetType() == typeof(byte) && propertyInfo.PropertyType == typeof(char))
            {
                propertyInfo.SetValue(pair.UserPair.OwnPairPerms, Convert.ToChar(ChangedValue));
            }
            else if (propertyInfo.CanWrite)
            {
                // convert the value to the appropriate type before setting.
                object value = Convert.ChangeType(ChangedValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(pair.UserPair.OwnPairPerms, value);
                Logger.LogDebug($"Updated global permission '{ChangedPermission}' to '{ChangedValue}'");
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy();
    }

    /// <summary>
    /// Updates an edit access permission that you've set for the paired user.
    /// </summary>>
    public void UpdateSelfPairAccessPermission(UserPairAccessChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedAccessPermission.Key;
        object ChangedValue = dto.ChangedAccessPermission.Value;

        // has the person just paused us.
        if (ChangedPermission == "IsPaused" && (pair.UserPair.OwnPairPerms.IsPaused != (bool)ChangedValue))
        {
            Mediator.Publish(new ClearProfileDataMessage(dto.User));
        }

        PropertyInfo? propertyInfo = typeof(UserEditAccessPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            if (propertyInfo.CanWrite)
            {
                // convert the value to the appropriate type before setting.
                object value = Convert.ChangeType(ChangedValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(pair.UserPair.OwnEditAccessPerms, value);
                Logger.LogDebug($"Updated global permission '{ChangedPermission}' to '{ChangedValue}'");
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy();
    }
}
