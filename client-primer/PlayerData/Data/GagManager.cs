using GagSpeak.MufflerCore.Handler;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.PlayerData.Data;

public class GagManager : DisposableMediatorSubscriberBase
{
    private readonly PlayerCharacterManager _characterManager;
    private readonly GagDataHandler _gagDataHandler;
    private readonly Ipa_EN_FR_JP_SP_Handler _IPAParser;
    public List<GagData> _activeGags;

    public GagManager(ILogger<GagManager> logger, GagspeakMediator mediator,
        PlayerCharacterManager characterManager, GagDataHandler gagDataHandler,
        Ipa_EN_FR_JP_SP_Handler IPAParser) : base(logger, mediator)
    {
        _characterManager = characterManager;
        _gagDataHandler = gagDataHandler;
        _IPAParser = IPAParser;
        // Call initial update for our gags.

        // Subscribe to the GagTypeChanged event through the mediator
        Mediator.Subscribe<GagTypeChanged>(this, (msg) => OnGagTypeChanged(msg));
    }

    /// <summary> Updates the list of active gag objects.
    /// <summary>
    /// Updates the list of active gags based on the character's appearance data.
    /// </summary>
    private void UpdateActiveGags()
    {
        _activeGags = new List<GagData>
        {
            _gagDataHandler.GetGagByName(_characterManager.AppearanceData.SlotOneGagType),
            _gagDataHandler.GetGagByName(_characterManager.AppearanceData.SlotTwoGagType),
            _gagDataHandler.GetGagByName(_characterManager.AppearanceData.SlotThreeGagType)
        }.Where(gag => gag != null).ToList();
    }

    /// <summary>
    /// Handles the GagTypeChanged event, updating the active gags list accordingly.
    /// </summary>
    private void OnGagTypeChanged(GagTypeChanged message)
    {
        Logger.LogTrace("GagTypeChanged event received.");
        // Update the corresponding slot in CharacterAppearanceData based on the GagLayer
        switch (message.Layer)
        {
            case GagLayer.UnderLayer:
                _characterManager.AppearanceData.SlotOneGagType = message.NewGagType.GetGagAlias();
                break;
            case GagLayer.MiddleLayer:
                _characterManager.AppearanceData.SlotTwoGagType = message.NewGagType.GetGagAlias();
                break;
            case GagLayer.TopLayer:
                _characterManager.AppearanceData.SlotThreeGagType = message.NewGagType.GetGagAlias();
                break;
        }

        // Update the list of active gags
        UpdateActiveGags();

        // Optionally, publish a message to notify other parts of the system about the update
        Mediator.Publish(new ActiveGagTypesUpdated());
    }


    /// <summary>
    /// Processes the input message by converting it to GagSpeak format
    /// </summary> 
    public string ProcessMessage(string inputMessage)
    {
        string outputStr = "";
        try
        {
            outputStr = ConvertToGagSpeak(inputMessage);
            Logger.LogError($"Converted message to GagSpeak: {outputStr}");
        }
        catch (Exception e)
        {
            Logger.LogError($"Error processing message: {e.Message}");
        }
        return outputStr;
    }

    /// <summary>
    /// Internal convert for gagspeak
    public string ConvertToGagSpeak(string inputMessage)
    {
        // If all gags are None, return the input message as is
        if (_activeGags.All(gag => gag.Name == "None")) return inputMessage;

        // Initialize the algorithm scoped variables 
        Logger.LogDebug($"Converting message to GagSpeak, at least one gag is not None.");
        StringBuilder finalMessage = new StringBuilder(); // initialize a stringbuilder object so we dont need to make a new string each time
        bool skipTranslation = false;
        try
        {
            // Convert the message to a list of phonetics for each word
            List<Tuple<string, List<string>>> wordsAndPhonetics = _IPAParser.ToIPAList(inputMessage);
            // Iterate over each word and its phonetics
            foreach (Tuple<string, List<string>> entry in wordsAndPhonetics)
            {
                string word = entry.Item1; // create a variable to store the word (which includes its puncuation)
                // If the word is "*", then toggle skip translations
                if (word == "*")
                {
                    skipTranslation = !skipTranslation;
                    finalMessage.Append(word + " "); // append the word to the string
                    continue; // Skip the rest of the loop for this word
                }
                // If the word starts with "*", toggle skip translations and remove the "*"
                if (word.StartsWith("*"))
                {
                    skipTranslation = !skipTranslation;
                }
                // If the word ends with "*", remove the "*" and set a flag to toggle skip translations after processing the word
                bool toggleAfter = false;
                if (word.EndsWith("*"))
                {
                    toggleAfter = true;
                }
                // If the word is not to be translated, just add the word to the final message and continue
                if (!skipTranslation && word.Any(char.IsLetter))
                {
                    // do checks for punctuation stuff
                    bool isAllCaps = word.All(c => !char.IsLetter(c) || char.IsUpper(c));       // Set to true if the full letter is in caps
                    bool isFirstLetterCaps = char.IsUpper(word[0]);
                    // Extract all leading and trailing punctuation
                    string leadingPunctuation = new string(word.TakeWhile(char.IsPunctuation).ToArray());
                    string trailingPunctuation = new string(word.Reverse().TakeWhile(char.IsPunctuation).Reverse().ToArray());
                    // Remove leading and trailing punctuation from the word
                    string wordWithoutPunctuation = word.Substring(leadingPunctuation.Length, word.Length - leadingPunctuation.Length - trailingPunctuation.Length);
                    // Convert the phonetics to GagSpeak if the list is not empty, otherwise use the original word
                    string gaggedSpeak = entry.Item2.Any() ? ConvertPhoneticsToGagSpeak(entry.Item2, isAllCaps, isFirstLetterCaps) : wordWithoutPunctuation;
                    // Add the GagSpeak to the final message

                    /* ---- THE BELOW LINE WILL CAUSE LOTS OF SPAM, ONLY FOR USE WHEN DEVELOPER DEBUGGING ---- */
                    //Logger.LogTrace($"[GagGarbleManager] Converted [{leadingPunctuation}] + [{word}] + [{trailingPunctuation}]");
                    finalMessage.Append(leadingPunctuation + gaggedSpeak + trailingPunctuation + " ");
                }
                else
                {
                    finalMessage.Append(word + " "); // append the word to the string
                }
                // If the word ended with "*", toggle skip translations now
                if (toggleAfter)
                {
                    skipTranslation = !skipTranslation;
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError($"[GagGarbleManager] Error converting from IPA Spaced to final output. Puncutation error or other type possible : {e.Message}");
        }
        return finalMessage.ToString().Trim();
    }

    /// <summary>
    /// Phonetic IPA -> Garbled sound equivalent in selected language
    /// </summary>
    public string ConvertPhoneticsToGagSpeak(List<string> phonetics, bool isAllCaps, bool isFirstLetterCapitalized)
    {
        StringBuilder outputString = new StringBuilder();
        foreach (string phonetic in phonetics)
        {
            try
            {
                var gagWithMaxMuffle = _activeGags
                    .Where(gag => gag.Phonemes.ContainsKey(phonetic) && !string.IsNullOrEmpty(gag.Phonemes[phonetic].Sound))
                    .OrderByDescending(gag => gag.Phonemes[phonetic].Muffle)
                    .FirstOrDefault();
                if (gagWithMaxMuffle != null)
                {
                    string translationSound = gagWithMaxMuffle.Phonemes[phonetic].Sound;
                    outputString.Append(translationSound);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error converting phonetic {phonetic} to GagSpeak: {e.Message}");
            }
        }
        string result = outputString.ToString();
        if (isAllCaps) result = result.ToUpper();
        if (isFirstLetterCapitalized && result.Length > 0)
        {
            result = char.ToUpper(result[0]) + result.Substring(1);
        }
        return result;
    }
}
