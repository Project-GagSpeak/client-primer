using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.Interop.Ipc;
using GagSpeak.Localization;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using OtterGui;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace GagSpeak.UI;

/// <summary> 
/// The shared service for UI elements within our plugin. 
/// 
/// This function should be expected to take advantage 
/// of classes with common functionality, preventing copy pasting.
/// 
/// Think of it as a collection of helpers for all functions.
/// </summary>
public partial class UiSharedService : DisposableMediatorSubscriberBase
{
    public const string TooltipSeparator = "--SEP--";                           // the tooltip seperator                                
    public static readonly ImGuiWindowFlags PopupWindowFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    private const string _nicknameEnd = "##GAGSPEAK_USER_NICKNAME_END##";       // the end of the nickname
    private const string _nicknameStart = "##GAGSPEAK_USER_NICKNAME_START##";   // the start of the nickname
    private readonly Dalamud.Localization _localization;                        // language localization for our plugin

    private readonly ApiController _apiController;                              // our api controller for the server connectivity
    private readonly ClientConfigurationManager _clientConfigs;    // the client-end related config service 
    private readonly ServerConfigurationManager _serverConfigs;    // the server-end related config manager
    private readonly OnFrameworkService _frameworkUtil;                         // helpers for functions that should occur dalamud's framework  thread
    private readonly IpcManager _ipcManager;                                    // manager for the IPC's our plugin links 

    private readonly IDalamudPluginInterface _pi;       // the primary interface for our plugin
    private readonly ITextureProvider _textureProvider; // the texture provider for our plugin
    private ISharedImmediateTexture _sharedTextures;    // represents a shared texture cache for plugin images.

    private readonly Dictionary<string, object> _selectedComboItems;    // the selected combo items
    private bool _glamourerExists = false;                              // if glamourer currently exists on the client
    private bool _moodlesExists = false;                                // if moodles currently exists on the client
    private bool _penumbraExists = false;                               // if penumbra currently exists on the client
    private bool _useTheme = true;                                      // if we should use the GagSpeak Theme

    // default image paths
    private const string GagspeakLogoPath = "iconUI.png";
    private const string GagspeakLogoPathSmall = "icon.png";
    private const string SupporterTierOnePath = "Tier1Supporter.png";
    private const string SupporterTierTwoPath = "Tier2Supporter.png";
    private const string SupporterTierThreePath = "Tier3Supporter.png";

    public UiSharedService(ILogger<UiSharedService> logger, GagspeakMediator mediator,
        Dalamud.Localization localization, ApiController apiController,
        ClientConfigurationManager clientConfigurationManager,
        ServerConfigurationManager serverConfigurationManager,
        OnFrameworkService frameworkUtil, IpcManager ipcManager,
        IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider)
        : base(logger, mediator)
    {
        _localization = localization;
        _apiController = apiController;
        _clientConfigs = clientConfigurationManager;
        _serverConfigs = serverConfigurationManager;
        _frameworkUtil = frameworkUtil;
        _ipcManager = ipcManager;
        _pi = pluginInterface;
        _textureProvider = textureProvider;

        _localization.SetupWithLangCode("en");
        _selectedComboItems = new(StringComparer.Ordinal);

        // A subscription from our mediator to see on each delayed framework if the IPC's are available from the IPC manager
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) =>
        {
            // _penumbraExists = _ipcManager.Penumbra.APIAvailable; (add soon)
            // _glamourerExists = _ipcManager.Glamourer.APIAvailable; (add soon)
            _moodlesExists = _ipcManager.Moodles.APIAvailable;
        });

        // the font atlas for our UID display (make it the font from gagspeak probably unless this fits more)
        UidFont = _pi.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansJpMedium, new()
            {
                SizePx = 35
            }));
        });

        // the font atlas for our game font
        GameFont = _pi.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis12));
        // the font atlas for our icon font
        IconFont = _pi.UiBuilder.IconFontFixedWidthHandle;
    }

    public ApiController ApiController => _apiController;   // a public accessible api controller for the plugin, pulled from the private field
    public IFontHandle GameFont { get; init; } // the current game font
    // public bool HasValidPenumbraModPath => !(_ipcManager.Penumbra.ModDirectory ?? string.Empty).IsNullOrEmpty() && Directory.Exists(_ipcManager.Penumbra.ModDirectory);
    public IFontHandle IconFont { get; init; } // the current icon font
    public string PlayerName => _frameworkUtil.GetPlayerName();
    public UserData PlayerUserData => _apiController.GetConnectionDto().Result.User;
    public IFontHandle UidFont { get; init; } // the current UID font
    public IFontHandle GagspeakFont { get; init; } // the current Gagspeak font
    public Dictionary<ushort, string> WorldData => _frameworkUtil.WorldData.Value;
    public uint WorldId => _frameworkUtil.GetHomeWorldId(); // the homeworld ID of the current player
    public bool UseTheme => _useTheme;
    public string SearchFilter { get; set; } = ""; // the search filter used in whitelist. Stored here to ensure the tab menu can clear it upon switching tabs.



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

    public Vector2 GetGlobalHelperScaleSize(Vector2 size) => size * ImGuiHelpers.GlobalScale;

    public float GetFontScalerFloat() => ImGuiHelpers.GlobalScale * (_pi.UiBuilder.DefaultFontSpec.SizePt / 12f);

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

    public static float GetWindowContentRegionWidth() => ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

    public bool DrawScaledCenterButtonImage(string ID, Vector2 buttonSize, Vector4 buttonColor,
        Vector2 imageSize, IDalamudTextureWrap image)
    {
        // push ID for the function
        ImGui.PushID(ID);
        // grab the current cursor position
        var InitialPos = ImGui.GetCursorPos();
        // calculate the difference in height between the button and the image
        var heightDiff = buttonSize.Y - imageSize.Y;
        // draw out the button centered
        if (UtilsExtensions.CenteredLineWidths.TryGetValue(ID, out var dims))
        {
            ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2 - dims / 2);
        }
        var oldCur = ImGui.GetCursorPosX();
        bool result = ImGui.Button(string.Empty, buttonSize);
        //Logger.LogTrace("Result of button: {result}", result);
        ImGui.SameLine(0, 0);
        UtilsExtensions.CenteredLineWidths[ID] = ImGui.GetCursorPosX() - oldCur;
        ImGui.Dummy(Vector2.Zero);

        // now go back up to the inital position, then step down by the height difference/2
        ImGui.SetCursorPosY(InitialPos.Y + heightDiff / 2);

        // now draw out the image over the button
        UtilsExtensions.ImGuiLineCentered($"###CenterImage{ID}", () =>
        {
            ImGui.Image(image.ImGuiHandle, imageSize, Vector2.Zero, Vector2.One, buttonColor);
        });
        ImGui.PopID();
        // return the result
        return result;
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

    private bool IconTextButtonInternal(FontAwesomeIcon icon, string text, Vector4? defaultColor = null, float? width = null, bool disabled = false)
    {
        using var dis = ImRaii.PushStyle(ImGuiStyleVar.Alpha, disabled ? 0.5f : 1f);
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
        dis.Pop();

        return result && !disabled;
    }

    public bool IconTextButton(FontAwesomeIcon icon, string text, float? width = null, bool isInPopup = false, bool disabled = false)
    {
        return IconTextButtonInternal(icon, text,
            isInPopup ? ColorHelpers.RgbaUintToVector4(ImGui.GetColorU32(ImGuiCol.PopupBg)) : null,
            width <= 0 ? null : width,
            disabled);
    }


    private bool IconInputTextInternal(FontAwesomeIcon icon, string label, string hint, ref string inputStr,
        uint maxLength, Vector4? defaultColor = null, float? width = null, bool disabled = false)
    {
        using var dis = ImRaii.PushStyle(ImGuiStyleVar.Alpha, disabled ? 0.5f : 1f);
        int num = 0;
        /*        if (defaultColor.HasValue)
                {
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, defaultColor.Value);
                    num++;
                }*/

        ImGui.PushID(label);
        Vector2 vector;
        using (IconFont.Push())
            vector = ImGui.CalcTextSize(icon.ToIconString());
        Vector2 vector2 = ImGui.CalcTextSize(label);
        ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
        Vector2 cursorScreenPos = ImGui.GetCursorScreenPos();
        float num2 = 3f * ImGuiHelpers.GlobalScale;
        float x = width ?? vector.X + vector2.X + ImGui.GetStyle().FramePadding.X * 2f + num2;
        float frameHeight = ImGui.GetFrameHeight();
        ImGui.SetCursorPosX(vector.X + ImGui.GetStyle().FramePadding.X * 2f + num2);
        ImGui.SetNextItemWidth(x - vector.X - num2);
        bool result = ImGui.InputTextWithHint(String.Empty, hint, ref inputStr, maxLength);

        Vector2 pos = new Vector2(cursorScreenPos.X + ImGui.GetStyle().FramePadding.X, cursorScreenPos.Y + ImGui.GetStyle().FramePadding.Y);
        using (IconFont.Push())
            windowDrawList.AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), icon.ToIconString());
        ImGui.PopID();
        if (num > 0)
        {
            ImGui.PopStyleColor(num);
        }
        dis.Pop();

        return result && !disabled;
    }

    public bool IconInputText(FontAwesomeIcon icon, string label, string hint, ref string inputStr,
        uint maxLength, float? width = null, bool isInPopup = false, bool disabled = false)
    {
        return IconInputTextInternal(icon, label, hint, ref inputStr, maxLength,
            isInPopup ? ColorHelpers.RgbaUintToVector4(ImGui.GetColorU32(ImGuiCol.PopupBg)) : null,
            width <= 0 ? null : width,
            disabled);
    }

    /// <summary> Grabs a TextureWrap from the sharedIntermediateTexture interface via a path directory. </summary>
    /// <returns> A DalamudTexture Wrap. Advised to use [ if(!(RESULT is { } wrap) to verify validity. </returns>
    public IDalamudTextureWrap GetImageFromDirectoryFile(string path)
    {
        // fetch the image from plugin directory and store as the active shared intermediate texture
        _sharedTextures = _textureProvider.GetFromFile(Path.Combine(_pi.AssemblyLocation.DirectoryName!, path));
        return _sharedTextures.GetWrapOrEmpty();
    }

    // helpers to get the static images
    public IDalamudTextureWrap GetGagspeakLogo() => GetImageFromDirectoryFile(GagspeakLogoPath);
    public IDalamudTextureWrap GetGagspeakLogoSmall() => GetImageFromDirectoryFile(GagspeakLogoPathSmall);
    public IDalamudTextureWrap GetSupporterTierOne() => GetImageFromDirectoryFile(SupporterTierOnePath);
    public IDalamudTextureWrap GetSupporterTierTwo() => GetImageFromDirectoryFile(SupporterTierTwoPath);
    public IDalamudTextureWrap GetSupporterTierThree() => GetImageFromDirectoryFile(SupporterTierThreePath);

    public IDalamudTextureWrap LoadImage(byte[] imageData)
    {
        return _textureProvider.CreateFromImageAsync(imageData).Result;
    }

    public Padlocks GetPadlock(string padlockType)
    {
        Enum.TryParse<Padlocks>(padlockType, true, out var PadlockType);
        return PadlockType;
    }

    /// <summary> Cleans sender string from the chatlog before processing, so it stays a valid player sender string. </summary>
    /// <param name="senderName"> The original uncleaned sender name string </param>
    /// <returns> The cleaned sender name string </returns>
    public static string CleanSenderName(string senderName)
    {
        string[] senderStrings = SplitCamelCase(RemoveSpecialSymbols(senderName)).Split(" ");
        string playerSender = senderStrings.Length == 1 ? senderStrings[0] : senderStrings.Length == 2 ?
            (senderStrings[0] + " " + senderStrings[1]) :
            (senderStrings[0] + " " + senderStrings[2]);
        return playerSender;
    }

    /// <summary> function for splitting camel case </summary>
    /// <param name="input"> the input string </param>
    /// <returns> The string with camel case split </returns>
    public static string SplitCamelCase(string input)
    {
        return Regex.Replace(input, "([A-Z])", " $1", RegexOptions.Compiled).Trim();
    }

    /// <summary> function for removing special symbols </summary>
    /// <returns> The string with special symbols removed </returns>
    public static string RemoveSpecialSymbols(string value)
    {
        Regex rgx = new Regex(@"[^\u4e00-\u9fff a-zA-Z:/._\ '-]");
        //      [^...] matches any character not in the brackets.
        //      a-z matches any lowercase letter.
        //      A-Z matches any uppercase letter.
        //      :/._\ '- matches a colon, slash, period, underscore, space, or hyphen, or apostrophe.
        //      \u4e00-\u9fff matches any Chinese character.
        return rgx.Replace(value, "");
    }


    /// <summary> Validates a password </summary>
    public bool ValidatePassword(string password)
    {
        Logger.LogDebug($"Validating Password {password}");
        return !string.IsNullOrWhiteSpace(password) && password.Length <= 20 && !password.Contains(" ");
    }

    /// <summary> Validates a 4 digit combination </summary>
    public bool ValidateCombination(string combination)
    {
        Logger.LogDebug($"Validating Combination {combination}");
        return int.TryParse(combination, out _) && combination.Length == 4;
    }

    /// <summary> Helper function to convert a timespan object into a string with format XdXhXmXs. </summary>
    public string TimeSpanToString(TimeSpan timeSpan)
    {
        return $"{timeSpan.Days}d{timeSpan.Hours}h{timeSpan.Minutes}m{timeSpan.Seconds}s";
    }

    public static DateTimeOffset GetEndTime(string input)
    {
        // Match days, hours, minutes, and seconds in the input string
        var match = Regex.Match(input, @"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$");

        if (match.Success)
        {
            // Parse days, hours, minutes, and seconds 
            int.TryParse(match.Groups[1].Value, out int days);
            int.TryParse(match.Groups[2].Value, out int hours);
            int.TryParse(match.Groups[3].Value, out int minutes);
            int.TryParse(match.Groups[4].Value, out int seconds);
            // Create a TimeSpan from the parsed values
            TimeSpan duration = new TimeSpan(days, hours, minutes, seconds);
            // Add the duration to the current DateTime to get a DateTimeOffset
            return DateTimeOffset.Now.Add(duration);
        }

        // If the input string is not in the correct format, throw an exception
        throw new FormatException($"Invalid duration format: {input}");
    }

    /// <summary>
    /// Helper function to try and take the string version of timespan and convert it back to a timespan object.
    /// </summary>
    /// <param name="input"> the string variant of the timespan </param>
    /// <param name="result"> the timespan equivalent of the string passed in </param>
    /// <returns> if the parse was successful or not. </returns>
    public bool TryParseTimeSpan(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        var regex = new Regex(@"(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?");
        var match = regex.Match(input);

        if (!match.Success)
        {
            return false;
        }

        int days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        int hours = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        int minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        int seconds = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;

        result = new TimeSpan(days, hours, minutes, seconds);
        return true;
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

    /// <summary> Fancy function that lets you edit a normally non-editable text field via a right click pop up text edit. </summary>
    /// <param name="popupId"> the ID for the popup we'll be making </param>
    /// <param name="text"> the text we are editing </param>
    /// <param name="maxLength"> the max length of the text </param>
    /// <param name="helpText"> the help text for the popup </param>

    public static void EditableTextFieldWithPopup(string popupId, ref string text, uint maxLength, string helpText)
    {
        ImGui.TextWrapped(text);
        if (ImGui.IsItemHovered() && ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup(popupId); // Open the context menu
        // open the popup if we satisfy that criteria
        if (ImGui.BeginPopup(popupId))
        {
            // store our text from when we open it
            string currentText = text;
            var oldText = currentText;
            // set keyboard focus to the text box
            if (ImGui.IsWindowAppearing()) { ImGui.SetKeyboardFocusHere(0); }
            // pompt the user to enter a new name
            ImGui.TextUnformatted(helpText);
            if (ImGui.InputText("##Rename", ref currentText, maxLength, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                // if our text is updated, send the updated text to the output result as an action string
                if (currentText != oldText)
                    text = currentText;
                // close the popup
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
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
                if (_serverConfigs.GetNicknameForUid(uid) != null && !overwrite) continue;
                _serverConfigs.SetNicknameForUid(uid, comment);
            }
            catch
            {
                Logger.LogWarning("Could not parse {note}", note);
            }
        }

        _serverConfigs.SaveNicknames();

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

    public T? DrawCombo<T>(string comboName, float width, IEnumerable<T> comboItems, Func<T, string> toName,
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

        ImGui.SetNextItemWidth(width);
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
        // Check if the item was right-clicked. If so, reset to default value.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            selectedItem = comboItems.First();
            _selectedComboItems[comboName] = selectedItem!;
            onSelected?.Invoke((T)selectedItem!);
        }

        return (T)_selectedComboItems[comboName];
    }

    public T? DrawComboSearchable<T>(string comboName, float width, ref string searchString, IEnumerable<T> comboItems,
        Func<T, string> toName, bool showLabel = true, Action<T?>? onSelected = null, T? initialSelectedItem = default)
    {
        // Return default if there are no items to display in the combo box.
        if (!comboItems.Any()) return default;

        // try to get currently selected item from a dictionary storing selections for each combo box.
        if (!_selectedComboItems.TryGetValue(comboName, out var selectedItem) && selectedItem == null)
        {
            if (!EqualityComparer<T>.Default.Equals(initialSelectedItem, default))
            {
                selectedItem = initialSelectedItem;
                _selectedComboItems[comboName] = selectedItem!;
            }
            else
            {
                selectedItem = comboItems.First();
                _selectedComboItems[comboName] = selectedItem!;
            }
        }

        // if the selected item is not in the list of items being passed in, update it to the first item in the comboItems list
        if(!comboItems.Contains((T)selectedItem!))
        {
            selectedItem = comboItems.First();
            _selectedComboItems[comboName] = selectedItem!;
        }

        ImGui.SetNextItemWidth(width);
        string comboLabel = showLabel ? $"{comboName}##{comboName}" : $"##{comboName}";
        if (ImGui.BeginCombo(comboLabel, toName((T)selectedItem!)))
        {
            // Search filter
            ImGui.SetNextItemWidth(width);
            ImGui.InputTextWithHint("##filter", "Filter...", ref searchString, 255);
            var searchText = searchString.ToLowerInvariant();

            var filteredItems = string.IsNullOrEmpty(searchText)
                ? comboItems
                : comboItems.Where(item => toName(item).ToLowerInvariant().Contains(searchText));

            // display filtered content.
            foreach (var item in filteredItems)
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
        // Check if the item was right-clicked. If so, reset to default value.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            Logger.LogTrace("Right-clicked on {comboName}. Resetting to default value.", comboName);
            selectedItem = comboItems.First();
            _selectedComboItems[comboName] = selectedItem!;
            onSelected?.Invoke((T)selectedItem!);
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
