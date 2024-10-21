using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Permissions;
using Lumina.Excel.GeneratedSheets;
using System.Text.RegularExpressions;

namespace GagSpeak.PlayerData.Handlers;

public class PuppeteerHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerChara;
    private readonly PairManager _pairManager;
    private readonly IDataManager _dataManager;


    public PuppeteerHandler(ILogger<PuppeteerHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PlayerCharacterData playerChara,
        PairManager pairManager, IDataManager dataManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _playerChara = playerChara;
        _pairManager = pairManager;
        _dataManager = dataManager;

        Mediator.Subscribe<UpdateCharacterListenerForUid>(this, (msg) =>
        {
            UpdatePlayerInfoForUID(msg.Uid, msg.CharName, msg.CharWorld);
        });
    }

    public Pair? SelectedPair = null; // Selected Pair we are viewing for Puppeteer.

    // Store an accessor of the alarm being edited.
    private AliasStorage? _storageBeingEdited;
    public string UidOfStorage => SelectedPair?.UserData.UID ?? string.Empty;
    public AliasStorage StorageBeingEdited
    {
        get
        {
            if (_storageBeingEdited == null && UidOfStorage != string.Empty)
            {
                _storageBeingEdited = _clientConfigs.FetchAliasStorageForPair(UidOfStorage);
            }
            return _storageBeingEdited!;
        }
        private set => _storageBeingEdited = value;
    }
    public bool EditingListIsNull => StorageBeingEdited == null;

    #region PuppeteerSettings

    public bool MessageContainsPairTriggerPhrase(string message)
    {
        foreach (var pair in _pairManager.DirectPairs)
        {
            string[] triggers = pair.UserPairUniquePairPerms.TriggerPhrase.Split("|").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (triggers.Any(trigger => message.Contains(trigger)))
            {
                return true;
            }
        }
        return false;
    }


    public void UpdateDisplayForNewPair(Pair pair)
    {
        // for firstime generations
        if (SelectedPair == null)
        {
            SelectedPair = pair;
            StorageBeingEdited = _clientConfigs.FetchAliasStorageForPair(pair.UserData.UID);
        }

        // for refreshing data once we switch pairs.
        if (SelectedPair.UserData.UID != pair.UserData.UID)
        {
            Logger.LogTrace($"Updating display to reflect pair " + pair.UserData.AliasOrUID, LoggerType.Puppeteer);
            SelectedPair = pair;
            StorageBeingEdited = _clientConfigs.FetchAliasStorageForPair(pair.UserData.UID);
        }
        // log if the storage being edited is null.
        if (StorageBeingEdited == null)
        {
            Logger.LogWarning($"Storage being edited is null for pair " + pair.UserData.AliasOrUID, LoggerType.Puppeteer);
        }
    }


    public void UpdatedEditedStorage()
    {
        // update the set in the client configs
        _clientConfigs.UpdateAliasStorage(UidOfStorage, StorageBeingEdited);
    }

    // Only intended to be called via the AliasStorage Callback dto.
    public void UpdatePlayerInfoForUID(string uid, string charaName, string charaWorld)
        => _clientConfigs.UpdateAliasStoragePlayerInfo(uid, charaName, charaWorld);


    public AliasStorage GetAliasStorage(string pairUID)
        => _clientConfigs.FetchAliasStorageForPair(pairUID);

    public string? GetUIDMatchingSender(string name, string world)
        => _clientConfigs.GetUidMatchingSender(name, world);

    public void AddAlias(AliasTrigger alias)
        => _clientConfigs.AddNewAliasTrigger(UidOfStorage, alias);
    public void RemoveAlias(AliasTrigger alias)
        => _clientConfigs.RemoveAliasTrigger(UidOfStorage, alias);

    public void UpdateAliasInput(int aliasIndex, string input)
    {
        StorageBeingEdited.AliasList[aliasIndex].InputCommand = input;
        _clientConfigs.AliasDataModified(UidOfStorage);
    }

    public void UpdateAliasOutput(int aliasIndex, string output)
    {
        StorageBeingEdited.AliasList[aliasIndex].OutputCommand = output;
        _clientConfigs.AliasDataModified(UidOfStorage);
    }

    public void UpdateAliasEnabled(int aliasIndex, bool enabled)
    {
        StorageBeingEdited.AliasList[aliasIndex].Enabled = enabled;
        _clientConfigs.AliasDataModified(UidOfStorage);
    }

    #endregion PuppeteerSettings
    #region PuppeteerTriggerDetection

    public List<string> GetPlayersToListenFor()
        => _clientConfigs.GetPlayersToListenFor();

    public Pair? GetPairOfUid(string uid)
        => _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == uid);

    /// <summary> Checks if the message contains the global trigger word </summary>
    /// <returns> True if the message contains the trigger word, false if not </returns>
    public bool IsValidGlobalTriggerWord(SeString chatmessage, XivChatType type)
    {
        if (_playerChara.GlobalPerms == null) return false;
        var triggerPhrase = _playerChara.GlobalPerms.GlobalTriggerPhrase;

        // see if it contains your trigger word for them
        if (string.IsNullOrEmpty(triggerPhrase) || string.IsNullOrWhiteSpace(triggerPhrase)) return false;

        // otherwise, see if we matched the trigger word
        var match = MatchTriggerWord(chatmessage.TextValue, triggerPhrase);
        if (match.Success) return true;

        // if it didnt match, return false
        return false;
    }

    public bool IsValidPuppeteerTriggerWord(List<string> triggerPhrases, SeString chatMessage)
    {
        // check to see if any of the triggers are valid triggers.
        foreach (string triggerWord in triggerPhrases)
        {
            if (string.IsNullOrEmpty(triggerWord) || string.IsNullOrWhiteSpace(triggerWord)) continue;

            // attempt to match it.
            var match = MatchTriggerWord(chatMessage.TextValue, triggerWord);
            if (!match.Success) continue;

            // return true if it was
            return true;
        }

        return false;
    }

    public SeString NewMessageFromGlobalTrigger(SeString chatmessage, XivChatType type)
    {
        if (_playerChara.GlobalPerms == null)
        {
            Logger.LogWarning("Global Permissions are null");
            return new SeString();
        }

        var triggerPhrase = _playerChara.GlobalPerms.GlobalTriggerPhrase;

        // get the string we are returning
        SeString result = new SeString();

        // Obtain the new message to send.
        var msgToSend = GetMessageToSend(chatmessage.TextValue, triggerPhrase);

        // throw exception if string is empty.
        if (msgToSend == string.Empty)
        {
            Logger.LogWarning("Message to send is empty. This should not occur!");
            return new SeString();
        }

        // now get the channel we received the message from.
        var incomingChannel = ChatChannel.GetChatChannelFromXivChatType(type);
        // if it isnt any of our active channels then we just dont wanna even process it
        if (incomingChannel == null || !IsValidPuppeteerChannel(incomingChannel.Value))
        {
            Logger.LogWarning("Channel is not valid for puppeteer to process");
            return new SeString();
        }

        // things are valid, so ensure our criteria, and return.
        result = msgToSend;
        // ensure it meets the criteria we have set for the global settings. and if so, construct the string.
        if (MeetsGlobalSettingCriteria(result))
        {
            return result;
        }
        else
        {
            // return an empty sestring
            return new SeString();
        }
    }

    public SeString NewMessageFromPuppeteerTrigger(List<string> triggerPhrases, string SenderUid,
        UserPairPermissions permsForSender, SeString chatMessage, XivChatType type)
    {
        // check to see if any of the triggers are valid triggers.
        foreach (string triggerWord in triggerPhrases)
        {
            //Logger.LogTrace("Checking trigger word: {0}", triggerWord);

            if (string.IsNullOrEmpty(triggerWord) || string.IsNullOrWhiteSpace(triggerWord)) continue;

            // attempt to match it.
            var match = MatchTriggerWord(chatMessage.TextValue, triggerWord);
            if (!match.Success) continue;

            //Logger.LogTrace("Matched trigger word: {0}", triggerWord);

            // obtain the substring of the match comes after the trigger phrase.
            SeString remainingMessage = chatMessage.TextValue.Substring(match.Index + match.Length).Trim();

            //Logger.LogTrace("Remaining message: {0}", remainingMessage);

            // obtqain the substring within the start and end char if provided.
            remainingMessage = GetSubstringWithinParentheses(remainingMessage.TextValue, permsForSender.StartChar, permsForSender.EndChar);

            //Logger.LogTrace("Remaining message after brackets: {0}", remainingMessage);


            //Logger.LogTrace("Checking for Aliases");
            remainingMessage = ConvertAliasCommandsIfAny(SenderUid, remainingMessage.TextValue);

            //Logger.LogTrace("Remaining message after aliases: {0}", remainingMessage);


            // if the substring in the custom brackets is not null or empty, then convert brackets to angled and return.
            if (remainingMessage.TextValue.IsNullOrEmpty()) return new SeString();

            remainingMessage = ConvertSquareToAngleBrackets(remainingMessage.TextValue);

            if (MeetsSettingCriteria(permsForSender, remainingMessage))
            {
                return remainingMessage;
            }
            else
            {
                // return an empty sestring
                Logger.LogDebug("Message did not meet the criteria for the sender", LoggerType.Puppeteer);
                return new SeString();
            }
        }
        // return blank string if invalid.
        Logger.LogDebug("No valid trigger word found in the message", LoggerType.Puppeteer);
        return new SeString();
    }


    public bool MeetsGlobalSettingCriteria(SeString messageRecieved)
    {
        // Check for sit commands
        if (_playerChara.GlobalPerms.GlobalAllowSitRequests)
        {
            if (messageRecieved.TextValue == "sit" || messageRecieved.TextValue == "groundsit") return true;
        }

        // check for emote commands.
        if (_playerChara.GlobalPerms.GlobalAllowMotionRequests)
        {
            // we can check to see if it is a valid emote
            var emotes = _dataManager.GetExcelSheet<Emote>();
            if (emotes != null)
            {
                if (emotes.Any(emotes => messageRecieved.TextValue == emotes.Name.RawString.Replace(" ", "").ToLower())) return true;

                if (messageRecieved.TextValue == "cpose") return true;
            }
        }

        // check for all commands
        if (_playerChara.GlobalPerms.GlobalAllowAllRequests) return true;

        // if we reach here, it means we dont meet the criteria
        return false;
    }


    // Only call if contains Global Trigger is true.
    public string GetMessageToSend(string messageRecieved, string triggerword)
    {
        // re-verify
        var match = MatchTriggerWord(messageRecieved, triggerword);
        if (match.Success)
        {
            string newMessage = messageRecieved.Substring(match.Index + match.Length).Trim();
            newMessage = GetGlobalSubstringWithinParentheses(newMessage);
            if (newMessage != null)
            {
                newMessage = ConvertSquareToAngleBrackets(newMessage);
                return newMessage;
            }
        }
        // failed, return string empty
        return string.Empty;
    }

    public bool IsValidPuppeteerChannel(ChatChannel.Channels chatChannel)
        => _clientConfigs.GagspeakConfig.ChannelsPuppeteer.Contains(chatChannel);


    public bool MeetsSettingCriteria(UserPairPermissions permsForSender, SeString messageRecieved)
    {
        // handle sit commands
        if (permsForSender.AllowSitRequests)
        {
            Logger.LogTrace("Checking if message is a sit command", LoggerType.Puppeteer);
            if (messageRecieved.TextValue == "sit" || messageRecieved.TextValue == "groundsit") return true;
        }

        // handle motion commands
        if (permsForSender.AllowMotionRequests)
        {
            Logger.LogTrace("Checking if message is a motion command", LoggerType.Puppeteer);
            // we can check to see if it is a valid emote
            var emotes = _dataManager.GetExcelSheet<Emote>();
            if (emotes != null)
            {
                if (emotes.Any(emotes => messageRecieved.TextValue == emotes.Name.RawString.Replace(" ", "").ToLower()))
                {
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.PuppeteerEmoteSent, messageRecieved.TextValue);
                    return true;
                }

                if (messageRecieved.TextValue == "cpose") return true;
            }
        }

        // handle all commands
        if (permsForSender.AllowAllRequests)
        {
            Logger.LogTrace("Checking if message is an all command", LoggerType.Puppeteer);
            return true;
        }

        // Failure
        return false;
    }

    public SeString ConvertAliasCommandsIfAny(string SenderUid, string puppeteerMessageToSend)
    {
        // now we can use this index to scan our aliasLists
        List<AliasTrigger> Triggers = GetAliasStorage(SenderUid).AliasList;

        Logger.LogTrace("Found " + Triggers.Count + " alias triggers for this user", LoggerType.Puppeteer);

        // sort by decending length so that shorter equivalents to not override longer variants.
        var sortedAliases = Triggers.OrderByDescending(alias => alias.InputCommand.Length);
        // see if our message contains any of the alias strings. For it to match, it must match the full alias string.
        foreach (AliasTrigger alias in Triggers)
        {
            // if the alias is enabled and in our message
            if (alias.Enabled && !string.IsNullOrWhiteSpace(alias.InputCommand) &&
                !string.IsNullOrWhiteSpace(alias.OutputCommand) && puppeteerMessageToSend.Contains(alias.InputCommand))
            {
                // replace the alias command with the output command
                puppeteerMessageToSend = puppeteerMessageToSend.Replace(alias.InputCommand, alias.OutputCommand);
            }
        }
        return puppeteerMessageToSend;
    }

    /// <summary> encapsulates the puppeteer command within '(' and ')' </summary>
    private string GetSubstringWithinParentheses(string str, char startBracket, char EndBracket)
    {
        int startIndex = str.IndexOf(startBracket);
        int endIndex = str.IndexOf(EndBracket);

        if (startIndex >= 0 && endIndex >= 0 && endIndex > startIndex)
        {
            return str.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
        }
        return str;
    }

    /// <summary> encapsulates the puppeteer command within '(' and ')' </summary>
    private string GetGlobalSubstringWithinParentheses(string str)
    {
        int startIndex = str.IndexOf('(');
        int endIndex = str.IndexOf(')');

        if (startIndex >= 0 && endIndex >= 0 && endIndex > startIndex)
        {
            return str.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
        }
        return str;
    }

    /// <summary> Converts square brackets to angle brackets </summary>
    private string ConvertSquareToAngleBrackets(string str) => str.Replace("[", "<").Replace("]", ">");

    private Match MatchTriggerWord(string message, string triggerWord)
    {
        var triggerRegex = $@"(?<=^|\s){triggerWord}(?=[^a-z])";
        return Regex.Match(message, triggerRegex);
    }


    #endregion PuppeteerTriggerDetection
}
