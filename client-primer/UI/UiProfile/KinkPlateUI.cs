using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Profile;

/// <summary>
/// The UI Design for the KinkPlates.
/// </summary>
public class KinkPlateUI : WindowMediatorSubscriberBase
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

        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize;

        Size = new Vector2(750, 450);

        IsOpen = true;
    }

    public Pair Pair { get; init; } // The pair this profile is being drawn for.

    protected override void PreDrawInternal() { }
    
    protected override void PostDrawInternal() { }


    protected override void DrawInternal()
    {
        // get some data for the drawing we will need to do.
        var spacing = ImGui.GetStyle().ItemSpacing;
        var drawList = ImGui.GetWindowDrawList();
        var rectMin = drawList.GetClipRectMin();
        var rectMax = drawList.GetClipRectMax();

        // obtain the profile for this userPair.
        var gagspeakProfile = _profileService.GetGagspeakProfile(Pair.UserData);

        // if the profile is flagged, mark that it is flagged and return.
        if (gagspeakProfile.Flagged)
        {
            ImGui.TextUnformatted("This profile is flagged.");
            return;
        }

        // obtain the profile picture wrap for the character.
        var pfpWrap = gagspeakProfile.GetCurrentProfileOrDefault();
        try
        {
            // Draw the circular profile here (if the texture wrap is valid)
            if (pfpWrap is { } wrap)
            {
                var region = ImGui.GetContentRegionAvail();
                ImGui.Spacing();
                Vector2 imgSize = new Vector2(180f, 180f);
                // move the x position so that it centeres the image to the center of the window.
                _uiShared.SetCursorXtoCenter(imgSize.X);
                var currentPosition = ImGui.GetCursorPos();

                var pos = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddImageRounded(wrap.ImGuiHandle, pos, pos + imgSize, Vector2.Zero, Vector2.One,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 90f);
                ImGui.SetCursorPos(new Vector2(currentPosition.X, currentPosition.Y + imgSize.Y));

            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex}");
        }
    }

    private void DrawKinkPlateOverview(ImDrawList drawList)
    {

    }
    private void DrawKinkPlateDescription(ImDrawList drawList)
    {

    }
    private void DrawKinkPlateStats(ImDrawList drawList)
    {

    }
    private void DrawPfpImageTitleGroup(ImDrawList drawList)
    {

    }

    public override void OnClose()
    {
        Mediator.Publish(new RemoveWindowMessage(this));
    }
}
