using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Achievements;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Dto.User;
using ImGuiNET;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Numerics;

namespace GagSpeak.UI.Profile;

public class KinkPlateEditorUI : WindowMediatorSubscriberBase
{
    private readonly MainHub _apiHubMain;
    private readonly FileDialogManager _fileDialogManager;
    private readonly KinkPlateService _KinkPlateManager;
    private readonly CosmeticService _cosmetics;
    private readonly UiSharedService _uiShared;
    public KinkPlateEditorUI(ILogger<KinkPlateEditorUI> logger, GagspeakMediator mediator,
        MainHub apiHubMain, FileDialogManager fileDialogManager,
        KinkPlateService KinkPlateManager, CosmeticService cosmetics,
        UiSharedService uiSharedService) : base(logger, mediator, "Edit Avatar###GagSpeakKinkPlateEditorUI")
    {
        Flags = ImGuiWindowFlags.NoScrollbar;
        IsOpen = false;
        Size = new(768, 512);
        _apiHubMain = apiHubMain;
        _fileDialogManager = fileDialogManager;
        _KinkPlateManager = KinkPlateManager;
        _cosmetics = cosmetics;
        _uiShared = uiSharedService;

        Mediator.Subscribe<MainHubDisconnectedMessage>(this, (_) => IsOpen = false);
    }

    private Vector2 RectMin = Vector2.Zero;
    private Vector2 RectMax = Vector2.Zero;

    protected override void PreDrawInternal() { }
    protected override void PostDrawInternal() { }

    protected override void DrawInternal()
    {
        var drawList = ImGui.GetWindowDrawList();
        RectMin = drawList.GetClipRectMin();
        RectMax = drawList.GetClipRectMax();
        var contentRegion = RectMax - RectMin;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        // grab our profile.
        var profile = _KinkPlateManager.GetKinkPlate(new UserData(MainHub.UID));

        // check if flagged
        if (profile.KinkPlateInfo.Flagged)
        {
            UiSharedService.ColorTextWrapped(profile.KinkPlateInfo.Description, ImGuiColors.DalamudRed);
            return;
        }
        var pos = new Vector2(ImGui.GetCursorScreenPos().X + contentRegion.X - 266, ImGui.GetCursorScreenPos().Y);
        _uiShared.GagspeakTitleText("KinkPlate Customization!");
        ImGui.SameLine();
        using (ImRaii.Group())
        {
            var width = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Expand, "Preview KinkPlate");
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileUpload, "Image Editor", width))
                Mediator.Publish(new UiToggleMessage(typeof(ProfilePictureEditor)));
            UiSharedService.AttachToolTip("Import and adjust a new profile picture to your liking!");

            if (_uiShared.IconTextButton(FontAwesomeIcon.Expand, "Preview KinkPlate", id: MainHub.UID + "KinkPlatePreview"))
                Mediator.Publish(new KinkPlateOpenStandaloneLightMessage(MainHub.PlayerUserData));
            UiSharedService.AttachToolTip("Preview your KinkPlate in a standalone window!");
        }

        // below this, we should draw out the description editor
        var refText = profile.KinkPlateInfo.Description.IsNullOrEmpty() ? "Description is Null" : profile.KinkPlateInfo.Description;
        var size = new Vector2(contentRegion.X - 286, 100f);
        ImGui.InputTextMultiline("##pfpDescription", ref refText, 1000, size);
        if (ImGui.IsItemDeactivatedAfterEdit())
            profile.KinkPlateInfo.Description = refText;

        var pfpWrap = profile.GetCurrentProfileOrDefault();
        if (pfpWrap != null)
        {
            var currentPosition = ImGui.GetCursorPos();
            drawList.AddImageRounded(pfpWrap.ImGuiHandle, pos, pos + Vector2.One*256f, Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 128f);
        }

        var publicRef = profile.KinkPlateInfo.PublicPlate;
        using (ImRaii.Group())
        {
            if (ImGui.Checkbox("Public Profile", ref publicRef))
                profile.KinkPlateInfo.PublicPlate = publicRef;
            UiSharedService.AttachToolTip("If checked, your profile picture and description will become visible\n" +
                "to others through private rooms and global chat!" +
                "--SEP--Non-Paired Kinksters still won't be able to see your UID if viewing your KinkPlate");

            List<(uint, string)> items = AchievementManager.CompletedAchievements;
            items.Insert(0, (0, "None"));

            ImGui.SameLine(0, 15f);

            _uiShared.DrawCombo("Displayed Title##ProfileSelectTitle", 200f, items,
                (achievement) => achievement.Item2,
                (i) => profile.KinkPlateInfo.ChosenTitleId = (int)i.Item1,
                initialSelectedItem: ((uint)profile.KinkPlateInfo.ChosenTitleId, AchievementManager.GetTitleById((uint)profile.KinkPlateInfo.ChosenTitleId)));
            UiSharedService.AttachToolTip("Select a title to display on your KinkPlate!--SEP--You will only be able to select Titles from achievements you have completed!");

            ImGui.SameLine(0, 15f);

            if (_uiShared.IconTextButton(FontAwesomeIcon.Save, "Save"))
                _ = _apiHubMain.UserSetKinkPlate(new UserKinkPlateDto(new UserData(MainHub.UID), profile.KinkPlateInfo, profile.Base64ProfilePicture));
            UiSharedService.AttachToolTip("Updated your stored profile with latest information");
        }

        ImGui.Spacing();
        using (ImRaii.Group())
        {
            using (ImRaii.Group())
            {
                // We should draw out all the selectable options for us.
                UiSharedService.ColorText("KinkPlate BG Style", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the background style for your KinkPlate!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                // grab all the items from the dictionary, who have a key that exists in our unlocks list.
                _uiShared.DrawCombo("##PlateBackgroundStyle", 150f, _cosmetics.UnlockedPlateBackgrounds, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.PlateBackground = i, profile.KinkPlateInfo.PlateBackground);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("KinkPlate Border", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the border style for your KinkPlate!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##PlateBorderStyle", 150f, _cosmetics.UnlockedPlateBorders, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.PlateBorder = i, profile.KinkPlateInfo.PlateBorder);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Blocked Slot Border", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the border style for your KinkPlate Blocked Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##BlockedSlotBorderStyle", 150f, _cosmetics.UnlockedBlockedSlotBorder, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.BlockedSlotBorder = i, profile.KinkPlateInfo.BlockedSlotBorder);
            }
        }
        // next row.
        ImGui.Spacing();
        using (ImRaii.Group())
        {
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Pfp Border", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the border style for your KinkPlate Profile Picture!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##ProfilePictureBorderStyle", 150f, _cosmetics.UnlockedProfilePictureBorder, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.ProfilePictureBorder = i, profile.KinkPlateInfo.ProfilePictureBorder);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Pfp Overlay", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Profile Picture!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##ProfilePictureOverlayStyle", 150f, _cosmetics.UnlockedProfilePictureOverlay, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.ProfilePictureOverlay = i, profile.KinkPlateInfo.ProfilePictureOverlay);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Blocked Slot Overlay", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Blocked Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##BlockedSlotOverlayStyle", 150f, _cosmetics.UnlockedBlockedSlotOverlay, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.BlockedSlotOverlay = i, profile.KinkPlateInfo.BlockedSlotOverlay);
            }
        }
        // next row.
        ImGui.Spacing();
        using (ImRaii.Group())
        {
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Description BG", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the background style for your KinkPlate Description!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##DescriptionBackgroundStyle", 150f, _cosmetics.UnlockedDescriptionBackground, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.DescriptionBackground = i, profile.KinkPlateInfo.DescriptionBackground);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Description Border", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the border style for your KinkPlate Description!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##DescriptionBorderStyle", 150f, _cosmetics.UnlockedDescriptionBorder, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.DescriptionBorder = i, profile.KinkPlateInfo.DescriptionBorder);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Description Overlay", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Description!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##DescriptionOverlayStyle", 150f, _cosmetics.UnlockedDescriptionOverlay, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.DescriptionOverlay = i, profile.KinkPlateInfo.DescriptionOverlay);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Blocked Slots BG", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the background style for your KinkPlate Blocked Slots!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##BlockedSlotsBackgroundStyle", 150f, _cosmetics.UnlockedBlockedSlotsBackground, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.BlockedSlotsBackground = i, profile.KinkPlateInfo.BlockedSlotsBackground);
            }
        }
        // next row.
        ImGui.Spacing();
        using (ImRaii.Group())
        {
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Gag Slot BG", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the background style for your KinkPlate Gag Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##GagSlotBackgroundStyle", 150f, _cosmetics.UnlockedGagSlotBackground, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.GagSlotBackground = i, profile.KinkPlateInfo.GagSlotBackground);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Gag Slot Border", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the border style for your KinkPlate Gag Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##GagSlotBorderStyle", 150f, _cosmetics.UnlockedGagSlotBorder, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.GagSlotBorder = i, profile.KinkPlateInfo.GagSlotBorder);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Gag Slot Overlay", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Gag Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##GagSlotOverlayStyle", 150f, _cosmetics.UnlockedGagSlotOverlay, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.GagSlotOverlay = i, profile.KinkPlateInfo.GagSlotOverlay);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Blocked Slots Border", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the border style for your KinkPlate Blocked Slots!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##BlockedSlotsBorderStyle", 150f, _cosmetics.UnlockedBlockedSlotsBorder, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.BlockedSlotsBorder = i, profile.KinkPlateInfo.BlockedSlotsBorder);
            }
        }
        // next row.
        ImGui.Spacing();
        using (ImRaii.Group())
        {
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Padlock BG", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the background style for your KinkPlate Padlock!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##PadlockBackgroundStyle", 150f, _cosmetics.UnlockedPadlockBackground, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.PadlockBackground = i, profile.KinkPlateInfo.PadlockBackground);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Padlock Border", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the border style for your KinkPlate Padlock!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##PadlockBorderStyle", 150f, _cosmetics.UnlockedPadlockBorder, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.PadlockBorder = i, profile.KinkPlateInfo.PadlockBorder);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Padlock Overlay", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Padlock!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##PadlockOverlayStyle", 150f, _cosmetics.UnlockedPadlockOverlay, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.PadlockOverlay = i, profile.KinkPlateInfo.PadlockOverlay);
            }
            ImGui.SameLine(0, 20f);
            using (ImRaii.Group())
            {
                UiSharedService.ColorText("Blocked Slots Overlay", ImGuiColors.ParsedGold);
                _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Blocked Slots!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
                _uiShared.DrawCombo("##BlockedSlotsOverlayStyle", 150f, _cosmetics.UnlockedBlockedSlotsOverlay, (style) => style.ToString(),
                    (i) => profile.KinkPlateInfo.BlockedSlotsOverlay = i, profile.KinkPlateInfo.BlockedSlotsOverlay);
            }
        }
    }
}
