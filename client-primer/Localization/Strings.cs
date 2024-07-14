using CheapLoc;

namespace GagSpeak.Localization;

/// <summary> Used to localize the strings to the respective languages for viewing the into and account creation. </summary>
public static class Strings
{
    public static ToSStrings ToS { get; set; } = new();

    public class ToSStrings
    {
        /*  Introduction */
        public readonly string CautionaryWarningPage1 = Loc.Localize("CautionaryWarningPage1",
            "You will only be able to read the following once, you have been warned.");

        public readonly string GagSpeakIntroduction = Loc.Localize("GagSpeakIntroduction",
            "GagSpeak is now a server-side application. This means past desync issues, slow message delay times, " +
            "and other pairing issues are now a thing of the past. Enjoy being able to interact with your partner " +
            "anywhere in the game, including duties, cutscenes, gpose, across datacenters and more!");

        public readonly string GagSpeakIntroduction2 = Loc.Localize("GagSpeakIntroduction2",
            "You can thank me in advance, but I took the extra time to formulate an account creation process for " +
            "GagSpeak servers that will not require you to modify your lodestone page or join discord and register "+
            "the lodestone information with a bot.");

        public readonly string GagSpeakIntroduction3 = Loc.Localize("GagSpeakIntroduction3",
            "It doesn't take much to realize that can be a mood killer for anyone wanting to use the plugin with one " +
            "another for a one night stand, or for someone who just wants to use the plugin for a short period of time. " +
            "To view the process, Click the account setup button below.");

        public readonly string PluginIpcNotice = Loc.Localize("PluginIpcNotice",
            "Note: Some components in GagSpeak make use of additional plugins to further enhance immersion, such " +
            "as Penumbra, Glamourer, Moodles, or Customize+ actions. If you wish to use features requiring them, " +
            "will need to have those plugins installed.");

        /* Account Creation Acknowledgements*/
        public readonly string LanguageLabel = Loc.Localize("LanguageLabel", "Lang:");
        public readonly string AgreementLabel = Loc.Localize("AgreementLabel", "Account & Server Acknowledgement");

        public readonly string ReadLabel = Loc.Localize("ReadLabel", "READ THIS CAREFULLY");

        public readonly string AccountCreationIntro = Loc.Localize("AccountCreationIntro",
            "To create a new account for GagSpeak server, be sure you are logged into the character to have as the primary profile for the " +
            "GagSpeak account. I say this because THE GENERATE PRIMARY KEY BUTTON BELOW CAN ONLY BE PRESSED ONCE. After that, if you want to use " +
            "GagSpeak on any other alt characters, you will will need to claim your Account through the GagSpeak discord bot.");

        public readonly string AccountCreationDetails = Loc.Localize("AccountCreationDetails",
            "Once your account is created, the account's secret key and user ID will be tied to the currently logged-in player but will initially " +
            "remain unclaimed. An unclaimed account poses a potential security risk, as anyone could input the key in their own 'Place Account Key here'" +
            "field during the plugin introduction to access or claim the account (However the chance of this happening is incredibly small, but still possible).");

        public readonly string PostAccountClaimInfo = Loc.Localize("PostAccountClaimInfo",
            "After claiming your account, you will be able to not only create additional profiles that can be linked to your alt characters, but you will also be able" +
            "to set nicknames for others, and overall provide yourself a more personalized user experience");

        public readonly string ServerUsageTransparency = Loc.Localize("ServerUsageTransparency",
            "Once you are connected to the GagSpeak Server, the only data transmitted across the servers and stored on the database will be new settings information, "+
            "and details about whitelisted user's components. Basically nothing sensitive is sent and you will be just fine.");

        public readonly string ButtonWillBeAvailableIn = Loc.Localize("ButtonWillBeAvailableIn", "'I agree' button will be available in");
        public readonly string AcknowledgeButton = Loc.Localize("AcknowledgeButton", "I Understand This Information");
    }
}
