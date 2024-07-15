using PInvoke;
using System.Windows.Forms;

namespace GagSpeak.Utils;

/// <summary> A class for all of the UI helpers, including basic functions for drawing repetative yet unique design elements </summary>
public static class GenericHelpers
{
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
}
