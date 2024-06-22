using FFStreamViewer.WebAPI.PlayerData.Handlers;
using Gagspeak.API.Data.CharacterData;
using FFStreamViewer.WebAPI.PlayerData.Data;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Types;

namespace FFStreamViewer.WebAPI.SignalR.Utils;

public static class GenericUtils
{
    public static void CancelDispose(this CancellationTokenSource? cts)
    {
        try
        {
            cts?.Cancel();
            cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // swallow it
        }
    }

    public static CancellationTokenSource CancelRecreate(this CancellationTokenSource? cts)
    {
        cts?.CancelDispose();
        return new CancellationTokenSource();
    }

    /// <summary>
    /// One big nasty function for checking for updated data. (obviously i shorted it a lot lol)
    /// </summary>
    /// <returns></returns>
    public static HashSet<PlayerChanges> CheckUpdatedData(this CharacterData newData, Guid applicationBase,
            CharacterData? oldData, ILogger logger, PairHandler cachedPlayer)
    {
        oldData ??= new();
        var charaDataToUpdate = new HashSet<PlayerChanges>();

        bool moodlesDataDifferent = !string.Equals(oldData.MoodlesData, newData.MoodlesData, StringComparison.Ordinal);
        if (moodlesDataDifferent)
        {
            logger.LogDebug("[BASE-{appBase}] Updating {object} (Diff moodles data) => {change}", applicationBase, cachedPlayer, PlayerChanges.Moodles);
            charaDataToUpdate.Add(PlayerChanges.Moodles);
        }

        return charaDataToUpdate;
    }

    public static T DeepClone<T>(this T obj)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj))!;
    }

    public static unsafe int? ObjectTableIndex(this GameObject? gameObject)
    {
        if (gameObject == null || gameObject.Address == IntPtr.Zero)
        {
            return null;
        }

        return ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address)->ObjectIndex;
    }
}
