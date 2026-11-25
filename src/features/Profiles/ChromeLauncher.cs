using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Starts Chrome in a specific profile with one or more URLs.
/// Uses Chrome's own --profile-directory switch against the REAL default user-data-dir.
/// It never copies, junctions, or force-closes anything.
/// </summary>
internal static class ChromeLauncher
{
    /// <summary>Adds https:// when the user typed a bare address.</summary>
    public static string NormalizeUrl(string input)
    {
        var value = input.Trim();
        if (value.Length == 0)
            return value;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return value;

        if (Uri.TryCreate("https://" + value, UriKind.Absolute, out _))
            return "https://" + value;

        return value;
    }

    public static void Launch(ChromeProfile profile, string url)
    {
        LaunchProcess(profile, url);
    }

    /// <summary>
    /// Starts Chrome and returns the process, so callers can track it. The
    /// profile is opened with Chrome's own supported switch against the REAL
    /// default user-data-dir - nothing is copied, aliased, or junctioned.
    ///
    /// When <paramref name="newWindow"/> is true, Chrome opens the page in its
    /// own window. That keeps each profile's page separate, which is what stops
    /// one run from pressing another run's claim button.
    /// </summary>
    public static Process LaunchProcess(ChromeProfile profile, string url, bool newWindow = false)
    {
        if (string.IsNullOrWhiteSpace(profile.ChromePath))
            throw new InvalidOperationException("chrome.exe was not found on this PC.");

        if (!File.Exists(profile.ChromePath))
            throw new InvalidOperationException("chrome.exe was not found at:\n" + profile.ChromePath);

        var safeUrl = url.Replace("\"", string.Empty);

        var arguments = new StringBuilder();
        arguments.Append($"--profile-directory=\"{profile.DirectoryName}\"");
        if (newWindow)
            arguments.Append(" --new-window");
        arguments.Append($" \"{safeUrl}\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = profile.ChromePath,
            Arguments = arguments.ToString(),
            UseShellExecute = false
        };

        var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Chrome did not start.");

        return process;
    }
}
