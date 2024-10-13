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
        .Where(p => p is not Padlocks.OwnerPadlock && p is not Padlocks.OwnerTimerPadlock && p is not Padlocks.MimicPadlock)
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

    public unsafe static string DecodeValue(AtkValue a)
    {
        var str = new StringBuilder(a.Type.ToString()).Append(": ");
        switch (a.Type)
        {
            case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int:
                {
                    str.Append(a.Int);
                    break;
                }
            case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String8:
            case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.WideString:
            case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.ManagedString:
            case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String:
                {
                    str.Append(Marshal.PtrToStringUTF8(new IntPtr(a.String)));
                    break;
                }
            case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt:
                {
                    str.Append(a.UInt);
                    break;
                }
            case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool:
                {
                    str.Append(a.Byte != 0);
                    break;
                }
            default:
                {
                    str.Append($"Unknown Type: {a.Int}");
                    break;
                }
        }
        return str.ToString();
    }

    public static unsafe IntPtr GetAddonByName(string name)
    {
        var atkStage = AtkStage.Instance();
        if (atkStage == null)
            return IntPtr.Zero;

        var unitMgr = atkStage->RaptureAtkUnitManager;
        if (unitMgr == null)
            return IntPtr.Zero;

        var addon = unitMgr->GetAddonByName(name, 1);
        if (addon == null)
            return IntPtr.Zero;

        return (IntPtr)addon;
    }


    public static readonly HashSet<string> TimerPadlocks = new HashSet<string>
    {
        Padlocks.FiveMinutesPadlock.ToName(),
        Padlocks.TimerPasswordPadlock.ToName(),
        Padlocks.OwnerTimerPadlock.ToName(),
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
