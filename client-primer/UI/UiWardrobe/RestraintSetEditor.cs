using Dalamud.Interface.Utility;
using Dalamud.Interface;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui.Text;
using OtterGui;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Dalamud.Plugin.Services;
using GagSpeak.Interop.IpcHelpers.Penumbra;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Textures;
using Penumbra.GameData.DataContainers;
using Dalamud.Interface.Utility.Raii;
using Penumbra.GameData.Data;
using System.Numerics;
using GagSpeak.WebAPI.Utils;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetEditor : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly WardrobeHandler _handler;
    private readonly DictStain _stainDictionary;
    private readonly ItemData _itemDictionary;
    private readonly DictBonusItems _bonusItemsDictionary;
    private readonly TextureService _textures;
    private readonly ModAssociations _relatedMods;
    private readonly PairManager _pairManager;
    private readonly IDataManager _gameData;

    public RestraintSetEditor(ILogger<RestraintSetEditor> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        WardrobeHandler handler, DictStain stains, ItemData items,
        DictBonusItems bonusItemsDictionary, TextureService textures,
        ModAssociations relatedMods, PairManager pairManager,
        IDataManager gameData) : base(logger, mediator)
    {
        _uiShared = uiSharedService;
        _handler = handler;
        _stainDictionary = stains;
        _itemDictionary = items;
        _bonusItemsDictionary = bonusItemsDictionary;
        _textures = textures;
        _gameData = gameData;
        _relatedMods = relatedMods;
        _pairManager = pairManager;

        // create a fresh instance of the restraint set 
        GameIconSize = new Vector2(2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);

        // setup the combos
        ItemCombos = EquipSlotExtensions.EqdpSlots
            .Select(e => new GameItemCombo(_gameData, e, _itemDictionary, logger))
            .ToArray();

        StainColorCombos = new StainColorCombo(ComboWidth - 20, _stainDictionary, logger);

        BonusItemCombos = BonusExtensions.AllFlags
            .Select(f => new BonusItemCombo(_gameData, f, _bonusItemsDictionary, logger))
            .ToArray();

        // subscribe to mediator
        Mediator.Subscribe<RestraintSetModified>(this, (msg) =>
        {
            // update the restraint set we are editing if the same restraint set that just got updated is the one we are modifying
            if(msg.RestraintSetIndex == _handler.SelectedSetIdx)
            {
                EditingRestraintSet = _handler.GetRestraintSet(msg.RestraintSetIndex).DeepClone();
            }
        });
    }

    private readonly GameItemCombo[] ItemCombos;
    private readonly StainColorCombo StainColorCombos;
    private readonly BonusItemCombo[] BonusItemCombos; // future proofing for potential multiples
    private string RefSearchString = string.Empty;
    private Vector2 GameIconSize;
    private const float ComboWidth = 200f;
    private float ItemComboLength;
    private string RefName = string.Empty;
    private string RefDescription = string.Empty;
    private RestraintSet EditingRestraintSet = null!;
    public void DrawRestraintSetEditor(Vector2 paddingHeight)
    {
        // if there are currently no created sets, then display in beg text that they must first create a set to edit one, and then return.
        if (_handler.GetRestraintSetsByName().Count == 0)
        {
            _uiShared.BigText("No Restraint Sets Created! Please Create a Set First.");
            return;
        }
        // check if the set is null, meaning it has not been set. If this is the case, update it to the selected set.
        if (EditingRestraintSet == null)
        {
            EditingRestraintSet = _handler.GetRestraintSet(_handler.SelectedSetIdx).DeepClone();
        }

        using (_ = ImRaii.Group())
        {
            // offer dropdown to select restraint set list.
            List<string> nameList = _handler.GetRestraintSetsByName();
            string defaultSelection = nameList.FirstOrDefault() ?? "No Restraint Sets Created!";

            // Draw the combo box with the default selected item and the action
            _uiShared.DrawComboSearchable("Select Restraint Set", 225f, ref RefSearchString,
                nameList, (i) => i, true,
            (i) =>
            {
                // Set the selected index to the selected item's index
                Logger.LogInformation($"Selected Set: {i}");
                int index = nameList.IndexOf(i);
                _handler.SelectedSetIdx = index;
                // update our edited restraint set to be the newly selected set
                EditingRestraintSet = _handler.GetRestraintSet(index).DeepClone();
            }, defaultSelection);


            string refName = EditingRestraintSet.Name;
            ImGui.SetNextItemWidth(225f);

            if (ImGui.InputTextWithHint($"Rename Set##RenameSetName", "Restraint Set Name...", ref refName, 48, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                EditingRestraintSet.Name = refName;
            }
            UiSharedService.AttachToolTip($"modify restraint set name here!");
        }

        var windowEndX = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth();
        var currentRightSide = windowEndX - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Save, "Revert Changes");
        ImGui.SameLine(currentRightSide);
        using (_ = ImRaii.Group())
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Save, "Update Outfit", _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Save, "Revert Changes")))
            {
                // add the new set to the config.
                _handler.UpdateRestraintSet(_handler.SelectedSetIdx, EditingRestraintSet);
            }

            // draw revert button at the same locatoin but right below that button
            if (_uiShared.IconTextButton(FontAwesomeIcon.Undo, "Revert Changes"))
            {
                // revert the changes to the selected set.
                EditingRestraintSet = _handler.GetRestraintSet(_handler.SelectedSetIdx).DeepClone();
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
                _relatedMods.DrawUnstoredSetTable(ref EditingRestraintSet, paddingHeight.Y);
            }
            associatedMods.Dispose();

            // store the current style for cell padding
            var cellPaddingCurrent = ImGui.GetStyle().CellPadding;
            // push Y cell padding.
            using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiShared.GetFontScalerFloat(), paddingHeight.Y)))
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
                EditingRestraintSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                // if we right click the icon, clear it
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    EditingRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(EditingRestraintSet.DrawData[slot].Slot);
                }
                ImGui.SameLine(0, 6);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawEquip(EditingRestraintSet.DrawData[slot].Slot, ItemComboLength);
                }
            }
            // i am dumb and dont know how to place adjustable divider lengths
            ImGui.TableNextColumn();
            //draw out the accessory slots
            foreach (var slot in EquipSlotExtensions.AccessorySlots)
            {
                using (var groupIcon = ImRaii.Group())
                {
                    EditingRestraintSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                    // if we right click the icon, clear it
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        EditingRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(EditingRestraintSet.DrawData[slot].Slot);
                    }
                }

                ImGui.SameLine(0, 6);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawEquip(EditingRestraintSet.DrawData[slot].Slot, ItemComboLength);
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

            var newWidth = ItemComboLength - _uiShared.GetIconButtonSize(FontAwesomeIcon.EyeSlash).X - ImUtf8.ItemInnerSpacing.X;
            // end of table, now draw the bonus items
            foreach (var slot in BonusExtensions.AllFlags)
            {
                EditingRestraintSet.BonusDrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                ImGui.SameLine(0, 6);
                DrawBonusItem(EditingRestraintSet.BonusDrawData[slot].Slot, newWidth);
                ImUtf8.SameLineInner();
                FontAwesomeIcon icon = EditingRestraintSet.BonusDrawData[slot].IsEnabled ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
                if (_uiShared.IconButton(icon))
                {
                    EditingRestraintSet.BonusDrawData[slot].IsEnabled = !EditingRestraintSet.BonusDrawData[slot].IsEnabled;
                }
                UiSharedService.AttachToolTip("Toggles Apply Style of Item." +
                    Environment.NewLine + "EYE Icon (Apply Mode) applies regardless of selected item. (nothing slots make the slot nothing)" +
                    Environment.NewLine + "EYE SLASH Icon (Overlay Mode) means that it only will apply the item if it is NOT an nothing slot.");
            }

            ImGui.TableNextColumn();

            string descriptiontext = EditingRestraintSet.Description;
            if (ImGui.InputTextMultiline("##description", ref descriptiontext, 500,
                ImGuiHelpers.ScaledVector2(width, GameIconSize.Y)))
            {
                // update the description
                EditingRestraintSet.Description = descriptiontext;
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
                var canSeeIcon = EditingRestraintSet.ViewAccess.IndexOf(pair.UserData.UID) == -1 ? FontAwesomeIcon.Times : FontAwesomeIcon.Check;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    string.Empty, false, true))
                    {
                        if (canSeeIcon == FontAwesomeIcon.Times)
                        {
                            // add them
                            EditingRestraintSet.ViewAccess.Add(pair.UserData.UID);
                            EditingRestraintSet.SetProperties[pair.UserData.UID] = new HardcoreSetProperties();
                        }
                        else
                        {
                            EditingRestraintSet.ViewAccess.Remove(pair.UserData.UID);
                            EditingRestraintSet.SetProperties.Remove(pair.UserData.UID);
                        }
                    }
                }
                ImGui.TableNextColumn();

                // draw the properties, but dont allow access if not in hardcore for them or if they are not in the list.
                if (!EditingRestraintSet.ViewAccess.Contains(pair.UserData.UID))
                {
                    ImGui.Text("Must Grant User View Access to Set Properties");
                }
                else if (EditingRestraintSet.ViewAccess.Contains(pair.UserData.UID) && !EditingRestraintSet.SetProperties.ContainsKey(pair.UserData.UID))
                {
                    // we have hit a case where we are editing a restraint with no saved hardcore properties, yet they have view access, so create one.
                    EditingRestraintSet.SetProperties[pair.UserData.UID] = new HardcoreSetProperties();
                }
                else
                {
                    // grab a quick reference variable
                    var properties = EditingRestraintSet.SetProperties[pair.UserData.UID];

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        // draw the properties
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.Socks.ToIconString(), _uiShared.GetIconButtonSize(FontAwesomeIcon.Socks)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.LegsRestrained = !properties.LegsRestrained;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Bound legs property for this set." +
                        Environment.NewLine + "Restricts use of any actions which rely on your legs to execute.");
                    ImGui.SameLine(0, 2);
                    _uiShared.BooleanToColoredIcon(properties.LegsRestrained, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.HandsBound.ToIconString(), _uiShared.GetIconButtonSize(FontAwesomeIcon.HandsBound)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.ArmsRestrained = !properties.ArmsRestrained;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Bound arms property for this set." +
                        Environment.NewLine + "Restricts use of any actions which rely on your arms to execute.");
                    ImGui.SameLine(0, 2);
                    _uiShared.BooleanToColoredIcon(properties.ArmsRestrained, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.CommentSlash.ToIconString(), _uiShared.GetIconButtonSize(FontAwesomeIcon.CommentSlash)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Gagged = !properties.Gagged;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Gagged property for this set." +
                        Environment.NewLine + "Restricts use of any actions which rely on your voice to execute.");
                    ImGui.SameLine(0, 2);
                    _uiShared.BooleanToColoredIcon(properties.Gagged, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.LowVision.ToIconString(), _uiShared.GetIconButtonSize(FontAwesomeIcon.LowVision)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Blindfolded = !properties.Blindfolded;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Blinded property for this set." +
                        Environment.NewLine + "Restricts use of any actions which rely on your sight to execute.");
                    ImGui.SameLine(0, 2);
                    _uiShared.BooleanToColoredIcon(properties.LegsRestrained, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.PersonCircleExclamation.ToIconString(), _uiShared.GetIconButtonSize(FontAwesomeIcon.PersonCircleExclamation)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Immobile = !properties.Immobile;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Immobile property for this set." +
                        Environment.NewLine + "You will become entirely unable to move while this is active (with exception of turning)");
                    ImGui.SameLine(0, 2);
                    _uiShared.BooleanToColoredIcon(properties.Immobile, false);

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.WeightHanging.ToIconString(), _uiShared.GetIconButtonSize(FontAwesomeIcon.WeightHanging)))
                            {
                                // TODO: Add Logic for this to notify changes
                                properties.Weighty = !properties.Weighty;
                            }
                        }
                    }
                    UiSharedService.AttachToolTip("Enables the Weighty property for this set." +
                        Environment.NewLine + "The Fastest movment you can perform while under this is RP walk.");
                    ImGui.SameLine(0, 2);
                    _uiShared.BooleanToColoredIcon(properties.Weighty, false);

                    StimulationDegree StimulationType = properties.LightStimulation
                        ? StimulationDegree.Light : properties.MildStimulation
                        ? StimulationDegree.Mild : properties.HeavyStimulation
                        ? StimulationDegree.Heavy : StimulationDegree.No;

                    using (ImRaii.Disabled(!pair.UserPairOwnUniquePairPerms.InHardcore))
                    {
                        ImGui.SameLine();
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            if (ImGui.Button(FontAwesomeIcon.Water.ToIconString(), _uiShared.GetIconButtonSize(FontAwesomeIcon.Water)))
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
                    if (StimulationType == StimulationDegree.No) { _uiShared.BooleanToColoredIcon(false, false); }
                    else if (StimulationType == StimulationDegree.Light) { _uiShared.BooleanToColoredIcon(properties.LightStimulation, false); }
                    else if (StimulationType == StimulationDegree.Mild) { _uiShared.BooleanToColoredIcon(properties.MildStimulation, false); }
                    else if (StimulationType == StimulationDegree.Heavy) { _uiShared.BooleanToColoredIcon(properties.HeavyStimulation, false); }
                }
            }
        }
    }

    // space for helper functions below
    public void DrawEquip(EquipSlot slot, float _comboLength)
    {
        using var id = ImRaii.PushId((int)EditingRestraintSet.DrawData[slot].Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var left = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        var ItemWidth = _comboLength - _uiShared.GetIconButtonSize(FontAwesomeIcon.EyeSlash).X - ImUtf8.ItemInnerSpacing.X;

        using var group = ImRaii.Group();
        DrawItem(out var label, right, left, slot, ItemWidth);
        ImUtf8.SameLineInner();
        FontAwesomeIcon icon = EditingRestraintSet.DrawData[slot].IsEnabled ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
        if (_uiShared.IconButton(icon))
        {
            EditingRestraintSet.DrawData[slot].IsEnabled = !EditingRestraintSet.DrawData[slot].IsEnabled;
        }
        UiSharedService.AttachToolTip("Toggles Apply Style of Item." +
            Environment.NewLine + "EYE Icon (Apply Mode) applies regardless of selected item. (nothing slots make the slot nothing)" +
            Environment.NewLine + "EYE SLASH Icon (Overlay Mode) means that it only will apply the item if it is NOT an nothing slot.");
        DrawStain(slot, _comboLength);
    }

    private void DrawItem(out string label, bool clear, bool open, EquipSlot slot, float width)
    {
        // draw the item combo.
        var combo = ItemCombos[EditingRestraintSet.DrawData[slot].Slot.ToIndex()];
        label = combo.Label;
        if (open)
        {
            GenericHelpers.OpenCombo($"##WardrobeCreateNewSetItem-{slot}");
            Logger.LogTrace($"{combo.Label} Toggled");
        }
        // draw the combo
        var change = combo.Draw(EditingRestraintSet.DrawData[slot].GameItem.Name,
            EditingRestraintSet.DrawData[slot].GameItem.ItemId, width, ComboWidth * 1.3f);

        // if we changed something
        if (change && !EditingRestraintSet.DrawData[slot].GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            Logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ItemId}] " +
                $"to {EditingRestraintSet.DrawData[slot].GameItem} [{EditingRestraintSet.DrawData[slot].GameItem.ItemId}]");
            // update the item to the new selection.
            EditingRestraintSet.DrawData[slot].GameItem = combo.CurrentSelection;
        }

        // if we right clicked
        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            // if we right click the item, clear it.
            Logger.LogTrace($"Item changed to {ItemIdVars.NothingItem(EditingRestraintSet.DrawData[slot].Slot)} " +
                $"[{ItemIdVars.NothingItem(EditingRestraintSet.DrawData[slot].Slot).ItemId}] " +
                $"from {EditingRestraintSet.DrawData[slot].GameItem} [{EditingRestraintSet.DrawData[slot].GameItem.ItemId}]");
            // clear the item.
            EditingRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(EditingRestraintSet.DrawData[slot].Slot);
        }
    }

    private void DrawBonusItem(BonusItemFlag flag, float width)
    {
        using var id = ImRaii.PushId((int)EditingRestraintSet.BonusDrawData[flag].Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        bool clear = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        bool open = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        // Assuming _bonusItemCombo is similar to ItemCombos but for bonus items
        var combo = BonusItemCombos[EditingRestraintSet.BonusDrawData[flag].Slot.ToIndex()];

        if (open)
            ImGui.OpenPopup($"##{combo.Label}");

        var change = combo.Draw(EditingRestraintSet.BonusDrawData[flag].GameItem.Name,
            EditingRestraintSet.BonusDrawData[flag].GameItem.Id,
            width, ComboWidth * 1.3f);

        if (change && !EditingRestraintSet.BonusDrawData[flag].GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            Logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ModelId}] " +
                $"to {EditingRestraintSet.BonusDrawData[flag].GameItem} [{EditingRestraintSet.BonusDrawData[flag].GameItem.ModelId}]");
            // change
            EditingRestraintSet.BonusDrawData[flag].GameItem = combo.CurrentSelection;
        }

        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            // Assuming a method to handle item reset or clear, similar to your DrawItem method
            Logger.LogTrace($"Item reset to default for slot {flag}");
            // reset it
            EditingRestraintSet.BonusDrawData[flag].GameItem = BonusItem.Empty(flag);
        }
    }

    private void DrawStain(EquipSlot slot, float width)
    {
        // fetch the correct stain from the stain data
        var widthStains = (width - ImUtf8.ItemInnerSpacing.X *
            (EditingRestraintSet.DrawData[slot].GameStain.Count - 1)) / EditingRestraintSet.DrawData[slot].GameStain.Count;

        // draw the stain combo for each of the 2 dyes (or just one)
        foreach (var (stainId, index) in EditingRestraintSet.DrawData[slot].GameStain.WithIndex())
        {
            using var id = ImUtf8.PushId(index);
            var found = _stainDictionary.TryGetValue(stainId, out var stain);
            // draw the stain combo.
            var change = StainColorCombos.Draw($"##stain{EditingRestraintSet.DrawData[slot].Slot}", stain.RgbaColor, stain.Name, found, stain.Gloss, widthStains);
            if (index < EditingRestraintSet.DrawData[slot].GameStain.Count - 1)
                ImUtf8.SameLineInner(); // instantly go to draw the next one if there are two stains

            // if we had a change made, update the stain data.
            if (change)
            {
                if (_stainDictionary.TryGetValue(StainColorCombos.CurrentSelection.Key, out stain))
                {
                    // if changed, change it.
                    EditingRestraintSet.DrawData[slot].GameStain = EditingRestraintSet.DrawData[slot].GameStain.With(index, stain.RowIndex);
                }
                else if (StainColorCombos.CurrentSelection.Key == Stain.None.RowIndex)
                {
                    // if set to none, reset it to default
                    EditingRestraintSet.DrawData[slot].GameStain = EditingRestraintSet.DrawData[slot].GameStain.With(index, Stain.None.RowIndex);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                // reset the stain to default
                EditingRestraintSet.DrawData[slot].GameStain = EditingRestraintSet.DrawData[slot].GameStain.With(index, Stain.None.RowIndex);
            }
        }
    }
}
