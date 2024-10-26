using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Data;
using GagspeakAPI.Extensions;
using System.Runtime.InteropServices;

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
    private readonly PlayerCharacterData _playerManager;
    private readonly GagManager _gagManager;
    private readonly EmoteMonitor _emoteMonitor;

    // define our delegates.
    private unsafe delegate byte ProcessChatInputDelegate(IntPtr uiModule, byte** message, IntPtr a3);
    [Signature("E8 ?? ?? ?? ?? FE 86 ?? ?? ?? ?? C7 86 ?? ?? ?? ?? ?? ?? ?? ??", DetourName = nameof(ProcessChatInputDetour), Fallibility = Fallibility.Auto)]
    private Hook<ProcessChatInputDelegate> ProcessChatInputHook { get; set; } = null!;

    internal ChatInputDetour(ILogger<ChatInputDetour> logger, GagspeakConfigService config,
        PlayerCharacterData playerManager, GagManager gagManager,
        EmoteMonitor emoteMonitor, ISigScanner scanner, IGameInteropProvider interop)
    {
        // initialize the classes
        _logger = logger;
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
            // begin the chat-input bit counter.
            var bc = 0;
            int matchSequence = 0; // (TODO: Consider Removing this) 4 means that message contains autocomplete marker so we ignore message completely

            // Check each bit in the length of the message (cannot exceed 500 bits) for an invalid send.
            for (var i = 0; i <= 500; i++)
            {
                // match autocomplete byte pattern 02 2e ... f2 ... 03
                if (i + 5 < 500 && (*message)[i] == 0x02 && (*message)[i + 1] == 0x2e) matchSequence += 2;
                if ((*message)[i] == 0xf2 && matchSequence == 2) matchSequence++;
                if ((*message)[i] == 0x03 && matchSequence == 3) matchSequence++;
                // if message contain autocomplete matchSequence will be 4
                if (matchSequence == 4) break;

                if (*(*message + i) != 0) continue; // if the message is empty, break
                bc = i; // increment bc
                break;
            }
            if (bc < 2 || bc > 500 || matchSequence == 4)
            {
                // If message is invalid, so send original untranslated text.
                return ProcessChatInputHook.Original(uiModule, message, a3);
            }

            // if our chat garbler is disabled or we dont have any active gags, dont worry about anything past this point and return original.
            if(_playerManager.GlobalPerms is null) 
            { 
                return ProcessChatInputHook.Original(uiModule, message, a3); 
            }

            // Handle unique condition for being in a forced sit state.
            if (!_playerManager.GlobalPerms.ForcedEmoteState.NullOrEmpty())
            {
                // cancel all emote commands if we are in a forced emote state.
                var emoteAttemptCmd = Encoding.UTF8.GetString(*message, bc);
                if (emoteAttemptCmd.StartsWith("/"))
                {
                    // cancel the message if it is a /cpose while forced to sit and on our knees, deny it.
                    if (EmoteMonitor.EmoteCommands.Any(cmd => emoteAttemptCmd.Contains(cmd, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogTrace("Attempted to execute emote while being forced to emote. Blocking!", LoggerType.HardcoreMovement);
                        // Send an empty string
                        var emptyString = "";
                        var emptyBytes = Encoding.UTF8.GetBytes(emptyString);
                        var mem1 = Marshal.AllocHGlobal(400);
                        var mem2 = Marshal.AllocHGlobal(emptyBytes.Length + 30);
                        Marshal.Copy(emptyBytes, 0, mem2, emptyBytes.Length);
                        Marshal.WriteByte(mem2 + emptyBytes.Length, 0);
                        Marshal.WriteInt64(mem1, mem2.ToInt64());
                        Marshal.WriteInt64(mem1 + 8, 64);
                        Marshal.WriteInt64(mem1 + 8 + 8, emptyBytes.Length + 1);
                        Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);
                        var r = ProcessChatInputHook.Original(uiModule, (byte**)mem1.ToPointer(), a3);
                        Marshal.FreeHGlobal(mem1);
                        Marshal.FreeHGlobal(mem2);
                        return r;
                    }
                }
            }


            // Handle Chat Garbling.
            if (!_playerManager.GlobalPerms.LiveChatGarblerActive || !_gagManager.AnyGagActive) 
            { 
                return ProcessChatInputHook.Original(uiModule, message, a3);
            }


            /* -------------------------- MUFFLERCORE / GAGSPEAK CHAT GARBLER TRANSLATION LOGIC -------------------------- */
            var inputString = Encoding.UTF8.GetString(*message, bc);
            var matchedCommand = "";
            var matchedChannelType = "";
            // DEBUG MESSAGE: (remove when not debugging)
            _logger.LogTrace($"Detouring Message: {inputString}", LoggerType.ChatDetours);

            // if the message is a command, see if the command is a channel command. If so, still use for translation, otherwise, return original.
            // TODO: Implement configured allowed channels for this later.
            if (inputString.StartsWith("/"))
            {
                // Match Command if Command being used is in our list of allowed Channels to translate in.
                var allowedChannels = _config.Current.ChannelsGagSpeak.GetChatChannelsListAliases();
                matchedCommand = allowedChannels.FirstOrDefault(prefix => inputString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                // If MatchedCommand is empty, it means it is not a channel command, or that it wasn't a channel we allowed, so send original untranslated text.
                if (matchedCommand.IsNullOrEmpty())
                {
                    // DEBUG MESSAGE: (remove when not debugging)
                    _logger.LogTrace("Ignoring Message as it is a command", LoggerType.ChatDetours);
                    return ProcessChatInputHook.Original(uiModule, message, a3);
                }

                // Set the matched command to the matched channel type. 
                matchedChannelType = matchedCommand;

                _logger.LogTrace($"Matched Command [{matchedCommand}] for matchedChannelType: [{matchedChannelType}]", LoggerType.ChatDetours);
            }

            // If current channel message is being sent to is in list of enabled channels, translate it.
            if (_config.Current.ChannelsGagSpeak.Contains(ChatChannel.GetChatChannel()) || _config.Current.ChannelsGagSpeak.IsAliasForAnyActiveChannel(matchedChannelType.Trim()))
            {
                // DEBUG MESSAGE: (Remove when not debugging)
                _logger.LogTrace($"MatchedCommand ->{matchedCommand} || Input ->{inputString}", LoggerType.ChatDetours);

                try
                {
                    // create the output string that will be processed.
                    var stringToProcess = inputString.Substring(matchedCommand.Length);

                    // see if this is an outgoing tell, if it is, we must make sure it isn't garbled for encoded messages
                    if (ChatChannel.GetChatChannel() == ChatChannel.Channels.Tell || matchedChannelType.Contains("/t") || matchedChannelType.Contains("/tell"))
                    {
                        // it is a tell, we need to make sure it is not garbled if it is an encoded message
                        _logger.LogTrace($"Message is a tell message, skipping garbling", LoggerType.ChatDetours);
                    }

                    // Translate the original message into Garbled Speech.
                    var output = _gagManager.ProcessMessage(stringToProcess);
                    
                    // Append the matched command to the front if one was included. If none was, it will be simply the message.
                    output = matchedCommand + output;

                    // DEBUG MESSAGE: (Remove when not debugging)
                    _logger.LogTrace(output);

                    // If Garbled Variant of string if less than 500 bytes in size, we can process it for sending.
                    if (output.Length <= 500)
                    {
                        // DEBUG MESSAGE: (Remove when not debugging)
                        _logger.LogTrace($"New Packet Message: {output}", LoggerType.ChatDetours);
                        // encode the new string
                        var bytes = Encoding.UTF8.GetBytes(output);
                        // allocate the memory
                        var mem1 = Marshal.AllocHGlobal(400);
                        var mem2 = Marshal.AllocHGlobal(bytes.Length + 30);
                        // copy and write the new memory into the allocated memory
                        Marshal.Copy(bytes, 0, mem2, bytes.Length);
                        Marshal.WriteByte(mem2 + bytes.Length, 0);
                        Marshal.WriteInt64(mem1, mem2.ToInt64());
                        Marshal.WriteInt64(mem1 + 8, 64);
                        Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
                        Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);
                        // properly send off the new message by setting it to r at the right pointer
                        var r = ProcessChatInputHook.Original(uiModule, (byte**)mem1.ToPointer(), a3);
                        // free up the memory we used for assigning
                        Marshal.FreeHGlobal(mem1);
                        Marshal.FreeHGlobal(mem2);
                        // return the result of the alias
                        return r;
                    }
                    // if we reached this point, it means our message was longer than 500 character, inform the user!
                    _logger.LogError("Chat Garbler Variant of Message was longer than max message length!");
                    return 0; // fucking ABORT!
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

