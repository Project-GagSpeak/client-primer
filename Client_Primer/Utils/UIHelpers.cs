using System.Text.RegularExpressions;
using System;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;  // Contains classes for handling special encoded (SeString) payloads in the Dalamud game
using ImGuiNET;
using Lumina.Misc;
using OtterGui;
using OtterGui.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Colors;

namespace FFStreamViewer.Utils;

/// <summary> A class for all of the UI helpers, including basic functions for drawing repetative yet unique design elements </summary>
public static class UIHelpers
{
    /// <summary>
    /// Removes special symbols from a string
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string RemoveSpecialSymbols(string value) {
        Regex rgx = new Regex(@"[^a-zA-Z:/._\ -]");
        return rgx.Replace(value, "");
    }
}