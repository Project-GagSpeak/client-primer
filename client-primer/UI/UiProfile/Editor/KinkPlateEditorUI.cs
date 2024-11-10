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
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(740, 500),
            MaximumSize = new Vector2(740, 500),
        };
        Size = new(740, 500);

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

        var pos = new Vector2(ImGui.GetCursorScreenPos().X + contentRegion.X - 242, ImGui.GetCursorScreenPos().Y);
        _uiShared.GagspeakTitleText("KinkPlate Customization!");
        ImGui.SameLine();
        using (ImRaii.Group())
        {
            var width = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Expand, "Preview KinkPlate");
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileUpload, "Image Editor", width, disabled: profile.KinkPlateInfo.Disabled))
                Mediator.Publish(new UiToggleMessage(typeof(ProfilePictureEditor)));
            UiSharedService.AttachToolTip(profile.KinkPlateInfo.Disabled
                ? "You're Profile Customization Access has been Revoked!"
                : "Import and adjust a new profile picture to your liking!");

            if (_uiShared.IconTextButton(FontAwesomeIcon.Expand, "Preview KinkPlate", id: MainHub.UID + "KinkPlatePreview"))
                Mediator.Publish(new KinkPlateOpenStandaloneLightMessage(MainHub.PlayerUserData));
            UiSharedService.AttachToolTip("Preview your KinkPlate in a standalone window!");
        }

        // below this, we should draw out the description editor
        using (ImRaii.Disabled(profile.KinkPlateInfo.Disabled))
        {
            var refText = profile.KinkPlateInfo.Description.IsNullOrEmpty() ? "No Description Set..." : profile.KinkPlateInfo.Description;
            var size = new Vector2(contentRegion.X - 262, 100f);
            ImGui.InputTextMultiline("##pfpDescription", ref refText, 1000, size);
            if (ImGui.IsItemDeactivatedAfterEdit())
                profile.KinkPlateInfo.Description = refText;
        }
        if(profile.KinkPlateInfo.Disabled)
            UiSharedService.AttachToolTip("You're Profile Customization Access has been Revoked!" +
                "--SEP--You will not be able to edit your KinkPlate Description!");

        var pfpWrap = profile.GetCurrentProfileOrDefault();
        if (pfpWrap != null)
        {
            var currentPosition = ImGui.GetCursorPos();
            drawList.AddImageRounded(pfpWrap.ImGuiHandle, pos, pos + Vector2.One*232f, Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 116f);
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
        var cursorPos = ImGui.GetCursorPos();
        using (ImRaii.Group())
        {
            _uiShared.DrawCombo("Plate Background##PlateBackgroundStyle", 150f, _cosmetics.UnlockedPlateBackgrounds, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.PlateBackground = i, profile.KinkPlateInfo.PlateBackground);
            _uiShared.DrawHelpText("Select the background style for your KinkPlate!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Plate Border##PlateBorderStyle", 150f, _cosmetics.UnlockedPlateBorders, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.PlateBorder = i, profile.KinkPlateInfo.PlateBorder);
            _uiShared.DrawHelpText("Select the border style for your KinkPlate!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Avatar Border##ProfilePictureBorderStyle", 150f, _cosmetics.UnlockedProfilePictureBorder, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.ProfilePictureBorder = i, profile.KinkPlateInfo.ProfilePictureBorder);
            _uiShared.DrawHelpText("Select the border style for your KinkPlate Profile Picture!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Avatar Overlay##ProfilePictureOverlayStyle", 150f, _cosmetics.UnlockedProfilePictureOverlay, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.ProfilePictureOverlay = i, profile.KinkPlateInfo.ProfilePictureOverlay);
            _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Profile Picture!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Description Background##DescriptionBackgroundStyle", 150f, _cosmetics.UnlockedDescriptionBackground, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.DescriptionBackground = i, profile.KinkPlateInfo.DescriptionBackground);
            _uiShared.DrawHelpText("Select the background style for your KinkPlate Description!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Description Border##DescriptionBorderStyle", 150f, _cosmetics.UnlockedDescriptionBorder, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.DescriptionBorder = i, profile.KinkPlateInfo.DescriptionBorder);
            _uiShared.DrawHelpText("Select the border style for your KinkPlate Description!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Description Overlay##DescriptionOverlayStyle", 150f, _cosmetics.UnlockedDescriptionOverlay, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.DescriptionOverlay = i, profile.KinkPlateInfo.DescriptionOverlay);
            _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Description!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Gag Slots Background##GagSlotBackgroundStyle", 150f, _cosmetics.UnlockedGagSlotBackground, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.GagSlotBackground = i, profile.KinkPlateInfo.GagSlotBackground);
            _uiShared.DrawHelpText("Select the background style for your KinkPlate Gag Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Gag Slots Border##GagSlotBorderStyle", 150f, _cosmetics.UnlockedGagSlotBorder, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.GagSlotBorder = i, profile.KinkPlateInfo.GagSlotBorder);
            _uiShared.DrawHelpText("Select the border style for your KinkPlate Gag Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Gag Slots Overlay##GagSlotOverlayStyle", 150f, _cosmetics.UnlockedGagSlotOverlay, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.GagSlotOverlay = i, profile.KinkPlateInfo.GagSlotOverlay);
            _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Gag Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");
        }

        ImGui.SetCursorPos(new Vector2(cursorPos.X + 350f, cursorPos.Y + ImGui.GetFrameHeightWithSpacing() * 2));

        using (ImRaii.Group())
        {
            _uiShared.DrawCombo("Padlock Slots Background##PadlockBackgroundStyle", 150f, _cosmetics.UnlockedPadlockBackground, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.PadlockBackground = i, profile.KinkPlateInfo.PadlockBackground);
            _uiShared.DrawHelpText("Select the background style for your KinkPlate Padlock!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Padlock Slots Border##PadlockBorderStyle", 150f, _cosmetics.UnlockedPadlockBorder, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.PadlockBorder = i, profile.KinkPlateInfo.PadlockBorder);
            _uiShared.DrawHelpText("Select the border style for your KinkPlate Padlock!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Padlock Slots Overlay##PadlockOverlayStyle", 150f, _cosmetics.UnlockedPadlockOverlay, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.PadlockOverlay = i, profile.KinkPlateInfo.PadlockOverlay);
            _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Padlock!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Blocked Slots Background##BlockedSlotBackgroundStyle", 150f, _cosmetics.UnlockedBlockedSlotsBackground, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.BlockedSlotsBackground = i, profile.KinkPlateInfo.BlockedSlotsBackground);
            _uiShared.DrawHelpText("Select the background style for your KinkPlate Blocked Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Blocked Slots Border##BlockedSlotsBorderStyle", 150f, _cosmetics.UnlockedBlockedSlotsBorder, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.BlockedSlotsBorder = i, profile.KinkPlateInfo.BlockedSlotsBorder);
            _uiShared.DrawHelpText("Select the border style for your KinkPlate Blocked Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Blocked Slots Overlay##BlockedSlotsOverlayStyle", 150f, _cosmetics.UnlockedBlockedSlotsOverlay, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.BlockedSlotsOverlay = i, profile.KinkPlateInfo.BlockedSlotsOverlay);
            _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Blocked Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Blocked Slot Border##BlockedSlotBorderStyle", 150f, _cosmetics.UnlockedBlockedSlotBorder, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.BlockedSlotBorder = i, profile.KinkPlateInfo.BlockedSlotBorder);
            _uiShared.DrawHelpText("Select the border style for your KinkPlate Blocked Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

            _uiShared.DrawCombo("Blocked Slot Overlay##BlockedSlotOverlayStyle", 150f, _cosmetics.UnlockedBlockedSlotOverlay, (style) => style.ToString(),
                (i) => profile.KinkPlateInfo.BlockedSlotOverlay = i, profile.KinkPlateInfo.BlockedSlotOverlay);
            _uiShared.DrawHelpText("Select the overlay style for your KinkPlate Blocked Slot!--SEP--You will only be able to see cosmetics you've unlocked from Achievements!");

        }
    }
}
