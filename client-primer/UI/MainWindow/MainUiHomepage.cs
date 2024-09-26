using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.GoldSaucer;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.UiGagSetup;
using GagSpeak.UI.UiOrders;
using GagSpeak.UI.UiPuppeteer;
using GagSpeak.UI.UiRemote;
using GagSpeak.UI.UiToybox;
using GagSpeak.UI.UiWardrobe;
using GagSpeak.UpdateMonitoring.Chat;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.MainWindow;

/// <summary> 
/// Partial class responsible for drawing the homepage element of the main UI.
/// The homepage will provide the player with links to open up other windows in the plugin via components.
/// </summary>
public class MainUiHomepage : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly ItemIdVars _itemHelpers;

    public MainUiHomepage(ILogger<MainUiHomepage> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        ItemIdVars itemHelpers) : base(logger, mediator)
    {
        _uiShared = uiSharedService;
        _itemHelpers = itemHelpers;
    }

    public float DrawHomepageSection(IDalamudPluginInterface pi)
    {
        // get the width of the window content region we set earlier
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        float pairlistEnd = 0;

        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;

        // span the height of the pair list to be the height of the window minus the transfer section, which we are removing later anyways.
        var ySize = ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y
            + ImGui.GetTextLineHeight() - ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().WindowBorderSize - ImGui.GetCursorPosY();

        // begin the list child, with no border and of the height calculated above
        ImGui.BeginChild("homepageModuleListings", new Vector2(_windowContentWidth, ySize), border: false);

        // draw the buttons (basic for now) for access to other windows
        DrawHomepageModules(availableWidth, spacing.X, pi);

        // then end the list child
        ImGui.EndChild();

        // fetch the cursor position where the footer is
        pairlistEnd = ImGui.GetCursorPosY();

        // return a push to the footer to know where to draw our bottom tab bar
        return ImGui.GetCursorPosY() - pairlistEnd - ImGui.GetTextLineHeight();
    }

    /// <summary>
    /// Draws the list of pairs belonging to the client user.
    /// </summary>
    private void DrawHomepageModules(float availableWidth, float spacingX, IDalamudPluginInterface pi)
    {
        // get the width of the window content region we set earlier
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        var _spacingX = ImGui.GetStyle().ItemSpacing.X;
        var buttonX = (availableWidth - spacingX);

        // My Remote
        if (_uiShared.IconTextButton(FontAwesomeIcon.WaveSquare, "Lovense Remote Interface", buttonX))
        {
            // possibly use a factory to generate pair-unique remotes, or, if you want to incorporate multiple use functionality,
            // you can configure users in the remote menu.
            Mediator.Publish(new UiToggleMessage(typeof(RemotePersonal)));
        }
        UiSharedService.AttachToolTip("Use your personal Lovense Remote to send vibrations to yourself or other pairs ");

        // Opens the Orders Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.ClipboardList, "Orders Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(OrdersUI)));
        }

        // Opens the Gags Status Interface UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.CommentSlash, "Gags Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(GagSetupUI)));
        }

        // Opens the Wardrobe Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.ToiletPortable, "Wardrobe Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(WardrobeUI)));
        }

        // Opens the Puppeteer Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.PersonHarassing, "Puppeteer Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(PuppeteerUI)));
        }

        // Opens the Toybox Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.BoxOpen, "Toybox Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(ToyboxUI)));
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        bool isVisible = ChatLogAddonHelper.IsChatInputVisible;
        bool isPanelsVisible = ChatLogAddonHelper.IsChatPanelVisible(0);

        float width = (ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemInnerSpacing.X*2) / 2;
        if (ImGui.Button("Toggle Chat Input", new Vector2(width, ImGui.GetFrameHeight())))
        {
            ChatLogAddonHelper.SetMainChatLogVisibility(!isVisible);
        }
        ImUtf8.SameLineInner();
        if(ImGui.Button("Toggle Chat Panels", new Vector2(width, ImGui.GetFrameHeight())))
        {
            ChatLogAddonHelper.SetChatLogPanelsVisibility(!isPanelsVisible);
        }
        ImUtf8.SameLineInner();
        ImGui.Checkbox("##Block Chat Input", ref BlockChatInput);
        UiSharedService.AttachToolTip("Toggle if chat input is blocked or not");

        if (BlockChatInput)
            ChatLogAddonHelper.DiscardCursorNodeWhenFocused();

        bool fashionCheckVisible = false;
        unsafe
        {
            var fashionCheckOpen = (AtkUnitBase*)GenericHelpers.GetAddonByName("FashionCheck");
            if (fashionCheckOpen != null)
                fashionCheckVisible = fashionCheckOpen->RootNode->IsVisible();
        };
        ImGui.Text("FashionCheck Open:" + fashionCheckVisible);
        ImGui.Separator();
        try
        {
            unsafe
            {
                ImGui.Text("Gold Saucer Information:");
                var GateDirector = GSManager->CurrentGFateDirector;
                var markerToolTip = GateDirector->MapMarkerTooltipText;
                var mapLvlId = GateDirector->MapMarkerLevelId;
                var EndTimeStamp = GateDirector->EndTimestamp;
                var GateType = GateDirector->GateType;
                var GatePosType = GateDirector->GatePositionType;
                var GateDirectorFlags = GateDirector->Flags;
                ImGui.Text("Marker Tooltip: " + markerToolTip);
                ImGui.Text("Map Level ID: " + mapLvlId);
                ImGui.Text("End Time Stamp: " + EndTimeStamp);
                ImGui.Text("Gate Type: " + (GateType)GateType);
                ImGui.Text("Gate Position Type: " + GatePosType);
                ImGui.Text("Gate Director Flags: " + ((GFateDirectorFlag)GateDirectorFlags).ToString());
                ImGui.Text("IsRunningGate: " + GateDirector->IsRunningGate());
                ImGui.Text("IsAcceptingGate: " + GateDirector->IsAcceptingGate());
                if(GateDirector->IsAcceptingGate() && (uint)GFateDirectorFlag.IsJoined != 0)
                {
                    if(!GagReflexReady)
                    {
                        GagReflexReady = true;
                        Logger.LogWarning("Gag Reflex is ready to be used.");
                    }
                }
                if(GateDirector->IsAcceptingGate() && (uint)GFateDirectorFlag.IsJoined != 0 && (uint)GFateDirectorFlag.IsFinished != 0)
                {
                    if(GagReflexReady)
                    {
                        GagReflexReady = false;
                        Logger.LogWarning("Gag Reflex is no longer ready to be used.");
                    }
                }
            };
        }
        catch (Exception e)
        {
            ImGui.Text("Error: " + e.Message);
        }
    }

    private bool GagReflexReady = false;

    // Gate Flags:
    // IsJoined = We have joined the gate.
    // IsFinished = Our Attempt in the Gate is finished.
    // Unk2 = We failed the attempt???
    // Unk3 = ???
    // Unk4 = ???
    // Unk5 = ???

    private enum GateType : byte
    {
        Something1 = 0,
        Something2 = 1,
        Something3 = 2,
        Something4 = 3,
        Something5 = 4,
        AnyWayTheWindBlows = 5, // fungai event.
        LeapOfFaith = 6,
    }

    // When joining an event:
    // IsAccepting Gate goes to true
    // The flag IsJoined is set,

    private unsafe GoldSaucerManager* GSManager = FFXIVClientStructs.FFXIV.Client.Game.GoldSaucer.GoldSaucerManager.Instance();

    private bool BlockChatInput = false;
}
