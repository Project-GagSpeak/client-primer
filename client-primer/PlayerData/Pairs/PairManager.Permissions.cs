using GagSpeak.Services.Mediator;
using GagspeakAPI.Enums;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Permissions;
using System.Reflection;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentNumericInput.Delegates;
using GagSpeak.WebAPI;

namespace GagSpeak.PlayerData.Pairs;

// Personal note, this could easily become part of Pair Handler.

/// <summary>
/// General note to self, pairs used to have "own permissions" and "other permissions" but they were removed.
/// <para> If down the line something like this is an answer to a problem of mine, then find a way to utilize it.</para>
/// </summary>
public sealed partial class PairManager : DisposableMediatorSubscriberBase
{
    /// <summary>
    /// Updates all permissions of a client pair user.
    /// Edit access is checked server-side to prevent abuse, so these should be all accurate
    /// </summary>
    public void UpdateOtherPairAllPermissions(UserPairUpdateAllPermsDto dto)
    {
        bool MoodlesChanged = false;
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
        // check to see if we are updating any moodles permissions
        MoodlesChanged = UpdatingMoodlesPerms(dto.PairPermissions, pair.UserPair.OtherPairPerms);
        // update pair perms
        pair.UserPair.OtherPairPerms = dto.PairPermissions;
        pair.UserPair.OtherEditAccessPerms = dto.EditAccessPermissions;

        Logger.LogTrace("Fresh update >> Paused: "+ pair.UserPair.OtherPairPerms.IsPaused, LoggerType.PairManagement);

        RecreateLazy(true);

        // push notify after recreating lazy.
        if (MoodlesChanged)
        {
            // only push the notification if they are online.
            if (GetVisibleUsers().Contains(pair.UserData))
            {
                // Handle Moodle permission change
                Logger.LogTrace($"Moodle permissions were changed, pushing change to provider!", LoggerType.PairManagement);
                Mediator.Publish(new MoodlesPermissionsUpdated(pair.PlayerNameWithWorld));
            }
        }

    }

    private bool UpdatingMoodlesPerms(UserPairPermissions newPerms, UserPairPermissions oldPerms)
    {
        return newPerms.AllowPositiveStatusTypes != oldPerms.AllowPositiveStatusTypes
            || newPerms.AllowNegativeStatusTypes != oldPerms.AllowNegativeStatusTypes
            || newPerms.AllowSpecialStatusTypes != oldPerms.AllowSpecialStatusTypes
            || newPerms.PairCanApplyOwnMoodlesToYou != oldPerms.PairCanApplyOwnMoodlesToYou
            || newPerms.PairCanApplyYourMoodlesToYou != oldPerms.PairCanApplyYourMoodlesToYou
            || newPerms.MaxMoodleTime != oldPerms.MaxMoodleTime
            || newPerms.AllowPermanentMoodles != oldPerms.AllowPermanentMoodles
            || newPerms.AllowRemovingMoodles != oldPerms.AllowRemovingMoodles;
    }

    public void UpdatePairUpdateOwnAllUniquePermissions(UserPairUpdateAllUniqueDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        // fetch the current actions that fire events
        var prevMotionPerms = pair.UserPair.OwnPairPerms.AllowMotionRequests;
        var prevAllPerms = pair.UserPair.OwnPairPerms.AllowAllRequests;
        var prevForcedFollowState = pair.UserPair.OwnPairPerms.IsForcedToFollow;
        var prevForcedSitState = pair.UserPair.OwnPairPerms.IsForcedToSit;
        var prevForcedGroundSitState = pair.UserPair.OwnPairPerms.IsForcedToGroundSit;
        var prevForcedStayState = pair.UserPair.OwnPairPerms.IsForcedToStay;
        var prevBlindfoldState = pair.UserPair.OwnPairPerms.IsBlindfolded;

        // see if the states are different.
        bool motionPermsChanged = prevMotionPerms != dto.UniquePerms.AllowMotionRequests;
        bool allPermsChanged = prevAllPerms != dto.UniquePerms.AllowAllRequests;
        bool forcedFollowChanged = prevForcedFollowState != dto.UniquePerms.IsForcedToFollow;
        bool forcedSitChanged = prevForcedSitState != dto.UniquePerms.IsForcedToSit;
        bool forcedGroundSitChanged = prevForcedGroundSitState != dto.UniquePerms.IsForcedToGroundSit;
        bool forcedStayChanged = prevForcedStayState != dto.UniquePerms.IsForcedToStay;
        bool blindfoldChanged = prevBlindfoldState != dto.UniquePerms.IsBlindfolded;

        // update the permissions.
        pair.UserPair.OwnPairPerms = dto.UniquePerms;
        pair.UserPair.OwnEditAccessPerms = dto.UniqueAccessPerms;

        // publish the mediator changes.
        if (motionPermsChanged) UnlocksEventManager.AchievementEvent(UnlocksEvent.PuppeteerAccessGiven, false);
        if (allPermsChanged) UnlocksEventManager.AchievementEvent(UnlocksEvent.PuppeteerAccessGiven, true);
        if (forcedFollowChanged)
        {
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedFollow, dto.UniquePerms.IsForcedToFollow ? NewState.Enabled : NewState.Disabled));
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedFollow, // Forced Follow Command Issued
                (bool)dto.UniquePerms.IsForcedToFollow ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (forcedSitChanged)
        {
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedSit, dto.UniquePerms.IsForcedToSit ? NewState.Enabled : NewState.Disabled));
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedSit, // Forced Sit Command Issued
                (bool)dto.UniquePerms.IsForcedToSit ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (forcedGroundSitChanged)
        {
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedGroundSit, dto.UniquePerms.IsForcedToGroundSit ? NewState.Enabled : NewState.Disabled));
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedSit, // Forced Sit Command Issued
                (bool)dto.UniquePerms.IsForcedToGroundSit ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (forcedStayChanged)
        {
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedStay, dto.UniquePerms.IsForcedToStay ? NewState.Enabled : NewState.Disabled));
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedStay, // Forced Stay Command Issued
                (bool)dto.UniquePerms.IsForcedToStay ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (blindfoldChanged)
        {
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedBlindfold, dto.UniquePerms.IsBlindfolded ? NewState.Enabled : NewState.Disabled));
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedBlindfold, // Forced Blindfold Command Issued
                (bool)dto.UniquePerms.IsBlindfolded ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        Logger.LogDebug($"Updated own unique permissions for '{pair.GetNickname() ?? pair.UserData.AliasOrUID}'", LoggerType.PairManagement);
    }


    public void UpdatePairUpdateOtherAllGlobalPermissions(UserAllGlobalPermChangeDto dto)
    {
        // update the pairs permissions.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }
        pair.UserPair.OtherGlobalPerms = dto.GlobalPermissions;
        Logger.LogDebug($"Updated global permissions for '{pair.GetNickname() ?? pair.UserData.AliasOrUID}'", LoggerType.PairManagement);
    }

    public void UpdatePairUpdateOtherAllUniquePermissions(UserPairUpdateAllUniqueDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }
        pair.UserPair.OtherPairPerms = dto.UniquePerms;
        pair.UserPair.OtherEditAccessPerms = dto.UniqueAccessPerms;
        Logger.LogDebug($"Updated pairs unique permissions for '{pair.GetNickname() ?? pair.UserData.AliasOrUID}'", LoggerType.PairManagement);
    }

    /// <summary>
    /// Updates a global permission of a client pair user.
    /// Edit access is checked server-side to prevent abuse, so these should be all accurate.
    /// </summary>>
    public void UpdateOtherPairGlobalPermission(UserGlobalPermChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedPermission.Key;
        object ChangedValue = dto.ChangedPermission.Value;

        PropertyInfo? propertyInfo = typeof(UserGlobalPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            // If the property exists and is found, update its value
            if (ChangedValue is UInt64 && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                long ticks = (long)(ulong)ChangedValue;
                propertyInfo.SetValue(pair.UserPair.OtherGlobalPerms, TimeSpan.FromTicks(ticks));
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
                Logger.LogDebug($"Updated global permission '{ChangedPermission}' to '{ChangedValue}'", LoggerType.PairManagement);
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy(false);
    }

    private bool IsMoodlePermission(string changedPermission)
    {
        return changedPermission == nameof(UserPairPermissions.AllowPositiveStatusTypes) ||
               changedPermission == nameof(UserPairPermissions.AllowNegativeStatusTypes) ||
               changedPermission == nameof(UserPairPermissions.AllowSpecialStatusTypes) ||
               changedPermission == nameof(UserPairPermissions.PairCanApplyOwnMoodlesToYou) ||
               changedPermission == nameof(UserPairPermissions.PairCanApplyYourMoodlesToYou) ||
               changedPermission == nameof(UserPairPermissions.MaxMoodleTime) ||
               changedPermission == nameof(UserPairPermissions.AllowPermanentMoodles) ||
               changedPermission == nameof(UserPairPermissions.AllowRemovingMoodles);
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
            if (pair.UserPair.OtherPairPerms.IsPaused != (bool)ChangedValue)
                Mediator.Publish(new ClearProfileDataMessage(dto.User));

        // We were the applier, the pair is the target, and we disabled the action
        if (ChangedPermission == "IsForcedToFollow" && (bool)ChangedValue is false)
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedFollow, // Forced Follow Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                ApiController.UID, // We are the enactor
                pair.UserData.UID); // and the pair is the target

        if (ChangedPermission == "IsForcedToSit" || ChangedPermission == "IsForcedToGroundSit" && (bool)ChangedValue is false)
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedSit, // Forced Follow Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                ApiController.UID, // We are the enactor
                pair.UserData.UID); // and the pair is the target

        if (ChangedPermission == "IsForcedToStay" && (bool)ChangedValue is false)
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedStay, // Forced Follow Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                ApiController.UID, // We are the enactor
                pair.UserData.UID); // and the pair is the target

        if (ChangedPermission == "IsBlindfolded" && (bool)ChangedValue is false)
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedBlindfold, // Forced Follow Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                ApiController.UID, // We are the enactor
                pair.UserData.UID); // and the pair is the target

        PropertyInfo? propertyInfo = typeof(UserPairPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            // If the property exists and is found, update its value
            if (ChangedValue is UInt64 && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                long ticks = (long)(ulong)ChangedValue;
                propertyInfo.SetValue(pair.UserPair.OtherPairPerms, TimeSpan.FromTicks(ticks));
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
                Logger.LogDebug($"Updated other pair permission permission '{ChangedPermission}' to '{ChangedValue}'", LoggerType.PairManagement);
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy(false);

        // push notify after recreating lazy.
        if (IsMoodlePermission(ChangedPermission))
        {
            // only push the notification if they are online.
            if (GetVisibleUsers().Contains(pair.UserData))
            {
                // Handle Moodle permission change
                Logger.LogTrace($"Moodle permission '{ChangedPermission}' was changed to '{ChangedValue}', pushing change to provider!", LoggerType.PairManagement);
                Mediator.Publish(new MoodlesPermissionsUpdated(pair.PlayerNameWithWorld));
            }
        }
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
            // If the property exists and is found, update its value
            if (ChangedValue is UInt64 && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                long ticks = (long)(ulong)ChangedValue;
                propertyInfo.SetValue(pair.UserPair.OtherEditAccessPerms, TimeSpan.FromTicks(ticks));
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
                Logger.LogDebug($"Updated other pair access perm '{ChangedPermission}' to '{ChangedValue}'", LoggerType.PairManagement);
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy(false);
    }


    /// <summary>
    /// Updates one of your unique pair permissions you have set with the paired user.
    /// </summary>>
    public void UpdateSelfPairPermission(UserPairPermChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedPermission.Key;
        object ChangedValue = dto.ChangedPermission.Value;

        if (ChangedPermission == "IsPaused" && (pair.UserPair.OwnPairPerms.IsPaused != (bool)ChangedValue))
        {
            Mediator.Publish(new ClearProfileDataMessage(dto.User));
        }

        // store changes pre-apply.
        bool motionPermsChanged = ChangedPermission == nameof(UserPairPermissions.AllowMotionRequests)
            && (pair.UserPair.OwnPairPerms.AllowMotionRequests != (bool)ChangedValue);
        bool allPermsChanged = ChangedPermission == nameof(UserPairPermissions.AllowAllRequests)
            && (pair.UserPair.OwnPairPerms.AllowAllRequests != (bool)ChangedValue);
        bool forcedFollowChanged = ChangedPermission == nameof(UserPairPermissions.IsForcedToFollow)
            && (pair.UserPair.OwnPairPerms.IsForcedToFollow != (bool)ChangedValue);
        bool forcedSitChanged = ChangedPermission == nameof(UserPairPermissions.IsForcedToSit)
            && (pair.UserPair.OwnPairPerms.IsForcedToSit != (bool)ChangedValue);
        bool forcedGroundSitChanged = ChangedPermission == nameof(UserPairPermissions.IsForcedToGroundSit)
            && (pair.UserPair.OwnPairPerms.IsForcedToGroundSit != (bool)ChangedValue);
        bool forcedStayChanged = ChangedPermission == nameof(UserPairPermissions.IsForcedToStay)
            && (pair.UserPair.OwnPairPerms.IsForcedToStay != (bool)ChangedValue);
        bool blindfoldChanged = ChangedPermission == nameof(UserPairPermissions.IsBlindfolded)
            && (pair.UserPair.OwnPairPerms.IsBlindfolded != (bool)ChangedValue);

        PropertyInfo? propertyInfo = typeof(UserPairPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            // If the property exists and is found, update its value
            if (ChangedValue is UInt64 && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                long ticks = (long)(ulong)ChangedValue;
                propertyInfo.SetValue(pair.UserPair.OwnPairPerms, TimeSpan.FromTicks(ticks));
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
                Logger.LogDebug($"Updated self pair permission '{ChangedPermission}' to '{ChangedValue}'", LoggerType.PairManagement);
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }

        // Handle special cases AFTER the change was made.
        if (motionPermsChanged)
        {
            Logger.LogInformation("Motion perms changed", LoggerType.PairManagement);
            UnlocksEventManager.AchievementEvent(UnlocksEvent.PuppeteerAccessGiven, false);
        }
        if (allPermsChanged)
        {
            Logger.LogInformation("All perms changed", LoggerType.PairManagement);
            UnlocksEventManager.AchievementEvent(UnlocksEvent.PuppeteerAccessGiven, true);
        }
        if (forcedFollowChanged)
        {
            Logger.LogInformation("Forced follow changed", LoggerType.PairManagement);
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedFollow, (bool)ChangedValue ? NewState.Enabled : NewState.Disabled));
            
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedFollow, // Forced Follow Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (forcedSitChanged)
        {
            Logger.LogInformation("Forced sit changed", LoggerType.PairManagement);
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedSit, (bool)ChangedValue ? NewState.Enabled : NewState.Disabled));
            
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedSit, // Forced Sit Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (forcedGroundSitChanged)
        {
            Logger.LogInformation("Forced ground sit changed", LoggerType.PairManagement);
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedGroundSit, (bool)ChangedValue ? NewState.Enabled : NewState.Disabled));
            
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedSit, // Forced Sit Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (forcedStayChanged)
        {
            Logger.LogInformation("Forced stay changed", LoggerType.PairManagement);
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedStay, (bool)ChangedValue ? NewState.Enabled : NewState.Disabled));
            
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedStay, // Forced Stay Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }
        if (blindfoldChanged)
        {
            Logger.LogInformation("Blindfold changed to: "+(bool)ChangedValue, LoggerType.PairManagement);
            Mediator.Publish(new HardcoreActionMessage(pair, HardcoreActionType.ForcedBlindfold, (bool)ChangedValue ? NewState.Enabled : NewState.Disabled));
            
            UnlocksEventManager.AchievementEvent(UnlocksEvent.HardcoreForcedPairAction,
                HardcorePairActionKind.ForcedBlindfold, // Forced Blindfold Command Issued
                (bool)ChangedValue ? NewState.Enabled : NewState.Disabled, // It Started/Stopped.
                pair.UserData.UID, // the pair was the enactor.
                ApiController.UID); // and we were the target
        }

        RecreateLazy(false);

        // push notify after recreating lazy.
        if (IsMoodlePermission(ChangedPermission))
        {
            // Handle Moodle permission change
            Logger.LogTrace($"Moodle permission '{ChangedPermission}' was changed to '{ChangedValue}', pushing change to provider!", LoggerType.PairManagement);
            Mediator.Publish(new MoodlesPermissionsUpdated(pair.PlayerNameWithWorld));
        }
    }

    /// <summary>
    /// Updates an edit access permission that you've set for the paired user.
    /// </summary>>
    public void UpdateSelfPairAccessPermission(UserPairAccessChangeDto dto)
    {
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) { throw new InvalidOperationException("No such pair for " + dto); }

        string ChangedPermission = dto.ChangedAccessPermission.Key;
        object ChangedValue = dto.ChangedAccessPermission.Value;

        PropertyInfo? propertyInfo = typeof(UserEditAccessPermissions).GetProperty(ChangedPermission);
        if (propertyInfo != null)
        {
            if (propertyInfo.CanWrite)
            {
                // convert the value to the appropriate type before setting.
                object value = Convert.ChangeType(ChangedValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(pair.UserPair.OwnEditAccessPerms, value);
                Logger.LogDebug($"Updated self pair access permission '{ChangedPermission}' to '{ChangedValue}'", LoggerType.PairManagement);
            }
            else
            {
                Logger.LogError($"Property '{ChangedPermission}' not found or cannot be updated.");
            }
        }
        RecreateLazy(false);
    }
}
