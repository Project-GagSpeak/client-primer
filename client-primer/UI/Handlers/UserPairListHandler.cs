using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components.UserPairList;
using GagSpeak.WebAPI;
using ImGuiNET;
using System.Collections.Immutable;
using System.Numerics;

namespace GagSpeak.UI.Handlers;

/// <summary>
/// Handler for drawing the list of user pairs in various ways.
/// Providing a handler for this allows us to draw the list in multiple formats and ways.
/// </summary>
public class UserPairListHandler
{
    private List<IDrawFolder> _drawFolders;
    private List<DrawUserPair> _allUserPairDrawsDistinct; // disinct userpairs to draw
    private readonly TagHandler _tagHandler;
    private readonly PairManager _pairManager;
    private readonly DrawEntityFactory _drawEntityFactory;
    private readonly GagspeakConfigService _configService;
    private readonly UiSharedService _uiSharedService;

    public UserPairListHandler(TagHandler tagHandler, PairManager pairManager,
        DrawEntityFactory drawEntityFactory, GagspeakConfigService configService,
        UiSharedService uiSharedService)
    {
        _tagHandler = tagHandler;
        _pairManager = pairManager;
        _drawEntityFactory = drawEntityFactory;
        _configService = configService;
        _uiSharedService = uiSharedService;

        UpdateDrawFoldersAndUserPairDraws();
    }

    /// <summary> List of all draw folders to display in the UI </summary>
    public List<DrawUserPair> AllPairDrawsDistinct => _allUserPairDrawsDistinct;


    /// <summary>
    /// Draws the list of pairs belonging to the client user.
    /// Groups the pairs by their tags (folders)
    /// </summary>
    public void DrawPairs(ref float lowerTabBarHeight, float windowContentWidth)
    {
        // span the height of the pair list to be the height of the window minus the transfer section, which we are removing later anyways.
        var ySize = lowerTabBarHeight == 0
            ? 1
            : ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y
                + ImGui.GetTextLineHeight() - ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().WindowBorderSize - _transferPartHeight - ImGui.GetCursorPosY();

        // begin the list child, with no border and of the height calculated above
        ImGui.BeginChild("list", new Vector2(windowContentWidth, ySize), border: false);

        // for each item in the draw folders,
        // _logger.LogTrace("Drawing {count} folders", _drawFolders.Count);
        foreach (var item in _drawFolders)
        {
            // draw the content
            item.Draw();
        }

        // then end the list child
        ImGui.EndChild();
    }

    /// <summary> Draws all bi-directionally paired users (online or offline) without any tag header. </summary>
    public void DrawPairsNoGroups()
    {
        // Assuming _drawFolders is your list of IDrawFolder
        var allTagFolder = _drawFolders
            .FirstOrDefault(folder => folder is DrawFolderBase && ((DrawFolderBase)folder).ID == TagHandler.CustomAllTag);

        if (allTagFolder == null)
        {
            return; /*CONSUME*/
        }

        var drawFolderBase = (DrawFolderBase)allTagFolder; // Cast to DrawFolderBase
        // using var indent = ImRaii.PushIndent(_uiSharedService.GetIconData(FontAwesomeIcon.EllipsisV).X + ImGui.GetStyle().ItemSpacing.X, false);
        if (drawFolderBase.DrawPairs.Any())
        {
            foreach (var item in drawFolderBase.DrawPairs)
            {
                item.DrawPairedClient(false); // Draw each pair directly
            }
        }
        else
        {
            ImGui.TextUnformatted("No Draw Pairs to Draw");
        }
    }




    /// <summary> Draws the search filter for our user pair list (whitelist) </summary>
    public void DrawSearchFilter(float availableWidth, float spacingX)
    {
        var buttonSize = _uiSharedService.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - spacingX);
        string filter = _uiSharedService.SearchFilter;
        if (ImGui.InputTextWithHint("##filter", "Filter for UID/notes", ref filter, 255))
        {
            _uiSharedService.SearchFilter = filter;
        }
        ImGui.SameLine();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(_uiSharedService.SearchFilter));
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            _uiSharedService.SearchFilter = string.Empty;
        }
    }

    /// <summary> 
    /// Updates our draw folders and user pair draws.
    /// Called upon construction and UI Refresh event.
    /// </summary>
    public void UpdateDrawFoldersAndUserPairDraws()
    {
        _drawFolders = GetDrawFolders().ToList();
        _allUserPairDrawsDistinct = _drawFolders
            .SelectMany(folder => folder.DrawPairs) // throughout all the folders
            .DistinctBy(pair => pair.Pair)          // without duplicates
            .ToList();
    }

    /// <summary> Fetches the folders to draw in the user pair list (whitelist) </summary>
    /// <returns> List of IDrawFolders to display in the UI </returns>
    public IEnumerable<IDrawFolder> GetDrawFolders()
    {
        List<IDrawFolder> drawFolders = [];

        // the list of all direct pairs.
        var allPairs = _pairManager.DirectPairs;

        // the filters list of pairs will be the pairs that match the filter.
        var filteredPairs = allPairs
            .Where(p =>
            {
                if (_uiSharedService.SearchFilter.IsNullOrEmpty()) return true;
                // return a user if the filter matches their alias or ID, playerChara name, or the nickname we set.
                return p.UserData.AliasOrUID.Contains(_uiSharedService.SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                       (p.GetNickname()?.Contains(_uiSharedService.SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (p.PlayerName?.Contains(_uiSharedService.SearchFilter, StringComparison.OrdinalIgnoreCase) ?? false);
            });

        // the alphabetical sort function of the pairs.
        string? AlphabeticalSort(Pair u)
            => !string.IsNullOrEmpty(u.PlayerName)
                    ? _configService.Current.PreferNicknamesOverNamesForVisible ? u.GetNickname() : u.PlayerName
                    : u.GetNickname() ?? u.UserData.AliasOrUID;

        // filter based on who is online (or paused but that shouldnt exist yet unless i decide to add it later here)
        bool FilterOnlineOrPausedSelf(Pair u)
            => u.IsOnline || !u.IsOnline && !_configService.Current.ShowOfflineUsersSeparately || u.UserPair.OwnPairPerms.IsPaused;

        // collect the sorted list
        List<Pair> BasicSortedList(IEnumerable<Pair> u)
            => u.OrderByDescending(u => u.IsVisible)
                .ThenByDescending(u => u.IsOnline)
                .ThenBy(AlphabeticalSort, StringComparer.OrdinalIgnoreCase)
                .ToList();

        ImmutableList<Pair> ImmutablePairList(IEnumerable<Pair> u) => u.ToImmutableList();

        // if we should filter visible users
        bool FilterVisibleUsers(Pair u) => u.IsVisible && u.IsDirectlyPaired;

        bool FilterTagusers(Pair u, string tag) => u.IsDirectlyPaired && !u.IsOneSidedPair && _tagHandler.HasTag(u.UserData.UID, tag);

        bool FilterNotTaggedUsers(Pair u) => u.IsDirectlyPaired && !u.IsOneSidedPair && !_tagHandler.HasAnyTag(u.UserData.UID);

        bool FilterOfflineUsers(Pair u) => u.IsDirectlyPaired && !u.IsOneSidedPair && !u.IsOnline && !u.UserPair.OwnPairPerms.IsPaused;


        // if we wish to display our visible users separately, then do so.
        if (_configService.Current.ShowVisibleUsersSeparately)
        {
            // display all visible pairs, without filter
            var allVisiblePairs = ImmutablePairList(allPairs
                .Where(FilterVisibleUsers));
            // display the filtered visible pairs based on the filter we applied
            var filteredVisiblePairs = BasicSortedList(filteredPairs
                .Where(FilterVisibleUsers));

            // add the draw folders based on the 
            drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(TagHandler.CustomVisibleTag, filteredVisiblePairs, allVisiblePairs));
        }

        // grab the tags stored in the tag handler
        var tags = _tagHandler.GetAllTagsSorted();
        // for each tag
        foreach (var tag in tags)
        {
            /*_logger.LogDebug("Adding Pair Section List Tag: {tag}", tag);*/
            // display the pairs that have the tag, and are not one sided pairs, and are online or paused
            var allTagPairs = ImmutablePairList(allPairs
                .Where(u => FilterTagusers(u, tag)));
            var filteredTagPairs = BasicSortedList(filteredPairs
                .Where(u => FilterTagusers(u, tag) && FilterOnlineOrPausedSelf(u)));

            // append the draw folders for the tag
            drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(tag, filteredTagPairs, allTagPairs));
        }

        // store the pair list of all online untagged pairs
        var allOnlineNotTaggedPairs = ImmutablePairList(allPairs
            .Where(FilterNotTaggedUsers));
        // store the online untagged pairs
        var onlineNotTaggedPairs = BasicSortedList(filteredPairs
            .Where(u => FilterNotTaggedUsers(u) && FilterOnlineOrPausedSelf(u)));

        // create the draw folders for the online untagged pairs
        /*_logger.LogDebug("Adding Pair Section List Tag: {tag}", TagHandler.CustomAllTag);*/
        drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(
            _configService.Current.ShowOfflineUsersSeparately ? TagHandler.CustomOnlineTag : TagHandler.CustomAllTag,
            onlineNotTaggedPairs, allOnlineNotTaggedPairs));

        // if we want to show offline users seperately,
        if (_configService.Current.ShowOfflineUsersSeparately)
        {
            // then do so.
            var allOfflinePairs = ImmutablePairList(allPairs
                .Where(FilterOfflineUsers));
            var filteredOfflinePairs = BasicSortedList(filteredPairs
                .Where(FilterOfflineUsers));

            // add the folder.
            /*_logger.LogDebug("Adding Pair Section List Tag: {tag}", TagHandler.CustomOfflineTag);*/
            drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(TagHandler.CustomOfflineTag, filteredOfflinePairs,
                allOfflinePairs));

        }

        // finally, add the unpaired users to the list.
        /*_logger.LogDebug("Adding Unpaired Pairs Section List Tag: {tag}", TagHandler.CustomUnpairedTag);*/
        drawFolders.Add(_drawEntityFactory.CreateDrawTagFolder(TagHandler.CustomUnpairedTag,
            BasicSortedList(filteredPairs.Where(u => u.IsOneSidedPair)),
            ImmutablePairList(allPairs.Where(u => u.IsOneSidedPair))));

        return drawFolders;
    }
}
