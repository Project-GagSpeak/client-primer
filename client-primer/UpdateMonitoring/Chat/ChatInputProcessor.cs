using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Extensions;
using Lumina.Excel.GeneratedSheets;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

// I swear to god, if any contributors even attempt to tinker with this file, I will swat you over the head. DO NOT DO IT.
namespace GagSpeak.UpdateMonitoring.Chat.ChatMonitors;

/// <summary> 
/// This class is used to handle the chat input detouring for the GagSpeak plugin
/// It is very dangerous to tinker with. 
/// If you ever decide to use this for a plugin of your own, take extreme caution..
/// </summary>
public unsafe class ChatInputDetour : IDisposable
{
    private readonly ILogger<ChatInputDetour> _logger;
    private readonly GagspeakConfigService _config;
    private readonly GagspeakMediator _mediator;
    private readonly PlayerCharacterData _playerManager;
    private readonly GagManager _gagManager;
    private readonly EmoteMonitor _emoteMonitor;

    // define our delegates.
    private unsafe delegate byte ProcessChatInputDelegate(IntPtr uiModule, byte** message, IntPtr a3);
    [Signature("E8 ?? ?? ?? ?? FE 86 ?? ?? ?? ?? C7 86 ?? ?? ?? ?? ?? ?? ?? ??", DetourName = nameof(ProcessChatInputDetour), Fallibility = Fallibility.Auto)]
    private Hook<ProcessChatInputDelegate> ProcessChatInputHook { get; set; } = null!;

    internal ChatInputDetour(ILogger<ChatInputDetour> logger, GagspeakMediator mediator,
        GagspeakConfigService config, PlayerCharacterData playerManager, GagManager gagManager,
        EmoteMonitor emoteMonitor, ISigScanner scanner, IGameInteropProvider interop)
    {
        // initialize the classes
        _logger = logger;
        _mediator = mediator;
        _config = config;
        _playerManager = playerManager;
        _gagManager = gagManager;
        _emoteMonitor = emoteMonitor;

        // try to get the chat-input-interceptor delegate
        try
        {
            // initialize the interop.
            interop.InitializeFromAttributes(this);
            // attempt to enable the hook.
            ProcessChatInputHook.Enable();
        }
        catch
        {
            // prevent the rest of the constructor from initializing.
            throw new Exception("ChatInputInterceptor Failed to find the correct Address / Signature!");
        }
    }

    /// <summary>
    /// Process each new input into the chat, intercepting it prior to it reaching the server.
    /// </summary>
    private unsafe byte ProcessChatInputDetour(IntPtr uiModule, byte** message, IntPtr a3)
    {
        // Put all this shit in a try-catch loop so we can catch any possible thrown exception.
        try
        {
            // if our chat garbler is disabled or we dont have any active gags, dont worry about anything past this point and return original.
            if(_playerManager.GlobalPerms is null) 
                return ProcessChatInputHook.Original(uiModule, message, a3); 

            // Grab the original string.
            var originalSeString = MemoryHelper.ReadSeStringNullTerminated((nint)(*message));
            var messageDecoded = originalSeString.ToString(); // the decoded message format.

            // Debug the output (remove later)
            foreach (var payload in originalSeString.Payloads)
                _logger.LogTrace($"Message Payload [{payload.Type}]: {payload.ToString()}", LoggerType.ChatDetours);

            if (string.IsNullOrWhiteSpace(messageDecoded))
            {
                _logger.LogTrace("Message was null or whitespace, returning original.", LoggerType.ChatDetours);
                return ProcessChatInputHook.Original(uiModule, message, a3);
            }

            // Create the new string to send.
            var newSeStringBuilder = new SeStringBuilder();

            // If we are being forced to emote, cancel all emote commands.
            if (!_playerManager.GlobalPerms.ForcedEmoteState.NullOrEmpty())
            {
                // cancel all emote commands if we are in a forced emote state.
                if (messageDecoded.StartsWith("/"))
                {
                    // Grab current EmoteID.
                    var emoteId = _emoteMonitor.CurrentEmoteId();
                    var blockersList = EmoteMonitor.IsSittingAny(emoteId) ? EmoteMonitor.EmoteCommandsYesNoAccepted : EmoteMonitor.EmoteCommands;
                    // cancel the message if it is a /cpose while forced to sit and on our knees, deny it.
                    if (blockersList.Any(cmd => messageDecoded.Contains(cmd, StringComparison.OrdinalIgnoreCase)))
                    {
                        _mediator.Publish(new NotifyChatMessage("You Tried to Execute an Emote while being Forced to Stay!", NotificationType.Warning));
                        _logger.LogTrace("Attempted to execute emote while being forced to emote. Blocking!", LoggerType.HardcoreMovement);

                        return 0;
                    }
                }
            }

            // If we are not meant to garble the message, then return original.
            if (!_playerManager.GlobalPerms.LiveChatGarblerActive || !_gagManager.AnyGagActive) 
                return ProcessChatInputHook.Original(uiModule, message, a3);

            /* -------------------------- MUFFLERCORE / GAGSPEAK CHAT GARBLER TRANSLATION LOGIC -------------------------- */
            var matchedCommand = "";
            var matchedChannelType = "";
            // At this point, make sure that the message is not a command, if it is we should ignore it.
            _logger.LogTrace($"Detouring Message: {messageDecoded}", LoggerType.ChatDetours);
            if (messageDecoded.StartsWith("/"))
            {
                // Match Command if Command being used is in our list of allowed Channels to translate in.
                var allowedChannels = _config.Current.ChannelsGagSpeak.GetChatChannelsListAliases();
                matchedCommand = allowedChannels.FirstOrDefault(prefix => messageDecoded.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                // This means its not a chat channel command and just a normal command, so return original.
                if (matchedCommand.IsNullOrEmpty())
                {
                    _logger.LogTrace("Ignoring Message as it is a command", LoggerType.ChatDetours);
                    return ProcessChatInputHook.Original(uiModule, message, a3);
                }

                // Set the matched command to the matched channel type. 
                matchedChannelType = matchedCommand;

                // if tell command is matched, need extra step to protect target name
                if (matchedCommand.StartsWith("/tell") || matchedCommand.StartsWith("/t"))
                {
                    _logger.LogTrace($"[Chat Processor]: Matched Command is a tell command");
                    /// Using /gag command on yourself sends /tell which should be caught by this
                    /// Depends on <seealso cref="MsgEncoder.MessageEncoder"/> message to start like :"/tell {targetPlayer} *{playerPayload.PlayerName}"
                    /// Since only outgoing tells are affected, {targetPlayer} and {playerPayload.PlayerName} will be the same
                    var selfTellRegex = @"(?<=^|\s)/t(?:ell)?\s{1}(?<name>\S+\s{1}\S+)@\S+\s{1}\*\k<name>(?=\s|$)";
                    if (!Regex.Match(messageDecoded, selfTellRegex).Value.IsNullOrEmpty())
                    {
                        _logger.LogTrace("[Chat Processor]: Ignoring Message as it is a self tell garbled message.");
                        return ProcessChatInputHook.Original(uiModule, message, a3);
                    }
                    // Match any other outgoing tell to preserve target name
                    var tellRegex = @"(?<=^|\s)/t(?:ell)?\s{1}(?:\S+\s{1}\S+@\S+|\<r\>)\s?(?=\S|\s|$)";
                    matchedCommand = Regex.Match(messageDecoded, tellRegex).Value;
                }
                _logger.LogTrace($"Matched Command [{matchedCommand}] for matchedChannelType: [{matchedChannelType}]", LoggerType.ChatDetours);
            }

            // If current channel message is being sent to is in list of enabled channels, translate it.
            if (_config.Current.ChannelsGagSpeak.Contains(ChatChannel.GetChatChannel()) || _config.Current.ChannelsGagSpeak.IsAliasForAnyActiveChannel(matchedChannelType.Trim()))
            {
                try
                {
                    // see if this is an outgoing tell, if it is, we must make sure it isn't garbled for encoded messages
                    if (ChatChannel.GetChatChannel() == ChatChannel.Channels.Tell || matchedChannelType.Contains("/t") || matchedChannelType.Contains("/tell"))
                    {
                        // it is a tell, we need to make sure it is not garbled if it is an encoded message
                        _logger.LogTrace($"Message is a outgoing tell message, skipping garbling", LoggerType.ChatDetours);
                        return ProcessChatInputHook.Original(uiModule, message, a3);
                    }

                    // only obtain the text payloads from this message, as nothing else should madder.
                    var textPayloads = originalSeString.Payloads.OfType<TextPayload>().ToList();

                    // merge together the text of all the split text payloads.
                    var originalText = string.Join("", textPayloads.Select(tp => tp.Text));

                    // after we have done that, take this string and get the substring with the matched command length.
                    var stringToProcess = originalText.Substring(matchedCommand.Length);

                    // once we have done that, garble that string, and then merge it back with the output command in front.
                    var output = matchedCommand + _gagManager.ProcessMessage(stringToProcess);

                    // append this to the newSeStringBuilder.
                    newSeStringBuilder.Add(new TextPayload(output));

                    // DEBUG MESSAGE: (Remove when not debugging)
                    _logger.LogTrace("Output: "+output, LoggerType.ChatDetours);

                    // if the message is empty add a blank text payload.
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        _logger.LogTrace("Message was null or whitespace, forbidding text from sending", LoggerType.ChatDetours);
                        return 0;
                    }


                    // Construct it for finalization.
                    var newSeString = newSeStringBuilder.Build();

                    // Verify its a legal width
                    if (newSeString.TextValue.Length <= 500)
                    {
                        var utf8String = Utf8String.FromString(".");
                        utf8String->SetString(newSeString.Encode());
                        return ProcessChatInputHook.Original(uiModule, (byte**)((nint)utf8String).ToPointer(), a3);
                    }
                    else // return original if invalid.
                    {
                        _logger.LogError("Chat Garbler Variant of Message was longer than max message length!");
                        return ProcessChatInputHook.Original(uiModule, message, a3);
                    }
                }
                catch (Exception e)
                {   
                    // if at any point we fail here, throw an exception.
                    _logger.LogError($"Error sending message to chat box: {e}");
                }
            }
        }
        catch (Exception e)
        { // cant ever have enough safety!
            _logger.LogError($"Error sending message to chat box (secondary): {e}");
        }
        // return the original message untranslated
        return ProcessChatInputHook.Original(uiModule, message, a3);
    }

    // method to disable the hook
    protected void Disable()
    {
        ProcessChatInputHook?.Disable();
    }

    // method to dispose of the hook, self Explanatory
    public void Dispose()
    {
        ProcessChatInputHook?.Disable();
        ProcessChatInputHook?.Dispose();
    }
}

