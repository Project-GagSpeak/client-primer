using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UI.MainWindow;
using OtterGui.Classes;

namespace GagSpeak.Services;

/// <summary> Handles all of the commands that are used in the plugin. </summary>
public sealed class CommandManagerService : IDisposable
{
    private const string MainCommand = "/gagspeak";
    private const string SafewordCommand = "/safeword";
    private const string SafewordHardcoreCommand = "/safewordhardcore";
    private readonly GagspeakMediator _mediator;
    private readonly GagspeakConfigService _mainConfig;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly IChatGui _chat;
    private readonly ICommandManager _commands;

    public CommandManagerService(GagspeakMediator mediator,
        GagspeakConfigService mainConfig, ServerConfigurationManager serverConfigs,
        IChatGui chat, ICommandManager commandManager)
    {
        _mediator = mediator;
        _mainConfig = mainConfig;
        _serverConfigs = serverConfigs;
        _chat = chat;
        _commands = commandManager;

        // Add handlers to the main commands
        _commands.AddHandler(MainCommand, new CommandInfo(OnGagSpeak)
        {
            HelpMessage = "Toggles main UI when used without arguements. Use with 'help' or '?' to view sub-commands.",
            ShowInHelp = true
        });
        _commands.AddHandler(SafewordCommand, new CommandInfo(OnSafeword)
        {
            HelpMessage = "revert all settings to false and disable any active components. For emergency uses.",
            ShowInHelp = true
        });
        _commands.AddHandler(SafewordHardcoreCommand, new CommandInfo(OnSafewordHardcore)
        {
            HelpMessage = "revert all settings to false and disable any active components. For emergency uses.",
            ShowInHelp = true
        });
    }

    public void Dispose()
    {
        // Remove the handlers from the main commands
        _commands.RemoveHandler(MainCommand);
        _commands.RemoveHandler(SafewordCommand);
    }

    private void OnGagSpeak(string command, string args)
    {
        var splitArgs = args.ToLowerInvariant().Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);
        // if no arguements.
        if (splitArgs.Length == 0)
        {
            // Interpret this as toggling the UI
            if (_mainConfig.Current.HasValidSetup())
                _mediator.Publish(new UiToggleMessage(typeof(MainWindowUI)));
            else
                _mediator.Publish(new UiToggleMessage(typeof(IntroUi)));
            return;
        }

        else if (string.Equals(splitArgs[0], "settings", StringComparison.OrdinalIgnoreCase))
        {
            _mediator.Publish(new UiToggleMessage(typeof(SettingsUi)));
        }

        // if its help or ?, print help
        else if (string.Equals(splitArgs[0], "help", StringComparison.OrdinalIgnoreCase) || string.Equals(splitArgs[0], "?", StringComparison.OrdinalIgnoreCase))
        {
            PrintHelpToChat();
        }
    }

    private void OnSafeword(string command, string argument)
    {
        // if the safeword was not provided, ask them to provide it.
        if (string.IsNullOrWhiteSpace(argument))
        { // If no safeword is provided
            _chat.Print("Please provide a safeword. Usage: /gagspeak safeword [your_safeword]");
            return;
        }

        // If safeword matches, invoke the safeword mediator
        if (string.Equals(_mainConfig.Current.Safeword, argument, StringComparison.OrdinalIgnoreCase))
        {
            _mediator.Publish(new SafewordUsedMessage());
        }
    }

    private void OnSafewordHardcore(string command, string argument)
    {
        // fire the event to invoke reverting hardcore actions.
        if (_mainConfig.Current.Safeword == argument)
        {
            _mediator.Publish(new SafewordHardcoreUsedMessage());
        }
    }

    private void PrintHelpToChat()
    {
        _chat.Print(new SeStringBuilder().AddYellow(" -- Gagspeak Commands --").BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("/gagspeak", "Toggles the primary UI").BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("/gagspeak settings", "Toggles the settings UI window.").BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("/safeword", "Cries out your safeword, disabling any active restrictions.").BuiltString);
    }
}

