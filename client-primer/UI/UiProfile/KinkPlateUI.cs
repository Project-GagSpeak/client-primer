using Dalamud.Interface.Colors;
using Dalamud.Utility;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagspeakAPI.Data.IPC;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Profile;

/// <summary>
/// The UI Design for the KinkPlates.
/// </summary>
public partial class KinkPlateUI : WindowMediatorSubscriberBase
{
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly KinkPlateService _profileService;
    private readonly CosmeticService _cosmetics;
    private readonly UiSharedService _uiShared;

    private bool ThemePushed = false;

    public KinkPlateUI(ILogger<KinkPlateUI> logger, GagspeakMediator mediator,
        PairManager pairManager, ServerConfigurationManager serverConfigs,
        KinkPlateService profileService, CosmeticService cosmetics,
        UiSharedService uiShared, Pair pair)
        : base(logger, mediator, pair.UserData.AliasOrUID + "'s KinkPlate##GagspeakKinkPlateUI" + pair.UserData.AliasOrUID)
    {
        _pairManager = pairManager;
        _serverConfigs = serverConfigs;
        _profileService = profileService;
        _cosmetics = cosmetics;
        _uiShared = uiShared;
        Pair = pair;

        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        Size = new Vector2(750, 450);
        IsOpen = true;
    }

    private bool HoveringCloseButton { get; set; } = false;
    public Pair Pair { get; init; } // The pair this profile is being drawn for.

    protected override void PreDrawInternal()
    {
        if (!ThemePushed)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 25f);

            ThemePushed = true;
        }
    }
    protected override void PostDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
        if (ThemePushed)
        {
            ImGui.PopStyleVar(2);
            ThemePushed = false;
        }
    }

    protected override void DrawInternal()
    {
        var drawList = ImGui.GetWindowDrawList();
        RectMin = drawList.GetClipRectMin();
        RectMax = drawList.GetClipRectMax();
        //_logger.LogDebug("RectMin: {rectMin}, RectMax: {rectMax}", rectMin, rectMax);

        // obtain the profile for this userPair.
        var KinkPlate = _profileService.GetKinkPlate(Pair.UserData);
        if (KinkPlate.KinkPlateInfo.Flagged)
        {
            ImGui.TextUnformatted("This profile is flagged by moderation.");
            return;
        }

        // Draw KinkPlateUI Function here.
        DrawKinkPlateWhole(drawList, KinkPlate);
    }

    // Size = 750 by 450
    private void DrawKinkPlateWhole(ImDrawListPtr drawList, KinkPlate profile)
    {
        var info = profile.KinkPlateInfo;
        // draw out the background for the window.
        if (_cosmetics.TryGetBackground(ProfileComponent.Plate, info.PlateBackground, out var plateBG))
            AddImageRounded(drawList, plateBG, RectMin, PlateSize, 25f);

        // draw out the border ontop of that.
        if (_cosmetics.TryGetBorder(ProfileComponent.Plate, info.PlateBorder, out var plateBorder))
            AddImageRounded(drawList, plateBorder, RectMin, PlateSize, 20f);

        // Draw the close button.
        CloseButton(drawList);

        // Draw the profile Picture
        var pfpWrap = profile.GetCurrentProfileOrDefault();
        AddImageRounded(drawList, pfpWrap, ProfilePicturePos, ProfilePictureSize, ProfilePictureSize.Y / 2);

        // draw out the border for the profile picture
        if (_cosmetics.TryGetBorder(ProfileComponent.ProfilePicture, info.ProfilePictureBorder, out var pfpBorder))
            AddImageRounded(drawList, pfpBorder, ProfilePictureBorderPos, ProfilePictureBorderSize, ProfilePictureSize.Y / 2);

        // Draw out Supporter Icon Black BG base.
        drawList.AddCircleFilled(SupporterIconBorderPos + SupporterIconBorderSize / 2,
            SupporterIconBorderSize.X / 2, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));

        // Draw out Supporter Icon.
        var supporterInfo = Pair.GetSupporterInfo();
        if (supporterInfo.SupporterWrap is { } wrap)
        {
            AddImageRounded(drawList, wrap, SupporterIconPos, SupporterIconSize, SupporterIconSize.Y / 2);
            //UiSharedService.AttachToolTip(supporterInfo.Tooltip); TODO: add tooltips later using scaled dummy objects.
        }

        // Draw out the border for the icon.
        drawList.AddCircle(SupporterIconBorderPos + SupporterIconBorderSize / 2, SupporterIconBorderSize.X / 2,
            ImGui.GetColorU32(ImGuiColors.ParsedPink), 0, 4f);

        // draw out the UID here. We must make it centered. To do this, we must fist calculate how to center it.
        var widthToCenterOn = ProfilePictureBorderSize.X;
        // determine the height gap between the icon overview and bottom of the profile picture.
        var gapHeight = IconOverviewListPos.Y - (ProfilePictureBorderPos.Y + ProfilePictureBorderSize.Y);
        using (_uiShared.UidFont.Push())
        {
            var aliasOrUidSize = ImGui.CalcTextSize(Pair.UserData.AliasOrUID);
            // calculate the Y height it should be drawn on by taking the gap height and dividing it by 2 and subtracting the text height.
            var yHeight = (gapHeight - aliasOrUidSize.Y) / 2;

            ImGui.SetCursorScreenPos(new Vector2(ProfilePictureBorderPos.X + widthToCenterOn / 2 - aliasOrUidSize.X / 2, ProfilePictureBorderPos.Y + ProfilePictureBorderSize.Y + yHeight));
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(ImGuiColors.ParsedPink, Pair.UserData.AliasOrUID);
        }

        int iconWidthPlusSpacing = 38;
        var iconOverviewPos = IconOverviewListPos;
        // draw out the icon row.
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Vibrator], iconOverviewPos, Vector2.One * 34, ImGuiColors.DalamudGrey3);
        iconOverviewPos.X += iconWidthPlusSpacing;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.ShockCollar], iconOverviewPos, Vector2.One * 34, ImGuiColors.DalamudGrey3);
        iconOverviewPos.X += iconWidthPlusSpacing;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Leash], iconOverviewPos, Vector2.One * 34, ImGuiColors.DalamudGrey3);
        iconOverviewPos.X += iconWidthPlusSpacing;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.ForcedEmote], iconOverviewPos, Vector2.One * 34, ImGuiColors.DalamudGrey3);
        iconOverviewPos.X += iconWidthPlusSpacing;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.ForcedStay], iconOverviewPos, Vector2.One * 34, ImGuiColors.DalamudGrey3);
        iconOverviewPos.X += iconWidthPlusSpacing;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.ChatBlocked], iconOverviewPos, Vector2.One * 34, ImGuiColors.DalamudGrey3);


        // draw out the description background.
        if (_cosmetics.TryGetBackground(ProfileComponent.Description, info.DescriptionBackground, out var descBG))
            AddImageRounded(drawList, descBG, DescriptionBorderPos, DescriptionBorderSize, 2f);

        // description border
        if (_cosmetics.TryGetBorder(ProfileComponent.Description, info.DescriptionBorder, out var descBorder))
            AddImageRounded(drawList, descBorder, DescriptionBorderPos, DescriptionBorderSize, 2f);

        // description overlay.
        if (_cosmetics.TryGetOverlay(ProfileComponent.Description, info.DescriptionOverlay, out var descOverlay))
            AddImageRounded(drawList, descOverlay, DescriptionBorderPos, DescriptionBorderSize, 2f);

        // draw out the description text here.
        ImGui.SetCursorScreenPos(DescriptionBorderPos + Vector2.One * 10f);
        var description = info.Description.IsNullOrEmpty() ? "No Description Was Set.." : info.Description;
        var color = info.Description.IsNullOrEmpty() ? ImGuiColors.DalamudGrey2 : ImGuiColors.DalamudWhite;
        UiSharedService.ColorTextWrapped(description, color);

        // Now let's draw out the chosen achievement Name..
        using (_uiShared.GagspeakTitleFont.Push())
        {
            var titleHeightGap = TitleLineStartPos.Y - (RectMin.Y + 4f);
            var chosenTitleSize = ImGui.CalcTextSize("Sample Title Chosen");
            // calculate the Y height it should be drawn on by taking the gap height and dividing it by 2 and subtracting the text height.
            var yHeight = (titleHeightGap - chosenTitleSize.Y) / 2;

            ImGui.SetCursorScreenPos(new Vector2(TitleLineStartPos.X + TitleLineSize.X / 2 - chosenTitleSize.X / 2, TitleLineStartPos.Y - chosenTitleSize.Y - yHeight));
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(ImGuiColors.ParsedGold, "Sample Title Chosen");
        }
        // move over to the top area to draw out the achievement title line wrap.
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.AchievementLineSplit], TitleLineStartPos, TitleLineSize);

        // Draw out the background for the gag layer one item.
        if (_cosmetics.TryGetBackground(ProfileComponent.GagSlot, info.GagSlotBackground, out var gagSlotBG))
        {
            AddImageRounded(drawList, gagSlotBG, GagSlotOneBorderPos, GagSlotBorderSize, 10f);
            AddImageRounded(drawList, gagSlotBG, GagSlotTwoBorderPos, GagSlotBorderSize, 10f);
            AddImageRounded(drawList, gagSlotBG, GagSlotThreeBorderPos, GagSlotBorderSize, 10f);
        }
        else
        {
            drawList.AddRectFilled(GagSlotOneBorderPos, GagSlotOneBorderPos + GagSlotBorderSize, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)), 15f);
            drawList.AddRectFilled(GagSlotTwoBorderPos, GagSlotTwoBorderPos + GagSlotBorderSize, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)), 15f);
            drawList.AddRectFilled(GagSlotThreeBorderPos, GagSlotThreeBorderPos + GagSlotBorderSize, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)), 15f);
        }

        // draw out the borders.
        if (_cosmetics.TryGetBorder(ProfileComponent.GagSlot, info.GagSlotBorder, out var gagSlotBorder))
        {
            AddImageRounded(drawList, gagSlotBorder, GagSlotOneBorderPos, GagSlotBorderSize, 10f);
            AddImageRounded(drawList, gagSlotBorder, GagSlotTwoBorderPos, GagSlotBorderSize, 10f);
            AddImageRounded(drawList, gagSlotBorder, GagSlotThreeBorderPos, GagSlotBorderSize, 10f);
        }

        // draw out the overlays.
        if (_cosmetics.TryGetOverlay(ProfileComponent.GagSlot, info.GagSlotOverlay, out var gagSlotOverlay))
        {
            AddImageRounded(drawList, gagSlotOverlay, GagSlotOneBorderPos, GagSlotBorderSize, 10f);
            AddImageRounded(drawList, gagSlotOverlay, GagSlotTwoBorderPos, GagSlotBorderSize, 10f);
            AddImageRounded(drawList, gagSlotOverlay, GagSlotThreeBorderPos, GagSlotBorderSize, 10f);
        }

        // draw out the padlock backgrounds.
        if (_cosmetics.TryGetBackground(ProfileComponent.Padlock, info.PadlockBackground, out var padlockBG))
        {
            AddImageRounded(drawList, padlockBG, GagLockOneBorderPos, GagLockBorderSize, 10f);
            AddImageRounded(drawList, padlockBG, GagLockTwoBorderPos, GagLockBorderSize, 10f);
            AddImageRounded(drawList, padlockBG, GagLockThreeBorderPos, GagLockBorderSize, 10f);
        }
        else
        {
            drawList.AddCircleFilled(GagLockOneBorderPos + GagLockBorderSize / 2, GagLockBorderSize.X / 2, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));
            drawList.AddCircleFilled(GagLockTwoBorderPos + GagLockBorderSize / 2, GagLockBorderSize.X / 2, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));
            drawList.AddCircleFilled(GagLockThreeBorderPos + GagLockBorderSize / 2, GagLockBorderSize.X / 2, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));
        }

        // draw out the padlock borders.
        if (_cosmetics.TryGetBorder(ProfileComponent.Padlock, info.PadlockBorder, out var padlockBorder))
        {
            AddImageRounded(drawList, padlockBorder, GagLockOneBorderPos, GagLockBorderSize, 10f);
            AddImageRounded(drawList, padlockBorder, GagLockTwoBorderPos, GagLockBorderSize, 10f);
            AddImageRounded(drawList, padlockBorder, GagLockThreeBorderPos, GagLockBorderSize, 10f);
        }

        // draw out the padlock overlays.
        if (_cosmetics.TryGetOverlay(ProfileComponent.Padlock, info.PadlockOverlay, out var padlockOverlay))
        {
            AddImageRounded(drawList, padlockOverlay, GagLockOneBorderPos, GagLockBorderSize, 10f);
            AddImageRounded(drawList, padlockOverlay, GagLockTwoBorderPos, GagLockBorderSize, 10f);
            AddImageRounded(drawList, padlockOverlay, GagLockThreeBorderPos, GagLockBorderSize, 10f);
        }

        // jump down to where we should draw out the stats, and draw out the achievement icon.
        var statsPos = StatsPos;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Clock], statsPos, Vector2.One * 20, ImGuiColors.ParsedGold);
        // set the cursor screen pos to the right of the clock, and draw out the joined date.
        statsPos += new Vector2(24, 0);

        ImGui.SetCursorScreenPos(statsPos);
        UiSharedService.ColorText("MM-DD-YYYY", ImGuiColors.ParsedGold);
        var textWidth = ImGui.CalcTextSize($"MM-DD-YYYY").X;

        statsPos += new Vector2(textWidth + 4, 0);

        // to the right of this, draw out the achievement icon.
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Achievement], statsPos, Vector2.One * 20, ImGuiColors.ParsedGold);
        // to the right of this, draw the players total earned achievements scoring.
        statsPos += new Vector2(24, 0);
        ImGui.SetCursorScreenPos(statsPos);
        UiSharedService.ColorText("100/141", ImGuiColors.ParsedGold);


        // Now we must draw out the restrained slots section.
        // draw out the background for the window.
        if (_cosmetics.TryGetBackground(ProfileComponent.BlockedSlots, info.BlockedSlotsBackground, out var lockedSlotsPanelBG))
            AddImageRounded(drawList, lockedSlotsPanelBG, LockedSlotsPanelBorderPos, LockedSlotsPanelBorderSize, 10f);

        // draw out the border ontop of that.
        if (_cosmetics.TryGetBorder(ProfileComponent.BlockedSlots, info.BlockedSlotsBorder, out var lockedSlotsPanelBorder))
            AddImageRounded(drawList, lockedSlotsPanelBorder, LockedSlotsPanelBorderPos, LockedSlotsPanelBorderSize, 10f);

        // draw out the overlay ontop of that.
        if (_cosmetics.TryGetOverlay(ProfileComponent.BlockedSlots, info.BlockedSlotsOverlay, out var lockedSlotsPanelOverlay))
            AddImageRounded(drawList, lockedSlotsPanelOverlay, LockedSlotsPanelBorderPos, LockedSlotsPanelBorderSize, 10f);

        // draw out the blocked causes icon row.
        var blockedAffecterPos = LockAffectersRowPos;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Restrained], blockedAffecterPos, LockAffecterIconSize, ImGuiColors.DalamudGrey3);
        blockedAffecterPos.X += LockAffecterIconSize.X + LockAffecterSpacing.X;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.CursedLoot], blockedAffecterPos, LockAffecterIconSize, ImGuiColors.DalamudGrey3);
        blockedAffecterPos.X += LockAffecterIconSize.X + 11f;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Blindfolded], blockedAffecterPos, LockAffecterIconSize, ImGuiColors.DalamudGrey3);


        // draw out the background for the head slot.
        if (_cosmetics.TryGetBorder(ProfileComponent.BlockedSlot, info.BlockedSlotBorder, out var blockedSlotBG))
        {
            // obtain the start position, then start drawing all of the backgrounds at once.
            var blockedSlotBorderPos = LockedSlotsGroupPos;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);

            blockedSlotBorderPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);

            blockedSlotBorderPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);

            blockedSlotBorderPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);

            blockedSlotBorderPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotBorderPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);
        }

        // draw out the background for the head slot.
        if (_cosmetics.TryGetOverlay(ProfileComponent.BlockedSlot, info.BlockedSlotOverlay, out var blockedSlotOverlay))
        {
            // obtain the start position, then start drawing all of the backgrounds at once.
            var blockedSlotOverlayPos = LockedSlotsGroupPos;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);
            // scooch down and repeat.

            blockedSlotOverlayPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);

            blockedSlotOverlayPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);

            blockedSlotOverlayPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);

            blockedSlotOverlayPos.Y += LockedSlotSize.Y + LockedSlotSpacing.Y;
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos, LockedSlotSize, 10f);
            AddImageRounded(drawList, blockedSlotBG, blockedSlotOverlayPos + new Vector2(LockedSlotSize.X + LockedSlotSpacing.X, 0), LockedSlotSize, 10f);
        }

        // draw the icon list underneath that displays the hardcore traits and shit
        var hardcoreTraitsPos = HardcoreTraitsRowPos;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.RestrainedArmsLegs], hardcoreTraitsPos, HardcoreTraitIconSize, ImGuiColors.DalamudGrey3);
        hardcoreTraitsPos.X += HardcoreTraitIconSize.X + HardcoreTraitSpacing.X;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Gagged], hardcoreTraitsPos, HardcoreTraitIconSize, ImGuiColors.DalamudGrey3);
        hardcoreTraitsPos.X += HardcoreTraitIconSize.X + HardcoreTraitSpacing.X;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.SightLoss], hardcoreTraitsPos, HardcoreTraitIconSize, ImGuiColors.DalamudGrey3);
        hardcoreTraitsPos.X += HardcoreTraitIconSize.X + HardcoreTraitSpacing.X;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Weighty], hardcoreTraitsPos, HardcoreTraitIconSize, ImGuiColors.DalamudGrey3);
        hardcoreTraitsPos.X += HardcoreTraitIconSize.X + HardcoreTraitSpacing.X;
        AddImage(drawList, _cosmetics.CorePluginTextures[CorePluginTexture.Stimulated], hardcoreTraitsPos, HardcoreTraitIconSize, ImGuiColors.DalamudGrey3);



    }

    public override void OnClose()
    {
        Mediator.Publish(new RemoveWindowMessage(this));
    }
}
