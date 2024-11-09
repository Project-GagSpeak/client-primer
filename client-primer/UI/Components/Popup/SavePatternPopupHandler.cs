using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Components.Popup;

/// <summary> A interface for handling the popups in the UI. </summary>
public class SavePatternPopupHandler : IPopupHandler
{
    private readonly PatternHandler _patternHandler;
    private readonly UiSharedService _uiShared;
    private PatternData CompiledPatternData = new PatternData(); // compile a new pattern to save
    // tag management
    private bool AddingTag = false;
    private string NewTagName = string.Empty;
    private float SaveWidth;
    private float RevertWidth;
    private const float PopupWidth = 270;
    public SavePatternPopupHandler(PatternHandler handler, UiSharedService uiShared)
    {
        _patternHandler = handler;
        _uiShared = uiShared;
    }

    private Vector2 _size = new(PopupWidth, 400);
    public Vector2 PopupSize => _size;
    public bool ShowClosed => false;
    public bool CloseHovered { get; set; } = false;

    public void DrawContent()
    {
        SaveWidth = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Save, "Save Pattern Data");
        RevertWidth = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Undo, "Discard Pattern");
        var start = 0f;
        using (_uiShared.UidFont.Push())
        {
            start = ImGui.GetCursorPosY() - ImGui.CalcTextSize("Create New Pattern").Y;
            ImGui.Text("Create New Pattern");
        }
        ImGuiHelpers.ScaledDummy(5f);
        ImGui.Separator();
        var name = CompiledPatternData.Name;
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("Pattern Name", "Enter a name...", ref name, 48);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            CompiledPatternData.Name = name;
        }
        // author field
        var author = CompiledPatternData.Author.IsNullOrEmpty() ? "Anonymous Kinkster" : CompiledPatternData.Author;
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("Author", "Enter your name...", ref author, 24);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            CompiledPatternData.Author = author;
        }
        // description field
        var description = CompiledPatternData.Description;
        if (ImGui.InputTextMultiline("Description", ref description, 256, new Vector2(150, 100)));
        if(ImGui.IsItemDeactivatedAfterEdit())
        {
            CompiledPatternData.Description = description;
        }

        // duration field.
        ImGui.Text("Pattern Duration: ");
        ImGui.SameLine();
        string text = CompiledPatternData.Duration.Hours > 0
                    ? CompiledPatternData.Duration.ToString("hh\\:mm\\:ss")
                    : CompiledPatternData.Duration.ToString("mm\\:ss");
        UiSharedService.ColorText(text, ImGuiColors.ParsedPink);
        // loop field
        var loop = CompiledPatternData.ShouldLoop;
        if (ImGui.Checkbox("Loop Pattern", ref loop))
        {
            CompiledPatternData.ShouldLoop = loop;
        }

        // display tags
        DrawTagField();
        // display save options
        ImGui.Separator();
        if (_uiShared.IconTextButton(FontAwesomeIcon.Save, "Save Pattern Data", SaveWidth))
        {
            _patternHandler.AddNewPattern(CompiledPatternData);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (_uiShared.IconTextButton(FontAwesomeIcon.Undo, "Discard Pattern", RevertWidth))
        {
            CompiledPatternData = new PatternData();
            ImGui.CloseCurrentPopup();
        }
        var height = ImGui.GetCursorPosY() - start;
        _size = _size with { Y = height };
    }

    private int IndexToRemote = -1;
    private float TagWidth = 0f;
    private void DrawTagField()
    {
        // get the spacing of items
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus).X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        if (ImGui.BeginPopup("DeleteTag"))
        {
            if (ImGui.Selectable("Delete"))
            {
                CompiledPatternData.Tags.RemoveAt(IndexToRemote);
            }
            ImGui.EndPopup();
        }

        using (var group = ImRaii.Group())
        {
            ImGui.Text($"Space left: {PopupWidth - (TagWidth + iconSize)}");
            // store the original width 
            TagWidth = ImGui.CalcTextSize("Tags: ").X + spacing + ImGui.GetStyle().PopupBorderSize;
            ImGui.Text("Tags: ");
            foreach (var tag in CompiledPatternData.Tags)
            {
                var newlinePushed = false;
                // add the button size to the width
                TagWidth += ImGui.CalcTextSize(tag).X + spacing;
                // if there is less than 60f in the content region go to the next row
                if (!(PopupWidth - TagWidth < 40f))
                {
                    ImGui.SameLine();
                }
                else
                {
                    newlinePushed = true;
                }
                ImGui.Button(tag);
                // if the item is leftclicked, display a popup menu asking if they want to delete.
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    IndexToRemote = CompiledPatternData.Tags.IndexOf(tag);
                    ImGui.OpenPopup("DeleteTag");
                }
                // account for newline
                if (PopupWidth - (TagWidth + iconSize) < 35f)
                {
                    if (!newlinePushed)
                    {
                        ImGui.NewLine();
                        TagWidth = 0f;
                    }
                    else
                    {
                        TagWidth = ImGui.CalcTextSize(tag).X + spacing;
                    }
                }
            }
            if (CompiledPatternData.Tags.Count >= 5) return;

            if (AddingTag)
            {
                // new field width
                var newWidth = ImGui.CalcTextSize(NewTagName).X + 35f;
                if (!(PopupWidth - (TagWidth + 15f) < newWidth))
                {
                    ImGui.SameLine();
                }
                ImGui.SetNextItemWidth(newWidth - 10f);
                ImGui.SetKeyboardFocusHere();
                ImGui.InputTextWithHint("##New Tag", string.Empty, ref NewTagName, 16);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    CompiledPatternData.Tags.Add(NewTagName);
                    AddingTag = false;
                    NewTagName = string.Empty;
                }
            }
            else
            {
                ImGui.SameLine();
                if (_uiShared.IconButton(FontAwesomeIcon.Plus) || ImGui.IsMouseClicked(ImGuiMouseButton.Middle))
                {
                    AddingTag = true;
                }
                UiSharedService.AttachToolTip("You can press Middle Mouse to automatically open me!");
            }
        }
    }

    public void Open(PatternSavePromptMessage message)
    {
        // compile a fresh pattern object
        CompiledPatternData = new PatternData();
        // create new GUID for it
        CompiledPatternData.UniqueIdentifier = Guid.NewGuid();
        // set the duration
        CompiledPatternData.Duration = message.Duration;
        // set the pattern data
        CompiledPatternData.PatternByteData = message.StoredData;
        // set vibration data
    }
}
