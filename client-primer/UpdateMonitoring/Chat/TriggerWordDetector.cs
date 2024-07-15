/*using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using GagSpeak.ChatMessages;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.UpdateMonitoring.Chat.Handler;

namespace UpdateMonitoring.Chat;
/// <summary>
/// Used for checking messages send to the games chatbox, not meant for detouring or injection
/// Messages passed through here are scanned to see if they are encoded, for puppeteer, or include any hardcore features.
public class TriggerWordDetector
{
    private readonly ILogger<TriggerWordDetector> _logger;                // logger for the class
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PuppeteerHandler _puppeteerMediator;                 // puppeteer mediator

    /// <summary> This is the constructor for the OnChatMsgManager class. </summary>
    public TriggerWordDetector(ILogger<TriggerWordDetector> logger,
        ClientConfigurationManager clientConfigs, PuppeteerHandler puppeteerMediator)
    {
        _logger = logger;
        _clientConfigs = clientConfigs;
        _puppeteerMediator = puppeteerMediator;
    }

    public bool IsValidGlobalTriggerWord(SeString chatmessage, XivChatType type, out SeString messageToSend)
    {
        // create the string that will be sent out
        messageToSend = new SeString();
        // see if it contains your trigger word for them
        if (_puppeteerMediator.ContainsGlobalTriggerWord(chatmessage.TextValue, out var globalPuppeteerMessageToSend))
        {
            // contained the trigger word, so process it.
            if (globalPuppeteerMessageToSend != string.Empty)
            {
                // set the message to send
                messageToSend = globalPuppeteerMessageToSend;
                // now get the incoming chattype converted to our chat channel,
                var incomingChannel = ChatChannel.GetChatChannelFromXivChatType(type);
                // if it isnt any of our active channels then we just dont wanna even process it
                if (incomingChannel != null)
                {
                    // it isnt null meaning it is eithing the channels so now we can check if it meets the criteria
                    if (_config.ChannelsPuppeteer.Contains(incomingChannel.Value)
                    && _puppeteerMediator.MeetsGlobalSettingCriteria(messageToSend))
                    {
                        return true;
                    }
                    else
                    {
                        _logger.LogDebug($"[TriggerWordDetector] Not an Enabled Chat Channel, or command didnt abide by your settings aborting");
                        return false;
                    }
                }
                else
                {
                    _logger.LogDebug($"[TriggerWordDetector] Not an Enabled Chat Channel, aborting");
                    return false;
                }
            }
            else
            {
                _logger.LogDebug($"[TriggerWordDetector] Puppeteer message to send was empty, aborting");
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    public bool IsValidPuppeteerTriggerWord(string senderName, SeString chatmessage, XivChatType type, ref bool isHandled, out SeString messageToSend)
    {
        // create the string that will be sent out
        messageToSend = new SeString();
        // see if it contains your trigger word for them
        if (_puppeteerMediator.ContainsTriggerWord(senderName, chatmessage.TextValue, out var puppeteerMessageToSend))
        {
            if (puppeteerMessageToSend != string.Empty)
            {
                // apply any alias translations, if any
                messageToSend = _puppeteerMediator.ConvertAliasCommandsIfAny(senderName, puppeteerMessageToSend);
                // now get the incoming chattype converted to our chat channel,
                var incomingChannel = ChatChannel.GetChatChannelFromXivChatType(type);
                // if it isnt any of our active channels then we just dont wanna even process it
                if (incomingChannel != null)
                {
                    // it isnt null meaning it is eithing the channels so now we can check if it meets the criteria
                    if (_config.ChannelsPuppeteer.Contains(incomingChannel.Value))
                    {
                        if (_puppeteerMediator.MeetsSettingCriteria(senderName, messageToSend))
                        {
                            return true;
                        }
                        else
                        {
                            _logger.LogDebug($"[TriggerWordDetector] Command didnt abide by your settings aborting");
                            return false;
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"[TriggerWordDetector] Not an Enabled Chat Channel, aborting");
                        return false;
                    }
                }
                else
                {
                    _logger.LogDebug($"[TriggerWordDetector] Not an Enabled Chat Channel, aborting");
                    return false;
                }
            }
            else
            {
                _logger.LogDebug($"[TriggerWordDetector] Puppeteer message to send was empty, aborting");
                return false;
            }
        }
        else
        {
            return false;
        }
    }
}
*/
