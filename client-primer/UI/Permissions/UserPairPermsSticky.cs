using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Handlers;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Character;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI.Permissions;

public partial class UserPairPermsSticky : WindowMediatorSubscriberBase
{
    private readonly OnFrameworkService _frameworkUtils;
    private readonly GagspeakConfigService _mainConfig;
    private readonly PlayerCharacterManager _playerManager;
    protected readonly IdDisplayHandler _displayHandler;
    private readonly UiSharedService _uiShared;
    private readonly ApiController _apiController;
    private readonly PairManager _pairManager;
    private readonly MoodlesService _moodlesService;
    private readonly PermissionPresetService _presetService;
    private readonly PermActionsComponents _permActions;
    private readonly PiShockProvider _shockProvider;
    private readonly IClientState _clientState;

    public enum PermissionType { Global, UniquePairPerm, UniquePairPermEditAccess };

    public UserPairPermsSticky(ILogger<UserPairPermsSticky> logger,
        GagspeakMediator mediator, Pair pairToDrawFor, StickyWindowType drawType,
        OnFrameworkService frameworkUtils, GagspeakConfigService mainConfig,
        PlayerCharacterManager pcManager, IdDisplayHandler displayHandler, 
        UiSharedService uiSharedService, ApiController apiController, 
        PairManager pairManager, MoodlesService moodlesService, 
        PermissionPresetService presetService, PermActionsComponents permActionHelpers, 
        IClientState clientState) : base(logger, mediator, "StickyPairPerms for " + pairToDrawFor.UserData.UID + "pair.")
    {
        _frameworkUtils = frameworkUtils;
        _mainConfig = mainConfig;
        _playerManager = pcManager;
        _uiShared = uiSharedService;
        _apiController = apiController;
        _pairManager = pairManager;
        _moodlesService = moodlesService;
        _displayHandler = displayHandler;
        _presetService = presetService;
        _permActions = permActionHelpers;
        _clientState = clientState;

        UserPairForPerms = pairToDrawFor; // set the pair we're drawing for
        DrawType = drawType; // set the type of window we're drawing

        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;

        IsOpen = true; // open the window
    }

    private CharacterIPCData? LastCreatedCharacterData => _playerManager.LastIpcData;


    public override void OnClose() => Mediator.Publish(new RemoveWindowMessage(this)); // remove window on close.

    public Pair UserPairForPerms { get; init; } // pair we're drawing the sticky permissions for.
    public StickyWindowType DrawType = StickyWindowType.None; // type of window drawn.
    public float WindowMenuWidth { get; private set; } = -1; // width of the window menu.
    public float IconButtonTextWidth => WindowMenuWidth - ImGui.GetFrameHeightWithSpacing();
    public string PairNickOrAliasOrUID => UserPairForPerms.GetNickname() ?? UserPairForPerms.UserData.AliasOrUID;
    public string PairUID => UserPairForPerms.UserData.UID;
    public bool InteractionSuccessful { get; private set; } = true;// set to true every time an interaction is successfully made.
                                                                   // Will display a banner at the top for 3 seconds for user feedback.

    protected override void PreDrawInternal()
    {
        var position = _uiShared.LastMainUIWindowPosition;
        position.X += _uiShared.LastMainUIWindowSize.X;
        position.Y += ImGui.GetFrameHeightWithSpacing();
        ImGui.SetNextWindowPos(position);

        Flags |= ImGuiWindowFlags.NoMove;

        // calculate X size based on the tab we are viewing.     ClientPermsForPair & PairActionFunctions are 100*ImGuiHelpers.GlobalScale, PairPerms is 160*ImGuiHelpers.GlobalScale
        var width = (DrawType == StickyWindowType.PairPerms) ? 160 * ImGuiHelpers.GlobalScale : 110 * ImGuiHelpers.GlobalScale;

        var size = new Vector2(7 * ImGui.GetFrameHeight() + 3 * ImGui.GetStyle().ItemInnerSpacing.X + width,
            _uiShared.LastMainUIWindowSize.Y - ImGui.GetFrameHeightWithSpacing() * 2);

        ImGui.SetNextWindowSize(size);
    }

    protected override void DrawInternal()
    {
        // fetch the width
        WindowMenuWidth = ImGui.GetContentRegionAvail().X;

        // draw content based on who's it is.
        if (DrawType == StickyWindowType.PairPerms)
        {
            ImGuiUtil.Center(PairNickOrAliasOrUID + "'s Permissions for You");
            ImGui.Separator();

            // create a new child below with no border that spans the rest of the content region and has no scrollbar
            ImGui.BeginChild("PairPermsContent", new Vector2(0, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.NoScrollbar);
            // draw the pair's permissions they have set for you
            DrawPairPermsForClient();
                
            ImGui.EndChild();
        }
        else if (DrawType == StickyWindowType.ClientPermsForPair)
        {
            ImGuiUtil.Center("Your Permissions for " + PairNickOrAliasOrUID);
            // draw out the permission preset applied.
            var presetListWidth = 175f;
            _uiShared.SetCursorXtoCenter(presetListWidth);
            _presetService.DrawPresetList(UserPairForPerms, presetListWidth);

            ImGui.Separator();

            // create a new child below with no border that spans the rest of the content region and has no scrollbar
            ImGui.BeginChild("ClientPermsForPairContent", new Vector2(0, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.NoScrollbar);
            // draw clients permission edit access page
            DrawClientPermsForPair();

            ImGui.EndChild();
        }
        else if (DrawType == StickyWindowType.PairActionFunctions)
        {
            // draw the pair action functions
            DrawPairActionFunctions();
        }
        else if (DrawType == StickyWindowType.None)
        {
            // Occurs when draw type is set to None
        }
    }

    protected override void PostDrawInternal() { }
}
