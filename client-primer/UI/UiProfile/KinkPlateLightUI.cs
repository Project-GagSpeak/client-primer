using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.IPC;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Extensions;
using ImGuiNET;
using Microsoft.IdentityModel.Tokens;
using NAudio.Gui;
using Penumbra.GameData.Enums;
using System;
using System.Numerics;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
using Dalamud.Interface.Utility.Raii;

namespace GagSpeak.UI.Profile;

/// <summary>
/// The UI Design for the KinkPlates.
/// </summary>
public class KinkPlateLightUI
{
    private readonly ILogger<KinkPlateLightUI> _logger;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly KinkPlateService _profileService;
    private readonly CosmeticService _cosmetics;
    private readonly TextureService _textures;
    private readonly UiSharedService _uiShared;

    private bool ThemePushed = false;
    public KinkPlateLightUI(ILogger<KinkPlateLightUI> logger,
        PairManager pairManager, ServerConfigurationManager serverConfigs,
        KinkPlateService profileService, CosmeticService cosmetics,
        TextureService textureService, UiSharedService uiShared)
    {
        _logger = logger;
        _pairManager = pairManager;
        _serverConfigs = serverConfigs;
        _profileService = profileService;
        _cosmetics = cosmetics;
        _textures = textureService;
        _uiShared = uiShared;
    }

    private Vector2 RectMin { get; set; } = Vector2.Zero;
    private Vector2 RectMax { get; set; } = Vector2.Zero;
    private Vector2 PlateSize => RectMax - RectMin;
    private Vector2 CloseButtonPos => RectMin + Vector2.One * 16f;
    private Vector2 CloseButtonSize => Vector2.One * 24f;
    private Vector2 ProfilePictureBorderPos => RectMin + Vector2.One * (PlateSize.X - ProfilePictureBorderSize.X)/2;
    private Vector2 ProfilePictureBorderSize => Vector2.One * 226f;
    private Vector2 ProfilePicturePos => RectMin + Vector2.One * (6+(PlateSize.X - ProfilePictureBorderSize.X) / 2);
    private Vector2 ProfilePictureSize => Vector2.One * 214f;
    private Vector2 SupporterIconBorderPos => RectMin + new Vector2(182, 16);
    private Vector2 SupporterIconBorderSize => Vector2.One * 52f;
    private Vector2 SupporterIconPos => RectMin + new Vector2(184, 18);
    private Vector2 SupporterIconSize => Vector2.One * 48f;
    private Vector2 DescriptionBorderPos => RectMin + new Vector2(12, 350);
    private Vector2 DescriptionBorderSize => new Vector2(232, 150);
    private Vector2 TitleLineStartPos => RectMin + new Vector2(12, 317);
    private Vector2 TitleLineSize => new Vector2(232, 5);
    private Vector2 StatsPos => RectMin + new Vector2(40, 326);

    private bool HoveringCloseButton { get; set; } = false;

    private static Vector4 Gold = new Vector4(1f, 0.851f, 0.299f, 1f);

    public void DrawKinkPlateLight(KinkPlate profile, string displayName, UserData userData, bool drawClose, Action onClosed)
    {
        var drawList = ImGui.GetWindowDrawList();
        // clip based on the region of our draw space.
        RectMin = drawList.GetClipRectMin();
        RectMax = drawList.GetClipRectMax();

        DrawPlate(drawList, profile.KinkPlateInfo, displayName, drawClose, onClosed);

        DrawProfilePic(drawList, profile, displayName, userData);

        DrawDescription(drawList, profile);

        // Now let's draw out the chosen achievement Name..
        using (_uiShared.GagspeakLabelFont.Push())
        {
            var chosenTitleSize = ImGui.CalcTextSize("Sample Title Chosen");
            ImGui.SetCursorScreenPos(new Vector2(TitleLineStartPos.X + TitleLineSize.X / 2 - chosenTitleSize.X / 2, TitleLineStartPos.Y - chosenTitleSize.Y));
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(ImGuiColors.ParsedGold, "Sample Title Chosen");
        }
        // move over to the top area to draw out the achievement title line wrap.
        KinkPlateUI.AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.AchievementLineSplit], TitleLineStartPos, TitleLineSize);

        DrawStats(drawList, profile.KinkPlateInfo, displayName);

    }

    private void DrawPlate(ImDrawListPtr drawList, KinkPlateContent info, string displayName, bool drawClose, Action onClosed)
    {
        // draw out the background for the window.
        if (_cosmetics.TryGetBackground(ProfileComponent.PlateLight, info.PlateBackground, out var plateBG))
            KinkPlateUI.AddImageRounded(drawList, plateBG, RectMin, PlateSize, 30f);

        // draw out the border on top of that.
        if (_cosmetics.TryGetBorder(ProfileComponent.PlateLight, info.PlateBorder, out var plateBorder))
            KinkPlateUI.AddImageRounded(drawList, plateBorder, RectMin, PlateSize, 20f);

        // Draw the close button.
        if(drawClose)
        {
            CloseButton(drawList, displayName, onClosed);
            KinkPlateUI.AddRelativeTooltip(CloseButtonPos, CloseButtonSize, "Close " + displayName + "'s KinkPlate™");
        }
    }

    private void DrawProfilePic(ImDrawListPtr drawList, KinkPlate profile, string displayName, UserData userData)
    {

        // Draw the profile Picture
        var pfpWrap = profile.GetCurrentProfileOrDefault();
        KinkPlateUI.AddImageRounded(drawList, pfpWrap, ProfilePicturePos, ProfilePictureSize, ProfilePictureSize.Y / 2);

        // draw out the border for the profile picture
        if (_cosmetics.TryGetBorder(ProfileComponent.ProfilePicture, profile.KinkPlateInfo.ProfilePictureBorder, out var pfpBorder))
            KinkPlateUI.AddImageRounded(drawList, pfpBorder, ProfilePictureBorderPos, ProfilePictureBorderSize, ProfilePictureSize.Y / 2);

        // Draw out Supporter Icon Black BG base.
        drawList.AddCircleFilled(SupporterIconBorderPos + SupporterIconBorderSize / 2,
            SupporterIconBorderSize.X / 2, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));

        // Draw out Supporter Icon.
        var supporterInfo = _cosmetics.GetSupporterInfo(userData);
        if (supporterInfo.SupporterWrap is { } wrap)
        {
            KinkPlateUI.AddImageRounded(drawList, wrap, SupporterIconPos, SupporterIconSize, SupporterIconSize.Y / 2, true, supporterInfo.Tooltip);
        }
        // Draw out the border for the icon.
        drawList.AddCircle(SupporterIconBorderPos + SupporterIconBorderSize / 2, SupporterIconBorderSize.X / 2,
            ImGui.GetColorU32(ImGuiColors.ParsedPink), 0, 4f);


        // draw out the UID here. We must make it centered. To do this, we must fist calculate how to center it.
        var widthToCenterOn = ProfilePictureBorderSize.X;
        using (_uiShared.UidFont.Push())
        {
            var aliasOrUidSize = ImGui.CalcTextSize(displayName);
            ImGui.SetCursorScreenPos(new Vector2(ProfilePictureBorderPos.X + widthToCenterOn / 2 - aliasOrUidSize.X / 2, ProfilePictureBorderPos.Y + ProfilePictureBorderSize.Y + 5));
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(ImGuiColors.ParsedPink, displayName);
        }
    }

    private void DrawDescription(ImDrawListPtr drawList, KinkPlate profile)
    {
        // draw out the description background.
        if (_cosmetics.TryGetBackground(ProfileComponent.DescriptionLight, profile.KinkPlateInfo.DescriptionBackground, out var descBG))
            KinkPlateUI.AddImageRounded(drawList, descBG, DescriptionBorderPos, DescriptionBorderSize, 2f);

        // description border
        if (_cosmetics.TryGetBorder(ProfileComponent.DescriptionLight, profile.KinkPlateInfo.DescriptionBorder, out var descBorder))
            KinkPlateUI.AddImageRounded(drawList, descBorder, DescriptionBorderPos, DescriptionBorderSize, 2f);

        // description overlay.
        if (_cosmetics.TryGetOverlay(ProfileComponent.DescriptionLight, profile.KinkPlateInfo.DescriptionOverlay, out var descOverlay))
            KinkPlateUI.AddImageRounded(drawList, descOverlay, DescriptionBorderPos, DescriptionBorderSize, 2f);

        // draw out the description text here.
        ImGui.SetCursorScreenPos(DescriptionBorderPos + new Vector2(12f, 8f));
        var description = profile.KinkPlateInfo.Description.IsNullOrEmpty() ? "No Description Was Set.." : profile.KinkPlateInfo.Description;
        var color = profile.KinkPlateInfo.Description.IsNullOrEmpty() ? ImGuiColors.DalamudGrey2 : ImGuiColors.DalamudWhite;
        DrawLimitedDescription(description, color, DescriptionBorderSize - new Vector2(15, 0));
    }

    private void DrawLimitedDescription(string desc, Vector4 color, Vector2 size)
    {
        // Get the basic line height.
        float lineHeight = ImGui.CalcTextSize("A").Y;
        int totalLines = (int)(size.Y / lineHeight) - 1; // Total lines to display based on height
        string newDescText = "";
        string[] words = desc.Split(' ');
        int currentLines = 0;

        while (newDescText.Length < desc.Length && currentLines < totalLines)
        {
            // Calculate how much of the message fits in the available space
            string fittingMessage = string.Empty;
            float currentWidth = 0;

            // Build the fitting message
            foreach (var word in words)
            {
                float wordWidth = ImGui.CalcTextSize(word + " ").X;

                // Check if adding this word exceeds the available width
                if (currentWidth + wordWidth > size.X)
                {
                    break; // Stop if it doesn't fit
                }

                fittingMessage += word + " ";
                currentWidth += wordWidth;
            }

            currentLines++;
            newDescText += fittingMessage.TrimEnd();

            // Only add newline if we're not on the last line
            if (currentLines < totalLines && newDescText.Length < desc.Length)
            {
                newDescText += "\n";
            }

            if (newDescText.Length < desc.Length)
            {
                words = desc.Substring(newDescText.Length).TrimStart().Split(' ');
            }
        }

        // Final check of truncated text before rendering
        _logger.LogDebug($"Truncated Description:\n {newDescText}");
        UiSharedService.ColorTextWrapped(newDescText, color);
    }

    private void DrawStats(ImDrawListPtr drawList, KinkPlateContent info, string displayName)
    {
        // jump down to where we should draw out the stats, and draw out the achievement icon.
        var statsPos = StatsPos;
        KinkPlateUI.AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Clock], statsPos, Vector2.One * 20, ImGuiColors.ParsedGold);
        // set the cursor screen pos to the right of the clock, and draw out the joined date.
        statsPos += new Vector2(24, 0);

        ImGui.SetCursorScreenPos(statsPos);
        UiSharedService.ColorText("MM-DD-YYYY", ImGuiColors.ParsedGold);
        var textWidth = ImGui.CalcTextSize($"MM-DD-YYYY").X;
        statsPos += new Vector2(textWidth + 4, 0);
        // to the right of this, draw out the achievement icon.
        KinkPlateUI.AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Achievement], statsPos, Vector2.One * 20, ImGuiColors.ParsedGold);
        // to the right of this, draw the players total earned achievements scoring.
        statsPos += new Vector2(24, 0);
        ImGui.SetCursorScreenPos(statsPos);
        UiSharedService.ColorText("100/141", ImGuiColors.ParsedGold);
        UiSharedService.AttachToolTip("The total achievements "+ displayName + " has earned.");
    }


    private void CloseButton(ImDrawListPtr drawList, string displayName, Action onClosed)
    {
        var btnPos = CloseButtonPos;
        var btnSize = CloseButtonSize;

        var closeButtonColor = HoveringCloseButton ? ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)) : ImGui.GetColorU32(ImGuiColors.ParsedPink);

        drawList.AddLine(btnPos, btnPos + btnSize, closeButtonColor, 3);
        drawList.AddLine(new Vector2(btnPos.X + btnSize.X, btnPos.Y), new Vector2(btnPos.X, btnPos.Y + btnSize.Y), closeButtonColor, 3);


        ImGui.SetCursorScreenPos(btnPos);
        if (ImGui.InvisibleButton($"CloseButton##KinkPlateClose" + displayName, btnSize))
        {
            onClosed.Invoke();
        }
        HoveringCloseButton = ImGui.IsItemHovered();
    }
}
