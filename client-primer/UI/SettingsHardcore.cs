using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Text;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Numerics;

namespace GagSpeak.UI;

public class SettingsHardcore
{
    private readonly ILogger<SettingsHardcore> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiShared;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly HardcoreHandler _hardcoreHandler;
    private readonly WardrobeHandler _blindfoldHandler;
    private readonly PairManager _pairManager;
    private readonly TextureService _textures;
    private readonly DictStain StainData;
    private readonly ItemData ItemData;
    private readonly IDataManager _gameData;

    private const float ComboWidth = 200;
    private Vector2 IconSize;
    private float ComboLength;
    private readonly GameItemCombo[] GameItemCombo;
    private readonly StainColorCombo StainCombo;

    public SettingsHardcore(ILogger<SettingsHardcore> logger, 
        GagspeakMediator mediator, ApiController apiController,
        UiSharedService uiShared, ClientConfigurationManager clientConfigs,
        HardcoreHandler hardcoreHandler, WardrobeHandler blindfoldHandler,
        PairManager pairManager, TextureService textures, DictStain stainData,
        ItemData itemData, IDataManager gameData)
    {
        _logger = logger;
        _mediator = mediator;
        _apiController = apiController;
        _uiShared = uiShared;
        _clientConfigs = clientConfigs;
        _hardcoreHandler = hardcoreHandler;
        _blindfoldHandler = blindfoldHandler;
        _pairManager = pairManager;
        _textures = textures;
        StainData = stainData;
        ItemData = itemData;
        _gameData = gameData;

        // create a new gameItemCombo for each equipment piece type, then store them into the array.
        GameItemCombo = EquipSlotExtensions.EqdpSlots.Select(e => new GameItemCombo(_gameData, e, ItemData, logger)).ToArray();
        StainCombo = new StainColorCombo(ComboWidth - 20, StainData, logger);
    }

    public void DrawHardcoreSettings()
    {
        if (ImGui.BeginTabBar("hardcoreSettingsTabBar"))
        {
            if (ImGui.BeginTabItem("Blindfold Item"))
            {
                DrawBlindfoldItem();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Lock 1st Person Whitelist"))
            {
                DrawBlindfoldSettings();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Forced To Stay Filters"))
            {
                DisplayTextButtons();
                ImGui.Spacing();
                DisplayTextNodes();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawBlindfoldItem()
    {
        // define icon size and combo length
        IconSize = new Vector2(3 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 2);
        ComboLength = ComboWidth * ImGuiHelpers.GlobalScale;

        // on the new line, lets draw out a group, containing the image, and the slot, item, and stain listings.
        var BlindfoldDrawData = _blindfoldHandler.GetBlindfoldDrawData();
        bool DrawDataChanged = false;
        using (var gagStorage = ImRaii.Group())
        {

            // draw out the listing for the slot, item, and stain(s). Also make sure that the bigtext it centered with the displayitem
            try
            {
                BlindfoldDrawData.GameItem.DrawIcon(_textures, IconSize, BlindfoldDrawData.Slot);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    _logger.LogTrace($"Blindfold changed to {ItemIdVars.NothingItem(BlindfoldDrawData.Slot)} [{ItemIdVars.NothingItem(BlindfoldDrawData.Slot).ItemId}] " +
                        $"from {BlindfoldDrawData.GameItem} [{BlindfoldDrawData.GameItem.ItemId}]");
                    BlindfoldDrawData.GameItem = ItemIdVars.NothingItem(BlindfoldDrawData.Slot);
                    DrawDataChanged = true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to draw gag icon.");
            }
            // right beside it, draw a secondary group of 3
            ImGui.SameLine(0, 6);
            using (var group = ImRaii.Group())
            {
                // display the wardrobe slot for this gag
                var refValue = Array.IndexOf(EquipSlotExtensions.EqdpSlots.ToArray(), BlindfoldDrawData.Slot);
                ImGui.SetNextItemWidth(ComboLength);
                if (ImGui.Combo(" Slot##WardrobeEquipSlot", ref refValue,
                    EquipSlotExtensions.EqdpSlots.Select(slot => slot.ToName()).ToArray(), EquipSlotExtensions.EqdpSlots.Count))
                {
                    // Update the selected slot when the combo box selection changes
                    BlindfoldDrawData.Slot = EquipSlotExtensions.EqdpSlots[refValue];
                    BlindfoldDrawData.GameItem = ItemIdVars.NothingItem(BlindfoldDrawData.Slot);
                    DrawDataChanged = true;
                }

                DrawDataChanged = DrawEquip(BlindfoldDrawData, GameItemCombo, StainCombo, StainData, ComboLength);
            }

            // if the data has changed, update it.
            if (DrawDataChanged)
            {
                _blindfoldHandler.SetBlindfoldDrawData(BlindfoldDrawData);
            }

            ImGui.Separator();
            _uiShared.BigText("Blindfold Type");
            var selectedBlindfoldType = _clientConfigs.GagspeakConfig.BlindfoldStyle;
            _uiShared.DrawCombo("Lace Style", 150f, Enum.GetValues<BlindfoldType>(), (type) => type.ToString(),
                (i) => { _clientConfigs.GagspeakConfig.BlindfoldStyle = i; _clientConfigs.Save(); }, selectedBlindfoldType);

            string filePath = _clientConfigs.GagspeakConfig.BlindfoldStyle switch
            {
                BlindfoldType.Light => "RequiredImages\\Blindfold_Light.png",
                BlindfoldType.Sensual => "RequiredImages\\Blindfold_Sensual.png",
                _ => "INVALID_FILE",
            };
            var previewImage = _uiShared.GetImageFromDirectoryFile(filePath);
            if ((previewImage is { } wrap))
            {
                ImGui.Image(wrap.ImGuiHandle, new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));
            }
        }
    }

    private void DrawBlindfoldSettings()
    { 
        DrawUidSearchFilter(ImGui.GetContentRegionAvail().X);
        using (var table = ImRaii.Table("blindfoldSettingsPerUID", 2, ImGuiTableFlags.RowBg, ImGui.GetContentRegionAvail()))
        {
            if (!table) return;

            ImGui.TableSetupColumn(" Nick/Alias/UID", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(" Lock 1st Person View", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Lock 1st Person View ").X);
            UiSharedService.AttachToolTip("Forces Player to stay in First Person Mode when enabled by any pairs who are checked");
            ImGui.TableHeadersRow();

            var PairList = _pairManager.DirectPairs
                .Where(pair => pair.UserPairOwnUniquePairPerms.InHardcore == true
                    && (string.IsNullOrEmpty(PairSearchString)
                    || pair.UserData.AliasOrUID.Contains(PairSearchString, StringComparison.OrdinalIgnoreCase)
                    || (pair.GetNickname() != null && pair.GetNickname().Contains(PairSearchString, StringComparison.OrdinalIgnoreCase))))
                .OrderBy(p => p.GetNickname() ?? p.UserData.AliasOrUID, StringComparer.OrdinalIgnoreCase);

            foreach (Pair pair in PairList)
            {
                using var tableId = ImRaii.PushId("userTable_" + pair.UserData.UID);

                ImGui.TableNextColumn(); // alias or UID of user.
                var nickname = pair.GetNickname();
                var text = nickname == null ? pair.UserData.AliasOrUID : nickname + " (" + pair.UserData.AliasOrUID + ")";
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(text);

                ImGui.TableNextColumn();
                // display nothing if they are not in the list, otherwise display a check
                var canSeeIcon = pair.UserPairOwnUniquePairPerms.ForceLockFirstPerson ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    "Force 1st Person mode when equipped by this pair.", false, true))
                    {
                        if (canSeeIcon == FontAwesomeIcon.Times)
                        {
                            _ = _apiController.UserUpdateOwnPairPerm(new UserPairPermChangeDto(pair.UserData,
                                new KeyValuePair<string, object>("ForceLockFirstPerson", true)));
                        }
                        else
                        {
                            _ = _apiController.UserUpdateOwnPairPerm(new UserPairPermChangeDto(pair.UserData,
                                new KeyValuePair<string, object>("ForceLockFirstPerson", false)));
                        }
                    }
                }
            }
        }
    }

    private void DisplayTextButtons()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        using var roundingStyle = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 0);

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.SearchPlus.ToIconString(), new Vector2(25, 25) * ImGuiHelpers.GlobalScale,
         "Add last seen as new entry interface as last entry" + Environment.NewLine
        + "(Must have active to record latest dialog option.)" + Environment.NewLine
        + "(Auto-selecting yes is not an allowed option)", false, true))
        {
            var newNode = new TextEntryNode()
            {
                Enabled = true,
                Label = _hardcoreHandler.LastSeenDialogText.Item1 + "-Label",
                Text = _hardcoreHandler.LastSeenDialogText.Item1,
                Options = _hardcoreHandler.LastSeenDialogText.Item2.ToArray(),
            };
            // if the list only has two elements
            if (_clientConfigs.GagspeakConfig.StoredEntriesFolder.Children.Count <= 6)
            {
                // add it to the end
                _clientConfigs.GagspeakConfig.StoredEntriesFolder.Children.Add(newNode);
                _clientConfigs.Save();
            }
            else
            {
                _clientConfigs.GagspeakConfig.StoredEntriesFolder.Children
                    .Insert(_clientConfigs.GagspeakConfig.StoredEntriesFolder.Children.Count - 1, newNode);
                _clientConfigs.Save();
            }
        }
        ImGui.SameLine();
        ImGuiUtil.DrawDisabledButton("Blockers List", new Vector2(ImGui.GetContentRegionAvail().X, ImGuiHelpers.GlobalScale * 25), "", true);
    }

    private void DisplayTextNodes()
    {
        if (_hardcoreHandler.StoredEntriesFolder.Children.Count == 0)
        {
            _clientConfigs.GagspeakConfig.StoredEntriesFolder.Children.Add(new TextEntryNode()
            {
                Enabled = false,
                Text = "NodeName",
                Label = "Placeholder Node, Add Last Selected Entry for proper node."
            });
            _clientConfigs.Save();
        }
        // if the list only has two elements (the required ones)
        if (_hardcoreHandler.StoredEntriesFolder.Children.Count <= 6)
        {
            // add it to the end
            _clientConfigs.GagspeakConfig.StoredEntriesFolder.Children.Add(new TextEntryNode()
            {
                Enabled = false,
                Text = "NodeName",
                Label = "Placeholder Node, Add Last Selected Entry for proper node."
            });
            _clientConfigs.Save();
        }

        foreach (var node in _hardcoreHandler.StoredEntriesFolder.Children.ToArray())
        {
            DisplayTextNode(node);
        }
    }
    private void DisplayTextNode(ITextNode node)
    {
        if (node is TextEntryNode textNode)
            DisplayTextEntryNode(textNode);
    }

    private void DisplayTextEntryNode(TextEntryNode node)
    {
        if (node.Enabled)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        if (!node.Enabled)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.5f, .5f, .5f, 1));

        ImGui.TreeNodeEx($"[{node.Text}] {node.Label}##{node.Name}-tree", ImGuiTreeNodeFlags.Leaf);
        ImGui.TreePop();

        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                node.Enabled = !node.Enabled;
                _clientConfigs.Save();
                return;
            }
            else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup($"{node.GetHashCode()}-popup");
            }
        }

        var disableElements = false;
        if (_hardcoreHandler.StoredEntriesFolder.Children.Count >= 6
        && (_hardcoreHandler.StoredEntriesFolder.Children[0] == node
         || _hardcoreHandler.StoredEntriesFolder.Children[1] == node
         || _hardcoreHandler.StoredEntriesFolder.Children[2] == node
         || _hardcoreHandler.StoredEntriesFolder.Children[3] == node
         || _hardcoreHandler.StoredEntriesFolder.Children[4] == node
         || _hardcoreHandler.StoredEntriesFolder.Children[5] == node))
        {
            disableElements = true;
        }
        TextNodePopup(node, disableElements);
    }

    private void TextNodePopup(TextEntryNode node, bool disableElements = false)
    {
        var style = ImGui.GetStyle();
        var newItemSpacing = new Vector2(style.ItemSpacing.X / 2, style.ItemSpacing.Y);
        if (ImGui.BeginPopup($"{node.GetHashCode()}-popup"))
        {
            if (node is TextEntryNode entryNode)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, newItemSpacing);
                try
                {
                    var enabled = entryNode.Enabled;
                    if (disableElements) { ImGui.BeginDisabled(); }
                    try
                    {
                        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.TrashAlt.ToIconString(), new Vector2(),
                        "Delete Custom Addition", false, true))
                        {
                            if (_hardcoreHandler.TryFindParent(node, out var parentNode))
                            {
                                parentNode!.Children.Remove(node);
                                // if the new size is now just 2 contents
                                if (parentNode.Children.Count == 0)
                                {
                                    // create a new blank one
                                    parentNode.Children.Add(new TextEntryNode()
                                    {
                                        Enabled = false,
                                        Text = "NodeName (Placeholder Node)",
                                        Label = "Add Last Selected Entry for proper node."
                                    });
                                }
                                _clientConfigs.Save();
                            }
                        }
                    }
                    finally { if (disableElements) { ImGui.EndDisabled(); } }
                    // Define the options for the dropdown menu
                    // Define the options for the dropdown menu
                    string[] options = entryNode.Options.ToArray(); // Use the node's options list
                    int currentOption = entryNode.SelectThisIndex; // Set the current option based on the SelectThisIndex property

                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 200);
                    // Create the dropdown menu
                    if (disableElements) { ImGui.BeginDisabled(); }
                    try
                    {
                        if (ImGui.Combo("##Options", ref currentOption, options, options.Length))
                        {
                            // Update the IsYes property based on the selected option
                            entryNode.SelectThisIndex = currentOption;
                            // the list of options contains the entry "Yes"
                            if (options[currentOption] == "Yes")
                            {
                                // select a different option within bounds
                                if (currentOption + 1 < options.Length)
                                {
                                    entryNode.SelectThisIndex = currentOption + 1;
                                }
                                else
                                {
                                    entryNode.SelectThisIndex = 0;
                                }
                            }
                            _clientConfigs.Save();
                        }
                        if (ImGui.IsItemHovered()) { ImGui.SetTooltip("The option to automatically select. Yes is always disabled"); }
                    }
                    finally { if (disableElements) { ImGui.EndDisabled(); } }

                    ImGui.SameLine();
                    if (disableElements) { ImGui.BeginDisabled(); }
                    try
                    {
                        if (ImGui.Checkbox("Enabled", ref enabled))
                        {
                            entryNode.Enabled = enabled;
                            _clientConfigs.Save();
                        }
                    }
                    finally { if (disableElements) { ImGui.EndDisabled(); } }
                    // draw the text input
                    if (disableElements) { ImGui.BeginDisabled(); }
                    try
                    {
                        var matchText = entryNode.Text;
                        if (entryNode.Text != "") { ImGui.BeginDisabled(); }
                        try
                        {
                            ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 225);
                            if (ImGui.InputText($"Node Name##{node.Name}-matchTextLebel", ref matchText, 10_000, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
                            {
                                entryNode.Label = matchText;
                                _clientConfigs.Save();
                            }
                        }
                        finally { if (entryNode.Text != "") { ImGui.EndDisabled(); } }
                        var matchText2 = entryNode.Label;
                        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 225);
                        if (ImGui.InputText($"Node Label##{node.Name}-matchText", ref matchText2, 10_000, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            entryNode.Label = matchText2;
                            _clientConfigs.Save();
                        }
                    }
                    finally { if (disableElements) { ImGui.EndDisabled(); } }
                }
                finally
                {
                    ImGui.PopStyleVar();
                }
            }
            ImGui.EndPopup();
        }
    }


    private LowerString PairSearchString = LowerString.Empty;
    public void DrawUidSearchFilter(float availableWidth)
    {
        var buttonSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Ban, "Clear");
        ImGui.SetNextItemWidth(availableWidth - buttonSize - ImGui.GetStyle().ItemInnerSpacing.X);
        string filter = PairSearchString;
        if (ImGui.InputTextWithHint("##filter", "Filter for UID/notes", ref filter, 255))
        {
            PairSearchString = filter;
        }
        ImUtf8.SameLineInner();
        using var disabled = ImRaii.Disabled(string.IsNullOrEmpty(PairSearchString));
        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Clear"))
        {
            PairSearchString = string.Empty;
        }
    }

    public bool DrawEquip(EquipDrawData blindfold, GameItemCombo[] _gameItemCombo, StainColorCombo _stainCombo, DictStain _stainData, float _comboLength)
    {
        using var id = ImRaii.PushId((int)blindfold.Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var left = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        bool itemChange = false;
        bool stainChange = false;

        using var group = ImRaii.Group();
        itemChange = DrawItem(out var label, right, left, _comboLength, _gameItemCombo, blindfold);
        stainChange = DrawStain(_comboLength, _stainCombo, _stainData, blindfold);

        return itemChange || stainChange;
    }

    private bool DrawItem(out string label, bool clear, bool open, float width,
    GameItemCombo[] _gameItemCombo, EquipDrawData blindfold)
    {
        // draw the item combo.
        var combo = _gameItemCombo[blindfold.Slot.ToIndex()];
        label = combo.Label;
        if (open)
        {
            GenericHelpers.OpenCombo($"##BlindfoldItem{blindfold.GameItem.Name}{combo.Label}");
            _logger.LogTrace($"{combo.Label} Toggled");
        }
        // draw the combo
        var change = combo.Draw(blindfold.GameItem.Name, blindfold.GameItem.ItemId, width, ComboWidth, " Item");

        // conditionals to detect for changes in the combo's
        if (change && !blindfold.GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            _logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ItemId}] " +
                $"to {blindfold.GameItem} [{blindfold.GameItem.ItemId}]");
            blindfold.GameItem = combo.CurrentSelection;
            change = true;
        }

        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _logger.LogTrace($"Item changed to {ItemIdVars.NothingItem(blindfold.Slot)} [{ItemIdVars.NothingItem(blindfold.Slot).ItemId}] " +
                $"from {blindfold.GameItem} [{blindfold.GameItem.ItemId}]");
            blindfold.GameItem = ItemIdVars.NothingItem(blindfold.Slot);
            change = true;
        }

        return change;
    }

    private bool DrawStain(float width, StainColorCombo _stainCombo, DictStain _stainData, EquipDrawData blindfold)
    {
        // fetch the correct stain from the stain data
        var widthStains = (width - ImUtf8.ItemInnerSpacing.X * (blindfold.GameStain.Count - 1)) / blindfold.GameStain.Count;

        bool dyeChanged = false;

        foreach (var (stainId, index) in blindfold.GameStain.WithIndex())
        {
            using var id = ImUtf8.PushId(index);
            var found = _stainData.TryGetValue(stainId, out var stain);
            // draw the stain combo.
            var change = _stainCombo.Draw($"##stain{blindfold.Slot}", stain.RgbaColor, stain.Name, found, stain.Gloss, widthStains);
            if (index < blindfold.GameStain.Count - 1)
                ImUtf8.SameLineInner(); // instantly go to draw the next one.

            // if we had a change made, update the stain data.
            if (change)
            {
                if (_stainData.TryGetValue(_stainCombo.CurrentSelection.Key, out stain))
                {
                    // if changed, change it.
                    blindfold.GameStain = blindfold.GameStain.With(index, stain.RowIndex);
                    change = true;
                }
                else if (_stainCombo.CurrentSelection.Key == Stain.None.RowIndex)
                {
                    // if set to none, reset it to default
                    blindfold.GameStain = blindfold.GameStain.With(index, Stain.None.RowIndex);
                    change = true;
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                // reset the stain to default
                blindfold.GameStain = blindfold.GameStain.With(index, Stain.None.RowIndex);
                change = true;
            }

            dyeChanged |= change;

        }
        ImGui.SameLine();
        ImGui.Text(" Dyes");
        return dyeChanged;
    }
}
