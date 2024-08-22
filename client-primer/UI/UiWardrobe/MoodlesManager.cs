using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using ImGuiNET;
using OtterGui.Classes;
using System.Numerics;

namespace GagSpeak.UI.UiWardrobe;

public class MoodlesManager
{
    private readonly ILogger<MoodlesManager> _logger;
    private readonly UiSharedService _uiShared;
    private readonly PairManager _pairManager;
    private readonly IpcCallerMoodles _ipcCallerMoodles;

    public MoodlesManager(ILogger<MoodlesManager> logger,
        UiSharedService uiSharedService, PairManager pairManager, 
        IpcCallerMoodles ipcCallerMoodles)
    {
        _logger = logger;
        _uiShared = uiSharedService;
        _pairManager = pairManager;
        _ipcCallerMoodles = ipcCallerMoodles;
    }

    // Info related to the person we are inspecting.
    private string PairSearchString = string.Empty;
    private Pair? PairToInspect = null;
    private List<MoodlesStatusInfo>? _moodlesInfo;
    private List<(Guid, List<Guid>)>? _presetsInfo;

    private void MoodlesHeader()
    {
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        Vector2 textSize;
        using (_uiShared.UidFont.Push()) { textSize = ImGui.CalcTextSize("Inspect Moodles & Presets"); }
        var centerYpos = (textSize.Y - ImGui.GetFrameHeight());

        using (ImRaii.Child("MoodlesManagerHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetFrameHeight() + (centerYpos - startYpos) * 2)))
        {
            // now next to it we need to draw the header text
            ImGui.SameLine(ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            using (_uiShared.UidFont.Push())
            {
                UiSharedService.ColorText("Inspect Moodles & Presets", ImGuiColors.ParsedPink);
            }

            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - 150f - ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            var PairList = _pairManager.GetOnlineUserPairs()
                .Where(pair => pair.LastReceivedIpcData != null
                && (string.IsNullOrEmpty(PairSearchString)
                || pair.UserData.AliasOrUID.Contains(PairSearchString, StringComparison.OrdinalIgnoreCase)
                || (pair.GetNickname() != null && pair.GetNickname()!.Contains(PairSearchString, StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(p => p.IsVisible) // Prioritize users with Visible == true
                .ThenBy(p => p.GetNickname() ?? p.UserData.AliasOrUID, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Add a special option for "Client Player" at the top of the list
            PairList.Insert(0, null!);

            _uiShared.DrawComboSearchable("##InspectPair", 150f, ref PairSearchString, PairList,
                (pair) => pair == null ? "Examine Self" : pair.GetNickname() ?? pair.UserData.AliasOrUID, false,
                (pair) =>
                {
                    if (pair == null)
                    {
                        PairToInspect = null;
                    }
                    else
                    {
                        PairToInspect = pair;
                    }
                });
            UiSharedService.AttachToolTip("Choose Who to Inspect.");
        }
    }


    public void DrawMoodlesManager()
    {
        MoodlesHeader();
        if (ImGui.Button("Retrieve Moodles Info"))
        {
            RetrieveMoodlesInfo();
        }

        ImGui.Separator();

        if (ImGui.BeginTabBar("moodlesPreviewTabBar"))
        {
            if (ImGui.BeginTabItem("Moodles"))
            {
                DrawMoodles();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Presets"))
            {
                DrawPresets();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawMoodles()
    {
        if (_moodlesInfo != null)
        {
            _uiShared.BigText("Moodles Info:");
            foreach (var moodle in _moodlesInfo)
            {
                PrintMoodleInfo(moodle);
                ImGui.Separator();
            }
        }
    }

    private void DrawPresets()
    {
        if (_presetsInfo != null)
        {
            _uiShared.BigText("Presets Info:");
            foreach (var preset in _presetsInfo)
            {
                ImGui.Text($"Preset ID [{preset.Item2}] applies the following Moodles:");
                ImGui.Indent();
                foreach (var presetStatusesGuid in preset.Item2)
                {
                    ImGui.Text(presetStatusesGuid.ToString());
                }
                ImGui.Unindent();
            }
        }
    }

    private async void RetrieveMoodlesInfo()
    {
        var start = DateTime.UtcNow;

        // grab the moodles data from the player object.
        _moodlesInfo = await _ipcCallerMoodles.GetMoodlesInfoAsync().ConfigureAwait(false);
        _presetsInfo = await _ipcCallerMoodles.GetPresetsInfoAsync().ConfigureAwait(false);

        _logger.LogInformation("IPC Update for player object took {time}ms", TimeSpan.FromTicks(DateTime.UtcNow.Ticks - start.Ticks).TotalMilliseconds);
    }

    private void PrintMoodleInfo(MoodlesStatusInfo moodle)
    {
        UiSharedService.ColorText("Moodle Name: " + moodle.Title, ImGuiColors.ParsedPink);
        ImGui.SameLine();
        ImGui.Text("("+moodle.GUID+")");
        ImGui.Text("Desc: " + moodle.Description);
        ImGui.Text("Status Type: " + moodle.Type.ToString());
        ImGui.Text("Applier: " + moodle.Applier);
        ImGui.Text("Length: " + moodle.Days.ToString() + "d " + moodle.Hours.ToString()
            + "h " + moodle.Minutes.ToString() + "m " + moodle.Seconds.ToString() + "s");
        ImGui.Text("PermanentMoodle: " + moodle.NoExpire.ToString());
        ImGui.Text("Dispelable: " + moodle.Dispelable.ToString());
        ImGui.Text("Stacks: " + moodle.Stacks.ToString());
        ImGui.Text("Persistent: " + moodle.Persistent.ToString());
        ImGui.Text("Is Sticky Moodle: " + moodle.AsPermanent.ToString());
    }
}
