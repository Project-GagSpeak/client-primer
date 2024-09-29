using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Achievements;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components;
using ImGuiNET;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI;

// this can easily become the "contact list" tab of the "main UI" window.
public class AchievementsUI : WindowMediatorSubscriberBase
{
    private readonly AchievementManager _achievementManager;
    private readonly AchievementTabsMenu _tabMenu;
    private readonly CosmeticService _cosmeticTextures;
    private readonly UiSharedService _uiShared;
    // for theme management
    public bool ThemePushed = false;

    public AchievementsUI(ILogger<AchievementsUI> logger, GagspeakMediator mediator,
        AchievementManager achievementManager, AchievementTabsMenu tabMenu,
        CosmeticService cosmeticTextures, UiSharedService uiShared)
        : base(logger, mediator, "###GagSpeakAchievementsUI")
    {
        _achievementManager = achievementManager;
        _tabMenu = tabMenu;
        _cosmeticTextures = cosmeticTextures;
        _uiShared = uiShared;

        AllowPinning = false;
        AllowClickthrough = false;

        WindowName = $"Achievements###GagSpeakAchievementsUI";

        Flags |= ImGuiWindowFlags.NoDocking;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(525, 400),
            MaximumSize = new Vector2(525, 2000)
        };
    }

    protected override void PreDrawInternal()
    {
        // no config option yet, so it will always be active. When one is added, append "&& !_configOption.useTheme" to the if statement.
        if (!ThemePushed)
        {
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.01f, 0.07f, 0.01f, 1f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0, 0.56f, 0.09f, 0.51f));

            ThemePushed = true;
        }
    }

    protected override void PostDrawInternal()
    {
        if (ThemePushed)
        {
            ImGui.PopStyleColor(2);
            ThemePushed = false;
        }
    }

    protected override void DrawInternal()
    {
        // get the width of the window content region we set earlier
        var contentRegion = UiSharedService.GetWindowContentRegionWidth();

        using (ImRaii.PushId("AchievementsHeader")) CenteredHeader();

        ImGui.Separator();
        // now we need to draw the tab bar below this.
        using (ImRaii.PushId("MainMenuTabBar")) _tabMenu.Draw();

        // Draw out the achievements in a child window we can scroll, but do not display the scroll bar.
        using (ImRaii.Child("AchievementsSection", new Vector2(UiSharedService.GetWindowContentRegionWidth(), 0), false, ImGuiWindowFlags.NoScrollbar))
        {

            // display content based on the tab selected
            switch (_tabMenu.TabSelection)
            {
                case AchievementTabsMenu.SelectedTab.Generic:
                    using (ImRaii.PushId("UnlocksComponentGeneric")) DrawAchievementList(AchievementModuleKind.Generic);
                    break;
                case AchievementTabsMenu.SelectedTab.Orders:
                    using (ImRaii.PushId("UnlocksComponentOrders")) DrawAchievementList(AchievementModuleKind.Orders);
                    break;
                case AchievementTabsMenu.SelectedTab.Gags:
                    using (ImRaii.PushId("UnlocksComponentGags")) DrawAchievementList(AchievementModuleKind.Gags);
                    break;
                case AchievementTabsMenu.SelectedTab.Wardrobe:
                    using (ImRaii.PushId("UnlocksComponentWardrobe")) DrawAchievementList(AchievementModuleKind.Wardrobe);
                    break;
                case AchievementTabsMenu.SelectedTab.Puppeteer:
                    using (ImRaii.PushId("UnlocksComponentPuppeteer")) DrawAchievementList(AchievementModuleKind.Puppeteer);
                    break;
                case AchievementTabsMenu.SelectedTab.Toybox:
                    using (ImRaii.PushId("UnlocksComponentToybox")) DrawAchievementList(AchievementModuleKind.Toybox);
                    break;
                case AchievementTabsMenu.SelectedTab.Hardcore:
                    using (ImRaii.PushId("UnlocksComponentHardcore")) DrawAchievementList(AchievementModuleKind.Hardcore);
                    break;
                case AchievementTabsMenu.SelectedTab.Remotes:
                    using (ImRaii.PushId("UnlocksComponentRemotes")) DrawAchievementList(AchievementModuleKind.Remotes);
                    break;
                case AchievementTabsMenu.SelectedTab.Secrets:
                    using (ImRaii.PushId("UnlocksComponentSecrets")) DrawAchievementList(AchievementModuleKind.Secrets);
                    break;
            }
        }
    }

    private void CenteredHeader()
    {

        var text = "GagSpeak Achievements (" + _achievementManager.CompletedAchievementsCount + "/" + _achievementManager.TotalAchievements + ")";
        using (_uiShared.UidFont.Push())
        {
            var uidTextSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - uidTextSize.X / 2);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().WindowPadding.Y / 2);
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(ImGuiColors.ParsedGold, text);
        }
    }

    private void DrawAchievementList(AchievementModuleKind type)
    {
        // We likely want to avoid pushing the style theme here if we are swapping the colors based on the state of an achievement.
        // If that is not the case. move them here.
        var unlocks = _achievementManager.GetComponent(type);
        if (unlocks.Total == 0)
        {
            ImGui.Text("No achievements Added to this list.");
            return;
        }

        // create a window for scrolling through the available achievements.
        using var achievementListChild = ImRaii.Child("##AchievementListings" + type.ToString(), ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.NoScrollbar);

        // draw the achievements in the first column.
        foreach (var achievement in unlocks.All)
            DrawAchievementProgressBox(achievement);
    }

    private static Vector2 AchievementIconSize = new(96, 96);

    private void DrawAchievementProgressBox(Achievement achievementItem)
    {
        // set up the style theme for the box.
        //using var windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(ImGui.GetStyle().WindowPadding.X, 3f));
        using var windowRounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 5f);
        //using var itemSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, 2f));
        using var borderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var borderColor = ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink);
        using var bgColor = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.25f, 0.2f, 0.2f, 0.4f));

        var imageTabWidth = AchievementIconSize.X + ImGui.GetStyle().ItemSpacing.X * 2;

        var size = new Vector2(ImGui.GetContentRegionAvail().X, AchievementIconSize.Y + ImGui.GetStyle().WindowPadding.Y * 2 + ImGui.GetStyle().CellPadding.Y * 2);
        using (ImRaii.Child("##Achievement-" + achievementItem.Title, size, true, ImGuiWindowFlags.ChildWindow))
        {
            try
            {
                // draw out a table that is 2 columns, and display the subsections in each column
                using (var table = ImRaii.Table("##AchievementTable" + achievementItem.Title, 2, ImGuiTableFlags.RowBg))
                {
                    if (!table) return;

                    ImGui.TableSetupColumn("##AchievementIcon", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("##AchievementText", ImGuiTableColumnFlags.WidthFixed, AchievementIconSize.X);

                    // draw the information about the achievement and its progress bar within the first section.
                    // maybe the progress bar could span the bottom if icon image size is too much of a concern idk.
                    ImGui.TableNextColumn();
                    using (ImRaii.Group())
                    {
                        var progress = achievementItem.CurrentProgress();
                        var icon = achievementItem.IsCompleted ? FontAwesomeIcon.Trophy : (progress != 0 ? FontAwesomeIcon.Stopwatch : FontAwesomeIcon.Trophy);
                        var color = achievementItem.IsCompleted ? ImGuiColors.ParsedGold : (progress != 0 ? ImGuiColors.DalamudGrey : ImGuiColors.DalamudGrey3);
                        var tooltip = achievementItem.IsCompleted ? "Achievement Completed!" : (progress != 0 ? "Achievement in Progress" : "Achievement Not Started");
                        ImGui.AlignTextToFramePadding();
                        _uiShared.IconText(icon, color);
                        UiSharedService.AttachToolTip(tooltip);

                        // beside it, draw out the achievement's Title in white text.
                        ImUtf8.SameLineInner();
                        ImGui.AlignTextToFramePadding();
                        using (ImRaii.PushFont(UiBuilder.MonoFont)) UiSharedService.ColorText(achievementItem.Title, ImGuiColors.ParsedGold);

                        // Split between the title and description
                        ImGui.Separator();

                        ImGui.AlignTextToFramePadding();
                        _uiShared.IconText(FontAwesomeIcon.InfoCircle, ImGuiColors.TankBlue);

                        ImUtf8.SameLineInner();
                        ImGui.AlignTextToFramePadding();
                        var descText = achievementItem.IsSecretAchievement ? "????" : achievementItem.Description;
                        UiSharedService.TextWrapped(descText);
                        if (achievementItem.IsSecretAchievement)
                        {
                            UiSharedService.AttachToolTip("Explore GagSpeak's Features or work together with others to uncover how you obtain this Achievement!)");
                        }
                    }
                    // underneath this, we should draw the current progress towards the goal.
                    DrawProgressForAchievement(achievementItem);

                    // draw the text in the second column.
                    ImGui.TableNextColumn();
                    // we should fetch the cached image from our texture cache service
                    var achievementCosmetic = _cosmeticTextures.InternalCosmeticCache["DummyTest"];
                    // Ensure its a valid texture wrap
                    if (!(achievementCosmetic is { } wrap))
                    {
                        _logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        try
                        {
                            ImGui.Image(wrap.ImGuiHandle, AchievementIconSize);

                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Failed to draw achievement icon.");
                        }
                    }
                } // End of Table

                /*                // underneath this, we should draw the current progress towards the goal.
                                DrawProgressForAchievement(achievementItem);*/
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to draw achievement progress box.");
            }

        } // End of Achievement Child Window
    }


    // Referenced draw-list structure for progress bar from DevUI Bar's and Mare's Progress bar.
    // https://github.com/Penumbra-Sync/client/blob/e35ed1b5297437cbcaa3dca5f5a089033c996020/MareSynchronos/UI/DownloadUi.cs#L138

    private const int Transparency = 100;
    private const int ProgressBarBorder = 1;
    private void DrawProgressForAchievement(Achievement achievement)
    {
        var region = ImGui.GetContentRegionAvail(); // content region
        var padding = ImGui.GetStyle().FramePadding; // padding

        // grab the current progress towards the milestone.
        var progress = achievement.CurrentProgress();

        // get the current milestone.
        var milestone = achievement.MilestoneGoal;

        // calculate the text size
        var textSize = ImGui.CalcTextSize(progress + " / " + milestone + " " + achievement.MeasurementUnit);

        // move the cursor screen pos to the bottom of the content region - the progress bar height.
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X + ImGuiHelpers.GlobalScale, ImGui.GetCursorScreenPos().Y + region.Y - ((int)textSize.Y + 5)));

        // grab the current cursor screen pos.
        var pos = ImGui.GetCursorScreenPos();

        // define the progress bar height and width for the windows drawlist.
        int progressHeight = (int)textSize.Y + 2;
        int progressWidth = (int)(region.X - padding.X);

        // mark the starting position of our progress bar in the drawlist.
        var progressBarDrawStart = pos;

        // mark the ending position of the progress bar in the drawlist.
        var progressBarDrawEnd = new Vector2(pos.X + progressWidth, pos.Y + progressHeight);

        // grab the WINDOW draw list
        var drawList = ImGui.GetWindowDrawList();

        // Parsed Pink == (225,104,168,255)


        drawList.AddRectFilled( // The Outer Border of the progress bar
            progressBarDrawStart with { X = progressBarDrawStart.X - ProgressBarBorder - 1, Y = progressBarDrawStart.Y - ProgressBarBorder - 1 },
            progressBarDrawEnd with { X = progressBarDrawEnd.X + ProgressBarBorder + 1, Y = progressBarDrawEnd.Y + ProgressBarBorder + 1 },
            UiSharedService.Color(0, 0, 0, Transparency),
            25f,
            ImDrawFlags.RoundCornersAll);

        drawList.AddRectFilled( // The inner Border of the progress bar
            progressBarDrawStart with { X = progressBarDrawStart.X - ProgressBarBorder, Y = progressBarDrawStart.Y - ProgressBarBorder },
            progressBarDrawEnd with { X = progressBarDrawEnd.X + ProgressBarBorder, Y = progressBarDrawEnd.Y + ProgressBarBorder },
            UiSharedService.Color(220, 220, 220, Transparency),
            25f,
            ImDrawFlags.RoundCornersAll);

        drawList.AddRectFilled( // The progress bar background
            progressBarDrawStart,
            progressBarDrawEnd,
            UiSharedService.Color(0, 0, 0, Transparency),
            25f,
            ImDrawFlags.RoundCornersAll);

        // Do not draw the progress bar fill if it is less than .02% of the progress bar width.
        if ((progress / milestone) >= 0.025)
        {
            drawList.AddRectFilled( // The progress bar fill
                progressBarDrawStart,
                progressBarDrawEnd with { X = progressBarDrawStart.X + (float)((progress / milestone) * progressWidth) },
                UiSharedService.Color(225, 104, 168, 255),
                45f,
                ImDrawFlags.RoundCornersAll);
        }

        var progressBarString = progress + " / " + milestone + " " + achievement.MeasurementUnit;

        UiSharedService.DrawOutlinedFont(
            drawList,
            progressBarString,
            pos with { X = pos.X + ((progressWidth - textSize.X) / 2f) - 1, Y = pos.Y + ((progressHeight - textSize.Y) / 2f) - 1 },
            UiSharedService.Color(255, 255, 255, 255),
            UiSharedService.Color(53, 24, 39, 255),
            1);
    }
}