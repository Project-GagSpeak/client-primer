using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Utility;
using FFStreamViewer.Localization;
using FFStreamViewer.WebAPI.Interop.Ipc;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.ConfigurationServices;
using FFStreamViewer.WebAPI.Services.Mediator;
using ImGuiNET;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace FFStreamViewer.WebAPI.UI;

/// <summary> The shared service for UI elements within our plugin. </summary>
public partial class UiSharedService : DisposableMediatorSubscriberBase
{
    public const string TooltipSeparator = "--SEP--";                           // the tooltip seperator                                
    public static readonly ImGuiWindowFlags PopupWindowFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    private const string _nicknameEnd = "##GAGSPEAK_USER_NICKNAME_END##";       // the end of the nickname
    private const string _nicknameStart = "##GAGSPEAK_USER_NICKNAME_START##";   // the start of the nickname
    private readonly ApiController _apiController;                              // our api controller for the server connectivity
    private readonly ClientConfigurationManager _clientConfigurationManager;    // the client-end related config service 
    private readonly ServerConfigurationManager _serverConfigurationManager;    // the server-end related config manager
    private readonly OnFrameworkService _frameworkUtil;                         // helpers for functions that should occur dalamud's framework  thread
    private readonly IpcManager _ipcManager;                                    // manager for the IPC's our plugin links with
    private readonly Dalamud.Localization _localization;                        // language localization for our plugin
    private readonly IDalamudPluginInterface _pluginInterface;                   // the primary interface for our plugin
    private readonly Dictionary<string, object> _selectedComboItems;            // the selected combo items
    private bool _glamourerExists = false;                                      // if glamourer currently exists on the client
    private bool _moodlesExists = false;                                        // if moodles currently exists on the client
    private bool _penumbraExists = false;                                       // if penumbra currently exists on the client
    public UiSharedService(ILogger<UiSharedService> logger, IpcManager ipcManager, ApiController apiController,
        OnFrameworkService frameworkutil, IDalamudPluginInterface pluginInterface, Dalamud.Localization localization,
        ClientConfigurationManager clientManager, ServerConfigurationManager serverManager,
        GagspeakMediator mediator) : base(logger, mediator)
    {
        _ipcManager = ipcManager;
        _apiController = apiController;
        _frameworkUtil = frameworkutil;
        _pluginInterface = pluginInterface;
        _localization = localization;
        _selectedComboItems = new(StringComparer.Ordinal);
        _clientConfigurationManager = clientManager;
        _serverConfigurationManager = serverManager;
        _localization.SetupWithLangCode("en");

        // A subsciption from our mediator to see on each delayed framework if the IPC's are available from the IPC manager
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) =>
        {
            // _penumbraExists = _ipcManager.Penumbra.APIAvailable; (add soon)
            // _glamourerExists = _ipcManager.Glamourer.APIAvailable; (add soon)
            _moodlesExists = _ipcManager.Moodles.APIAvailable;
        });

        // the font atlas for our UID display (make it the font from gagspeak probably unless this fits more)
        UidFont = _pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansJpMedium, new()
            {
                SizePx = 35
            }));
        });

        // the font atlas for our game font
        GameFont = _pluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis12));
        // the font atlas for our icon font
        IconFont = _pluginInterface.UiBuilder.IconFontFixedWidthHandle;
    }

    public ApiController ApiController => _apiController;   // a public accessible api controller for the plugin, pulled from the private field
    public IFontHandle GameFont { get; init; } // the current game font
    // public bool HasValidPenumbraModPath => !(_ipcManager.Penumbra.ModDirectory ?? string.Empty).IsNullOrEmpty() && Directory.Exists(_ipcManager.Penumbra.ModDirectory);
    public IFontHandle IconFont { get; init; } // the current icon font
    public string PlayerName => _frameworkUtil.GetPlayerName();
    public IFontHandle UidFont { get; init; } // the current UID font
    public Dictionary<ushort, string> WorldData => _frameworkUtil.WorldData.Value;
    public uint WorldId => _frameworkUtil.GetHomeWorldId(); // the homeworld ID of the current player

    /// <summary> 
    /// A helper function to attach a tooltip to a section in the UI currently hovered. 
    /// </summary>
    /// <param name="text"> The text to display in the tooltip. </param>
    public static void AttachToolTip(string text)
    {
        // if the item is currently hovered, with the ImGuiHoveredFlags set to allow when disabled
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            // begin the tooltip interface
            ImGui.BeginTooltip();
            // push the text wrap position to the font size times 35
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            // we will then check to see if the text contains a tooltip seperator
            if (text.Contains(TooltipSeparator, StringComparison.Ordinal))
            {
                // if it does, we will split the text by the tooltip seperator
                var splitText = text.Split(TooltipSeparator, StringSplitOptions.RemoveEmptyEntries);
                // for each of the split text, we will display the text unformatted
                for (int i = 0; i < splitText.Length; i++)
                {
                    ImGui.TextUnformatted(splitText[i]);
                    if (i != splitText.Length - 1) ImGui.Separator();
                }
            }
            // otherwise, if it contains no tooltip seperator, then we will display the text unformatted
            else
            {
                ImGui.TextUnformatted(text);
            }
            // finally, pop the text wrap position and end the tooltip
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    /// <summary>
    /// A helper function for centering the next displayed window.
    /// </summary>
    /// <param name="width"> The width of the window. </param>
    /// <param name="height"> The height of the window. </param>
    /// <param name="cond"> The condition for the ImGuiWindow to be displayed . </param>
    public static void CenterNextWindow(float width, float height, ImGuiCond cond = ImGuiCond.None)
    {
        // get the center of the main viewport
        var center = ImGui.GetMainViewport().GetCenter();
        // then set the next window position to the center minus half the width and height
        ImGui.SetNextWindowPos(new Vector2(center.X - width / 2, center.Y - height / 2), cond);
    }

    /// <summary>
    /// A helper function for retrieving the proper color value given RGBA.
    /// </summary>
    /// <returns> The color formatted as a uint </returns>
    public static uint Color(byte r, byte g, byte b, byte a)
    { uint ret = a; ret <<= 8; ret += b; ret <<= 8; ret += g; ret <<= 8; ret += r; return ret; }

    /// <summary>
    /// A helper function for retrieving the proper color value given a vector4.
    /// </summary>
    /// <returns> The color formatted as a uint </returns>
    public static uint Color(Vector4 color)
    {
        uint ret = (byte)(color.W * 255);
        ret <<= 8;
        ret += (byte)(color.Z * 255);
        ret <<= 8;
        ret += (byte)(color.Y * 255);
        ret <<= 8;
        ret += (byte)(color.X * 255);
        return ret;
    }

    /// <summary>
    /// A helper function for displaying colortext. Keep in mind that this already exists in ottergui and we likely dont need it.
    /// </summary>
    public static void ColorText(string text, Vector4 color)
    {
        using var raiicolor = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
    }

    /// <summary>
    /// A helper function for wrapped text that is colored.  Keep in mind that this already exists in ottergui and we likely dont need it.
    /// </summary>
    public static void ColorTextWrapped(string text, Vector4 color)
    {
        using var raiicolor = ImRaii.PushColor(ImGuiCol.Text, color);
        TextWrapped(text);
    }

    /// <summary> 
    /// Helper function to see if the CTRL key is currently held down.
    ///  Keep in mind that this already exists in ottergui and we likely dont need it.
    /// </summary>
    /// <returns> True if CTRL is being pressed, false if not. </returns>
    public static bool CtrlPressed() => (GetKeyState(0xA2) & 0x8000) != 0 || (GetKeyState(0xA3) & 0x8000) != 0;

    /// <summary>
    /// Helper function to draw the outlined font in ImGui.
    /// Im not actually sure if this is in ottergui or not.
    /// </summary>
    public static void DrawOutlinedFont(string text, Vector4 fontColor, Vector4 outlineColor, int thickness)
    {
        var original = ImGui.GetCursorPos();

        using (ImRaii.PushColor(ImGuiCol.Text, outlineColor))
        {
            ImGui.SetCursorPos(original with { Y = original.Y - thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X - thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { Y = original.Y + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X - thickness, Y = original.Y - thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X + thickness, Y = original.Y + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X - thickness, Y = original.Y + thickness });
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original with { X = original.X + thickness, Y = original.Y - thickness });
            ImGui.TextUnformatted(text);
        }

        using (ImRaii.PushColor(ImGuiCol.Text, fontColor))
        {
            ImGui.SetCursorPos(original);
            ImGui.TextUnformatted(text);
            ImGui.SetCursorPos(original);
            ImGui.TextUnformatted(text);
        }
    }

    public static void DrawOutlinedFont(ImDrawListPtr drawList, string text, Vector2 textPos, uint fontColor, uint outlineColor, int thickness)
    {
        drawList.AddText(textPos with { Y = textPos.Y - thickness },
            outlineColor, text);
        drawList.AddText(textPos with { X = textPos.X - thickness },
            outlineColor, text);
        drawList.AddText(textPos with { Y = textPos.Y + thickness },
            outlineColor, text);
        drawList.AddText(textPos with { X = textPos.X + thickness },
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y - thickness),
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y + thickness),
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X - thickness, textPos.Y + thickness),
            outlineColor, text);
        drawList.AddText(new Vector2(textPos.X + thickness, textPos.Y - thickness),
            outlineColor, text);

        drawList.AddText(textPos, fontColor, text);
        drawList.AddText(textPos, fontColor, text);
    }

    public static Vector4 GetBoolColor(bool input) => input ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed;

    public float GetIconTextButtonSize(FontAwesomeIcon icon, string text)
    {
        Vector2 vector;
        using (IconFont.Push())
            vector = ImGui.CalcTextSize(icon.ToIconString());

        Vector2 vector2 = ImGui.CalcTextSize(text);
        float num = 3f * ImGuiHelpers.GlobalScale;
        return vector.X + vector2.X + ImGui.GetStyle().FramePadding.X * 2f + num;
    }

    /// <summary>
    /// Helper function for retrieving the nickname of a clients paired users.
    /// </summary>
    /// <param name="pairs"> The list of pairs the client has. </param>
    /// <returns> The string of nicknames for the pairs. </returns>
    public static string GetNicknames(List<Pair> pairs)
    {
        StringBuilder sb = new();
        sb.AppendLine(_nicknameStart);
        foreach (var entry in pairs)
        {
            var note = entry.GetNickname();
            if (note.IsNullOrEmpty()) continue;

            sb.Append(entry.UserData.UID).Append(":\"").Append(entry.GetNickname()).AppendLine("\"");
        }
        sb.AppendLine(_nicknameEnd);

        return sb.ToString();
    }

    public static float GetWindowContentRegionWidth()
    {
        return ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
    }

    public bool IconButton(FontAwesomeIcon icon, float? height = null)
    {
        string text = icon.ToIconString();

        ImGui.PushID(text);
        Vector2 vector;
        using (IconFont.Push())
            vector = ImGui.CalcTextSize(text);
        ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
        Vector2 cursorScreenPos = ImGui.GetCursorScreenPos();
        float x = vector.X + ImGui.GetStyle().FramePadding.X * 2f;
        float frameHeight = height ?? ImGui.GetFrameHeight();
        bool result = ImGui.Button(string.Empty, new Vector2(x, frameHeight));
        Vector2 pos = new Vector2(cursorScreenPos.X + ImGui.GetStyle().FramePadding.X,
            cursorScreenPos.Y + (height ?? ImGui.GetFrameHeight()) / 2f - (vector.Y / 2f));
        using (IconFont.Push())
            windowDrawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), text);
        ImGui.PopID();

        return result;
    }

    private bool IconTextButtonInternal(FontAwesomeIcon icon, string text, Vector4? defaultColor = null, float? width = null)
    {
        int num = 0;
        if (defaultColor.HasValue)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, defaultColor.Value);
            num++;
        }

        ImGui.PushID(text);
        Vector2 vector;
        using (IconFont.Push())
            vector = ImGui.CalcTextSize(icon.ToIconString());
        Vector2 vector2 = ImGui.CalcTextSize(text);
        ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
        Vector2 cursorScreenPos = ImGui.GetCursorScreenPos();
        float num2 = 3f * ImGuiHelpers.GlobalScale;
        float x = width ?? vector.X + vector2.X + ImGui.GetStyle().FramePadding.X * 2f + num2;
        float frameHeight = ImGui.GetFrameHeight();
        bool result = ImGui.Button(string.Empty, new Vector2(x, frameHeight));
        Vector2 pos = new Vector2(cursorScreenPos.X + ImGui.GetStyle().FramePadding.X, cursorScreenPos.Y + ImGui.GetStyle().FramePadding.Y);
        using (IconFont.Push())
            windowDrawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), icon.ToIconString());
        Vector2 pos2 = new Vector2(pos.X + vector.X + num2, cursorScreenPos.Y + ImGui.GetStyle().FramePadding.Y);
        windowDrawList.AddText(pos2, ImGui.GetColorU32(ImGuiCol.Text), text);
        ImGui.PopID();
        if (num > 0)
        {
            ImGui.PopStyleColor(num);
        }

        return result;
    }

    public bool IconTextButton(FontAwesomeIcon icon, string text, float? width = null, bool isInPopup = false)
    {
        return IconTextButtonInternal(icon, text,
            isInPopup ? ColorHelpers.RgbaUintToVector4(ImGui.GetColorU32(ImGuiCol.PopupBg)) : null,
            width <= 0 ? null : width);
    }

    public static bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
    {
        try
        {
            using FileStream fs = File.Create(
                       Path.Combine(
                           dirPath,
                           Path.GetRandomFileName()
                       ),
                       1,
                       FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            if (throwIfFails)
                throw;

            return false;
        }
    }

    public static void SetScaledWindowSize(float width, bool centerWindow = true)
    {
        var newLineHeight = ImGui.GetCursorPosY();
        ImGui.NewLine();
        newLineHeight = ImGui.GetCursorPosY() - newLineHeight;
        var y = ImGui.GetCursorPos().Y + ImGui.GetWindowContentRegionMin().Y - newLineHeight * 2 - ImGui.GetStyle().ItemSpacing.Y;

        SetScaledWindowSize(width, y, centerWindow, scaledHeight: true);
    }

    public static void SetScaledWindowSize(float width, float height, bool centerWindow = true, bool scaledHeight = false)
    {
        ImGui.SameLine();
        var x = width * ImGuiHelpers.GlobalScale;
        var y = scaledHeight ? height : height * ImGuiHelpers.GlobalScale;

        if (centerWindow)
        {
            CenterWindow(x, y);
        }

        ImGui.SetWindowSize(new Vector2(x, y));
    }

    public static bool ShiftPressed() => (GetKeyState(0xA1) & 0x8000) != 0 || (GetKeyState(0xA0) & 0x8000) != 0;

    public static void CopyableDisplayText(string text, string tooltip = "Click to copy")
    {
        // then when the item is clicked, copy it to clipboard so we can share with others
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(text);
        }
        UiSharedService.AttachToolTip(tooltip);
    }


    public static void TextWrapped(string text)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    public static Vector4 UploadColor((long, long) data) => data.Item1 == 0 ? ImGuiColors.DalamudGrey :
        data.Item1 == data.Item2 ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudYellow;

    public bool ApplyNicknamesFromClipboard(string notes, bool overwrite)
    {
        var splitNicknames = notes.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
        var splitNicknamesStart = splitNicknames.FirstOrDefault();
        var splitNicknamesEnd = splitNicknames.LastOrDefault();
        if (!string.Equals(splitNicknamesStart, _nicknameStart, StringComparison.Ordinal) || !string.Equals(splitNicknamesEnd, _nicknameEnd, StringComparison.Ordinal))
        {
            return false;
        }

        splitNicknames.RemoveAll(n => string.Equals(n, _nicknameStart, StringComparison.Ordinal) || string.Equals(n, _nicknameEnd, StringComparison.Ordinal));

        foreach (var note in splitNicknames)
        {
            try
            {
                var splittedEntry = note.Split(":", 2, StringSplitOptions.RemoveEmptyEntries);
                var uid = splittedEntry[0];
                var comment = splittedEntry[1].Trim('"');
                if (_serverConfigurationManager.GetNicknameForUid(uid) != null && !overwrite) continue;
                _serverConfigurationManager.SetNicknameForUid(uid, comment);
            }
            catch
            {
                Logger.LogWarning("Could not parse {note}", note);
            }
        }

        _serverConfigurationManager.SaveNicknames();

        return true;
    }

    public void BigText(string text, Vector4? color = null)
    {
        FontText(text, UidFont, color);
    }

    public void BooleanToColoredIcon(bool value, bool inline = true)
    {
        using var colorgreen = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.HealerGreen, value);
        using var colorred = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed, !value);

        if (inline) ImGui.SameLine();

        if (value)
        {
            IconText(FontAwesomeIcon.Check);
        }
        else
        {
            IconText(FontAwesomeIcon.Times);
        }
    }

    public T? DrawCombo<T>(string comboName, IEnumerable<T> comboItems, Func<T, string> toName,
        Action<T?>? onSelected = null, T? initialSelectedItem = default)
    {
        if (!comboItems.Any()) return default;

        if (!_selectedComboItems.TryGetValue(comboName, out var selectedItem) && selectedItem == null)
        {
            if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
            {
                selectedItem = initialSelectedItem;
                _selectedComboItems[comboName] = selectedItem!;
                if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
                    onSelected?.Invoke(initialSelectedItem);
            }
            else
            {
                selectedItem = comboItems.First();
                _selectedComboItems[comboName] = selectedItem!;
            }
        }

        if (ImGui.BeginCombo(comboName, toName((T)selectedItem!)))
        {
            foreach (var item in comboItems)
            {
                bool isSelected = EqualityComparer<T>.Default.Equals(item, (T?)selectedItem);
                if (ImGui.Selectable(toName(item), isSelected))
                {
                    _selectedComboItems[comboName] = item!;
                    onSelected?.Invoke(item!);
                }
            }

            ImGui.EndCombo();
        }

        return (T)_selectedComboItems[comboName];
    }

    public void DrawHelpText(string helpText)
    {
        ImGui.SameLine();
        IconText(FontAwesomeIcon.QuestionCircle, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        AttachToolTip(helpText);
    }

    public bool DrawOtherPluginState()
    {
        var check = FontAwesomeIcon.Check;
        var cross = FontAwesomeIcon.SquareXmark;
        ImGui.TextUnformatted("Optional Plugins:");

        ImGui.SameLine();
        ImGui.TextUnformatted("Penumbra");
        ImGui.SameLine();
        IconText(_penumbraExists ? check : cross, GetBoolColor(_penumbraExists));
        ImGui.SameLine();
        AttachToolTip($"Penumbra is " + (_penumbraExists ? "available and up to date." : "unavailable or not up to date."));
        ImGui.Spacing();

        ImGui.SameLine();
        ImGui.TextUnformatted("Glamourer");
        ImGui.SameLine();
        IconText(_glamourerExists ? check : cross, GetBoolColor(_glamourerExists));
        ImGui.SameLine();
        AttachToolTip($"Glamourer is " + (_glamourerExists ? "available and up to date." : "unavailable or not up to date."));
        ImGui.Spacing();

        ImGui.SameLine();
        ImGui.TextUnformatted("Moodles");
        ImGui.SameLine();
        IconText(_moodlesExists ? check : cross, GetBoolColor(_moodlesExists));
        ImGui.SameLine();
        AttachToolTip($"Moodles is " + (_moodlesExists ? "available and up to date." : "unavailable or not up to date."));
        ImGui.Spacing();

        return true;
    }

    public Vector2 GetIconButtonSize(FontAwesomeIcon icon)
    {
        using var font = IconFont.Push();
        return ImGuiHelpers.GetButtonSize(icon.ToIconString());
    }

    public Vector2 GetIconData(FontAwesomeIcon icon)
    {
        using var font = IconFont.Push();
        return ImGui.CalcTextSize(icon.ToIconString());
    }

    public void IconText(FontAwesomeIcon icon, uint color)
    {
        FontText(icon.ToIconString(), IconFont, color);
    }

    public void IconText(FontAwesomeIcon icon, Vector4? color = null)
    {
        IconText(icon, color == null ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(color.Value));
    }

    public void LoadLocalization(string languageCode)
    {
        _localization.SetupWithLangCode(languageCode);
        Strings.ToS = new Strings.ToSStrings();
    }

    // for grabbing key states (we did something similar with the hardcore module)
    [LibraryImport("user32")]
    internal static partial short GetKeyState(int nVirtKey);


    private static void CenterWindow(float width, float height, ImGuiCond cond = ImGuiCond.None)
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetWindowPos(new Vector2(center.X - width / 2, center.Y - height / 2), cond);
    }

    [GeneratedRegex(@"^(?:[a-zA-Z]:\\[\w\s\-\\]+?|\/(?:[\w\s\-\/])+?)$", RegexOptions.ECMAScript, 5000)]
    private static partial Regex PathRegex();

    private void FontText(string text, IFontHandle font, Vector4? color = null)
    {
        FontText(text, font, color == null ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(color.Value));
    }

    private void FontText(string text, IFontHandle font, uint color)
    {
        using var pushedFont = font.Push();
        using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
    }

    public sealed record IconScaleData(Vector2 IconSize, Vector2 NormalizedIconScale, float OffsetX, float IconScaling);

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;

        base.Dispose(disposing);

        UidFont.Dispose();
        GameFont.Dispose();
    }
}
