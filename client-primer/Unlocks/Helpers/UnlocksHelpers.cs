using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Lumina.Excel.GeneratedSheets;
using System.Windows.Forms;

namespace GagSpeak.Achievements;

public static class UnlocksHelpers
{
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
}
