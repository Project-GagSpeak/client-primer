using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using System.Numerics;
using static Penumbra.GameData.Data.GamePaths.Monster;

namespace GagSpeak.UI.Profile;

/// <summary>
/// The UI Design for the KinkPlates.
/// </summary>
public partial class KinkPlateUI : WindowMediatorSubscriberBase
{
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly ProfileService _profileService;
    private readonly UiSharedService _uiShared;

    public KinkPlateUI(ILogger<KinkPlateUI> logger, GagspeakMediator mediator,
        PairManager pairManager, ServerConfigurationManager serverConfigs,
        ProfileService profileService, UiSharedService uiShared,
        Pair pair) : base(logger, mediator, pair.UserData.AliasOrUID + "'s KinkPlate##GagspeakKinkPlateUI" + pair.UserData.AliasOrUID)
    {
        _pairManager = pairManager;
        _serverConfigs = serverConfigs;
        _profileService = profileService;
        _uiShared = uiShared;
        Pair = pair;

        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar;
        Size = new Vector2(750, 450);
        IsOpen = true;
    }

    private bool HoveringCloseButton { get; set; } = false;
    public Pair Pair { get; init; } // The pair this profile is being drawn for.

    protected override void PreDrawInternal() { }
    
    protected override void PostDrawInternal() { }

    protected override void DrawInternal()
    {
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 5f);
        var drawList = ImGui.GetWindowDrawList();
        RectMin = drawList.GetClipRectMin();
        RectMax = drawList.GetClipRectMax();
        //_logger.LogDebug("RectMin: {rectMin}, RectMax: {rectMax}", rectMin, rectMax);

        // obtain the profile for this userPair.
        var gagspeakProfile = _profileService.GetGagspeakProfile(Pair.UserData);
        if (gagspeakProfile.Flagged)
        {
            ImGui.TextUnformatted("This profile is flagged by moderation.");
            return;
        }

        // Draw KinkPlateUI Function here.
        DrawKinkPlateWhole(drawList, gagspeakProfile);
    }

    // Size = 750 by 450
    private void DrawKinkPlateWhole(ImDrawListPtr drawList, GagspeakProfile profile)
    {
        // Draw the close button.
        CloseButton(drawList);

        // Draw the profile Picture
        var pfpWrap = profile.GetCurrentProfileOrDefault();
        AddImageRounded(drawList, pfpWrap, ProfilePicturePos, ProfilePictureSize, ProfilePictureSize.Y/2);
        // and its border.
        drawList.AddCircle(ProfilePictureBorderPos + ProfilePictureBorderSize / 2, 
            ProfilePictureBorderSize.X / 2, 
            ImGui.GetColorU32(ImGuiColors.ParsedPink), 
            0, 4f); // 8 from end to end, marking 4 if radius.

        // Draw out Supporter Icon Black BG base.
        drawList.AddCircleFilled(SupporterIconBorderPos + SupporterIconBorderSize / 2, 
            SupporterIconBorderSize.X / 2, 
            ImGui.GetColorU32(new Vector4(0,0,0,1)));
        // Draw out Supporter Icon.
        // TODO;

        // Draw out the border for the icon.
        drawList.AddCircle(SupporterIconBorderPos + SupporterIconBorderSize / 2,
            SupporterIconBorderSize.X / 2,
            ImGui.GetColorU32(ImGuiColors.ParsedPink),
            0,
            3f);




        // Draw the profile picture title group.
        DrawPfpImageTitleGroup(drawList);
        DrawKinkPlateOverview(drawList);
        DrawKinkPlateDescription(drawList);
        DrawKinkPlateStats(drawList);

    }

    private void DrawKinkPlateOverview(ImDrawListPtr drawList)
    {


    }

    private void DrawKinkPlateDescription(ImDrawListPtr drawList)
    {

    }
    private void DrawKinkPlateStats(ImDrawListPtr drawList)
    {

    }
    private void DrawPfpImageTitleGroup(ImDrawListPtr drawList)
    {

    }

    public override void OnClose()
    {
        Mediator.Publish(new RemoveWindowMessage(this));
    }
}
