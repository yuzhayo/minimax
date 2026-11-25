using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ChromeProfileLauncher.Features.Update;

internal sealed record MiniMaxRuntimeVersion(Version Version, string DisplayVersion,
    string BaseDirectory, bool IsPortable, string? PortableReason);

internal static class MiniMaxVersionProvider
{
    public static MiniMaxRuntimeVersion Read()
    {
        var directory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var executable = Environment.ProcessPath ?? Path.Combine(directory, "MiniMax.exe");
        var product = File.Exists(executable) ? FileVersionInfo.GetVersionInfo(executable).ProductVersion : null;
        var text = (product ?? typeof(MiniMaxVersionProvider).Assembly.GetName().Version?.ToString(3) ?? "0.0.0").Split('+')[0];
        if (!Version.TryParse(text, out var version)) version = new Version(0, 0, 0);
        var manifestPath = Path.Combine(directory, "release.json");
        if (!File.Exists(manifestPath)) return new(version, text, directory, false, "Development build — updates install only in Portable.");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var manifestVersion = root.GetProperty("version").GetString();
            var distribution = root.GetProperty("distribution").GetString();
            var valid = distribution == "portable-self-contained" && manifestVersion == text;
            return new(version, text, directory, valid, valid ? null : "Portable release metadata does not match this executable.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException)
        { return new(version, text, directory, false, "Portable release metadata could not be read."); }
    }
}
