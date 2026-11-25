using System;
using System.Collections.Generic;
using System.IO;

namespace ChromeProfileLauncher.Services;

/// <summary>Knows where Chrome keeps its data and where chrome.exe lives.</summary>
internal static class ChromePaths
{
    public static string UserDataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data");

    public static string LocalStatePath => Path.Combine(UserDataDirectory, "Local State");

    /// <summary>Returns the first chrome.exe found in the standard install locations.</summary>
    public static string FindChromeExecutable()
    {
        var candidates = new List<string>();
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrEmpty(pf))
            candidates.Add(Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"));
        if (!string.IsNullOrEmpty(pf86))
            candidates.Add(Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe"));
        if (!string.IsNullOrEmpty(local))
            candidates.Add(Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe"));

        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (ArgumentException)
            {
                // Ignore invalid paths from unusual environment configurations.
            }
        }

        return string.Empty;
    }
}
