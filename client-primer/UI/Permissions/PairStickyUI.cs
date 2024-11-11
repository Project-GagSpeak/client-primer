using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.UI.Handlers;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Enums;
using GagspeakAPI.Extensions;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI.Permissions;

public partial class PairStickyUI : WindowMediatorSubscriberBase
{
    private readonly OnFrameworkService _frameworkUtils;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerManager;
    protected readonly IdDisplayHandler _displayHandler;
    private readonly UiSharedService _uiShared;
    private readonly MainHub _apiHubMain;
    private readonly PairManager _pairManager;
    private readonly MoodlesService _moodlesService;
    private readonly PermissionPresetService _presetService;
    private readonly PermActionsComponents _permActions;
    private readonly PiShockProvider _shockProvider;

    public PairStickyUI(ILogger<PairStickyUI> logger, GagspeakMediator mediator, Pair pairToDrawFor, 
        StickyWindowType drawType, OnFrameworkService frameworkUtils, ClientConfigurationManager clientConfigs,
        PlayerCharacterData pcManager, IdDisplayHandler displayHandler, UiSharedService uiSharedService, 
        MainHub apiHubMain, PairManager pairManager, MoodlesService moodlesService, PermissionPresetService presetService, 
        PermActionsComponents permActionHelpers) : base(logger, mediator, "PairStickyUI for " + pairToDrawFor.UserData.UID + "pair.")
    {
        _frameworkUtils = frameworkUtils;
        _clientConfigs = clientConfigs;
        _playerManager = pcManager;
        _uiShared = uiSharedService;
        _apiHubMain = apiHubMain;
        _pairManager = pairManager;
        _moodlesService = moodlesService;
        _displayHandler = displayHandler;
        _presetService = presetService;
        _permActions = permActionHelpers;

        StickyPair = pairToDrawFor; // set the pair we're drawing for
        DrawType = drawType; // set the type of window we're drawing

        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;

        IsOpen = true; // open the window
    }

    private CharaIPCData? LastCreatedCharacterData => _playerManager.LastIpcData;
    public Pair StickyPair { get; init; } // pair we're drawing the sticky permissions for.
    private UserGlobalPermissions PairGlobals => StickyPair.PairGlobals;
    private UserPairPermissions OwnPerms => StickyPair.OwnPerms;
    private UserPairPermissions PairPerms => StickyPair.PairPerms;

    public StickyWindowType DrawType = StickyWindowType.None; // type of window drawn.
    public float WindowMenuWidth { get; private set; } = -1; // width of the window menu.
    public float IconButtonTextWidth => WindowMenuWidth - ImGui.GetFrameHeightWithSpacing();
    public string PairNickOrAliasOrUID => StickyPair.GetNickname() ?? StickyPair.UserData.AliasOrUID;
    public string PairUID => StickyPair.UserData.UID;

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
            var presetListWidth = 225f;
            _uiShared.SetCursorXtoCenter(presetListWidth);
            _presetService.DrawPresetList(StickyPair, presetListWidth);

            ImGui.Separator();

            // create a new child below with no border that spans the rest of the content region and has no scrollbar
            ImGui.BeginChild("ClientPermsForPairContent", new Vector2(0, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.NoScrollbar);
            // draw clients permission edit access page
            DrawClientPermsForPair();

            ImGui.EndChild();
        }
        else if (DrawType == StickyWindowType.PairActionFunctions)
        {
            ImGuiUtil.Center("Actions For " + PairNickOrAliasOrUID);
            if(!_currentErrorMessage.NullOrEmpty())
               UiSharedService.ColorTextWrapped(_currentErrorMessage, ImGuiColors.DalamudRed);

            ImGui.Separator();

            ImGui.BeginChild("ActionsForPairContent", new Vector2(0, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.NoScrollbar);
            // draw the pair action functions
            DrawPairActionFunctions();

            ImGui.EndChild();
        }
        else if (DrawType == StickyWindowType.None)
        {
            // Occurs when draw type is set to None
        }
    }

    #region ErrorHandler
    private string _currentErrorMessage = string.Empty;
    private CancellationTokenSource? _errorCTS;
    private async Task DisplayError(string errorMessage)
    {
        // Cancel the previous error message display if it exists
        _errorCTS?.Cancel();
        _errorCTS = new CancellationTokenSource();

        _currentErrorMessage = errorMessage;

        try
        {
            // Wait for 5 seconds or until the task is cancelled
            await Task.Delay(5000, _errorCTS.Token)
                .ContinueWith(t =>
                {
                    // Clear the error message if the task was not cancelled
                    if (!t.IsCanceled)
                    {
                        _currentErrorMessage = string.Empty;
                    }
                }, TaskScheduler.Default);
        }
        catch (TaskCanceledException)
        {
            // Task was cancelled, do nothing
        }
    }

    #endregion ErrorHandler



    protected override void PostDrawInternal() { }

    public override void OnClose()
    {
        Mediator.Publish(new RemoveWindowMessage(this));
    }
}
