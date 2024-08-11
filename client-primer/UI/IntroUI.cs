using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using GagSpeak.Localization;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI;

/// <summary> The introduction UI that will be shown the first time that the user starts the plugin. </summary>
public class IntroUi : WindowMediatorSubscriberBase
{
    private readonly GagspeakConfigService _configService;                  // the configuration service for the gagspeak plugin
    private readonly Dictionary<string, string> _languages;                 // The various languages
    private readonly ServerConfigurationManager _serverConfigurationManager;// the configuration manager for the server
    private readonly OnFrameworkService _frameworkUtils;                    // for functions running on the games framework thread
    private readonly UiSharedService _uiShared;                             // the shared UI service.
    private int _currentLanguage;                                           // the current lgnauge of the client.
    private bool _readFirstPage;                                            // if they have read the validation string
    private string _secretKey = string.Empty;                               // the secret key to register with
    private string _aquiredUID = string.Empty;                              // the UID that was aquired
    private string _timeoutLabel = string.Empty;                            // the timeout label for the page
    private Task? _timeoutTask;                                             // the timeout task
    private string[]? _tosParagraphs;                                       // the various paragraphs for the ToS

    public IntroUi(ILogger<IntroUi> logger, UiSharedService uiShared, GagspeakConfigService configService,
        ServerConfigurationManager serverConfigurationManager, GagspeakMediator gagspeakMediator,
        OnFrameworkService frameworkutils) : base(logger, gagspeakMediator, "Gagspeak Setup")
    {
        _languages = new(StringComparer.Ordinal) { { "English", "en" }, { "Deutsch", "de" }, { "Français", "fr" } };
        _uiShared = uiShared;
        _configService = configService;
        _serverConfigurationManager = serverConfigurationManager;
        _frameworkUtils = frameworkutils;
        
        IsOpen = false;                 // do not start with the window open
        
        ShowCloseButton = false;        // do not show the close button
        RespectCloseHotkey = false;     // do not respect the close hotkey (in otherwords, require the user to read this)
        AllowPinning = false;
        AllowClickthrough = false;

        SizeConstraints = new WindowSizeConstraints()           // set the size of the window
        {
            MinimumSize = new Vector2(600, 500),
            MaximumSize = new Vector2(600, 2000),
        };

        // get the localization for the ToS
        GetToSLocalization();

        // subscribe to event which requests to switch to the main UI, and set IsOpen to false when it does
        Mediator.Subscribe<SwitchToMainUiMessage>(this, (_) => IsOpen = false);

        // subscribe to event which requests to switch to the intro UI, and set IsOpen to true when it does
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = true);
    }

    protected override void PreDrawInternal() { }
    protected override void PostDrawInternal() { }

    /// <summary> The interal draw method for the intro UI. </summary>
    protected override void DrawInternal()
    {
        // if the user has not accepted the agreement and they have not read the first page,
        // Then show the first page (everything in this if statement)
        if (!_configService.Current.AcknowledgementUnderstood && !_readFirstPage)
        {
            _uiShared.BigText("Welcome to CK's GagSpeak Plugin!");
            ImGui.Separator();
            _uiShared.BigText("THIS PLUGIN IS IN BETA.\nIF YOU PROCEED FROM HERE,\nINFORM CORDY YOU ARE JOINING BETA." +
                Environment.NewLine + "OTHERWISE, YOU WILL BE REMOVED\n(WITHOUT WARNING)");
            ImGui.Separator();
            UiSharedService.ColorTextWrapped(Strings.ToS.CautionaryWarningPage1, ImGuiColors.DalamudRed);
            ImGui.NewLine();
            UiSharedService.TextWrapped(Strings.ToS.GagSpeakIntroduction);
            ImGui.NewLine();
            UiSharedService.TextWrapped(Strings.ToS.GagSpeakIntroduction2);
            ImGui.NewLine();
            UiSharedService.TextWrapped(Strings.ToS.GagSpeakIntroduction3);
            ImGui.NewLine();
            UiSharedService.ColorTextWrapped(Strings.ToS.PluginIpcNotice, ImGuiColors.DalamudYellow);

            // seperator before the next button is available
            ImGui.Separator();
            // add a button to switch to the account setup page
            if (ImGui.Button("Setup Account##toAgreement"))
            {
                // mark that they read the first page
                _readFirstPage = true;
                // start a timer that runs for 30 seconds, then makes the account creation possible.
                _timeoutTask = Task.Run(async () =>
                {
                    // for 30 seconds, update the timeout label to show the time remaining
                    for (int i = 5; i > 0; i--)
                    {
                        _timeoutLabel = $"{Strings.ToS.ButtonWillBeAvailableIn} {i}s";
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    }
                });
            }
        }
        // if they have read the first page but not yet created an account, we will need to present the account setup page for them.
        else if (!_configService.Current.AcknowledgementUnderstood && _readFirstPage)
        {
            // fetch a text size var for the language label
            Vector2 textSize;
            // push the following text in the using statement to display as text with the size of the UidFont
            using (_uiShared.UidFont.Push())
            {
                // calc the text size of the language label
                textSize = ImGui.CalcTextSize(Strings.ToS.LanguageLabel);
                // display the "Acknowledgement of Account Creation & Server Usage"
                ImGui.TextUnformatted(Strings.ToS.AgreementLabel);
            }
            // on the same line, display the language label
            ImGui.SameLine();
            var languageSize = ImGui.CalcTextSize(Strings.ToS.LanguageLabel);
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - languageSize.X - 80);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textSize.Y / 2 - languageSize.Y / 2);
            ImGui.TextUnformatted(Strings.ToS.LanguageLabel);
            // and on the same line once again, display the language combo box
            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textSize.Y / 2 - (languageSize.Y + ImGui.GetStyle().FramePadding.Y) / 2);
            ImGui.SetNextItemWidth(80);
            if (ImGui.Combo("", ref _currentLanguage, _languages.Keys.ToArray(), _languages.Count))
            {
                GetToSLocalization(_currentLanguage);
            }

            // separator for the Acknowledgement Display
            ImGui.Separator();
            ImGui.SetWindowFontScale(1.5f);
            // display the "READ THIS" text
            string readThis = Strings.ToS.ReadLabel;
            textSize = ImGui.CalcTextSize(readThis);
            ImGui.SetCursorPosX(ImGui.GetWindowSize().X / 2 - textSize.X / 2);
            UiSharedService.ColorText(readThis, ImGuiColors.DalamudRed); // "READ THIS" text
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator(); // separator to list the acknowledgements
            ImGui.NewLine();
            UiSharedService.TextWrapped(Strings.ToS.AccountCreationIntro);
            ImGui.NewLine();
            UiSharedService.TextWrapped(Strings.ToS.AccountCreationDetails);
            ImGui.NewLine();
            UiSharedService.TextWrapped(Strings.ToS.PostAccountClaimInfo);
            ImGui.NewLine();
            UiSharedService.TextWrapped(Strings.ToS.ServerUsageTransparency);

            ImGui.Separator(); // separator for the buttons.

            // if the timeout task is completed, display the create account label
            if (_timeoutTask?.IsCompleted ?? true)
            {
                // display the button to agree to the acknowledgement.
                if (ImGui.Button(Strings.ToS.AcknowledgeButton + "##toSetup"))
                {
                    _configService.Current.AcknowledgementUnderstood = true;
                    _configService.Save();
                }
            }
            else
            {
                // otherwise, show the number of seconds left until it displays
                UiSharedService.TextWrapped(_timeoutLabel);
            }
        }
        // if the user has read the acknowledgements and the server is not alive, display the account creation window.
        else if (!_uiShared.ApiController.ServerAlive || !_configService.Current.AccountCreated)
        {
            // title for this page of the intro UI
            using (_uiShared.UidFont.Push()) { ImGui.TextUnformatted("Account Registration / Creation"); }

            ImGui.Separator();


            UiSharedService.TextWrapped("Because this is a fresh install, you may generate one primary account key below. Once the key " +
                "has been generated, you may hit 'Login' to log into your account.");

            UiSharedService.TextWrapped("If you already have an account and are on a new computer or had to reinstall for any reason, " +
                "paste the generated secret key for your account in the text field below and hit 'Login'.");

            ImGui.Separator();

            // display the fields for generation and creation
            var oneTimeKeyGenButtonText = "One-Time Primary Key Generator";
            if (ImGuiUtil.DrawDisabledButton(oneTimeKeyGenButtonText, Vector2.Zero, oneTimeKeyGenButtonText, _configService.Current.ButtonUsed))
            {
                // toggle the account created flag to true
                _configService.Current.ButtonUsed = true;
                _configService.Save();
                // generate a secret key for the user.
                _ = Task.Run(async () =>
                {
                    var accountDetails = await _uiShared.ApiController.FetchNewAccountDetailsAndDisconnect();
                    _aquiredUID = accountDetails.Item1;
                    _secretKey = accountDetails.Item2;
                });
            }

            // next place the text field for inserting the key, and then the button for creating the account.
            var text = "Account Secret Key: ";
            var buttonText = "Create / Sign into your Account with Secret Key";
            var buttonWidth = _secretKey.Length != 64 ? 0 : ImGuiHelpers.GetButtonSize(buttonText).X + ImGui.GetStyle().ItemSpacing.X;
            var textSize = ImGui.CalcTextSize(text);
            ImGui.AlignTextToFramePadding();
            // display the text field for the secret key
            ImGui.SetNextItemWidth(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetWindowContentRegionMin().X);
            ImGui.TextUnformatted(text);
            ImGui.SetNextItemWidth(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetWindowContentRegionMin().X - textSize.X);
            ImGui.InputTextWithHint("", text, ref _secretKey, 64);
            // if the key inserted is not 64 characters long, display a warning.
            if (_secretKey.Length > 0 && _secretKey.Length != 64)
            {
                UiSharedService.ColorTextWrapped("Your secret key must be exactly 64 characters long.", ImGuiColors.DalamudRed);
            }
            // if the key is 64 characters long, display the button to create the account.
            else if (_secretKey.Length == 64)
            {
                // display the create account button.
                if (ImGui.Button(buttonText))
                {
                    _serverConfigurationManager.GenerateAuthForCurrentCharacter(true);
                    // grab our local content id
                    var contentId = _frameworkUtils.GetPlayerLocalContentIdAsync().GetAwaiter().GetResult();

                    // set the key to that newly added authentication
                    SecretKey newKey = new()
                    {
                        Label = $"GagSpeak Main Account Secret Key - ({DateTime.Now:yyyy-MM-dd})",
                        Key = _secretKey,
                    };

                    // set the secret key for the character
                    _serverConfigurationManager.SetSecretKeyForCharacter(contentId, newKey);

                    // run the create connections and set our account created to true
                    _ = Task.Run(() => _uiShared.ApiController.CreateConnections());
                    _secretKey = string.Empty;
                    _configService.Current.AccountCreated = true; // set the account created flag to true
                    _configService.Save(); // save the configuration
                }
                UiSharedService.AttachToolTip("THIS WILL CREATE YOUR PRIMARY ACCOUNT. ENSURE YOUR KEY IS CORRECT.");
            }

            ImGui.Separator();
            // display the secret key and UID
            ImGui.InputText("Secret Key", ref _secretKey, 64, ImGuiInputTextFlags.ReadOnly);
            ImGui.InputText("UID", ref _aquiredUID, 64, ImGuiInputTextFlags.ReadOnly);
        }
        // otherwise, if the server is alive, meaning we are validated, then boot up the main UI.
        else
        {
            _logger.LogDebug("Switching to main UI");
            // call the main UI event via the mediator
            Mediator.Publish(new SwitchToMainUiMessage());
            // toggle this intro UI window off.
            IsOpen = false;
        }
    }

    /// <summary> Helper function to fetch all the ToS paragraphs and load the localization for the ToS. </summary>
    private void GetToSLocalization(int changeLanguageTo = -1)
    {
        if (changeLanguageTo != -1)
        {
            _uiShared.LoadLocalization(_languages.ElementAt(changeLanguageTo).Value);
        }
    }
}
