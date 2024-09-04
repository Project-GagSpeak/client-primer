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
using System.Windows.Forms.VisualStyles;
using Dalamud.Interface.Colors;
using GagspeakAPI.Data.Character;
using static System.ComponentModel.Design.ObjectSelectorEditor;
using GagSpeak.Interop.IpcHelpers.Moodles;
using GagSpeak.Services;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetEditor : IMediatorSubscriber
{
    private readonly ILogger<RestraintSetEditor> Logger;
    private readonly UiSharedService _uiShared;
    private readonly WardrobeHandler _handler;
    private readonly DictStain _stainDictionary;
    private readonly ItemData _itemDictionary;
    private readonly DictBonusItems _bonusItemsDictionary;
    private readonly TextureService _textures;
    private readonly ModAssociations _relatedMods;
    private readonly MoodlesAssociations _relatedMoodles;
    private readonly PairManager _pairManager;
    private readonly IDataManager _gameData;
    public GagspeakMediator Mediator { get; init; }

    public RestraintSetEditor(ILogger<RestraintSetEditor> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        WardrobeHandler handler, DictStain stains, ItemData items,
        DictBonusItems bonusItemsDictionary, TextureService textures,
        ModAssociations relatedMods, MoodlesAssociations relatedMoodles,
        PairManager pairManager, IDataManager gameData)
    {
        Logger = logger;
        Mediator = mediator;
        _uiShared = uiSharedService;
        _handler = handler;
        _stainDictionary = stains;
        _itemDictionary = items;
        _bonusItemsDictionary = bonusItemsDictionary;
        _textures = textures;
        _gameData = gameData;
        _relatedMods = relatedMods;
        _relatedMoodles = relatedMoodles;
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

        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) => LastCreatedCharacterData = msg.CharacterIPCData);
    }


    // Info related to the person we are inspecting.
    private CharacterIPCData LastCreatedCharacterData = null!;
    private readonly GameItemCombo[] ItemCombos;
    private readonly StainColorCombo StainColorCombos;
    private readonly BonusItemCombo[] BonusItemCombos; // future proofing for potential multiples
    private string RefSearchString = string.Empty;
    private Vector2 GameIconSize;
    private const float ComboWidth = 200f;
    private float ItemComboLength;

    // Can pass in the set to create, or the set to edit. Either will result in appropriate action.
    public void DrawRestraintSetEditor(RestraintSet refRestraint, Vector2 cellPadding)
    {
        // create a tab bar for the display
        using var tabBar = ImRaii.TabBar("Outfit_Editor");

        if (tabBar)
        {
            var infoTab = ImRaii.TabItem("Info");
            if (infoTab)
            {
                DrawInfo(refRestraint);
            }
            infoTab.Dispose();

            // create glamour tab (applying the visuals)
            var glamourTab = ImRaii.TabItem("Appearance");
            if (glamourTab)
            {
                DrawAppearance(refRestraint);
            }
            glamourTab.Dispose();

            var associatedMods = ImRaii.TabItem("Mods");
            if (associatedMods)
            {
                _relatedMods.DrawUnstoredSetTable(refRestraint, cellPadding.Y);
            }
            associatedMods.Dispose();

            var associatedMoodles = ImRaii.TabItem("Moodles");
            if (associatedMoodles)
            {
                DrawMoodlesOptions(refRestraint, cellPadding.Y);
            }
            associatedMoodles.Dispose();

            var associatedCustomizePreset = ImRaii.TabItem("C+ Preset");
            if (associatedCustomizePreset)
            {
                DrawCustomizePlusOptions(refRestraint, cellPadding.Y);
            }
            associatedCustomizePreset.Dispose();

            var associatedSpatialAudioType = ImRaii.TabItem("Sounds");
            if (associatedSpatialAudioType)
            {
                DrawSpatialAudioOptions(refRestraint, cellPadding.Y);
            }
            associatedSpatialAudioType.Dispose();

            // store the current style for cell padding
            var cellPaddingCurrent = ImGui.GetStyle().CellPadding;
            // push Y cell padding.
            using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5f * _uiShared.GetFontScalerFloat(), cellPadding.Y)))
            {
                var visibilityAccess = ImRaii.TabItem("Pair Visibility & Hardcore");
                if (visibilityAccess)
                {
                    DrawVisibilityAndProperties(ref refRestraint);
                }
                visibilityAccess.Dispose();
            }
        }
    }

    private void DrawInfo(RestraintSet refRestraintSet)
    {
        string refName = refRestraintSet.Name;
        var width = ImGui.GetContentRegionAvail().X * 0.7f;

        ImGui.Text("Restraint Set Name:");
        ImGui.SetNextItemWidth(width);
        if (ImGui.InputTextWithHint($"##NameText", "Restraint Set Name...", ref refName, 48, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            refRestraintSet.Name = refName;
        }
        UiSharedService.AttachToolTip($"Gives the Restraint Set a name!");

        ImGui.Text("Restraint Set Description:");
        string descriptiontext = refRestraintSet.Description;
        using (var descColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey2))
        {
            if (UiSharedService.InputTextWrapMultiline("##InputSetDesc", ref descriptiontext, 150, 4, width))
            {
                refRestraintSet.Description = descriptiontext;
            }
        }
    }

    private void DrawAppearance(RestraintSet refRestraintSet)
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
                refRestraintSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                // if we right click the icon, clear it
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    refRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(refRestraintSet.DrawData[slot].Slot);
                }
                ImGui.SameLine(0, 6);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawEquip(ref refRestraintSet, refRestraintSet.DrawData[slot].Slot, ItemComboLength);
                }
            }
            // i am dumb and dont know how to place adjustable divider lengths
            ImGui.TableNextColumn();
            //draw out the accessory slots
            foreach (var slot in EquipSlotExtensions.AccessorySlots)
            {
                using (var groupIcon = ImRaii.Group())
                {
                    refRestraintSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                    // if we right click the icon, clear it
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        refRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(refRestraintSet.DrawData[slot].Slot);
                    }
                }

                ImGui.SameLine(0, 6);
                using (var groupDraw = ImRaii.Group())
                {
                    DrawEquip(ref refRestraintSet, refRestraintSet.DrawData[slot].Slot, ItemComboLength);
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
                refRestraintSet.BonusDrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                ImGui.SameLine(0, 6);
                DrawBonusItem(ref refRestraintSet, refRestraintSet.BonusDrawData[slot].Slot, newWidth);
                ImUtf8.SameLineInner();
                FontAwesomeIcon icon = refRestraintSet.BonusDrawData[slot].IsEnabled ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
                if (_uiShared.IconButton(icon))
                {
                    refRestraintSet.BonusDrawData[slot].IsEnabled = !refRestraintSet.BonusDrawData[slot].IsEnabled;
                }
                UiSharedService.AttachToolTip("Toggles Apply Style of Item." +
                    Environment.NewLine + "EYE Icon (Apply Mode) applies regardless of selected item. (nothing slots make the slot nothing)" +
                    Environment.NewLine + "EYE SLASH Icon (Overlay Mode) means that it only will apply the item if it is NOT an nothing slot.");
            }
            // move to the next column.
            ImGui.TableNextColumn();
            // draw the checkbox options.
            // preset some variables to grab from our config service.
            bool forceHelmetOnEnable = refRestraintSet.ForceHeadgearOnEnable;
            bool forceVisorOnEnable = refRestraintSet.ForceVisorOnEnable;

            if (ImGui.Checkbox("Force-Enable Headgear", ref forceHelmetOnEnable))
            {
                refRestraintSet.ForceHeadgearOnEnable = forceHelmetOnEnable;
            }
            _uiShared.DrawHelpText("Will force your headgear to become visible when the set is applied. (Via Glamourer State)");

            if (ImGui.Checkbox("Force-Enable Visor", ref forceVisorOnEnable))
            {
                refRestraintSet.ForceVisorOnEnable = forceVisorOnEnable;
            }
            _uiShared.DrawHelpText("Will force your visor to become visible when the set is applied. (Via Glamourer State)");
        }
    }

    private void DrawMoodlesOptions(RestraintSet refRestraintSet, float cellPaddingY)
    {
        if(LastCreatedCharacterData == null)
        {
            ImGui.TextWrapped("No Character Data Found. Please select a character to edit.");
            return;
        }

        try
        {
            using var table = ImRaii.Table("MoodlesSelections", 2, ImGuiTableFlags.BordersInnerV);
            if (!table) return;

            ImGui.TableSetupColumn("MoodleSelection", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("FinalizedPreviewList", ImGuiTableColumnFlags.WidthFixed, 200f);

            ImGui.TableNextRow(); ImGui.TableNextColumn();

            using (var child = ImRaii.Child("##RestraintMoodleStatusSelection", new(ImGui.GetContentRegionAvail().X - 1f, ImGui.GetContentRegionAvail().Y / 2), false))
            {
                if (!child) return;
                _relatedMoodles.DrawMoodlesStatusesListForSet(refRestraintSet, LastCreatedCharacterData, cellPaddingY, false);
            }
            ImGui.Separator();
            using (var child2 = ImRaii.Child("##RestraintMoodlePresetSelection", -Vector2.One, false))
            {
                if (!child2) return;
                _relatedMoodles.DrawMoodlesStatusesListForSet(refRestraintSet, LastCreatedCharacterData, cellPaddingY, true);
            }


            ImGui.TableNextColumn();
            // Filter the MoodlesStatuses list to get only the moodles that are in AssociatedMoodles
            var associatedMoodles = LastCreatedCharacterData.MoodlesStatuses
                .Where(moodle => refRestraintSet.AssociatedMoodles.Contains(moodle.GUID))
                .ToList();
            // draw out all the active associated moodles in the restraint set with thier icon beside them.
            UiSharedService.ColorText("Moodles Applied with Set:", ImGuiColors.ParsedPink);
            ImGui.Separator();
            foreach (var moodle in associatedMoodles)
            {
                using (var group = ImRaii.Group())
                {

                    var currentPos = ImGui.GetCursorPos();
                    if (moodle.IconID != 0 && currentPos != Vector2.Zero)
                    {
                        var statusIcon = _uiShared.GetGameStatusIcon((uint)((uint)moodle.IconID + moodle.Stacks - 1));

                        if (statusIcon is { } wrap)
                        {
                            ImGui.SetCursorPos(currentPos);
                            ImGui.Image(statusIcon.ImGuiHandle, MoodlesService.StatusSize);
                        }
                    }
                    ImGui.SameLine();
                    float shiftAmmount = (MoodlesService.StatusSize.Y - ImGui.GetTextLineHeight()) / 2;
                    ImGui.SetCursorPosY(currentPos.Y + shiftAmmount);
                    ImGui.Text(moodle.Title);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error Drawing Moodles Options for Restraint Set.");
        }
    }

    private void DrawCustomizePlusOptions(RestraintSet refRestraintSet, float cellPaddingY)
    {
        ImGui.TextWrapped("Not dealing with this crap until C+ pulls itself together. Too much a drain on my sanity and posing it too much of a bitch right now.");
    }

    private void DrawSpatialAudioOptions(RestraintSet refRestraintSet, float cellPaddingY)
    {
        _uiShared.BigText("Select if Restraint Set Uses:\nRopes, Chains, Leather, Latex, ext* here.");
        ImGui.Text("They will then play immersive spatial audio on queue.");
    }


    public enum StimulationDegree { No, Light, Mild, Heavy }

    private void DrawVisibilityAndProperties(ref RestraintSet refRestraintSet)
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
                var canSeeIcon = refRestraintSet.ViewAccess.IndexOf(pair.UserData.UID) == -1 ? FontAwesomeIcon.Times : FontAwesomeIcon.Check;
                using (ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0))))
                {
                    if (ImGuiUtil.DrawDisabledButton(canSeeIcon.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
                    string.Empty, false, true))
                    {
                        if (canSeeIcon == FontAwesomeIcon.Times)
                        {
                            // add them
                            refRestraintSet.ViewAccess.Add(pair.UserData.UID);
                            refRestraintSet.SetProperties[pair.UserData.UID] = new HardcoreSetProperties();
                        }
                        else
                        {
                            refRestraintSet.ViewAccess.Remove(pair.UserData.UID);
                            refRestraintSet.SetProperties.Remove(pair.UserData.UID);
                        }
                    }
                }
                ImGui.TableNextColumn();

                // draw the properties, but dont allow access if not in hardcore for them or if they are not in the list.
                if (!refRestraintSet.ViewAccess.Contains(pair.UserData.UID))
                {
                    ImGui.Text("Must Grant User View Access to Set Properties");
                }
                else if (refRestraintSet.ViewAccess.Contains(pair.UserData.UID) && !refRestraintSet.SetProperties.ContainsKey(pair.UserData.UID))
                {
                    // we have hit a case where we are editing a restraint with no saved hardcore properties, yet they have view access, so create one.
                    refRestraintSet.SetProperties[pair.UserData.UID] = new HardcoreSetProperties();
                }
                else
                {
                    // grab a quick reference variable
                    var properties = refRestraintSet.SetProperties[pair.UserData.UID];

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
    public void DrawEquip(ref RestraintSet refRestraintSet, EquipSlot slot, float _comboLength)
    {
        using var id = ImRaii.PushId((int)refRestraintSet.DrawData[slot].Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var left = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        var ItemWidth = _comboLength - _uiShared.GetIconButtonSize(FontAwesomeIcon.EyeSlash).X - ImUtf8.ItemInnerSpacing.X;

        using var group = ImRaii.Group();
        DrawItem(ref refRestraintSet, out var label, right, left, slot, ItemWidth);
        ImUtf8.SameLineInner();
        FontAwesomeIcon icon = refRestraintSet.DrawData[slot].IsEnabled ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
        if (_uiShared.IconButton(icon))
        {
            refRestraintSet.DrawData[slot].IsEnabled = !refRestraintSet.DrawData[slot].IsEnabled;
        }
        UiSharedService.AttachToolTip("Toggles Apply Style of Item." +
            Environment.NewLine + "EYE Icon (Apply Mode) applies regardless of selected item. (nothing slots make the slot nothing)" +
            Environment.NewLine + "EYE SLASH Icon (Overlay Mode) means that it only will apply the item if it is NOT an nothing slot.");
        DrawStain(ref refRestraintSet, slot, _comboLength);
    }

    private void DrawItem(ref RestraintSet refRestraintSet, out string label, bool clear, bool open, EquipSlot slot, float width)
    {
        // draw the item combo.
        var combo = ItemCombos[refRestraintSet.DrawData[slot].Slot.ToIndex()];
        label = combo.Label;
        if (open)
        {
            GenericHelpers.OpenCombo($"##WardrobeCreateNewSetItem-{slot}");
            Logger.LogTrace($"{combo.Label} Toggled");
        }
        // draw the combo
        var change = combo.Draw(refRestraintSet.DrawData[slot].GameItem.Name,
            refRestraintSet.DrawData[slot].GameItem.ItemId, width, ComboWidth * 1.3f);

        // if we changed something
        if (change && !refRestraintSet.DrawData[slot].GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            Logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ItemId}] " +
                $"to {refRestraintSet.DrawData[slot].GameItem} [{refRestraintSet.DrawData[slot].GameItem.ItemId}]");
            // update the item to the new selection.
            refRestraintSet.DrawData[slot].GameItem = combo.CurrentSelection;
        }

        // if we right clicked
        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            // if we right click the item, clear it.
            Logger.LogTrace($"Item changed to {ItemIdVars.NothingItem(refRestraintSet.DrawData[slot].Slot)} " +
                $"[{ItemIdVars.NothingItem(refRestraintSet.DrawData[slot].Slot).ItemId}] " +
                $"from {refRestraintSet.DrawData[slot].GameItem} [{refRestraintSet.DrawData[slot].GameItem.ItemId}]");
            // clear the item.
            refRestraintSet.DrawData[slot].GameItem = ItemIdVars.NothingItem(refRestraintSet.DrawData[slot].Slot);
        }
    }

    private void DrawBonusItem(ref RestraintSet refRestraintSet, BonusItemFlag flag, float width)
    {
        using var id = ImRaii.PushId((int)refRestraintSet.BonusDrawData[flag].Slot);
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        bool clear = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        bool open = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        // Assuming _bonusItemCombo is similar to ItemCombos but for bonus items
        var combo = BonusItemCombos[refRestraintSet.BonusDrawData[flag].Slot.ToIndex()];

        if (open)
            ImGui.OpenPopup($"##{combo.Label}");

        var change = combo.Draw(refRestraintSet.BonusDrawData[flag].GameItem.Name,
            refRestraintSet.BonusDrawData[flag].GameItem.Id,
            width, ComboWidth * 1.3f);

        if (change && !refRestraintSet.BonusDrawData[flag].GameItem.Equals(combo.CurrentSelection))
        {
            // log full details.
            Logger.LogTrace($"Item changed from {combo.CurrentSelection} [{combo.CurrentSelection.ModelId}] " +
                $"to {refRestraintSet.BonusDrawData[flag].GameItem} [{refRestraintSet.BonusDrawData[flag].GameItem.ModelId}]");
            // change
            refRestraintSet.BonusDrawData[flag].GameItem = combo.CurrentSelection;
        }

        if (clear || ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            // Assuming a method to handle item reset or clear, similar to your DrawItem method
            Logger.LogTrace($"Item reset to default for slot {flag}");
            // reset it
            refRestraintSet.BonusDrawData[flag].GameItem = BonusItem.Empty(flag);
        }
    }

    private void DrawStain(ref RestraintSet refRestraintSet, EquipSlot slot, float width)
    {
        // fetch the correct stain from the stain data
        var widthStains = (width - ImUtf8.ItemInnerSpacing.X *
            (refRestraintSet.DrawData[slot].GameStain.Count - 1)) / refRestraintSet.DrawData[slot].GameStain.Count;

        // draw the stain combo for each of the 2 dyes (or just one)
        foreach (var (stainId, index) in refRestraintSet.DrawData[slot].GameStain.WithIndex())
        {
            using var id = ImUtf8.PushId(index);
            var found = _stainDictionary.TryGetValue(stainId, out var stain);
            // draw the stain combo.
            var change = StainColorCombos.Draw($"##stain{refRestraintSet.DrawData[slot].Slot}", stain.RgbaColor, stain.Name, found, stain.Gloss, widthStains);
            if (index < refRestraintSet.DrawData[slot].GameStain.Count - 1)
                ImUtf8.SameLineInner(); // instantly go to draw the next one if there are two stains

            // if we had a change made, update the stain data.
            if (change)
            {
                if (_stainDictionary.TryGetValue(StainColorCombos.CurrentSelection.Key, out stain))
                {
                    // if changed, change it.
                    refRestraintSet.DrawData[slot].GameStain = refRestraintSet.DrawData[slot].GameStain.With(index, stain.RowIndex);
                }
                else if (StainColorCombos.CurrentSelection.Key == Stain.None.RowIndex)
                {
                    // if set to none, reset it to default
                    refRestraintSet.DrawData[slot].GameStain = refRestraintSet.DrawData[slot].GameStain.With(index, Stain.None.RowIndex);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                // reset the stain to default
                refRestraintSet.DrawData[slot].GameStain = refRestraintSet.DrawData[slot].GameStain.With(index, Stain.None.RowIndex);
            }
        }
    }
}
