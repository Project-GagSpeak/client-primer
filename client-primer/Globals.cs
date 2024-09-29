

/// Global Usings
global using Newtonsoft.Json;
global using Newtonsoft.Json.Linq;
global using Microsoft.Extensions.Logging;
global using System.Collections.Concurrent;
global using System.Collections;
global using System.Diagnostics;
global using System.Text;
global using GagspeakAPI.Enums;
global using GagspeakAPI;

/// Global Tuples
global using MoodlesStatusInfo = (
    System.Guid GUID,
    int IconID,
    string Title,
    string Description,
    GagspeakAPI.Data.IPC.StatusType Type,
    string Applier,
    bool Dispelable,
    int Stacks,
    bool Persistent,
    int Days,
    int Hours,
    int Minutes,
    int Seconds,
    bool NoExpire,
    bool AsPermanent
    );

global using MoodlesGSpeakPairPerms = (
    bool AllowPositive,
    bool AllowNegative,
    bool AllowSpecial,
    bool AllowApplyingOwnMoodles,
    bool AllowApplyingPairsMoodles,
    System.TimeSpan MaxDuration,
    bool AllowPermanent,
    bool AllowRemoval
    );

global using IPCProfileDataTuple = (System.Guid UniqueId, string Name, string VirtualPath, string CharacterName, bool IsEnabled);

// See later https://github.com/xivdev/Penumbra/blob/5c5e45114f25f9429d8757b6edf852ecc37173c9/Penumbra/UI/LaunchButton.cs#L27
