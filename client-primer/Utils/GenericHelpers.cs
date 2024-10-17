using ImGuiNET;
using PInvoke;
using System.Windows.Forms;
using Lumina.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;
using GagspeakAPI.Enums;

namespace GagSpeak.Utils;

/// <summary> A class for all of the UI helpers, including basic functions for drawing repetative yet unique design elements </summary>
public static class GenericHelpers
{
    public static IEnumerable<Padlocks> NoOwnerPadlockList = Enum.GetValues<Padlocks>()
        .Cast<Padlocks>()
        .Where(p => p is not Padlocks.OwnerPadlock && p is not Padlocks.OwnerTimerPadlock 
            && p is not Padlocks.DevotionalPadlock && p is not Padlocks.DevotionalTimerPadlock
            && p is not Padlocks.MimicPadlock)
        .ToArray();

    public static IEnumerable<Padlocks> NoMimicPadlockList = Enum.GetValues<Padlocks>()
        .Cast<Padlocks>()
        .Where(p => p is not Padlocks.MimicPadlock)
        .ToArray();

    /// <summary> A generic function to iterate through a collection and perform an action on each item </summary>
    public static void Each<T>(this IEnumerable<T> collection, Action<T> function)
    {
        foreach (var x in collection)
        {
            function(x);
        }
    }

    public static bool EqualsAny<T>(this T obj, params T[] values)
    {
        return values.Any(x => x!.Equals(obj));
    }

    // execute agressive inlining functions safely
    public static void Safe(Action action, bool suppressErrors = false)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            // log errors if not surpressed
            if (!suppressErrors)
            {
                throw new Exception($"{e.Message}\n{e.StackTrace ?? ""}");
            }
        }
    }

    public static readonly HashSet<string> TimerPadlocks = new HashSet<string>
    {
        Padlocks.FiveMinutesPadlock.ToName(),
        Padlocks.TimerPasswordPadlock.ToName(),
        Padlocks.OwnerTimerPadlock.ToName(),
        Padlocks.DevotionalTimerPadlock.ToName(),
        Padlocks.MimicPadlock.ToName()
    };


    // determines if getkeystate or getkeystateasync is called
    public static bool UseAsyncKeyCheck = false;

    // see if a key is pressed
    public static bool IsKeyPressed(Keys key)
    {
        // if it isnt any key just return false
        if (key == Keys.None)
        {
            return false;
        }
        // if we are using async key check, use getkeystateasync, otherwise use getkeystate
        if (UseAsyncKeyCheck)
        {
            return IsBitSet(User32.GetKeyState((int)key), 15);
        }
        else
        {
            return IsBitSet(User32.GetAsyncKeyState((int)key), 15);
        }
    }

    // see if the key bit is set
    public static bool IsBitSet(short b, int pos) => (b & (1 << pos)) != 0;

    public static void OpenCombo(string comboLabel)
    {
        var windowId = ImGui.GetID(comboLabel);
        var popupId = ~Crc32.Get("##ComboPopup", windowId);
        ImGui.OpenPopup(popupId); // was originally popup ID
    }
}
