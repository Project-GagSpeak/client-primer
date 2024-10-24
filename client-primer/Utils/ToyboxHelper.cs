using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using PInvoke;
using System.Runtime.InteropServices;

namespace GagSpeak.Utils;

/// <summary>
/// Handles Intiface Access
/// </summary> 
public static class ToyboxHelper
{
    // the path to intiface central.exe
    public static string AppPath = string.Empty;

    /// <summary> Gets the application running path for Intiface Central.exe if installed.</summary>
    public static void GetApplicationPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AppPath = Path.Combine(appData, "IntifaceCentral", "intiface_central.exe");
            return;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Adjust the path according to where the application resides on macOS
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            AppPath = Path.Combine(homePath, "Applications", "IntifaceCentral", "intiface_central.app");
            return;
        }
    }

    public static void OpenIntiface(ILogger logger, bool pushToForeground)
    {
        // search for the intiface celtral window
        IntPtr windowHandle = User32.FindWindow(null, "Intiface\u00AE Central");
        // if it's present, place it to the foreground
        if (windowHandle != IntPtr.Zero)
        {
            if (pushToForeground)
            {
                logger.LogDebug("Intiface Central found, bringing to foreground.");
                User32.SetForegroundWindow(windowHandle);
            }
        }
        // otherwise, start the process to open intiface central
        else if (!string.IsNullOrEmpty(AppPath) && File.Exists(AppPath))
        {
            logger.LogInformation("Starting Intiface Central");
            Process.Start(AppPath);
        }
        // or just open the installer if it doesnt exist.
        else
        {
            logger.LogWarning("Application not found, redirecting you to download installer." + Environment.NewLine
                + "Current App Path is: " + AppPath);
            Util.OpenLink("https://intiface.com/central/");
        }
    }
}
