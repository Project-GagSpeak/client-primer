using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Components.Popup;

internal class ReportPopupHandler : IPopupHandler
{
    private readonly MainHub _apiHubMain;
    private readonly UiSharedService _uiSharedService;
    private Pair? _reportedPair;
    private string _reportReason = string.Empty;

    public ReportPopupHandler(MainHub apiHubMain, UiSharedService uiSharedService)
    {
        _apiHubMain = apiHubMain;
        _uiSharedService = uiSharedService;
    }

    public Vector2 PopupSize => new(500, 500);

    public bool ShowClose => true;

    public void DrawContent()
    {
        using (_uiSharedService.UidFont.Push())
        {
            UiSharedService.TextWrapped("Report " + _reportedPair!.UserData.AliasOrUID + " GagSpeak Profile");
        }
        ImGui.InputTextMultiline("##reportReason", ref _reportReason, 500, new Vector2(500 - ImGui.GetStyle().ItemSpacing.X * 2, 200));
        UiSharedService.TextWrapped($"Note: The Profile Report System is a way to report others anonymously who see others setting hurtful descriptions," +
            "or photos uploaded of others without their consent." + Environment.NewLine +
            "The report will include your user and your contact info (Discord User)." + Environment.NewLine +
            "Due to the nature of info provided with your report, you must have a claimed Account to report others." + Environment.NewLine +
            "Reports are reviewed by C.K's Team & Action will be taken with full anonymity.");
        UiSharedService.ColorTextWrapped("Miss-use of reports will result in your account being Timed out.", ImGuiColors.DalamudRed);

        using (ImRaii.Disabled(string.IsNullOrEmpty(_reportReason)))
        {
            if (_uiSharedService.IconTextButton(FontAwesomeIcon.ExclamationTriangle, "Send Report"))
            {
                ImGui.CloseCurrentPopup();
                var reason = _reportReason;
                _ = _apiHubMain.UserReportKinkPlate(new(_reportedPair.UserData, reason));
            }
        }
    }

    public void Open(ReportKinkPlateMessage msg)
    {
        _reportedPair = msg.PairToReport;
        _reportReason = string.Empty;
    }
}
