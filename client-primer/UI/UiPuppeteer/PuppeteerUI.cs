using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Handlers;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using System.Drawing;
using System.Numerics;

namespace GagSpeak.UI.UiPuppeteer;

public class PuppeteerUI : WindowMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly UserPairListHandler _userPairListHandler;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly AliasTable _aliasTable;
    public PuppeteerUI(ILogger<PuppeteerUI> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, ClientConfigurationManager clientConfigs, 
        UserPairListHandler userPairListHandler, AliasTable aliasTable)
        : base(logger, mediator, "Puppeteer UI")
    {
        _uiSharedService = uiSharedService;
        _clientConfigs = clientConfigs;
        _userPairListHandler = userPairListHandler;
        _aliasTable = aliasTable;

        AllowPinning = false;
        AllowClickthrough = false;
        // define initial size of window and to not respect the close hotkey.
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(545, 370),
            MaximumSize = new Vector2(1000, float.MaxValue)
        };
        RespectCloseHotkey = false;

        // subscriber to update the pair being displayed.
        Mediator.Subscribe<UpdateDisplayWithPair>(this, (msg) => 
        {
            // for firstime generations
            if(SelectedPair == null)
            {
                SelectedPair = msg.Pair;
                _aliasTable.AliasTriggerList = _clientConfigs.FetchListForPair(msg.Pair.UserData.UID);
            }

            // for refreshing data once we switch pairs.
            if (SelectedPair.UserData.UID != msg.Pair.UserData.UID)
            {
                _logger.LogTrace($"Updating display to reflect pair {msg.Pair.UserData.AliasOrUID}");
                SelectedPair = msg.Pair;
                _aliasTable.AliasTriggerList = _clientConfigs.FetchListForPair(msg.Pair.UserData.UID);
                TempTriggerStorage = SelectedPair.UserPairOwnUniquePairPerms.TriggerPhrase ?? null!;
                TempStartChar = SelectedPair.UserPairOwnUniquePairPerms.StartChar.ToString() ?? null!;
                TempEndChar = SelectedPair.UserPairOwnUniquePairPerms.EndChar.ToString() ?? null!;
            }
        });
    }

    public Pair? SelectedPair = null; // the selected pair we are referencing when drawing the right half.
    private string TempTriggerStorage = null!;
    private string TempStartChar = null!;
    private string TempEndChar = null!;

    protected override void PreDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }
    protected override void PostDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }
    protected override void DrawInternal()
    {
        // _logger.LogInformation(ImGui.GetWindowSize().ToString()); <-- USE FOR DEBUGGING ONLY.
        // get information about the window region, its item spacing, and the top left side height.
        var region = ImGui.GetContentRegionAvail();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        var topLeftSideHeight = region.Y;
        var cellPadding = ImGui.GetStyle().CellPadding;

        // create the draw-table for the selectable and viewport displays
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiSharedService.GetFontScalerFloat(), 0));
        try
        {
            using (var table = ImRaii.Table($"PuppeteerUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
            {
                if (!table) return;
                // setup the columns for the table
                ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, 200f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("##RightColumn", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextColumn();

                var regionSize = ImGui.GetContentRegionAvail();
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

                using (var leftChild = ImRaii.Child($"###PuppeteerLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                {
                    var iconTexture = _uiSharedService.GetLogoSmall();
                    if (!(iconTexture is { } wrap))
                    {
                        _logger.LogWarning("Failed to render image!");
                    }
                    else
                    {
                        UtilsExtensions.ImGuiLineCentered("###PuppeteerLogo", () =>
                        {
                            ImGui.Image(wrap.ImGuiHandle, new(125f * _uiSharedService.GetFontScalerFloat(), 125f * _uiSharedService.GetFontScalerFloat()));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text($"You found a wild easter egg, Y I P P E E !!!");
                                ImGui.EndTooltip();
                            }
                        });
                    }
                    // add separator
                    ImGui.Spacing();
                    ImGui.Separator();
                    // Add the tab menu for the left side
                    _userPairListHandler.DrawPairsNoGroups(region.X);
                }
                // pop pushed style variables and draw next column.
                ImGui.PopStyleVar();
                ImGui.TableNextColumn();
                // display right half viewport based on the tab selection
                using (var rightChild = ImRaii.Child($"###PuppeteerRightSide", Vector2.Zero, false))
                {
                    DrawPuppeteer(cellPadding);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex}");
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }

    // Main Right-half Draw function for puppeteer.
    private void DrawPuppeteer(Vector2 DefaultCellPadding)
    {
        if (SelectedPair == null)
        {
            ImGui.Text("Select a pair to view their puppeteer setup.");
            return;
        }
        var region = ImGui.GetContentRegionAvail();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        // draw title
        using (_uiSharedService.UidFont.Push())
        {
            ImGui.Text($"Settings for {SelectedPair.UserData.AliasOrUID}");
        }

        // below, draw trigger phrase info prompt
        ImGui.Text($"Your Trigger Phrase for {SelectedPair.PlayerName}");
        UiSharedService.AttachToolTip(
                $"When {SelectedPair.UserData.AliasOrUID} says the entered phrase in chat,\n" +
                "you will execute everything after it, (or within your defined brackets)");
        
        // draw out trigger phrase input
        DrawSelectedPairTriggerPhrase(region.X);

        // draw example usage
        if (!string.IsNullOrEmpty(SelectedPair.UserPairOwnUniquePairPerms.TriggerPhrase))
        {
            // if trigger phrase exists, see if it has splits to contain multiple.
            bool hasSplits = SelectedPair.UserPairOwnUniquePairPerms.TriggerPhrase.Contains("|");
            var displayText = hasSplits ? SelectedPair.UserPairOwnUniquePairPerms.TriggerPhrase.Split('|')[0] 
                                        : SelectedPair.UserPairOwnUniquePairPerms.TriggerPhrase;
            // example display
            ImGui.Text($"Example Usage from : {SelectedPair.UserData.AliasOrUID}");
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), $"<{SelectedPair.UserData.AliasOrUID}> " +
            $"{displayText} {SelectedPair.UserPairOwnUniquePairPerms.StartChar} " + $"glamour apply Hogtied | p | [me] "+
            $"{SelectedPair.UserPairOwnUniquePairPerms.EndChar}");
            UiSharedService.AttachToolTip($"The spaces between the brackets and commands/trigger phrases are optional.");
        }

        // draw permissions & channels
        ImGui.Separator();

        bool allowSitRequests = SelectedPair.UserPairOwnUniquePairPerms.AllowSitRequests;
        if (ImGui.Checkbox("Allow Sit Commands", ref allowSitRequests))
        {
            SelectedPair.UserPairOwnUniquePairPerms.AllowSitRequests = allowSitRequests;
            // TODO : publish to mediator our update so we push it
        }
        UiSharedService.AttachToolTip($"Allows {SelectedPair.UserData.AliasOrUID} to make you perform /sit and /groundsit");

        bool allowMotionRequests = SelectedPair.UserPairOwnUniquePairPerms.AllowMotionRequests;
        if (ImGui.Checkbox("Allow Emotes & Expressions", ref allowMotionRequests))
        {
            SelectedPair.UserPairOwnUniquePairPerms.AllowMotionRequests = allowMotionRequests;
            // TODO : publish to mediator our update so we push it
        }
        UiSharedService.AttachToolTip($"Allows {SelectedPair.UserData.AliasOrUID} to make you perform emotes and expressions");

        bool allowAllRequests = SelectedPair.UserPairOwnUniquePairPerms.AllowAllRequests;
        if (ImGui.Checkbox("Allow All Commands", ref allowAllRequests))
        {
            SelectedPair.UserPairOwnUniquePairPerms.AllowAllRequests = allowAllRequests;
            // TODO : publish to mediator our update so we push it
        }
        UiSharedService.AttachToolTip($"Allows {SelectedPair.UserData.AliasOrUID} to make you perform any command");

        using (_uiSharedService.UidFont.Push())
        {
            ImGui.Text("AliasList");
        }

        using (var aliasTable = ImRaii.Child($"###PuppeteerAliasList", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar))
        {
            _aliasTable.DrawAliasListTable(SelectedPair.UserData.UID, DefaultCellPadding.Y);
        }
    }

    private void DrawSelectedPairTriggerPhrase(float width)
    {
        ImGui.SetNextItemWidth(width * 0.8f);
        // store temp value to contain within the text input
        var TriggerPhrase = TempTriggerStorage ?? SelectedPair!.UserPairOwnUniquePairPerms.TriggerPhrase;
        if (ImGui.InputTextWithHint($"##{SelectedPair!.UserData.AliasOrUID}sTrigger", "Leave Blank for no trigger phrase...",
            ref TriggerPhrase, 64, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            TempTriggerStorage = TriggerPhrase;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            SelectedPair.UserPairOwnUniquePairPerms.TriggerPhrase = TriggerPhrase;
            TempTriggerStorage = null!;
            // TODO: publish to mediator our update so we push it
        }
        UiSharedService.AttachToolTip("You can create multiple trigger phrases by placing a | between phrases.");

        // on the same line inner, draw the start char input directly beside it.
        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(20*ImGuiHelpers.GlobalScale);
        // draw out the start and end characters
        var startChar = TempStartChar ?? SelectedPair.UserPairOwnUniquePairPerms.StartChar.ToString();
        if (ImGui.InputText($"##{SelectedPair.UserData.AliasOrUID}sStarChar", ref startChar, 1, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            TempStartChar = startChar;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (string.IsNullOrEmpty(startChar) || startChar == " ")
            {
                startChar = "(";
            }
            SelectedPair.UserPairOwnUniquePairPerms.StartChar = startChar[0];
            TempStartChar = null!;
            // TODO: publish to mediator our update so we push it
        }
        UiSharedService.AttachToolTip($"Custom Start Character that replaces the left enclosing bracket.\n" +
            "Replaces the [ ( ] in Ex: [ TriggerPhrase (commandToExecute) ]");

        // on same line inner, draw the end char.
        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(20 * ImGuiHelpers.GlobalScale);
        var endChar = TempEndChar ?? SelectedPair.UserPairOwnUniquePairPerms.EndChar.ToString();
        if (ImGui.InputText($"##{SelectedPair.UserData.AliasOrUID}sStarChar", ref startChar, 1, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            TempStartChar = startChar;
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (string.IsNullOrEmpty(startChar) || startChar == " ")
            {
                startChar = ")";
            }
            SelectedPair.UserPairOwnUniquePairPerms.StartChar = startChar[0];
            TempStartChar = null!;
            // TODO: publish to mediator our update so we push it
        }
        UiSharedService.AttachToolTip($"Custom End Character that replaces the right enclosing bracket.\n" +
            "Replaces the [ ) ] in Ex: [ TriggerPhrase (commandToExecute) ]");
    }
}
