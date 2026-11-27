using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IWshRuntimeLibrary;

public static class StartupShortcutCreator
{
    // Check if the shortcut exists in the Startup folder and points to the current executable
    public static bool ShortcutExists()
    {
        string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        // Remove ".exe" from the application name for the shortcut file name
        string appName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
        string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

        if (!System.IO.File.Exists(shortcutPath))
        {
            return false; // Shortcut file does not exist
        }

        WshShell shell = null;
        IWshShortcut shortcut = null;
        try
        {
            // Initialize Windows Script Host Shell
            shell = new WshShell();
            shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

            // Check if the TargetPath matches the current executable's path
            return string.Equals(shortcut.TargetPath, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false; // If any error occurs, assume the shortcut is invalid
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    // Create a shortcut to the application's executable in the Startup folder
    public static void CreateStartupShortcut()
    {
        string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        // Remove ".exe" from the application name for the shortcut file name
        string appName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
        string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

        WshShell shell = null;
        IWshShortcut shortcut = null;
        try
        {
            // Initialize Windows Script Host Shell
            shell = new WshShell();
            shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

            // Set properties for the shortcut
            shortcut.TargetPath = Application.ExecutablePath; // Path to the app's executable
            shortcut.WorkingDirectory = Application.StartupPath; // App's working directory
            shortcut.Description = "Launch My Application"; // Description for the shortcut

            // Save the shortcut
            shortcut.Save();
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
