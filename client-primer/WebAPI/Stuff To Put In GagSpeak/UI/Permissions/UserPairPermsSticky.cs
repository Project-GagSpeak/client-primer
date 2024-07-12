using Dalamud.Interface.Utility;
using FFStreamViewer.WebAPI.PlayerData.Data;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services.Mediator;
using ImGuiNET;
using System.Numerics;

namespace FFStreamViewer.WebAPI.UI.Permissions;
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

    private readonly PlayerCharacterManager _playerCharacterManager;
    private readonly UiSharedService _uiSharedService;
    private readonly ApiController _apiController;
    private readonly PairManager _pairManager;
    private readonly ILogger<UserPairPermsSticky> _logger;

    public StickyWindowType DrawType = StickyWindowType.None; // determines if we draw the pair permissions, or the clients perms for the pair.

    public UserPairPermsSticky(ILogger<UserPairPermsSticky> logger, PlayerCharacterManager pcManager,
        GagspeakMediator mediator, UiSharedService uiSharedService, ApiController apiController,
        PairManager configService) : base(logger, mediator)
    {
        _playerCharacterManager = pcManager; // define our services
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
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar;

        // Set position to the right of the main window when attached
        // The downwards offset is implicit through child position.
        if (true)
        {
            var position = ImGui.GetWindowPos();
            position.X += ImGui.GetWindowSize().X;
            position.Y += ImGui.GetFrameHeightWithSpacing();
            ImGui.SetNextWindowPos(position);
            flags |= ImGuiWindowFlags.NoMove;
        }

        var size = new Vector2(7 * ImGui.GetFrameHeight() + 3 * ImGui.GetStyle().ItemInnerSpacing.X + 100 * ImGuiHelpers.GlobalScale,
            ImGui.GetWindowSize().Y - ImGui.GetFrameHeightWithSpacing() * 2);
        ImGui.SetNextWindowSize(size);

        bool isFocused = false;
        var window = ImGui.Begin("###PairPermissionStickyUI" + (UserPairForPerms.UserPair.User.AliasOrUID), flags);
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

        ImGuiHelpers.ScaledDummy(1f);

        // draw content based on who's it is.
        if (DrawType == StickyWindowType.PairPerms)
        {
            // draw the pair's permissions they have set for you
            DrawPairPermsForClient();
        }
        else if (DrawType == StickyWindowType.ClientPermsForPair)
        {
            // draw clients permission edit access page
            DrawClientPermsForPair();
        }
        else
        {
            // Occurs when draw type is set to None
        }
    }
}
