using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Handlers;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Enums;
using ImGuiNET;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.UiPuppeteer;

public class PuppeteerUI : WindowMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly UserPairListHandler _userPairListHandler;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PuppeteerHandler _puppeteerHandler;
    private readonly AliasTable _aliasTable;
    private PuppeteerTab _currentTab = PuppeteerTab.TriggerPhrases;
    private enum PuppeteerTab { TriggerPhrases, ClientAliasList, PairAliasList }

    public PuppeteerUI(ILogger<PuppeteerUI> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, ClientConfigurationManager clientConfigs,
        UserPairListHandler userPairListHandler, PuppeteerHandler handler,
        AliasTable aliasTable) : base(logger, mediator, "Puppeteer UI")
    {
        _uiShared = uiSharedService;
        _clientConfigs = clientConfigs;
        _userPairListHandler = userPairListHandler;
        _puppeteerHandler = handler;
        _aliasTable = aliasTable;

        AllowPinning = false;
        AllowClickthrough = false;
        // define initial size of window and to not respect the close hotkey.
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(650, 370),
            MaximumSize = new Vector2(1000, float.MaxValue)
        };
        RespectCloseHotkey = false;
    }

    private bool isEditingTriggerOptions = false;
    private string? UnsavedTriggerPhrase = null;
    private string? UnsavedNewStartChar = null;
    private string? UnsavedNewEndChar = null;

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
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiShared.GetFontScalerFloat(), 0));

        using (ImRaii.Table($"PuppeteerUiWindowTable", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
        {
            // setup the columns for the table
            ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, 200f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("##RightColumn", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn();

            var regionSize = ImGui.GetContentRegionAvail();
            ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));

            using (var leftChild = ImRaii.Child($"###PuppeteerLeft", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
            {
                var iconTexture = _uiShared.GetLogo();
                if (!(iconTexture is { } wrap))
                {
                    /*_logger.LogWarning("Failed to render image!");*/
                }
                else
                {
                    UtilsExtensions.ImGuiLineCentered("###PuppeteerLogo", () =>
                    {
                        ImGui.Image(wrap.ImGuiHandle, new(125f * _uiShared.GetFontScalerFloat(), 125f * _uiShared.GetFontScalerFloat()));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text($"What's this? A tooltip hidden in plain sight?");
                            ImGui.EndTooltip();
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                            UnlocksEventManager.AchievementEvent(UnlocksEvent.EasterEggFound, "Puppeteer");
                    });
                }
                // add separator
                ImGui.Spacing();
                ImGui.Separator();
                float width = ImGui.GetContentRegionAvail().X;
                // show the search filter just above the contacts list to form a nice separation.
                _userPairListHandler.DrawSearchFilter(width, ImGui.GetStyle().ItemInnerSpacing.X, false);
                ImGui.Separator();
                using (var listChild = ImRaii.Child($"###PuppeteerList", ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.NoScrollbar))
                {
                    _userPairListHandler.DrawPairListSelectable(width, true);
                }
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

    // Main Right-half Draw function for puppeteer.
    private void DrawPuppeteer(Vector2 DefaultCellPadding)
    {
        // update the display if we switched selected Pairs.
        if (_userPairListHandler.SelectedPair is null)
        {
            _uiShared.BigText("Select a Pair to view information!");
            return;
        }

        if (_userPairListHandler.SelectedPair is not null && _puppeteerHandler.SelectedPair is null)
        {
            _puppeteerHandler.UpdateDisplayForNewPair(_userPairListHandler.SelectedPair);
            return;
        }

        if (_puppeteerHandler.SelectedPair is null || _userPairListHandler.SelectedPair is null)
        {
            _uiShared.BigText("No Pair Selected, select one first!");
            return;
        }

        // update display if we switched selected pairs.
        if (_puppeteerHandler.SelectedPair.UserData.UID != _userPairListHandler.SelectedPair.UserData.UID)
        {
            _puppeteerHandler.UpdateDisplayForNewPair(_userPairListHandler.SelectedPair);
        }

        var region = ImGui.GetContentRegionAvail();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        // draw title
        DrawPuppeteerHeader(DefaultCellPadding);

        ImGui.Separator();

        switch (_currentTab)
        {
            case PuppeteerTab.TriggerPhrases:
                DrawTriggerPhrases(region.X);
                break;
            case PuppeteerTab.ClientAliasList:
                _aliasTable.DrawAliasListTable(_puppeteerHandler.SelectedPair.UserData.UID, DefaultCellPadding.Y);
                break;
            case PuppeteerTab.PairAliasList:
                DrawPairAliasList(_puppeteerHandler.SelectedPair.LastReceivedAliasData);
                break;
        }
    }

    private bool AliasDataListExists
        => _puppeteerHandler.SelectedPair is not null
        && _puppeteerHandler.SelectedPair.LastReceivedAliasData is not null
        && _puppeteerHandler.SelectedPair.LastReceivedAliasData.AliasList.Any();

    private void DrawPuppeteerHeader(Vector2 DefaultCellPadding)
    {
        if (_puppeteerHandler.SelectedPair == null) return;

        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        Vector2 textSize;
        using (_uiShared.UidFont.Push()) { textSize = ImGui.CalcTextSize("View Info"); }
        var triggerButtonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Microphone, "Triggers");
        var clientAliasListSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.EllipsisV, "Your List");
        var pairAliasListSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.EllipsisV, "Pair's List");
        var centerYpos = (textSize.Y - ImGui.GetFrameHeight());

        using (ImRaii.Child("ViewPairInformationHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(),
            _uiShared.GetIconButtonSize(FontAwesomeIcon.Voicemail).Y + (centerYpos - startYpos) * 2 - DefaultCellPadding.Y)))
        {
            // now next to it we need to draw the header text
            ImGui.SameLine(ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText($"View Info", ImGuiColors.ParsedPink);
            }


            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - triggerButtonSize - clientAliasListSize - pairAliasListSize - ImGui.GetStyle().ItemSpacing.X * 3);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var currentYpos = ImGui.GetCursorPosY();
            // draw revert button at the same location but right below that button
            if (_uiShared.IconTextButton(FontAwesomeIcon.Microphone, "Triggers", null, false, _currentTab == PuppeteerTab.TriggerPhrases))
            {
                _currentTab = PuppeteerTab.TriggerPhrases;
            }
            UiSharedService.AttachToolTip("View your set trigger phrase, your pairs, and use case examples!");

            // draw revert button at the same location but right below that button
            ImGui.SameLine();
            ImGui.SetCursorPosY(currentYpos);
            if (_uiShared.IconTextButton(FontAwesomeIcon.EllipsisV, "Your List", null, false, _currentTab == PuppeteerTab.ClientAliasList
                || (_puppeteerHandler.StorageBeingEdited.CharacterName.IsNullOrEmpty() || _puppeteerHandler.StorageBeingEdited.CharacterWorld.IsNullOrEmpty())))
            {
                _currentTab = PuppeteerTab.ClientAliasList;
            }
            UiSharedService.AttachToolTip("Configure your Alias List.");

            ImGui.SameLine();
            ImGui.SetCursorPosY(currentYpos);
            if (_uiShared.IconTextButton(FontAwesomeIcon.EllipsisV, "Pair's List", null, false, _currentTab == PuppeteerTab.PairAliasList))
            {
                _currentTab = PuppeteerTab.PairAliasList;
            }
            UiSharedService.AttachToolTip("View this Pair's Alias List.");
        }
    }

    private void DrawTriggerPhrases(float width)
    {
        if (_puppeteerHandler.SelectedPair is null) return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiShared.GetFontScalerFloat(), 0));
        using var table = ImRaii.Table($"TriggersDisplayForPair", 2, ImGuiTableFlags.BordersInnerV);

        if (!table) return;
        ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X / 2);
        ImGui.TableSetupColumn("##RightColumn", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn();

        // compile a struct for displaying the example data.
        var clientTriggerData = new TriggerData(string.Empty, "Client",
            _puppeteerHandler.SelectedPair.UserPairOwnUniquePairPerms.TriggerPhrase,
            _puppeteerHandler.SelectedPair.UserPairOwnUniquePairPerms.StartChar,
            _puppeteerHandler.SelectedPair.UserPairOwnUniquePairPerms.EndChar);

        DrawTriggerPhraseDetailBox(clientTriggerData);

        ImGui.TableNextColumn();

        var pairTriggerData = new TriggerData(_puppeteerHandler.SelectedPair.GetNickname() ?? _puppeteerHandler.SelectedPair.UserData.Alias ?? string.Empty,
            _puppeteerHandler.SelectedPair.UserData.UID,
            _puppeteerHandler.SelectedPair.UserPairUniquePairPerms.TriggerPhrase,
            _puppeteerHandler.SelectedPair.UserPairUniquePairPerms.StartChar,
            _puppeteerHandler.SelectedPair.UserPairUniquePairPerms.EndChar);

        DrawTriggerPhraseDetailBox(pairTriggerData);
    }

    private void DrawPairAliasList(CharacterAliasData? pairAliasData)
    {
        if (!AliasDataListExists || ApiController.ServerState is not ServerState.Connected || pairAliasData == null)
        {
            _uiShared.BigText("Pair has no List for you!");
            return;
        }

        using var pairAliasListChild = ImRaii.Child("##PairAliasListChild", ImGui.GetContentRegionAvail(), false);
        if (!pairAliasListChild) return;
        // display a custom box icon for each search result obtained.
        foreach (var aliasItem in pairAliasData.AliasList)
            DrawAliasItemBox(aliasItem);
    }

    private void DrawTriggerPhraseDetailBox(TriggerData triggerInfo)
    {
        if (_puppeteerHandler.SelectedPair is null) 
            return;

        bool isClient = triggerInfo.UID == "Client";
        bool displayInRed = isClient && (_puppeteerHandler.StorageBeingEdited.CharacterName.IsNullOrEmpty() || _puppeteerHandler.StorageBeingEdited.CharacterWorld.IsNullOrEmpty());
        var buttonIcon = isEditingTriggerOptions ? FontAwesomeIcon.Save : FontAwesomeIcon.Edit;
        var iconSize = isEditingTriggerOptions ? _uiShared.GetIconButtonSize(FontAwesomeIcon.Save) : _uiShared.GetIconButtonSize(FontAwesomeIcon.Edit);
        string displayName = triggerInfo.NickOrAlias.IsNullOrEmpty() ? triggerInfo.UID : triggerInfo.NickOrAlias;


        // push rounding window corners
        using var windowRounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 5f);
        // push a pink border color for the window border.
        using var borderColor = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var borderCol = ImRaii.PushColor(ImGuiCol.Border, displayInRed ? ImGuiColors.DPSRed : ImGuiColors.ParsedPink);
        // push a less transparent very dark grey background color.
        using var bgColor = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.25f, 0.2f, 0.2f, 0.4f));
        // create the child window.
        using (var patternResChild = ImRaii.Child("##TriggerDataFor" + triggerInfo.UID, ImGui.GetContentRegionAvail(), true, ImGuiWindowFlags.ChildWindow))
        {
            if (!patternResChild) return;

            // Handle Case where data is not yet matched.
            if (displayInRed && isClient)
            {
                using (ImRaii.Group())
                {
                    UiSharedService.ColorTextCentered("Not Listening To Pair's Character.", ImGuiColors.DalamudRed);
                    ImGui.Spacing();
                    UiSharedService.ColorTextCentered("This pair must press the action:", ImGuiColors.DalamudRed);
                    ImGui.Spacing();

                    ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2
                        - (_uiShared.GetIconTextButtonSize(FontAwesomeIcon.Sync, "Update [UID] with your Name") - 5 * ImGuiHelpers.GlobalScale) / 2);

                    using (ImRaii.Disabled(true))
                    {
                        _uiShared.IconTextButton(FontAwesomeIcon.Sync, "Update [UID] with your Name", null, false);
                    }
                    ImGui.Spacing();
                    UiSharedService.ColorTextCentered("(If you wanna be controlled by them)", ImGuiColors.DalamudRed);
                    return;
                }
            }

            if (isClient)
            {
                using (var group = ImRaii.Group())
                {
                    // display name, then display the downloads and likes on the other side.
                    ImGui.AlignTextToFramePadding();
                    UiSharedService.ColorText("Listening To", ImGuiColors.ParsedPink);

                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - iconSize.X * 4 - ImGui.GetStyle().ItemInnerSpacing.X * 3);
                    bool allowSitRequests = _puppeteerHandler.SelectedPair.UserPairOwnUniquePairPerms.AllowSitRequests;
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, allowSitRequests ? ImGuiColors.ParsedGold : ImGuiColors.DalamudGrey))
                    {
                        if (_uiShared.IconButton(FontAwesomeIcon.Chair, null, null, false, true))
                        {
                            _logger.LogTrace($"Updated own pair permission: AllowSitCommands to {!allowSitRequests}");
                            _ = _uiShared.ApiController.UserUpdateOwnPairPerm(new(_puppeteerHandler.SelectedPair.UserData,
                                new KeyValuePair<string, object>("AllowSitRequests", !allowSitRequests)));
                        }
                    }
                    UiSharedService.AttachToolTip($"Allows {_puppeteerHandler.SelectedPair.UserData.AliasOrUID} to make you perform /sit and /groundsit");

                    ImUtf8.SameLineInner();
                    bool allowMotionRequests = _puppeteerHandler.SelectedPair.UserPairOwnUniquePairPerms.AllowMotionRequests;
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, allowMotionRequests ? ImGuiColors.ParsedGold : ImGuiColors.DalamudGrey))
                    {
                        if (_uiShared.IconButton(FontAwesomeIcon.Walking, null, null, false, true))
                        {
                            _logger.LogTrace($"Updated own pair permission: AllowEmotesExpressions to {!allowMotionRequests}");
                            _ = _uiShared.ApiController.UserUpdateOwnPairPerm(new(_puppeteerHandler.SelectedPair.UserData,
                                new KeyValuePair<string, object>("AllowMotionRequests", !allowMotionRequests)));
                        }
                    }
                    UiSharedService.AttachToolTip($"Allows {_puppeteerHandler.SelectedPair.UserData.AliasOrUID} to make you perform emotes " +
                        "and expressions (cpose included)");

                    ImUtf8.SameLineInner();
                    bool allowAllRequests = _puppeteerHandler.SelectedPair.UserPairOwnUniquePairPerms.AllowAllRequests;
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, allowAllRequests ? ImGuiColors.ParsedGold : ImGuiColors.DalamudGrey))
                    {
                        if (_uiShared.IconButton(FontAwesomeIcon.CheckDouble, null, null, false, true))
                        {
                            _logger.LogTrace($"Updated own pair permission: AllowAllCommands to {!allowAllRequests}");
                            _ = _uiShared.ApiController.UserUpdateOwnPairPerm(new(_puppeteerHandler.SelectedPair.UserData,
                                new KeyValuePair<string, object>("AllowAllRequests", !allowAllRequests)));
                        }
                    }
                    UiSharedService.AttachToolTip($"Allows {_puppeteerHandler.SelectedPair.UserData.AliasOrUID} to make you perform any command");

                    ImUtf8.SameLineInner();
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, isEditingTriggerOptions ? ImGuiColors.ParsedPink : ImGuiColors.DalamudGrey))
                    {
                        if (_uiShared.IconButton(buttonIcon, null, null, false, true))
                        {
                            if (isEditingTriggerOptions)
                            {
                                // save and update our changes.
                                if (UnsavedTriggerPhrase is not null)
                                {
                                    _logger.LogTrace($"Updated own pair permission: TriggerPhrase to {UnsavedTriggerPhrase}");
                                    _ = _uiShared.ApiController.UserUpdateOwnPairPerm(new(_puppeteerHandler.SelectedPair.UserData, new KeyValuePair<string, object>("TriggerPhrase", UnsavedTriggerPhrase)));
                                    UnsavedTriggerPhrase = null;
                                }
                                if (UnsavedNewStartChar is not null)
                                {
                                    _logger.LogTrace($"Updated own pair permission: StartChar to {UnsavedNewStartChar}");
                                    _ = _uiShared.ApiController.UserUpdateOwnPairPerm(new(_puppeteerHandler.SelectedPair.UserData, new KeyValuePair<string, object>("StartChar", UnsavedNewStartChar[0])));
                                    UnsavedNewStartChar = null;
                                }
                                if (UnsavedNewEndChar is not null)
                                {
                                    _logger.LogTrace($"Updated own pair permission: EndChar to {UnsavedNewEndChar}");
                                    _ = _uiShared.ApiController.UserUpdateOwnPairPerm(new(_puppeteerHandler.SelectedPair.UserData, new KeyValuePair<string, object>("EndChar", UnsavedNewEndChar[0])));
                                    UnsavedNewEndChar = null;
                                }
                            }
                            isEditingTriggerOptions = !isEditingTriggerOptions;
                        }
                    }
                    UiSharedService.AttachToolTip(isEditingTriggerOptions ? "Stop Editing your TriggerPhrase Info." : "Modify Your TriggerPhrase Info");
                }

                using (ImRaii.PushFont(UiBuilder.MonoFont)) ImGui.TextUnformatted($"{_puppeteerHandler.StorageBeingEdited.CharacterName} @ {_puppeteerHandler.StorageBeingEdited.CharacterWorld}");
                ImGui.Spacing();
                ImGui.Separator();
            }

            // Handle the case where data is matched.
            string[] triggers = triggerInfo.TriggerPhrase.Split('|');

            StringBuilder label = new StringBuilder();
            if (isClient) label.Append("Your ");
            label.Append(label.Length > 0 ? "Trigger Phrases" : "Trigger Phrase");
            if (!isClient) label.Append(" set for you.");

            ImGui.AlignTextToFramePadding();
            UiSharedService.ColorText(label.ToString(), ImGuiColors.ParsedPink);
            ImGui.Spacing();

            if (isEditingTriggerOptions && isClient)
            {
                var TriggerPhrase = UnsavedTriggerPhrase ?? triggerInfo.TriggerPhrase;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputTextWithHint($"##{displayName}-Trigger", "Leave Blank for none...", ref TriggerPhrase, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    UnsavedTriggerPhrase = TriggerPhrase;
                UiSharedService.AttachToolTip("You can create multiple trigger phrases by placing a | between phrases.");
            }
            else
            {
                if (!triggers.Any() || triggers[0] == string.Empty)
                {
                    using (ImRaii.PushFont(UiBuilder.MonoFont)) ImGui.TextUnformatted("No Trigger Phrase Set.");
                }

                foreach (var trigger in triggers)
                {
                    if (trigger.IsNullOrEmpty()) continue;

                    _uiShared.IconText(FontAwesomeIcon.QuoteLeft, ImGuiColors.ParsedPink);
                    ImUtf8.SameLineInner();
                    using (ImRaii.PushFont(UiBuilder.MonoFont)) ImGui.TextUnformatted(trigger);
                    ImUtf8.SameLineInner();
                    _uiShared.IconText(FontAwesomeIcon.QuoteRight, ImGuiColors.ParsedPink);
                }
            }

            using (ImRaii.Group())
            {
                ImGui.Spacing();
                ImGui.AlignTextToFramePadding();
                UiSharedService.ColorText("Custom Brackets:", ImGuiColors.ParsedPink);
                ImGui.SameLine();
                if (isEditingTriggerOptions && isClient)
                {
                    ImGui.SetNextItemWidth(20 * ImGuiHelpers.GlobalScale);
                    var startChar = UnsavedNewStartChar ?? triggerInfo.StartChar.ToString();
                    if (ImGui.InputText($"##{displayName}sStarChar", ref startChar, 1, ImGuiInputTextFlags.EnterReturnsTrue))
                        UnsavedNewStartChar = startChar;
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        if (string.IsNullOrEmpty(startChar) || startChar == " ")
                            UnsavedNewStartChar = "(";
                    }
                }
                else
                {
                    using (ImRaii.PushFont(UiBuilder.MonoFont)) ImGui.TextUnformatted(triggerInfo.StartChar.ToString());
                }
                UiSharedService.AttachToolTip($"Custom Start Character that replaces the left enclosing bracket." +
                    Environment.NewLine + "Replaces the [ ( ] in: [ TriggerPhrase (commandToExecute) ]");

                ImUtf8.SameLineInner();
                _uiShared.IconText(FontAwesomeIcon.GripLinesVertical, ImGuiColors.ParsedPink);
                ImUtf8.SameLineInner();
                if (isEditingTriggerOptions && isClient)
                {
                    ImGui.SetNextItemWidth(20 * ImGuiHelpers.GlobalScale);
                    var endChar = UnsavedNewEndChar ?? triggerInfo.EndChar.ToString();
                    if (ImGui.InputText($"##{displayName}sEndChar", ref endChar, 1, ImGuiInputTextFlags.EnterReturnsTrue))
                        UnsavedNewEndChar = endChar;
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        if (string.IsNullOrEmpty(endChar) || endChar == " ")
                            UnsavedNewEndChar = ")";
                    }
                }
                else
                {
                    using (ImRaii.PushFont(UiBuilder.MonoFont)) ImGui.TextUnformatted(triggerInfo.EndChar.ToString());
                }
                UiSharedService.AttachToolTip($"Custom End Character that replaces the right enclosing bracket." +
                    Environment.NewLine + "Replaces the [ ) ] in Ex: [ TriggerPhrase (commandToExecute) ]");
            }

            // if no trigger phrase set, return.
            if (triggerInfo.TriggerPhrase.IsNullOrEmpty()) return;

            ImGui.Spacing();
            ImGui.Separator();

            if (!displayInRed)
            {
                string charaName = !isClient
                    ? $"<YourNameWorld> "
                    : $"<{_puppeteerHandler.StorageBeingEdited.CharacterName.Split(' ').First()}{_puppeteerHandler.StorageBeingEdited.CharacterWorld}> ";
                UiSharedService.ColorText("Example Usage:", ImGuiColors.ParsedPink);
                ImGui.TextWrapped(charaName + triggers[0] + " " +
                    _puppeteerHandler.SelectedPair?.UserPairOwnUniquePairPerms.StartChar +
                   " glamour apply Hogtied | p | [me] " +
                   _puppeteerHandler.SelectedPair?.UserPairOwnUniquePairPerms.EndChar);
            }
        }
    }


    private void DrawAliasItemBox(AliasTrigger aliasItem)
    {
        // push rounding window corners
        using var windowRounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 5f);
        // push a pink border color for the window border.
        using var borderColor = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var borderCol = ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink);
        // push a less transparent very dark grey background color.
        using var bgColor = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.25f, 0.2f, 0.2f, 0.4f));
        // create the child window.

        float height = ImGui.GetFrameHeight() * 3 + ImGui.GetStyle().ItemSpacing.Y * 2 + ImGui.GetStyle().WindowPadding.Y * 2;
        using (var patternResChild = ImRaii.Child("##PatternResult_" + aliasItem.InputCommand + aliasItem.OutputCommand, new Vector2(ImGui.GetContentRegionAvail().X, height), true, ImGuiWindowFlags.ChildWindow))
        {
            if (!patternResChild) return;

            using (ImRaii.Group())
            {
                _uiShared.IconText(FontAwesomeIcon.QuoteLeft, ImGuiColors.ParsedPink);
                ImUtf8.SameLineInner();
                UiSharedService.ColorText(aliasItem.InputCommand, ImGuiColors.ParsedPink);
                ImUtf8.SameLineInner();
                _uiShared.IconText(FontAwesomeIcon.QuoteRight, ImGuiColors.ParsedPink);
                ImGui.Separator();

                _uiShared.IconText(FontAwesomeIcon.LongArrowAltRight, ImGuiColors.ParsedPink);
                ImUtf8.SameLineInner();
                UiSharedService.TextWrapped(aliasItem.OutputCommand);
            }
        }
    }

    private struct TriggerData
    {
        public string NickOrAlias;
        public string UID;
        public string TriggerPhrase;
        public char StartChar;
        public char EndChar;
        public TriggerData(string nickOrAlias, string uid, string triggerPhrase, char startChar, char endChar)
        {
            NickOrAlias = nickOrAlias;
            UID = uid;
            TriggerPhrase = triggerPhrase;
            StartChar = startChar;
            EndChar = endChar;
        }
    }
}
