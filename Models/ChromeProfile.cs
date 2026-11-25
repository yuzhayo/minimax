using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ChromeProfileLauncher.Models;

/// <summary>One Chrome profile discovered on this PC.</summary>
internal sealed class ChromeProfile
{
    /// <summary>Chrome's internal folder name, e.g. "Default" or "Profile 1".</summary>
    public string DirectoryName { get; init; } = string.Empty;

    /// <summary>The display name the user gave the profile.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The Google account attached to the profile, if any.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Full path to chrome.exe found on this PC.</summary>
    public string ChromePath { get; init; } = string.Empty;

    // ---- Fields that feed the card grid (mirrors Chrome's own profile picker) ----

    /// <summary>Full path of the Google account picture, when the profile has one.</summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>True when the profile picture file was actually found on disk.</summary>
    public bool HasAvatarFile { get; init; }

    /// <summary>True when Chrome shows its built-in default avatar for this profile.</summary>
    public bool IsUsingDefaultAvatar { get; init; }

    /// <summary>Fill colour Chrome uses for the default avatar circle.</summary>
    public Color AvatarFillColor { get; init; } = Color.FromArgb(58, 64, 78);

    /// <summary>Stroke colour Chrome uses for the default avatar circle.</summary>
    public Color AvatarStrokeColor { get; init; } = Color.FromArgb(90, 96, 110);

    /// <summary>True when the profile is signed in with a Google account.</summary>
    public bool IsSignedIn { get; init; }

    /// <summary>The Google account's display name (may differ from the profile name).</summary>
    public string GaiaName { get; init; } = string.Empty;

    /// <summary>Chrome's own ordering (position in Local State's profiles_order).</summary>
    public int OrderIndex { get; init; }

    /// <summary>Position in Chrome's last_active_profiles list; -1 when not listed.</summary>
    public int RecentRank { get; init; } = -1;

    /// <summary>Raw active_time value from Local State (seconds since 1601).</summary>
    public double ActiveTime { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? DirectoryName : Name;

    /// <summary>Two-letter initials used when drawing a default avatar circle.</summary>
    public string Initials
    {
        get
        {
            var source = DisplayName.Trim();
            if (source.Length == 0)
                return "?";

            var words = source.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

            // Profile names often start with a number ("01 dhepilinuz"), which makes a
            // useless initial. Drop leading words that are purely numeric.
            var meaningful = new List<string>();
            foreach (var word in words)
            {
                if (meaningful.Count == 0 && word.All(char.IsDigit))
                    continue;
                meaningful.Add(word);
            }

            if (meaningful.Count == 0)
                meaningful = new List<string>(words);

            if (meaningful.Count >= 2)
                return (meaningful[0][0].ToString() + meaningful[1][0]).ToUpperInvariant();
            if (meaningful.Count == 1)
                return meaningful[0].Length >= 2
                    ? meaningful[0].Substring(0, 2).ToUpperInvariant()
                    : meaningful[0].ToUpperInvariant();
            return "?";
        }
    }

    /// <summary>Human-friendly "last active" text, or an empty string when unknown.</summary>
    public string LastActiveText
    {
        get
        {
            if (ActiveTime <= 0)
                return string.Empty;

            try
            {
                // Chrome stores active_time as a Unix epoch timestamp in seconds
                // (with fractional part), not a Windows FILETIME.
                var when = DateTimeOffset.FromUnixTimeSeconds((long)ActiveTime).LocalDateTime;
                var delta = DateTime.Now - when;
                if (delta.TotalMinutes < 2) return "Active now";
                if (delta.TotalHours < 1) return $"Active {(int)delta.TotalMinutes} min ago";
                if (delta.TotalDays < 1) return $"Active {(int)delta.TotalHours} hr ago";
                if (delta.TotalDays < 30) return $"Active {(int)delta.TotalDays} day(s) ago";
                return "Active " + when.ToString("d MMM yyyy");
            }
            catch (ArgumentOutOfRangeException)
            {
                return string.Empty;
            }
            catch (OverflowException)
            {
                return string.Empty;
            }
        }
    }
}
