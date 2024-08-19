using Dalamud.Interface.Colors;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class MoodlesManager : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly WardrobeHandler _handler;
    private readonly IpcCallerMoodles _ipcCallerMoodles;
    private List<MoodlesStatusInfo>? _moodlesInfo;
    private List<(List<MoodlesStatusInfo>, Guid)>? _presetsInfo;

    public MoodlesManager(ILogger<MoodlesManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        WardrobeHandler handler, IpcCallerMoodles ipcCallerMoodles) : base(logger, mediator)
    {
        _uiShared = uiSharedService;
        _handler = handler;
        _ipcCallerMoodles = ipcCallerMoodles;
    }

    public void DrawMoodlesManager()
    {
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
                foreach (var moodle in preset.Item1)
                {
                    ImGui.Text(moodle.Title);
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

        Logger.LogInformation("IPC Update for player object took {time}ms", TimeSpan.FromTicks(DateTime.UtcNow.Ticks - start.Ticks).TotalMilliseconds);
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
