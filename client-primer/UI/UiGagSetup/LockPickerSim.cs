using GagSpeak.Services.Mediator;
using GagSpeak.UI.Simulation;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.UiGagSetup;

public class LockPickerSim : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly LockpickMinigame _lockpickSim;

    public LockPickerSim(ILogger<LockPickerSim> logger,
        GagspeakMediator mediator, UiSharedService uiShared,
        LockpickMinigame lockpickSim) : base(logger, mediator)
    {
        _uiShared = uiShared;
        _lockpickSim = lockpickSim;
    }

    private bool isLocked = true;
    private bool isSuccess = false;
    // Sample data for pin positions and targets
    public List<float> pinOffsets = new List<float> { 0, 0, 0, 0, 0 };
    public List<float> pinTargetOffsets = new List<float> { 80, 60, 90, 50, 70 };

    public void DrawLockPickingSim()
    {
        _uiShared.BigText("Very Likely to not be added. Idk");
/*        var drawList = ImGui.GetWindowDrawList();

        // Variables for layout
        float pinSpacing = 100.0f;
        float pinWidth = 40.0f;
        float pinHeight = 80.0f;
        float pinStartX = ImGui.GetCursorScreenPos().X;
        float pinStartY = ImGui.GetCursorScreenPos().Y+100f;

        // Draw the pins and cylinder
        for (int i = 0; i < pinOffsets.Count; i++)
        {
            // Pin positions
            float pinX = pinStartX + i * pinSpacing;
            float pinY = pinStartY;

            // Cylinder Background (representing the cylinder itself)
            drawList.AddRectFilled(new Vector2(pinX, pinY), new Vector2(pinX + pinWidth, pinY + pinHeight), ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)));

            // Pin (moving element)
            float pinCurrentOffset = pinOffsets[i];
            float pinOffsetTarget = pinTargetOffsets[i];

            // Adjust pin based on target (simulate movement)
            if (pinCurrentOffset < pinOffsetTarget)
                pinOffsets[i] += 1.0f; // Adjust this to create movement

            // Draw the pin
            drawList.AddRectFilled(new Vector2(pinX, pinY - pinOffsets[i]), new Vector2(pinX + pinWidth, pinY + pinHeight - pinOffsets[i]), ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)));

            // Interaction - check if mouse is within bounds of pin and cylinder
            if (ImGui.IsMouseHoveringRect(new Vector2(pinX, pinY), new Vector2(pinX + pinWidth, pinY + pinHeight)))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                drawList.AddRect(new Vector2(pinX, pinY), new Vector2(pinX + pinWidth, pinY + pinHeight), ImGui.GetColorU32(new Vector4(1.0f, 0.0f, 0.0f, 1.0f)), 0.0f, ImDrawFlags.None, 3.0f);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    // Action on clicking pin (e.g., try to move pin to target position)
                    pinOffsets[i] += 5.0f; // Simulate movement on click
                }
            }
        }

        // Draw text for attempts or progress
        ImGui.SetCursorPos(new Vector2(300, 400));
        ImGui.Text($"Tries Remaining: {_lockpickSim.maxAttempts - _lockpickSim.currentAttempts}");

        // Display failure or success state
        if (isLocked)
        {
            ImGui.SetCursorPos(new Vector2(300, 450));
            ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "Failed!");
        }
        else if (isSuccess)
        {
            ImGui.SetCursorPos(new Vector2(300, 450));
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Success!");
        }*/
    }
}
