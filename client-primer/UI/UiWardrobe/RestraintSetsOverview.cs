using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI.Components.Combos;
using GagSpeak.Utils;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using System.Numerics;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetsOverview
{
    private readonly ILogger<RestraintSetsOverview> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;
    private readonly WardrobeHandler _handler;
    private readonly TextureService _textures;
    private readonly DictStain _stainDictionary;

    public RestraintSetsOverview(ILogger<RestraintSetsOverview> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        WardrobeHandler handler, TextureService textureService,
        DictStain stainDictionary)
    {
        _logger = logger;
        _mediator = mediator;
        _uiShared = uiSharedService;
        _handler = handler;
        _textures = textureService;
        _stainDictionary = stainDictionary;

        GameIconSize = new Vector2(2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
        StainColorCombos = new StainColorCombo(0, _stainDictionary, logger);
    }

    private Vector2 GameIconSize;
    private string RefSearchString = string.Empty;
    private readonly StainColorCombo StainColorCombos;
    private string InputTime = string.Empty;

    public void DrawSetsOverview()
    {
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        if (_handler.RestraintSetCount() <= 0)
        {
            using (_uiShared.UidFont.Push())
            {
                ImGui.Text($"No Restraint Sets Created!");
            }
            ImGui.TextWrapped("To Create a Restraint Set, head over to the Restraint Set Creator tab.");
            return;
        }

        // grab the list of names
        List<string> nameList = _handler.GetRestraintSetsByName();
        string defaultSelection = nameList.FirstOrDefault() ?? "No Restraint Sets Created!";

        // Draw the combo box with the default selected item and the action
        _uiShared.DrawComboSearchable("Restraint Set", 200f, ref RefSearchString,
            nameList, (i) => i, true,
        (i) =>
        {
            // Set the selected index to the selected item's index
            _logger.LogInformation($"Selected Set: {i}");
            int index = nameList.IndexOf(i);
            _handler.SelectedSetIdx = index;
            _mediator.Publish(new RestraintSetModified(index));
        }, defaultSelection);


        // if we reach this point it means we have a valid restraint set counter ( greater than 0 )
        ImGui.SameLine();

        var icon = _handler.SelectedSet.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff;
        var text = _handler.SelectedSet.Enabled ? "Enabled" : "Disabled";
        if (_uiShared.IconTextButton(icon, text))
        {
            if (_handler.SelectedSet.Enabled)
            {
                _handler.DisableRestraintSet(_handler.SelectedSetIdx, "SelfApplied");
            }
            else
            {
                _handler.EnableRestraintSet(_handler.SelectedSetIdx, "SelfApplied");
            }
        }

        ImGui.SameLine();

        if (_uiShared.IconTextButton(FontAwesomeIcon.TrashAlt, "Delete"))
        {
            var idxToDelete = _handler.SelectedSetIdx;
            // publish update to reset back to 0 index,
            _mediator.Publish(new RestraintSetRemovedMessage(idxToDelete)); // REVIEW : Likely uneeded
            // remove the set at the index.
            _handler.RemoveRestraintSet(idxToDelete);
        }

        // table from here on out
        using (var infoTable = ImRaii.Table("RestraintsOverviewTable", 2, ImGuiTableFlags.None))
        {
            if (!infoTable) return;
            // setup the columns
            ImGui.TableSetupColumn("BasicInfo", ImGuiTableColumnFlags.WidthFixed, 300f);
            ImGui.TableSetupColumn("PreviewSet", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow(); ImGui.TableNextColumn();

            _uiShared.BigText(_handler.SelectedSet.Name); // display name
            ImGui.TextWrapped(_handler.SelectedSet.Description); // display description

            // provide an input text box to update the timer string.
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.7f);
            using (var disableTimeInput = ImRaii.Disabled(_handler.SelectedSet.Locked))
            {
                ImGui.InputTextWithHint($"##{_handler.SelectedSet.Name}TimerLockField", "self-lock duration: XdXhXmXs format..", ref InputTime, 24);
            }
            // in the same line draw a button to toggle the lock.
            ImGui.SameLine();

            var iconLock = _handler.SelectedSet.Enabled ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff;
            var textLock = _handler.SelectedSet.Enabled ? "Locked" : "Unlocked";
            if (_uiShared.IconTextButton(iconLock, textLock, null, false, !_handler.SelectedSet.Enabled))
            {
                // when we try to unlock, ONLY allow unlock if you are the one who locked it.
                if (_handler.SelectedSet.Locked && _handler.SelectedSet.LockedBy == "SelfApplied")
                {
                    _handler.UnlockRestraintSet(_handler.SelectedSetIdx, "SelfApplied");
                }
                // if trying to lock it, allow this to happen.
                else
                {
                    // if the time we input is valid, do not clear it.
                    if (_uiShared.TryParseTimeSpan(InputTime, out var timeSpan))
                    {
                        // parse the timespan to the new offset and lock the set.
                        var endTimeUTC = DateTimeOffset.UtcNow.Add(timeSpan);
                        _handler.LockRestraintSet(_handler.SelectedSetIdx, "SelfApplied", endTimeUTC);
                    }
                    else
                    {
                        InputTime = "Invalid Format use (XdXhXmXs)";
                    }
                }
            }

            // display attached hardcore attributes if we have any
            _uiShared.BigText("Hardcore Attributes:");
            // create a list of strings from the hardcore set properties dictionary keyset
            string UIDtoView = _handler.SelectedSet.SetProperties.Keys.FirstOrDefault() ?? "No User Selected";
            if (!_handler.SelectedSet.SetProperties.Keys.Any())
            {
                ImGui.Text("No Hardcore Attributes Attached");
            }
            else
            {
                _uiShared.DrawCombo("View Properties for Pair", 150f, _handler.SelectedSet.SetProperties.Keys.ToList(), (i) => i,
                (i) =>
                {
                    // set the viewing UID to the index selected
                    UIDtoView = i;
                }, UIDtoView);

                if (UIDtoView != "No User Selected")
                {
                    // display the properties
                    ImGui.Text("Legs Restrained");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].LegsRestrained);
                    ImGui.Text("Arms Restrained");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].ArmsRestrained);
                    ImGui.Text("Gagged");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].Gagged);
                    ImGui.Text("Blindfolded");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].Blindfolded);
                    ImGui.Text("Immobile");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].Immobile);
                    ImGui.Text("Weighty");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].Weighty);
                    ImGui.Text("Light Stimulation");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].LightStimulation);
                    ImGui.Text("Mild Stimulation");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].MildStimulation);
                    ImGui.Text("Heavy Stimulation");
                    _uiShared.BooleanToColoredIcon(_handler.SelectedSet.SetProperties[UIDtoView].HeavyStimulation);
                }
            }
            // end of hardcore attributes

            // display the active associated mods
            _uiShared.BigText("Attached Mods");

            if (_handler.SelectedSet.AssociatedMods.Count == 0)
            {
                ImGui.Text("No Mods Attached");
            }
            else
            {
                foreach (var mod in _handler.SelectedSet.AssociatedMods)
                {
                    ImGui.Text(mod.Mod.Name);
                    ImGui.SameLine(0, 4);
                    if (mod.DisableWhenInactive)
                    {
                        ImGui.TextColored(ImGuiColors.ParsedGreen, "Toggles Mod");
                    }
                    ImGui.SameLine(0, 4);
                    if (mod.RedrawAfterToggle)
                    {
                        ImGui.TextColored(ImGuiColors.ParsedGreen, "Forces Redraw");
                    }
                }
            }

            // rest of general information
            ImGui.TableNextColumn();

            // embed a new table within this table.
            using (var equipIconsTable = ImRaii.Table("equipIconsTable", 2, ImGuiTableFlags.RowBg))
            {
                if (!equipIconsTable) return;
                // Create the headers for the table
                var width = GameIconSize.X + ImGui.GetFrameHeight() + itemSpacing.X;
                // setup the columns
                ImGui.TableSetupColumn("EquipmentSlots", ImGuiTableColumnFlags.WidthFixed, width);
                ImGui.TableSetupColumn("AccessorySlots", ImGuiTableColumnFlags.WidthStretch);

                // draw out the equipment slots
                ImGui.TableNextRow(); ImGui.TableNextColumn();

                foreach (var slot in EquipSlotExtensions.EquipmentSlots)
                {
                    _handler.SelectedSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                    ImGui.SameLine(0, 3);
                    using (var groupDraw = ImRaii.Group())
                    {
                        DrawStain(slot);
                    }
                }
                foreach (var slot in BonusExtensions.AllFlags)
                {
                    _handler.SelectedSet.BonusDrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                }
                // i am dumb and dont know how to place adjustable divider lengths
                ImGui.TableNextColumn();
                //draw out the accessory slots
                foreach (var slot in EquipSlotExtensions.AccessorySlots)
                {
                    _handler.SelectedSet.DrawData[slot].GameItem.DrawIcon(_textures, GameIconSize, slot);
                    ImGui.SameLine(0, 3);
                    using (var groupDraw = ImRaii.Group())
                    {
                        DrawStain(slot);
                    }
                }
            }
        }
    }

    private void DrawStain(EquipSlot slot)
    {

        // draw the stain combo for each of the 2 dyes (or just one)
        foreach (var (stainId, index) in _handler.SelectedSet.DrawData[slot].GameStain.WithIndex())
        {
            using var id = ImUtf8.PushId(index);
            var found = _stainDictionary.TryGetValue(stainId, out var stain);
            // draw the stain combo, but dont make it hoverable
            using (var disabled = ImRaii.Disabled(true))
            {
                StainColorCombos.Draw($"##stain{_handler.SelectedSet.DrawData[slot].Slot}",
                    stain.RgbaColor, stain.Name, found, stain.Gloss, MouseWheelType.None);
            }
        }
    }
}
