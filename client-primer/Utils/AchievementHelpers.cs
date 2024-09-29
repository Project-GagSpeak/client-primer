using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.GoldSaucer;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Lumina.Excel.GeneratedSheets;
using System.Windows.Forms;

namespace GagSpeak.Utils;

public static class AchievementHelpers
{

    #region Deep Dungeon Helpers
    public static unsafe InstanceContentDeepDungeon* GetDirector()
    {
        var eventFramework = EventFramework.Instance();
        return eventFramework == null ? null : eventFramework->GetInstanceContentDeepDungeon();
    }

    public static unsafe bool InDeepDungeon() => GetDirector() != null;

    public static unsafe byte? GetFloor()
    {
        var director = GetDirector();
        if (director is null) return null;
        return director->Floor;
    }

    public static uint? GetFloorSetId()
    {
        if (GetFloor() is { } floor)
        {
            return (uint)((floor - 1) / 10 * 10 + 1);
        }

        return null;
    }

    public static int GetFloorSetId(int floor)
    {
        return (floor - 1) / 10 * 10 + 1;
    }
    #endregion Deep Dungeon Helpers

    // The Gold Saucer Manager for the GATE Guy that spawns whenever a GATE is Active.
    public static unsafe GoldSaucerManager* GSManager = GoldSaucerManager.Instance();

    /// <summary> Gets the gude that only exists while a GATE is active. </summary>
    public static unsafe GFateDirector* GetFateDirector() => GSManager->CurrentGFateDirector;

    public static unsafe bool GateDirectorIsValid => GetFateDirector() is not null;

    public enum GateType : byte
    {
        CliffHanger = 1, // the one that spawns bombs fucking everywhere and knocks you off the tower
        AnyWayTheWindBlows = 5, // Fungai event with the giant wind mob that blows you off the platform.
        LeapOfFaith = 6, // That boring one that spawns all the time compared to everything else
        AirForceOne = 7, // The one where you go nyoom in a airplane around the saucer and stuff.
    }

    public static unsafe GateType GetActiveGate()
    {
        var type = GetFateDirector()->GateType;
        return (GateType)type;
    }

    private static bool HasJoinedGate(GFateDirectorFlag currentFlags) => (currentFlags & GFateDirectorFlag.IsJoined) != 0;
    private static bool HasFinishedGate(GFateDirectorFlag currentFlags) => (currentFlags & GFateDirectorFlag.IsFinished) != 0;

    public static unsafe bool IsInGateWithKnockback()
    {
        var director = GetFateDirector();
        if (director == null) return false;

        // check if the gate type is one with KB
        byte gate = director->GateType;
        // ensure we are in the right gate
        if ((GateType)gate is GateType.CliffHanger || (GateType)gate is GateType.AnyWayTheWindBlows)
        {
            // ensure we have joined, but not yet completed.
            if(HasJoinedGate(director->Flags) && !HasFinishedGate(director->Flags))
            {
                return true;
            }
        }
        return false;
    }
}