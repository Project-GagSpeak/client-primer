using GagSpeak.MufflerCore.Handler;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.PlayerData.Data;

public class GagManager : DisposableMediatorSubscriberBase
{
    private readonly PlayerCharacterManager _characterManager;
    private readonly GagDataHandler _gagDataHandler;
    private readonly PadlockHandler _padlockHandler;
    private readonly Ipa_EN_FR_JP_SP_Handler _IPAParser;
    public List<GagData> _activeGags;

    public GagManager(ILogger<GagManager> logger, GagspeakMediator mediator,
        PlayerCharacterManager characterManager, GagDataHandler gagDataHandler,
        PadlockHandler padlockHandler, Ipa_EN_FR_JP_SP_Handler IPAParser)
        : base(logger, mediator)
    {
        _characterManager = characterManager;
        _gagDataHandler = gagDataHandler;
        _padlockHandler = padlockHandler;
        _IPAParser = IPAParser;

        // Called by callback forced update, meant to not trigger endless feedback loop.
        Mediator.Subscribe<UpdateActiveGags>(this, async (msg) =>
        {
            await UpdateActiveGags();
            if (msg.CompletionTaskSource != null)
            {
                msg.CompletionTaskSource.SetResult(true);
            }
        });

        // Triggered whenever the client updated the gagType from the dropdown menus in the UI
        Mediator.Subscribe<GagTypeChanged>(this, (msg) => OnGagTypeChanged(msg));

        // Triggered whenever the client updated the padlockType from the dropdown menus in the UI
        Mediator.Subscribe<GagLockToggle>(this, (msg) => OnGagLockChanged(msg));

        // check for any locked gags on delayed framework to see if their timers expired.
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => CheckForExpiredTimers());
    }

    public bool AnyGagActive => _activeGags.Any(gag => gag.Name != "None");
    public bool AnyGagLocked => _characterManager.AppearanceData?.GagSlots.Any(x => x.Padlock != "None") ?? false; 
    public List<Padlocks> PadlockPrevs => _padlockHandler.PadlockPrevs;
    public string[] Passwords => _padlockHandler.Passwords;
    public string[] Timers => _padlockHandler.Timers;

    public bool ValidatePassword(int gagLayer, bool currentlyLocked) => _padlockHandler.PasswordValidated(gagLayer, currentlyLocked);

    public bool DisplayPasswordField(int slot, bool currentlyLocked) => _padlockHandler.DisplayPasswordField(slot, currentlyLocked);

    /// <summary>
    /// Updates the list of active gags based on the character's appearance data.
    /// </summary>
    public Task UpdateActiveGags()
    {
        Logger.LogTrace("GagTypeOne: {0}, GagTypeTwo: {1}, GagTypeThree: {2}",
            _characterManager.AppearanceData.GagSlots[0].GagType,
            _characterManager.AppearanceData.GagSlots[1].GagType,
            _characterManager.AppearanceData.GagSlots[2].GagType);

        // compile the strings into a list of strings, then locate the names in the handler storage that match it.
        _activeGags = new List<string>
        {
            _characterManager.AppearanceData.GagSlots[0].GagType,
            _characterManager.AppearanceData.GagSlots[1].GagType,
            _characterManager.AppearanceData.GagSlots[2].GagType,
        }
        .Where(gagType => _gagDataHandler._gagTypes.Any(gag => gag.Name == gagType))
        .Select(gagType => _gagDataHandler._gagTypes.First(gag => gag.Name == gagType))
        .ToList();

        Mediator.Publish(new ActiveGagsUpdated());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the GagTypeChanged event, updating the active gags list accordingly.
    /// </summary>
    private void OnGagTypeChanged(GagTypeChanged message)
    {
        Logger.LogTrace("GagTypeChanged event received.");
        bool IsApplying = (message.NewGagType != GagList.GagType.None);

        // Update the corresponding slot in CharacterAppearanceData based on the GagLayer
        if (message.Layer == GagLayer.UnderLayer)
        {
            // Set the new Gag Type
            _characterManager.AppearanceData.GagSlots[0].GagType = message.NewGagType.GetGagAlias();
            // Publish our change to the mediator.
            Mediator.Publish(new PlayerCharAppearanceChanged(IsApplying ? DataUpdateKind.AppearanceGagAppliedLayerOne : DataUpdateKind.AppearanceGagRemovedLayerOne));
        }
        if (message.Layer == GagLayer.MiddleLayer)
        {
            _characterManager.AppearanceData.GagSlots[1].GagType = message.NewGagType.GetGagAlias();
            Mediator.Publish(new PlayerCharAppearanceChanged(IsApplying ? DataUpdateKind.AppearanceGagAppliedLayerTwo : DataUpdateKind.AppearanceGagRemovedLayerTwo));
        }
        if (message.Layer == GagLayer.TopLayer)
        {
            _characterManager.AppearanceData.GagSlots[2].GagType = message.NewGagType.GetGagAlias();
            Mediator.Publish(new PlayerCharAppearanceChanged(IsApplying ? DataUpdateKind.AppearanceGagAppliedLayerThree : DataUpdateKind.AppearanceGagRemovedLayerThree));
        }

        // Update the list of active gags
        UpdateActiveGags();
    }

    /// <summary>
    /// Handles Mediator call for a lock changed event.
    /// </summary>
    private void OnGagLockChanged(GagLockToggle msg)
    {
        // If Unlocking, clear the things.
        if (msg.Unlocking == true)
        {
            if (msg.PadlockInfo.Layer == GagLayer.UnderLayer)
            {
                DisableLock(0);
                if (msg.pushChanges) Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.AppearanceGagUnlockedLayerOne));
            }
            else if (msg.PadlockInfo.Layer == GagLayer.MiddleLayer)
            {
                DisableLock(1);
                if (msg.pushChanges) Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.AppearanceGagUnlockedLayerTwo));
            }
            else if (msg.PadlockInfo.Layer == GagLayer.TopLayer)
            {
                DisableLock(2);
                if (msg.pushChanges) Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.AppearanceGagUnlockedLayerThree));
            }
        }
        else
        {
            if(_characterManager.AppearanceData == null)
            {
                Logger.LogWarning("AppearanceData is null, cannot apply padlock.");
                return;
            }

            int layer = (int)msg.PadlockInfo.Layer;
            _characterManager.AppearanceData.GagSlots[layer].Padlock = msg.PadlockInfo.PadlockType.ToString();
            _characterManager.AppearanceData.GagSlots[layer].Password = msg.PadlockInfo.Password;
            _characterManager.AppearanceData.GagSlots[layer].Timer = msg.PadlockInfo.Timer;
            _characterManager.AppearanceData.GagSlots[layer].Assigner = msg.PadlockInfo.Assigner;
            Mediator.Publish(new ActiveLocksUpdated());
            // publish the mediator type based on the padlockinfo layer value.
            if (msg.PadlockInfo.Layer == GagLayer.UnderLayer)
            {
                Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.AppearanceGagLockedLayerOne));
            }
            else if (msg.PadlockInfo.Layer == GagLayer.MiddleLayer)
            {
                Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.AppearanceGagLockedLayerTwo));
            }
            else if (msg.PadlockInfo.Layer == GagLayer.TopLayer)
            {
                Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.AppearanceGagLockedLayerThree));
            }
        }
    }

    public void SafewordWasUsed()
    {
        if(_characterManager.AppearanceData == null)
        {
            Logger.LogWarning("AppearanceData is null, cannot apply safeword.");
            return;
        }

        DisableLock(0);
        DisableLock(1);
        DisableLock(2);
        // clear the gags
        _characterManager.AppearanceData.GagSlots[0].GagType = GagList.GagType.None.GetGagAlias();
        _characterManager.AppearanceData.GagSlots[1].GagType= GagList.GagType.None.GetGagAlias();
        _characterManager.AppearanceData.GagSlots[2].GagType = GagList.GagType.None.GetGagAlias();
        // Update the list of active gags
        UpdateActiveGags();
        // push this change to the mediator.
        Mediator.Publish(new PlayerCharAppearanceChanged(DataUpdateKind.Safeword));
    }


    private void DisableLock(int layerIdx)
    {
        _characterManager.AppearanceData.GagSlots[layerIdx].Padlock = "None";
        _characterManager.AppearanceData.GagSlots[layerIdx].Password = string.Empty;
        _characterManager.AppearanceData.GagSlots[layerIdx].Timer = DateTimeOffset.MinValue;
        _characterManager.AppearanceData.GagSlots[layerIdx].Assigner = string.Empty;
        PadlockPrevs[layerIdx] = Padlocks.None;
        Mediator.Publish(new ActiveLocksUpdated());
    }

    /// <summary>
    /// Processes the input message by converting it to GagSpeak format
    /// </summary> 
    public string ProcessMessage(string inputMessage)
    {
        if (_activeGags == null || _activeGags.All(gag => gag.Name == "None")) return inputMessage;
        string outputStr = "";
        try
        {
            outputStr = ConvertToGagSpeak(inputMessage);
            Logger.LogTrace($"Converted message to GagSpeak: {outputStr}");
        }
        catch (Exception e)
        {
            Logger.LogError($"Error processing message: {e}");
        }
        return outputStr;
    }

    /// <summary>
    /// Internal convert for gagspeak
    public string ConvertToGagSpeak(string inputMessage)
    {


        // If all gags are None, return the input message as is
        if (_activeGags.All(gag => gag.Name == "None"))
        {
            return inputMessage;
        }

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

    private void UnlockGagSlot(int slotIndex)
    {
        var gagSlot = _characterManager.AppearanceData!.GagSlots[slotIndex];
        gagSlot.Padlock = "None";
        gagSlot.Password = string.Empty;
        gagSlot.Timer = DateTimeOffset.MinValue;
        gagSlot.Assigner = string.Empty;
        PadlockPrevs[slotIndex] = Padlocks.None;
        Mediator.Publish(new ActiveLocksUpdated());

        var dataUpdateKind = slotIndex switch
        {
            0 => DataUpdateKind.AppearanceGagUnlockedLayerOne,
            1 => DataUpdateKind.AppearanceGagUnlockedLayerTwo,
            2 => DataUpdateKind.AppearanceGagUnlockedLayerThree,
            _ => throw new ArgumentOutOfRangeException()
        };
        Mediator.Publish(new PlayerCharAppearanceChanged(dataUpdateKind));
    }

    private void CheckForExpiredTimers()
    {
        // return if characterManager not valid
        if (_characterManager == null) return;

        // return if appearance data not present.
        if (_characterManager.AppearanceData == null) return;

        // return if none of our gags have padlocks.
        if (!AnyGagLocked) return;

        // If a gag does have a padlock, ensure it is a timer padlock
        for (int i = 0; i < _characterManager.AppearanceData.GagSlots.Length; i++)
        {
            var gagSlot = _characterManager.AppearanceData.GagSlots[i];
            if (GenericHelpers.TimerPadlocks.Contains(gagSlot.Padlock) && gagSlot.Timer - DateTimeOffset.UtcNow <= TimeSpan.Zero)
            {
                UnlockGagSlot(i);
            }
        }
    }
}
