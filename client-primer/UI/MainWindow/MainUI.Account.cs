/* Temp file.


Contents of Account Page (in order of display):

- Profile Image display
- UID Display (centered, under profile image)
- Player name (centered, small-text, under UID)
---- Separator ----
- Safeword text field
- Open Account Settings
- Open Profile Editor
- Help Button
- About Button
- Buttons for opening plugin config ext. (if applicable)


//////////////// The below is reponsible for displaying the copiable UID in big text./


// otherwise, if we are on the current version, begin by pushing the main header, containing our UID
using ImGuiNET;

using (ImRaii.PushId("header")) DrawUIDHeader();
// draw a separation boundry
ImGui.Separator();


/// <summary>
/// Draws the UID header for the currently connected client (you)
/// </summary>
private void DrawUIDHeader()
{
    // fetch the Uid Text of yourself
    var uidText = GetUidText();

    // push the big boi font for the UID
    using (_uiSharedService.UidFont.Push())
    {
        var uidTextSize = ImGui.CalcTextSize(uidText);
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - uidTextSize.X / 2);
        // display it, it should be green if connected and red when not.
        ImGui.TextColored(GetUidColor(), uidText);
    }

    // if we are connected
    if (_apiController.ServerState is ServerState.Connected)
    {
        UiSharedService.CopyableDisplayText(_apiController.DisplayName);

        // if the UID does not equal the display name
        if (!string.Equals(_apiController.DisplayName, _apiController.UID, StringComparison.Ordinal))
        {
            // grab the original text size for the UID in the api controller
            var origTextSize = ImGui.CalcTextSize(_apiController.UID);
            // adjust the cursor and redraw the UID (really not sure why this is here but we can trial and error later.
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - origTextSize.X / 2);
            ImGui.TextColored(GetUidColor(), _apiController.UID);
            // give it the same functionality.
            UiSharedService.CopyableDisplayText(_apiController.UID);
        }
    }
    // otherwise, if we are not connected
    else
    {
        // we should display in the color wrapped text the server error.
        UiSharedService.ColorTextWrapped(GetServerError(), GetUidColor());
        if (_apiController.ServerState is ServerState.NoSecretKey)
        {
            // if the connected state is due to not having a secret key,
            // we should ask it to add our character
            DrawAddCharacter();
        }
    }
}


    private void DrawUserConfig(float availableWidth, float spacingX)
    {
        var buttonX = (availableWidth - spacingX) / 2f;
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.UserCircle, "Edit Gagspeak Profile", buttonX))
        {
            _mediator.Publish(new UiToggleMessage(typeof(EditProfileUi)));
        }
        UiSharedService.AttachToolTip("Edit your Gagspeak Profile");
        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.PersonCircleQuestion, "Chara Data Analysis", buttonX))
        {
            // _mediator.Publish(new UiToggleMessage(typeof(DataAnalysisUi)));
        }
        UiSharedService.AttachToolTip("View and analyze your generated character data");
    }


*/
