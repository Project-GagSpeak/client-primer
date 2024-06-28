using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Gagspeak.API.Data.Enum;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI;
using Microsoft.Extensions.Logging;
using FFStreamViewer.WebAPI.UI.Components.Popup;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using System.Numerics;
using FFStreamViewer.UI.Tabs.MediaTab;
using Dalamud.Interface;

namespace FFStreamViewer.WebAPI.UI;
/// <summary>
/// Normally, to update window positions, we would use event handling, but we make an exception here.
/// <para>
/// The exception is when we call the event too frequently, while it will follow, it will "lag behind" 
/// like a slow mouse on an old computer. To fix this, we will inject the window directly into ImGui.Begin
/// </para>
/// </summary>
public partial class UserPairPermsSticky : DisposableMediatorSubscriberBase
{
    public Pair UserPairForPerms; // the user pair we are drawing the sticky permissions for.

    private readonly UiSharedService    _uiSharedService;
    private readonly ApiController      _apiController;
    private readonly PairManager        _pairManager;
    private readonly ILogger<UserPairPermsSticky> _logger;
   
    private SelectedPermissionsTab TabSelection = SelectedPermissionsTab.None;

    private enum SelectedPermissionsTab
    {
        None,
        UserPairPermissions,
        ClientPermissions,
    }

    public UserPairPermsSticky(ILogger<UserPairPermsSticky> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService, ApiController apiController, 
        PairManager configService) : base(logger, mediator)
    {
        _uiSharedService = uiSharedService; // define our services
        _apiController = apiController;
        _pairManager = configService;
        _logger = logger;
    }

    /// <summary>
    /// This call insures that we are drawing this additional window inside the context of the current parent window (the user pair for now)
    /// <para>
    /// Subscribing to compact UI change is a good idea, but the mediator will not update the window as fast as if we did it manually, so
    /// we have to compensate for this special case.
    /// </para>
    /// </summary>
    /// <param name="userPairToDrawPermsFor">The user pair to draw the permissions for</param>
    public bool DrawSticky(float topMenuEnd)
    {
        // Set the window flags
        var flags = ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize;

        // Set position to the right of the main window when attached
        // The downwards offset is implicit through child position.
        if (true)
        {
            var position = ImGui.GetWindowPos();
            position.X  += ImGui.GetWindowSize().X;
            position.Y  += topMenuEnd;
            ImGui.SetNextWindowPos(position);
            flags |= ImGuiWindowFlags.NoMove;
        }

        var size = new Vector2(7 * ImGui.GetFrameHeight() + 3 * ImGui.GetStyle().ItemInnerSpacing.X + 300 * ImGuiHelpers.GlobalScale,
            18 * ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y + 2 * ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetNextWindowSize(size);

        bool isFocused = false;
        var window = ImGui.Begin("###PairPermissionStickyUI"+(UserPairForPerms.UserPair.User.AliasOrUID), flags);
        try
        {
            if (window)
            {
                isFocused = ImGui.IsWindowFocused();
                DrawContent(UserPairForPerms);
            }
        }
        finally
        {
            ImGui.End();
        }
        return isFocused;
    }

    private void DrawContent(Pair userPairToDrawPermsFor)
    {
        var style = ImGui.GetStyle();
        var indentSize = ImGui.GetFrameHeight() + style.ItemSpacing.X;

        _uiSharedService.BigText("Permissions for " + userPairToDrawPermsFor.UserData.AliasOrUID);
        ImGuiHelpers.ScaledDummy(1f);

        // draw the permissions header
        DrawPermissionsTabSelector();

        // draw content based on who's it is.
        if (TabSelection == SelectedPermissionsTab.UserPairPermissions)
        {
            DrawPairPermsForClient();

        }
        else if (TabSelection == SelectedPermissionsTab.ClientPermissions)
        {
            DrawClientPermsForPair();
        }

        if (TabSelection != SelectedPermissionsTab.None) ImGuiHelpers.ScaledDummy(3f);

    }


    public void DrawPermissionsTabSelector()
    {
        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var buttonX = (availableWidth - (spacing.X * 3)) / 4f;
        var buttonY = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Pause).Y;
        var buttonSize = new Vector2(buttonX, buttonY);
        var drawList = ImGui.GetWindowDrawList();
        var underlineColor = ImGui.GetColorU32(ImGuiCol.Separator);
        var btncolor = ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0)));

        ImGuiHelpers.ScaledDummy(spacing.Y / 2f);

        // push the icon for the pairs settings.
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.UserAstronaut.ToIconString(), buttonSize))
            {
                TabSelection = TabSelection == SelectedPermissionsTab.UserPairPermissions ? SelectedPermissionsTab.None : SelectedPermissionsTab.UserPairPermissions;
            }
            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedPermissionsTab.UserPairPermissions)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Client Pair's Permissions");

        // the other button 
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.Filter.ToIconString(), buttonSize))
            {
                TabSelection = TabSelection == SelectedPermissionsTab.ClientPermissions ? SelectedPermissionsTab.None : SelectedPermissionsTab.ClientPermissions;
            }

            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedPermissionsTab.ClientPermissions)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Your Permissions");


        ImGui.NewLine();
        btncolor.Dispose();

        ImGuiHelpers.ScaledDummy(spacing);

    /*
            if (ImGui.Checkbox("Preferred Permissions", ref sticky))
            {
                _ownPermissions.SetSticky(sticky);
            }
            _uiSharedService.DrawHelpText("Preferred Permissions, when enabled, will exclude this user from any permission changes on any syncshells you share with this user.");

            ImGuiHelpers.ScaledDummy(1f);


            if (ImGui.Checkbox("Pause Sync", ref paused))
            {
                _ownPermissions.SetPaused(paused);
            }
            _uiSharedService.DrawHelpText("Pausing will completely cease any sync with this user." + UiSharedService.TooltipSeparator
                + "Note: this is bidirectional, either user pausing will cease sync completely.");
            var otherPerms = Pair.UserPair.OtherPermissions;

            var otherIsPaused = otherPerms.IsPaused();
            var otherDisableSounds = otherPerms.IsDisableSounds();
            var otherDisableAnimations = otherPerms.IsDisableAnimations();
            var otherDisableVFX = otherPerms.IsDisableVFX();

            using (ImRaii.PushIndent(indentSize, false))
            {
                _uiSharedService.BooleanToColoredIcon(!otherIsPaused, false);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(Pair.UserData.AliasOrUID + " has " + (!otherIsPaused ? "not " : string.Empty) + "paused you");
            }

            ImGuiHelpers.ScaledDummy(0.5f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(0.5f);

            if (ImGui.Checkbox("Disable Sounds", ref disableSounds))
            {
                _ownPermissions.SetDisableSounds(disableSounds);
            }
            _uiSharedService.DrawHelpText("Disabling sounds will remove all sounds synced with this user on both sides." + UiSharedService.TooltipSeparator
                + "Note: this is bidirectional, either user disabling sound sync will stop sound sync on both sides.");
            using (ImRaii.PushIndent(indentSize, false))
            {
                _uiSharedService.BooleanToColoredIcon(!otherDisableSounds, false);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(Pair.UserData.AliasOrUID + " has " + (!otherDisableSounds ? "not " : string.Empty) + "disabled sound sync with you");
            }

            if (ImGui.Checkbox("Disable Animations", ref disableAnimations))
            {
                _ownPermissions.SetDisableAnimations(disableAnimations);
            }
            _uiSharedService.DrawHelpText("Disabling sounds will remove all animations synced with this user on both sides." + UiSharedService.TooltipSeparator
                + "Note: this is bidirectional, either user disabling animation sync will stop animation sync on both sides.");
            using (ImRaii.PushIndent(indentSize, false))
            {
                _uiSharedService.BooleanToColoredIcon(!otherDisableAnimations, false);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(Pair.UserData.AliasOrUID + " has " + (!otherDisableAnimations ? "not " : string.Empty) + "disabled animation sync with you");
            }

            if (ImGui.Checkbox("Disable VFX", ref disableVfx))
            {
                _ownPermissions.SetDisableVFX(disableVfx);
            }
            _uiSharedService.DrawHelpText("Disabling sounds will remove all VFX synced with this user on both sides." + UiSharedService.TooltipSeparator
                + "Note: this is bidirectional, either user disabling VFX sync will stop VFX sync on both sides.");
            using (ImRaii.PushIndent(indentSize, false))
            {
                _uiSharedService.BooleanToColoredIcon(!otherDisableVFX, false);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(Pair.UserData.AliasOrUID + " has " + (!otherDisableVFX ? "not " : string.Empty) + "disabled VFX sync with you");
            }

            ImGuiHelpers.ScaledDummy(0.5f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(0.5f);

            bool hasChanges = _ownPermissions != Pair.UserPair.OwnPermissions;

            using (ImRaii.Disabled(!hasChanges))
                if (_uiSharedService.IconTextButton(Dalamud.Interface.FontAwesomeIcon.Save, "Save"))
                {
                    _ = _apiController.SetBulkPermissions(new(
                        new(StringComparer.Ordinal)
                        {
                            { Pair.UserData.UID, _ownPermissions }
                        },
                        new(StringComparer.Ordinal)
                    ));
                }
            UiSharedService.AttachToolTip("Save and apply all changes");

            var rightSideButtons = _uiSharedService.GetIconTextButtonSize(Dalamud.Interface.FontAwesomeIcon.Undo, "Revert") +
                _uiSharedService.GetIconTextButtonSize(Dalamud.Interface.FontAwesomeIcon.ArrowsSpin, "Reset to Default");
            var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

            ImGui.SameLine(availableWidth - rightSideButtons);

            using (ImRaii.Disabled(!hasChanges))
                if (_uiSharedService.IconTextButton(Dalamud.Interface.FontAwesomeIcon.Undo, "Revert"))
                {
                    _ownPermissions = Pair.UserPair.OwnPermissions.DeepClone();
                }
            UiSharedService.AttachToolTip("Revert all changes");

            ImGui.SameLine();
            if (_uiSharedService.IconTextButton(Dalamud.Interface.FontAwesomeIcon.ArrowsSpin, "Reset to Default"))
            {
                var defaultPermissions = _apiController.DefaultPermissions!;
                _ownPermissions.SetSticky(Pair.IsDirectlyPaired || defaultPermissions.IndividualIsSticky);
                _ownPermissions.SetPaused(false);
                _ownPermissions.SetDisableVFX(Pair.IsDirectlyPaired ? defaultPermissions.DisableIndividualVFX : defaultPermissions.DisableGroupVFX);
                _ownPermissions.SetDisableSounds(Pair.IsDirectlyPaired ? defaultPermissions.DisableIndividualSounds : defaultPermissions.DisableGroupSounds);
                _ownPermissions.SetDisableAnimations(Pair.IsDirectlyPaired ? defaultPermissions.DisableIndividualAnimations : defaultPermissions.DisableGroupAnimations);
                _ = _apiController.SetBulkPermissions(new(
                    new(StringComparer.Ordinal)
                    {
                        { Pair.UserData.UID, _ownPermissions }
                    },
                    new(StringComparer.Ordinal)
                ));
            }
            UiSharedService.AttachToolTip("This will set all permissions to your defined default permissions in the Gagspeak Settings");*/

/*    var ySize = ImGui.GetCursorPosY() + style.FramePadding.Y * ImGuiHelpers.GlobalScale + style.FrameBorderSize;
        ImGui.SetWindowSize(new(400, ySize));*/
    }
}
