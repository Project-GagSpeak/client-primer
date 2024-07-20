using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Interop.IpcHelpers.Penumbra;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using ImGuiNET;
using ImGuizmoNET;
using OtterGui;
using OtterGui.Text;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Drawing.Text;
using System.Numerics;


namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetCreator : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly WardrobeHandler _handler;
    private readonly DictStain _stainDictionary;
    private readonly ItemData _itemDictionary;
    private readonly DictBonusItems _bonusItemsDictionary;
    private readonly TextureService _textures;
    private readonly ModAssociations _relatedMods;
    private readonly PairManager _pairManager;
    private readonly IDataManager _gameData;
    public RestraintSetCreator(ILogger<RestraintSetCreator> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        WardrobeHandler handler, DictStain stains, ItemData items,
        DictBonusItems bonusItemsDictionary, TextureService textures,
        ModAssociations relatedMods, PairManager pairManager,
        IDataManager gameData) : base(logger, mediator)
    {
        _uiSharedService = uiSharedService;
        _handler = handler;
        _stainDictionary = stains;
        _itemDictionary = items;
        _bonusItemsDictionary = bonusItemsDictionary;
        _textures = textures;
        _gameData = gameData;
        _relatedMods = relatedMods;
        _pairManager = pairManager;

        // create a fresh instance of the restraint set object
        NewRestraintSet = new RestraintSet();

        GameIconSize = new Vector2(2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);

        // setup the combos
        ItemCombos = EquipSlotExtensions.EqdpSlots
            .Select(e => new GameItemCombo(_gameData, e, _itemDictionary, logger))
            .ToArray();

        StainColorCombos = new StainColorCombo(ComboWidth - 20, _stainDictionary, logger);

        BonusItemCombos = BonusExtensions.AllFlags
            .Select(f => new BonusItemCombo(_gameData, f, _bonusItemsDictionary, logger))
            .ToArray();
    }

    private readonly GameItemCombo[] ItemCombos;
    private readonly StainColorCombo StainColorCombos;
    private readonly BonusItemCombo[] BonusItemCombos; // future proofing for potential multiples
    private Vector2 GameIconSize;
    private const float ComboWidth = 200f;
    private float ItemComboLength;
    private RestraintSet NewRestraintSet = null!;
    private string RefName = string.Empty;
    private string RefDescription = string.Empty;
    public void DrawRestraintSetCreator(Vector2 paddingHeight)
    {
        using (var groupIcon = ImRaii.Group())
        {
            using (_uiSharedService.UidFont.Push())
            {
                string refName = NewRestraintSet.Name;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.7f);

                if (ImGui.InputTextWithHint($"##NameText", "Restraint Set Name...", ref refName, 48, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    NewRestraintSet.Name = refName;
                }
            }
            UiSharedService.AttachToolTip($"Gives the Restraint Set a name!");

            var windowEndX = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth();
            var currentRightSide = windowEndX - _uiSharedService.GetIconTextButtonSize(FontAwesomeIcon.Save, "Add New Restraint Set");
            ImGui.AlignTextToFramePadding();
            ImGui.SameLine(currentRightSide);
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.Save, "Add New Restraint Set"))
            {
                // add the new set to the config.
                _handler.AddRestraintSet(NewRestraintSet);
                // we need to prevent issues where we run into duplicate references when we create a new set.
                NewRestraintSet = new RestraintSet();
            }
        }
        ImGui.Separator();

        // create a tab bar for the display
        using var tabBar = ImRaii.TabBar("Outfit_Creator");

        if (tabBar)
        {
            // create glamour tab (applying the visuals)
            var glamourTab = ImRaii.TabItem("Appearance / Glamour");
            if (glamourTab)
            {
                DrawAppearance();
            }
            glamourTab.Dispose();

            var associatedMods = ImRaii.TabItem("Associated Mods");
            if (associatedMods)
            {
                _relatedMods.DrawUnstoredSetTable(ref NewRestraintSet, paddingHeight.Y);
            }
            associatedMods.Dispose();

            // store the current style for cell padding
            var cellPaddingCurrent = ImGui.GetStyle().CellPadding;
            // push Y cell padding.
            using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiSharedService.GetFontScalerFloat(), paddingHeight.Y)))
            {
                var visibilityAccess = ImRaii.TabItem("Pair Visibility & Hardcore Properties");
                if (visibilityAccess)
                {
                    DrawVisibilityAndProperties();
                }
                visibilityAccess.Dispose();
            }
        }
    }

    private void DrawAppearance()
    {
        ItemComboLength = ComboWidth * ImGuiHelpers.GlobalScale;
        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        using (var table2 = ImRaii.Table("RestraintEquipSelection", 2, ImGuiTableFlags.RowBg))
        {
            if (!table2) return;
            // Create the headers for the table
            var width = ItemComboLength + GameIconSize.X + itemSpacing.X;
            // setup the columns
            ImGui.TableSetupColumn("EquipmentSlots", ImGuiTableColumnFlags.WidthFixed, width);
            ImGui.TableSetupColumn("AccessorySlots", ImGuiTableColumnFlags.WidthStretch);

            // draw out the equipment slots
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            int i = 0;
            foreach (var slot in EquipSlotExtensions.EquipmentSlots)
            {
                NewRestraintSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                // if we right click the icon, clear it
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    NewRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(NewRestraintSet.DrawData[slot].Slot);
                }
                ImGui.SameLine(0, 6);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawEquip(NewRestraintSet.DrawData[slot].Slot, ItemComboLength);
                }
            }
            // i am dumb and dont know how to place adjustable divider lengths
            ImGui.TableNextColumn();
            //draw out the accessory slots
            foreach (var slot in EquipSlotExtensions.AccessorySlots)
            {
                using (var groupIcon = ImRaii.Group())
                {
                    NewRestraintSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                    // if we right click the icon, clear it
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        NewRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(NewRestraintSet.DrawData[slot].Slot);
                    }
                }

                ImGui.SameLine(0, 6);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawEquip(NewRestraintSet.DrawData[slot].Slot, ItemComboLength);
                }
            }
        }
        using (var lowerTable = ImRaii.Table("BonusItemDescription", 2, ImGuiTableFlags.None))
        {
            // setup the columns
            var width = ItemComboLength + GameIconSize.X + itemSpacing.X;
            ImGui.TableSetupColumn("BonusItem", ImGuiTableColumnFlags.WidthFixed, width);
            ImGui.TableSetupColumn("DescriptionField", ImGuiTableColumnFlags.WidthStretch);

            // draw out the equipment slots
            ImGui.TableNextRow(); ImGui.TableNextColumn();

            var newWidth = ItemComboLength - _uiSharedService.GetIconButtonSize(FontAwesomeIcon.EyeSlash).X - ImUtf8.ItemInnerSpacing.X;
            // end of table, now draw the bonus items
            foreach (var slot in BonusExtensions.AllFlags)
            {
                NewRestraintSet.BonusDrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                ImGui.SameLine(0, 6);
                DrawBonusItem(NewRestraintSet.BonusDrawData[slot].Slot, newWidth);
                ImUtf8.SameLineInner();
                FontAwesomeIcon icon = NewRestraintSet.BonusDrawData[slot].IsEnabled ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
                if (_uiSharedService.IconButton(icon))
                {
                    NewRestraintSet.BonusDrawData[slot].IsEnabled = !NewRestraintSet.BonusDrawData[slot].IsEnabled;
                }
                UiSharedService.AttachToolTip("Toggles Apply Style of Item." +
                    Environment.NewLine + "EYE Icon (Apply Mode) applies regardless of selected item. (nothing slots make the slot nothing)" +
                    Environment.NewLine + "EYE SLASH Icon (Overlay Mode) means that it only will apply the item if it is NOT an nothing slot.");
            }

            ImGui.TableNextColumn();

            string descriptiontext = NewRestraintSet.Description;
            if(ImGui.InputTextMultiline("##description", ref descriptiontext, 500, 
                ImGuiHelpers.ScaledVector2(width, GameIconSize.Y)))
            {
                // update the description
                NewRestraintSet.Description = descriptiontext;
            }
        }
    }

    public enum StimulationDegree { No, Light, Mild, Heavy }

    private void DrawVisibilityAndProperties()
    {
        using var table = ImRaii.Table("userListForVisibility", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.ScrollY);
        if (table)
        {
            ImGui.TableSetupColumn("Alias/UID", ImGuiTableColumnFlags.None, 2f);
            ImGui.TableSetupColumn("Access", ImGuiTableColumnFlags.None, .75f);
            ImGui.TableSetupColumn("Enabled Properties when Applied by Pair.", ImGuiTableColumnFlags.None, 7.25f);
            ImGui.TableHeadersRow();

            var PairList = _pairManager.DirectPairs;

            foreach (Pair pair in PairList.OrderBy(p => p.GetNickname() ?? p.UserData.AliasOrUID, StringComparer.OrdinalIgnoreCase))
            {
                using var tableId = ImRaii.PushId("userTable_" + pair.UserData.UID);

                ImGui.TableNextColumn(); // alias or UID of user.
                var nickname = pair.GetNickname();
                var text = nickname == null ? pair.UserData.AliasOrUID : nickname;
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(text);

                ImGui.TableNextColumn();
                // display nothing if they are not in the list, otherwise display a check
                var canSeeIcon = NewRestraintSet.ViewAccess.IndexOf(pair.UserData.UID) == -1 ? FontAwesomeIcon.Times : FontAwesomeIcon.Check;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    string.Empty, false, true))
                    {
                        if(canSeeIcon == FontAwesomeIcon.Times)
                        { 
                            // add them
                            NewRestraintSet.ViewAccess.Add(pair.UserData.UID);
                            NewRestraintSet.SetProperties[pair.UserData.UID] = new HardcoreSetProperties();
                        }
                        else
                        {
                            NewRestraintSet.ViewAccess.Remove(pair.UserData.UID);
                            NewRestraintSet.SetProperties.Remove(pair.UserData.UID);
                        }
                    }
                }
                ImGui.TableNextColumn();

                // draw the properties, but dont allow access if not in hardcore for them or if they are not in the list.
                if (!NewRestraintSet.ViewAccess.Contains(pair.UserData.UID))
                {
                    ImGui.Text("Must Grant User View Access to Set Properties");
                }
                else
                {
                    // grab a quick reference variable
                    var properties = NewRestraintSet.SetProperties[pair.UserData.UID];

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        // draw the properties
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.Socks.ToIconString(), _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Socks)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.LegsRestrained = !properties.LegsRestrained;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Bound legs property for this set."+
                        Environment.NewLine + "Restricts use of any actions which rely on your legs to execute.");
                    ImGui.SameLine(0,2);
                    _uiSharedService.BooleanToColoredIcon(properties.LegsRestrained, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.HandsBound.ToIconString(), _uiSharedService.GetIconButtonSize(FontAwesomeIcon.HandsBound)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.ArmsRestrained = !properties.ArmsRestrained;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Bound arms property for this set." +
                        Environment.NewLine + "Restricts use of any actions which rely on your arms to execute.");
                    ImGui.SameLine(0, 2);
                    _uiSharedService.BooleanToColoredIcon(properties.ArmsRestrained, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.CommentSlash.ToIconString(), _uiSharedService.GetIconButtonSize(FontAwesomeIcon.CommentSlash)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Gagged = !properties.Gagged;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Gagged property for this set." +
                        Environment.NewLine + "Restricts use of any actions which rely on your voice to execute.");
                    ImGui.SameLine(0, 2);
                    _uiSharedService.BooleanToColoredIcon(properties.Gagged, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.LowVision.ToIconString(), _uiSharedService.GetIconButtonSize(FontAwesomeIcon.LowVision)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Blindfolded = !properties.Blindfolded;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Blinded property for this set." +
                        Environment.NewLine + "Restricts use of any actions which rely on your sight to execute.");
                    ImGui.SameLine(0, 2);
                    _uiSharedService.BooleanToColoredIcon(properties.LegsRestrained, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.PersonCircleExclamation.ToIconString(), _uiSharedService.GetIconButtonSize(FontAwesomeIcon.PersonCircleExclamation)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Immobile = !properties.Immobile;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Immobile property for this set." +
                        Environment.NewLine + "You will become entirely unable to move while this is active (with exception of turning)");
                    ImGui.SameLine(0, 2);
                    _uiSharedService.BooleanToColoredIcon(properties.Immobile, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.WeightHanging.ToIconString(), _uiSharedService.GetIconButtonSize(FontAwesomeIcon.WeightHanging)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Weighty = !properties.Weighty;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Weighty property for this set." +
                        Environment.NewLine + "The Fastest movment you can perform while under this is RP walk.");
                    ImGui.SameLine(0, 2);
                    _uiSharedService.BooleanToColoredIcon(properties.Weighty, false);

                    StimulationDegree StimulationType = properties.LightStimulation
                        ? StimulationDegree.Light : properties.MildStimulation
                        ? StimulationDegree.Mild : properties.HeavyStimulation
                        ? StimulationDegree.Heavy : StimulationDegree.No;

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.Water.ToIconString(), _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Water)))
                            {
                                // Increment the StimulationLevel, wrapping back to None after Heavy
                                if (properties.LightStimulation)
                                {
                                    properties.LightStimulation = false;
                                    properties.MildStimulation = true;
                                }
                                else if (properties.MildStimulation)
                                {
                                    properties.MildStimulation = false;
                                    properties.HeavyStimulation = true;
                                }
                                else if (properties.HeavyStimulation)
                                {
                                    properties.HeavyStimulation = false;
                                }
                                else
                                {
                                    properties.LightStimulation = true;
                                }
                                // TODO: Add Logic for this to notify changes
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Stimulation property for this set." +
                        Environment.NewLine + "The Stimulation property will slow down the cast time of any action requiring focus or concentration.");

                    ImUtf8.SameLineInner();
                    if (StimulationType == StimulationDegree.No) { _uiSharedService.BooleanToColoredIcon(false, false); }
                    else if (StimulationType == StimulationDegree.Light) { _uiSharedService.BooleanToColoredIcon(properties.LightStimulation, false); }
                    else if (StimulationType == StimulationDegree.Mild) { _uiSharedService.BooleanToColoredIcon(properties.MildStimulation, false); }
                    else if (StimulationType == StimulationDegree.Heavy) { _uiSharedService.BooleanToColoredIcon(properties.HeavyStimulation, false); }
                }
            }
        }
    }

    // space for helper functions below
    public void DrawEquip(EquipSlot slot, float _comboLength)
    {
        using var id = ImRaii.PushId((int)NewRestraintSet.DrawData[slot].Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var left = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        var ItemWidth = _comboLength - _uiSharedService.GetIconButtonSize(FontAwesomeIcon.EyeSlash).X - ImUtf8.ItemInnerSpacing.X;

        using var group = ImRaii.Group();
        DrawItem(out var label, right, left, slot, ItemWidth);
        ImUtf8.SameLineInner();
        FontAwesomeIcon icon = NewRestraintSet.DrawData[slot].IsEnabled ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
        if (_uiSharedService.IconButton(icon))
        {
            NewRestraintSet.DrawData[slot].IsEnabled = !NewRestraintSet.DrawData[slot].IsEnabled;
        }
        UiSharedService.AttachToolTip("Toggles Apply Style of Item." +
            Environment.NewLine + "EYE Icon (Apply Mode) applies regardless of selected item. (nothing slots make the slot nothing)" +
            Environment.NewLine + "EYE SLASH Icon (Overlay Mode) means that it only will apply the item if it is NOT an nothing slot.");
        DrawStain(slot, _comboLength);
    }

    private void DrawItem(out string label, bool clear, bool open, EquipSlot slot, float width)
    {
        // draw the item combo.
        var combo = ItemCombos[NewRestraintSet.DrawData[slot].Slot.ToIndex()];
        label = combo.Label;
        if (open)
        {
            GenericHelpers.OpenCombo($"##WardrobeCreateNewSetItem-{slot}");
            Logger.LogTrace($"{combo.Label} Toggled");
        }
        // draw the combo
        var change = combo.Draw(NewRestraintSet.DrawData[slot].GameItem.Name,
            NewRestraintSet.DrawData[slot].GameItem.ItemId, width, ComboWidth * 1.3f);

        // if we changed something
        if (change && !NewRestraintSet.DrawData[slot].GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            Logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ItemId}] " +
                $"to {NewRestraintSet.DrawData[slot].GameItem} [{NewRestraintSet.DrawData[slot].GameItem.ItemId}]");
            // update the item to the new selection.
            NewRestraintSet.DrawData[slot].GameItem = combo.CurrentSelection;
        }

        // if we right clicked
        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            // if we right click the item, clear it.
            Logger.LogTrace($"Item changed to {ItemIdVars.NothingItem(NewRestraintSet.DrawData[slot].Slot)} " +
                $"[{ItemIdVars.NothingItem(NewRestraintSet.DrawData[slot].Slot).ItemId}] " +
                $"from {NewRestraintSet.DrawData[slot].GameItem} [{NewRestraintSet.DrawData[slot].GameItem.ItemId}]");
            // clear the item.
            NewRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(NewRestraintSet.DrawData[slot].Slot);
        }
    }

    private void DrawBonusItem(BonusItemFlag flag, float width)
    {
        using var id = ImRaii.PushId((int)NewRestraintSet.BonusDrawData[flag].Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        bool clear = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        bool open = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        // Assuming _bonusItemCombo is similar to ItemCombos but for bonus items
        var combo = BonusItemCombos[NewRestraintSet.BonusDrawData[flag].Slot.ToIndex()];

        if (open)
            ImGui.OpenPopup($"##{combo.Label}");

        var change = combo.Draw(NewRestraintSet.BonusDrawData[flag].GameItem.Name,
            NewRestraintSet.BonusDrawData[flag].GameItem.Id,
            width, ComboWidth * 1.3f);

        if (change && !NewRestraintSet.BonusDrawData[flag].GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            Logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ModelId}] " +
                $"to {NewRestraintSet.BonusDrawData[flag].GameItem} [{NewRestraintSet.BonusDrawData[flag].GameItem.ModelId}]");
            // change
            NewRestraintSet.BonusDrawData[flag].GameItem = combo.CurrentSelection;
        }

        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            // Assuming a method to handle item reset or clear, similar to your DrawItem method
            Logger.LogTrace($"Item reset to default for slot {flag}");
            // reset it
            NewRestraintSet.BonusDrawData[flag].GameItem = BonusItem.Empty(flag);
        }
    }

    private void DrawStain(EquipSlot slot, float width)
    {
        // fetch the correct stain from the stain data
        var widthStains = (width - ImUtf8.ItemInnerSpacing.X *
            (NewRestraintSet.DrawData[slot].GameStain.Count - 1)) / NewRestraintSet.DrawData[slot].GameStain.Count;

        // draw the stain combo for each of the 2 dyes (or just one)
        foreach (var (stainId, index) in NewRestraintSet.DrawData[slot].GameStain.WithIndex())
        {
            using var id = ImUtf8.PushId(index);
            var found = _stainDictionary.TryGetValue(stainId, out var stain);
            // draw the stain combo.
            var change = StainColorCombos.Draw($"##stain{NewRestraintSet.DrawData[slot].Slot}", stain.RgbaColor, stain.Name, found, stain.Gloss, widthStains);
            if (index < NewRestraintSet.DrawData[slot].GameStain.Count - 1)
                ImUtf8.SameLineInner(); // instantly go to draw the next one if there are two stains

            // if we had a change made, update the stain data.
            if (change)
            {
                if (_stainDictionary.TryGetValue(StainColorCombos.CurrentSelection.Key, out stain))
                {
                    // if changed, change it.
                    NewRestraintSet.DrawData[slot].GameStain = NewRestraintSet.DrawData[slot].GameStain.With(index, stain.RowIndex);
                }
                else if (StainColorCombos.CurrentSelection.Key == Stain.None.RowIndex)
                {
                    // if set to none, reset it to default
                    NewRestraintSet.DrawData[slot].GameStain = NewRestraintSet.DrawData[slot].GameStain.With(index, Stain.None.RowIndex);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                // reset the stain to default
                NewRestraintSet.DrawData[slot].GameStain = NewRestraintSet.DrawData[slot].GameStain.With(index, Stain.None.RowIndex);
            }
        }
    }
}
