using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Simulation;
public class LockpickMinigame
{
    public const int MaxPins = 5; // Example number of pins
    public int[] pinOffsets = new int[MaxPins];
    public int[] pinOffsetTargets = new int[MaxPins];
    public bool[] pinSet = new bool[MaxPins];
    public int currentAttempts = 0;
    public int maxAttempts = 5; // Maximum number of attempts

    // Variables for feedback (similar to hover effects and UI messages)
    public string feedbackMessage = "";
    public float arousalTick = 0;

    public void StartMinigame()
    {
        ResetPins();
        currentAttempts = 0;
    }

    private void ResetPins()
    {
        for (int i = 0; i < MaxPins; i++)
        {
            pinOffsets[i] = 0;
            pinOffsetTargets[i] = 100; // Pin needs to reach this to be set
            pinSet[i] = false;
        }
    }

    public void DrawMinigame()
    {
        // This is where you'd use ImGui or Unity UI to draw each pin visually
        // Draw background and pins
        for (int i = 0; i < MaxPins; i++)
        {
            DrawPin(i);
        }

        // Display attempts remaining and feedback messages
        ImGui.Text($"Attempts remaining: {maxAttempts - currentAttempts}");
        if (!string.IsNullOrEmpty(feedbackMessage))
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), feedbackMessage);
        }
    }

    private void DrawPin(int pinIndex)
    {
        int pinX = 100 + pinIndex * 50; // Example X position
        int pinY = 200 - pinOffsets[pinIndex]; // Pin moves vertically

        // Simulate pin being hovered (this would be ImGui button or Unity mouse check)
        if (ImGui.Button($"Pin {pinIndex}", new Vector2(50, 50)))
        {
            TryPickPin(pinIndex);
        }

        // Draw the pin visually (this is pseudo-code, replace with actual drawing logic)
        ImGui.Text($"Pin {pinIndex} Offset: {pinOffsets[pinIndex]}");
    }

    private void TryPickPin(int pinIndex)
    {
        if (pinSet[pinIndex])
            return; // Pin already set

        // Move the pin towards the target
        pinOffsets[pinIndex] += 10; // Adjust this value for smoother movement

        if (pinOffsets[pinIndex] >= pinOffsetTargets[pinIndex])
        {
            pinSet[pinIndex] = true; // The pin is now set
            feedbackMessage = $"Pin {pinIndex} set!";
        }

        // Increment attempts
        currentAttempts++;
        if (currentAttempts >= maxAttempts)
        {
            feedbackMessage = "Too many attempts! Failed.";
            EndMinigame(false);
        }

        // Check if all pins are set
        if (AllPinsSet())
        {
            EndMinigame(true);
        }
    }

    private bool AllPinsSet()
    {
        foreach (bool pin in pinSet)
        {
            if (!pin)
                return false;
        }
        return true;
    }

    private void EndMinigame(bool success)
    {
        if (success)
        {
            feedbackMessage = "Lock successfully picked!";
        }
        else
        {
            feedbackMessage = "Lockpick failed.";
        }
        ResetPins(); // Optionally reset pins after the game ends
    }
}
