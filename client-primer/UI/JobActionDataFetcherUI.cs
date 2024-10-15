using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using JetBrains.Annotations;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI;

public class JobActionDataFetcherUI : WindowMediatorSubscriberBase
{
    private readonly IDataManager _gameData;
    private readonly ITextureProvider _textures;

    public JobActionDataFetcherUI(ILogger<JobActionDataFetcherUI> logger, GagspeakMediator mediator,
        IDataManager gameData, ITextureProvider textures) : base(logger, mediator, "###GagSpeakActionDebugger")
    {
        _gameData = gameData;
        _textures = textures;

        AllowPinning = false;
        AllowClickthrough = false;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(1500, 1500)
        };
    }

    private int selectedSavedIndex = 0;
    public enum HotBarType { Normal, Cross, }

    protected override void PreDrawInternal() { }

    protected override void DrawInternal()
    {
        DrawDebugWindow();
    }

    protected override void PostDrawInternal() { }

    private unsafe void DrawHotbarType(RaptureHotbarModule* hotbarModule, HotBarType type)
    {
        var isNormalBar = type == HotBarType.Normal;
        var baseSpan = isNormalBar ? hotbarModule->StandardHotbars : hotbarModule->CrossHotbars;

        if (ImGui.BeginTabBar("##hotbarTabs"))
        {
            for (var i = 0; i < baseSpan.Length; i++)
            {
                if (ImGui.BeginTabItem($"{i + 1:00}##hotbar{i}"))
                {
                    var hotbar = baseSpan.GetPointer(i);
                    if (hotbar != null)
                    {
                        DrawHotbar(hotbar);
                    }
                    ImGui.EndTabItem();
                }

            }
            // Pet hotbar is a special case
            if (ImGui.BeginTabItem("Pet##hotbarex"))
            {
                var petBar = isNormalBar ? &hotbarModule->PetHotbar : &hotbarModule->PetCrossHotbar;
                DrawHotbar(petBar);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private unsafe void DrawHotbar(RaptureHotbarModule.Hotbar* hotbar)
    {
        using var tableBorderLight = ImRaii.PushColor(ImGuiCol.TableBorderLight, ImGui.GetColorU32(ImGuiCol.Border));
        using var tableBorderStrong = ImRaii.PushColor(ImGuiCol.TableBorderStrong, ImGui.GetColorU32(ImGuiCol.Border));
        using (var debugTable = ImRaii.Table("HotbarTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            if (!debugTable)
                return;

            ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("Cooldown", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableHeadersRow();

            try
            {
                for (var i = 0; i < 16; i++)
                {
                    var slot = hotbar->Slots.GetPointer(i);
                    if (slot == null) break;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty ? 0x99999999 : 0xFFFFFFFF);
                        ImGui.SameLine();
                        ImGui.Dummy(new Vector2(1, ImGui.GetTextLineHeight() * 4));
                        ImGui.TableNextColumn();
                        ImGui.Text("Empty");
                        ImGui.PopStyleColor();
                        continue;
                    }
                    var adjustedId = slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action ? ActionManager.Instance()->GetAdjustedActionId(slot->CommandId) : slot->CommandId;

                    ImGui.Text($"{slot->CommandType} : {slot->CommandId}");
                    if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action)
                    {
                        ImGui.Text($"Adjusted: {adjustedId}");
                    }

                    if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Macro)
                    {
                        ImGui.Text($"{(slot->CommandId >= 256 ? "Shared" : "Individual")} #{slot->CommandId % 256}");
                    }

                    ImGui.TableNextColumn();

                    var icon = _textures.GetFromGameIcon(new GameIconLookup(slot->IconId % 1000000, slot->IconId >= 1000000)).GetWrapOrDefault();
                    if (icon != null)
                    {
                        ImGui.Image(icon.ImGuiHandle, new Vector2(32));
                    }
                    else
                    {
                        ImGui.GetWindowDrawList().AddRect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(32), 0xFF0000FF, 4);
                        ImGui.GetWindowDrawList().AddText(ImGui.GetCursorScreenPos(), 0xFFFFFFFF, $"{slot->IconId}");

                        ImGui.Dummy(new Vector2(32));
                    }
                    ImGui.SameLine();

                    ImGui.Text($"A: {slot->OriginalApparentActionId}#{slot->OriginalApparentActionId}\nB: {slot->ApparentSlotType}#{slot->ApparentSlotType}");

                    // Column "Name"
                    ImGui.TableNextColumn();

                    var popUpHelp = SeString.Parse(slot->PopUpHelp).ToString();
                    if (popUpHelp.IsNullOrEmpty())
                    {
                        ImGui.TextDisabled("Empty PopUpHelp");
                    }
                    else
                    {
                        ImGui.TextWrapped(popUpHelp);
                    }

                    if (this.ResolveSlotName(slot->CommandType, slot->CommandId, out var resolvedName))
                    {
                        ImGui.TextWrapped($"Resolved: {resolvedName}");
                    }
                    else
                    {
                        ImGui.TextDisabled($"Resolved: {resolvedName}");
                    }

                    // Column "Cooldown"
                    ImGui.TableNextColumn();

                    var cooldownGroup = -1;

                    switch (slot->CommandType)
                    {
                        case RaptureHotbarModule.HotbarSlotType.Action:
                            {
                                var action = _gameData.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Action>()!.GetRow(adjustedId);
                                if (action == null)
                                {
                                    ImGui.TextDisabled("Not Found");
                                    break;
                                }

                                cooldownGroup = action.CooldownGroup;
                                break;
                            }
                        case RaptureHotbarModule.HotbarSlotType.Item:
                            {
                                var item = _gameData.Excel.GetSheet<Item>()!.GetRow(slot->CommandId);
                                if (item == null)
                                {
                                    ImGui.TextDisabled("Not Found");
                                    break;
                                }

                                var cdg = ActionManager.Instance()->GetRecastGroup(2, slot->CommandId);
                                if (cdg < 81) cooldownGroup = cdg + 1;

                                break;
                            }
                        case RaptureHotbarModule.HotbarSlotType.GeneralAction:
                            {
                                var action = _gameData.Excel.GetSheet<GeneralAction>()!.GetRow(slot->CommandId);
                                if (action?.Action == null)
                                {
                                    ImGui.TextDisabled("Not Found");
                                    break;
                                }

                                cooldownGroup = ActionManager.Instance()->GetRecastGroup(5, slot->CommandId);
                                break;
                            }
                    }

                    if (cooldownGroup > 0)
                    {
                        ImGui.Text($"Cooldown Group: {cooldownGroup}");

                        var cooldown = ActionManager.Instance()->GetRecastGroupDetail(cooldownGroup);
                        ImGui.Text(cooldown != null ? $"{cooldown->IsActive} / {cooldown->Elapsed} / {cooldown->Total}" : "Failed");
                    }

                    if (cooldownGroup > 0)
                    {

                        ImGui.Text($"Cooldown Group: {cooldownGroup}");

                        var cooldown = ActionManager.Instance()->GetRecastGroupDetail(cooldownGroup);
                        ImGui.Text($"{(ulong)cooldown:X}");
                        if (cooldown != null)
                        {
                            ImGui.Text($"{cooldown->IsActive} / {cooldown->Elapsed} / {cooldown->Total}");
                        }
                        else
                        {
                            ImGui.Text("Failed");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                StaticLogger.Logger.LogError(e.ToString());
            }
        }
    }

    public unsafe void DrawDebugWindow()
    {
        var raptureHotbarModule = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();

        using var child = ImRaii.Child("##Hotbar Data Snagger", ImGui.GetContentRegionAvail(), true);

        if (ImGui.BeginTabBar("##hotbarDebugDisplay"))
        {
            if (ImGui.BeginTabItem("Current Bars"))
            {
                if (ImGui.BeginTabBar($"###{GetType().Name}_debug_tabs"))
                {
                    if (ImGui.BeginTabItem("Normal"))
                    {
                        DrawHotbarType(raptureHotbarModule, HotBarType.Normal);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Cross"))
                    {
                        DrawHotbarType(raptureHotbarModule, HotBarType.Cross);
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Saved Bars"))
            {
                var classJobSheet = _gameData.GetExcelSheet<ClassJob>()!;

                if (ImGui.BeginChild("savedBarsIndexSelect", new Vector2(150, -1) * ImGui.GetIO().FontGlobalScale, true))
                {
                    for (byte i = 0; i < raptureHotbarModule->SavedHotbars.Length; i++)
                    {
                        var classJobId = raptureHotbarModule->GetClassJobIdForSavedHotbarIndex(i);
                        var jobName = classJobId == 0 ? "Shared" : classJobSheet.GetRow(classJobId)?.Abbreviation?.RawString;
                        var isPvp = i >= classJobSheet.RowCount;

                        // hack for unreleased jobs
                        if (jobName.IsNullOrEmpty() || (i > classJobSheet.RowCount && classJobId == 0)) jobName = "Unknown";

                        if (ImGui.Selectable($"{i}: {(isPvp ? "[PVP]" : "")} {jobName}", selectedSavedIndex == i))
                        {
                            selectedSavedIndex = i;
                        }
                    }
                }

                ImGui.EndChild();
                ImGui.SameLine();
                ImGui.BeginGroup();
                var savedBarClassJob = raptureHotbarModule->SavedHotbars.GetPointer(selectedSavedIndex);
                if (savedBarClassJob != null && ImGui.BeginTabBar("savedClassJobBarSelectType"))
                {
                    void ShowBar(int b)
                    {
                        var savedBar = savedBarClassJob->Hotbars.GetPointer(b);
                        if (savedBar == null)
                        {
                            ImGui.Text("Bar is Null");
                            return;
                        }

                        if (ImGui.BeginTable("savedClassJobBarSlots", 4))
                        {
                            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 50);
                            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 100);
                            ImGui.TableSetupColumn("Resolved Name", ImGuiTableColumnFlags.WidthStretch, 128);

                            ImGui.TableHeadersRow();

                            for (var i = 0; i < 16; i++)
                            {
                                ImGui.TableNextColumn();
                                ImGui.Text($"{i:00}");
                                ImGui.TableNextColumn();
                                var slot = savedBar->Slots.GetPointer(i);
                                if (slot == null)
                                {
                                    ImGui.TableNextRow();
                                    continue;
                                }

                                ImGui.Text($"{slot->CommandType}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{slot->CommandId}");
                                ImGui.TableNextColumn();
                                if (this.ResolveSlotName(slot->CommandType, slot->CommandId, out var resolvedName))
                                {
                                    ImGui.TextWrapped(resolvedName);
                                }
                                else
                                {
                                    ImGui.TextDisabled(resolvedName);
                                }
                            }

                            ImGui.EndTable();
                        }
                    }

                    if (ImGui.BeginTabItem("Normal"))
                    {
                        if (ImGui.BeginTabBar("savecClassJobBarSelectCross"))
                        {
                            for (var i = 0; i < 10; i++)
                            {
                                if (ImGui.BeginTabItem($"{i + 1:00}"))
                                {
                                    ShowBar(i);
                                    ImGui.EndTabItem();
                                }
                            }

                            ImGui.EndTabBar();
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Cross"))
                    {
                        if (ImGui.BeginTabBar("savecClassJobBarSelectCross"))
                        {
                            for (var i = 10; i < 18; i++)
                            {
                                if (ImGui.BeginTabItem($"{i - 9:00}"))
                                {
                                    ShowBar(i);
                                    ImGui.EndTabItem();
                                }
                            }

                            ImGui.EndTabBar();
                        }

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.EndGroup();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private unsafe bool ResolveSlotName(RaptureHotbarModule.HotbarSlotType type, uint commandId, [CanBeNull] out string resolvedName)
    {
        resolvedName = "Not Found";

        switch (type)
        {
            case RaptureHotbarModule.HotbarSlotType.Empty:
                {
                    resolvedName = "N/A";
                    return false;
                }
            case RaptureHotbarModule.HotbarSlotType.Action:
                {

                    var action = _gameData.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Action>()!.GetRow(commandId);
                    if (action == null)
                    {
                        return false;
                    }

                    resolvedName = action.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.Item:
                {
                    var item = _gameData.GetExcelSheet<Item>()!.GetRow(commandId % 500000);
                    if (item == null)
                    {
                        return false;
                    }

                    resolvedName = item.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.CraftAction:
                {
                    var action = _gameData.GetExcelSheet<CraftAction>()!.GetRow(commandId);
                    if (action == null)
                    {
                        return false;
                    }

                    resolvedName = action.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.GeneralAction:
                {
                    var action = _gameData.GetExcelSheet<GeneralAction>()!.GetRow(commandId);
                    if (action == null)
                    {
                        return false;
                    }

                    resolvedName = action.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.MainCommand:
                {
                    var action = _gameData.GetExcelSheet<MainCommand>()!.GetRow(commandId);
                    if (action == null)
                    {
                        return false;
                    }

                    resolvedName = action.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.ExtraCommand:
                {
                    var exc = _gameData.GetExcelSheet<ExtraCommand>()!.GetRow(commandId);
                    if (exc == null)
                    {
                        return false;
                    }

                    resolvedName = exc.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.GearSet:
                {
                    var gearsetModule = RaptureGearsetModule.Instance();
                    var gearset = gearsetModule->GetGearset((int)commandId);

                    if (gearset == null)
                    {
                        resolvedName = $"InvalidGearset#{commandId}";
                        return false;
                    }

                    // resolvedName = $"{Encoding.UTF8.GetString(gearset->Name, 0x2F)}";
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.Macro:
                {
                    var macroModule = RaptureMacroModule.Instance();
                    var macro = macroModule->GetMacro(commandId / 256, commandId % 256);

                    if (macro == null)
                    {
                        return false;
                    }

                    var macroName = macro->Name.ToString();
                    if (macroName.IsNullOrEmpty())
                    {
                        macroName = $"{(commandId >= 256 ? "Shared" : "Individual")} #{commandId % 256}";
                    }

                    resolvedName = macroName;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.Emote:
                {
                    var m = _gameData.GetExcelSheet<Emote>()!.GetRow(commandId);
                    if (m == null)
                    {
                        return false;
                    }

                    resolvedName = m.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.EventItem:
                {
                    var item = _gameData.GetExcelSheet<EventItem>()!.GetRow(commandId);
                    if (item == null)
                    {
                        return false;
                    }

                    resolvedName = $"{item.Name}";
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.Mount:
                {
                    var m = _gameData.Excel.GetSheet<Mount>()!.GetRow(commandId);
                    if (m == null)
                    {
                        return false;
                    }

                    resolvedName = $"{m.Singular}";
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.Companion:
                {
                    var m = _gameData.Excel.GetSheet<Companion>()!.GetRow(commandId);
                    if (m == null)
                    {
                        return false;
                    }

                    resolvedName = $"{m.Singular}";
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.McGuffin:
                {
                    var c = _gameData.Excel.GetSheet<McGuffin>()!.GetRow(commandId);
                    if (c == null)
                    {
                        return false;
                    }

                    resolvedName = c.UIData.Value!.Name;
                    return true;
                }

            case RaptureHotbarModule.HotbarSlotType.PetAction:
                {
                    var pa = _gameData.GetExcelSheet<PetAction>()!.GetRow(commandId);
                    if (pa == null)
                    {
                        return false;
                    }

                    resolvedName = pa.Name;
                    return true;
                }

            default:
                {
                    resolvedName = "Not Yet Supported";
                    return false;
                }
        }
    }
}
