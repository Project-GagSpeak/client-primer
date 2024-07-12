using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.UI.Handlers;
using GagSpeak;
using System.Collections.Immutable;

namespace GagSpeak.UI.Components;

/// <summary>
/// Class handling the tag (name) that a dropdown folder section has in the list of paired users
/// <para> 
/// Notibly, by being a parent of the draw folder base, it is able to override some functions inside the base,
/// such as draw icon, allowing it to draw customized icon's for the spesific catagoeiss of folder dropdowns
/// </para>
/// </summary>
public class DrawFolderTag : DrawFolderBase
{
    private readonly ApiController _apiController;
    private readonly SelectPairForTagUi _selectPairForTagUi;

    public DrawFolderTag(string id, IImmutableList<DrawUserPair> drawPairs, IImmutableList<Pair> allPairs,
        TagHandler tagHandler, ApiController apiController, SelectPairForTagUi selectPairForTagUi, 
        UiSharedService uiSharedService, ILogger<DrawEntityFactory> logger)
        : base(id, drawPairs, allPairs, tagHandler, uiSharedService, logger)
    {
        _apiController = apiController;
        _selectPairForTagUi = selectPairForTagUi;
    }

    protected override bool RenderIfEmpty => _id switch
    {
        TagHandler.CustomUnpairedTag => false,
        TagHandler.CustomOnlineTag => false,
        TagHandler.CustomOfflineTag => false,
        TagHandler.CustomVisibleTag => false,
        TagHandler.CustomAllTag => true,
        TagHandler.CustomOfflineSyncshellTag => false,
        _ => true,
    };

    protected override bool RenderMenu => _id switch
    {
        TagHandler.CustomUnpairedTag => false,
        TagHandler.CustomOnlineTag => false,
        TagHandler.CustomOfflineTag => false,
        TagHandler.CustomVisibleTag => false,
        TagHandler.CustomAllTag => false,
        TagHandler.CustomOfflineSyncshellTag => false,
        _ => true,
    };

    private bool RenderPause => _id switch
    {
        TagHandler.CustomUnpairedTag => false,
        TagHandler.CustomOnlineTag => false,
        TagHandler.CustomOfflineTag => false,
        TagHandler.CustomVisibleTag => false,
        TagHandler.CustomAllTag => false,
        TagHandler.CustomOfflineSyncshellTag => false,
        _ => true,
    } && _allPairs.Any();

    private bool RenderCount => _id switch
    {
        TagHandler.CustomUnpairedTag => false,
        TagHandler.CustomOnlineTag => false,
        TagHandler.CustomOfflineTag => false,
        TagHandler.CustomVisibleTag => false,
        TagHandler.CustomAllTag => false,
        TagHandler.CustomOfflineSyncshellTag => false,
        _ => true
    };

    protected override float DrawIcon()
    {
        var icon = _id switch
        {
            TagHandler.CustomUnpairedTag => FontAwesomeIcon.ArrowsLeftRight,
            TagHandler.CustomOnlineTag => FontAwesomeIcon.Link,
            TagHandler.CustomOfflineTag => FontAwesomeIcon.Unlink,
            TagHandler.CustomOfflineSyncshellTag => FontAwesomeIcon.Unlink,
            TagHandler.CustomVisibleTag => FontAwesomeIcon.Eye,
            TagHandler.CustomAllTag => FontAwesomeIcon.User,
            _ => FontAwesomeIcon.Folder
        };

        ImGui.AlignTextToFramePadding();
        _uiSharedService.IconText(icon);

        if (RenderCount)
        {
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = ImGui.GetStyle().ItemSpacing.X / 2f }))
            {
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();

                ImGui.TextUnformatted("[" + OnlinePairs.ToString() + "]");
            }
            UiSharedService.AttachToolTip(OnlinePairs + " online" + Environment.NewLine + TotalPairs + " total");
        }
        ImGui.SameLine();
        return ImGui.GetCursorPosX();
    }

    protected override void DrawMenu(float menuWidth)
    {
        ImGui.TextUnformatted("Group Menu");
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Users, "Select Pairs", menuWidth, true))
        {
            _selectPairForTagUi.Open(_id);
        }
        UiSharedService.AttachToolTip("Select Individual Pairs for this Pair Group");
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Trash, "Delete Pair Group", menuWidth, true) && UiSharedService.CtrlPressed())
        {
            _tagHandler.RemoveTag(_id);
        }
        UiSharedService.AttachToolTip("Hold CTRL to remove this Group permanently." + Environment.NewLine +
            "Note: this will not unpair with users in this Group.");
    }

    /// <summary>
    /// The label for each dropdown folder in the list.
    /// </summary>
    /// <param name="width"></param>
    protected override void DrawName(float width)
    {
        ImGui.AlignTextToFramePadding();

        string name = _id switch
        {
            TagHandler.CustomUnpairedTag => "One-sided Individual Pairs",
            TagHandler.CustomOnlineTag => "GagSpeak Online Users",
            TagHandler.CustomOfflineTag => "GagSpeak Offline Users",
            TagHandler.CustomVisibleTag => "Visible",
            TagHandler.CustomAllTag => "Users",
            _ => _id
        };

        ImGui.TextUnformatted(name);
    }

    protected override float DrawRightSide(float currentRightSideX)
    {
        /*if (!RenderPause) return currentRightSideX;

        var allArePaused = _allPairs.All(pair => pair.UserPair!.OwnPermissions.IsPaused());
        var pauseButton = allArePaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
        var pauseButtonX = _uiSharedService.GetIconButtonSize(pauseButton).X;

        var buttonPauseOffset = currentRightSideX - pauseButtonX;
        ImGui.SameLine(buttonPauseOffset);
        if (allArePaused)
        {
            UiSharedService.AttachToolTip($"Resume pairing with all pairs in {_id}");
        }
        else
        {
            UiSharedService.AttachToolTip($"Pause pairing with all pairs in {_id}");
        }
        */
        return currentRightSideX;
    }
}
