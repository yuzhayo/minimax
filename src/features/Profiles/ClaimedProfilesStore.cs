using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Remembers which profiles have already been checked in, so a later run can
/// skip them. Profiles are stored by their Chrome folder name ("Profile 3"),
/// which stays the same even if the profile is renamed in Chrome.
///
/// Every claim is stamped with the date it was made. A claim only counts
/// while it is made TODAY - that is what the site itself does, and it means
/// the marks do not have to be cleared by hand. The day the machine's clock
/// rolls over, yesterday's claims stop counting and the column starts empty
/// again.
///
/// The file on disk looks like this:
///
///     {
///       "Profile 3": "2026-11-23",
///       "Default":   "2026-11-23"
///     }
///
/// The older plain-list format is still read, and is treated as "claimed
/// today" so nothing is lost when the app is upgraded.
/// </summary>
internal sealed class ClaimedProfilesStore
{
    private readonly string _path;

    /// <summary>Folder name -> the date the claim was made ("yyyy-MM-dd").</summary>
    private readonly Dictionary<string, string> _claimed;

    public ClaimedProfilesStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromeProfileLauncher",
            "claimed-profiles.json");
        _claimed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string FilePath => _path;

    /// <summary>Today's local date, in the format the claims are stamped with.</summary>
    public static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    /// <summary>The folder names of the profiles already checked in today.</summary>
    public IReadOnlyCollection<string> Claimed => _claimed.Keys;

    /// <summary>
    /// Reads the file and drops every claim that was made on an earlier day.
    ///
    /// The filtering happens here, at load time, so the app never shows a
    /// stale tick in the first place.
    /// </summary>
    public void Load()
    {
        _claimed.Clear();

        if (!File.Exists(_path))
            return;

        try
        {
            var json = File.ReadAllText(_path);
            using var document = JsonDocument.Parse(json);
            var today = Today;

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                // The old format: a plain list of folder names with no date.
                // They were written today, so they count as today's claims.
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var name = item.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        _claimed[name.Trim()] = today;
                }
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (string.IsNullOrWhiteSpace(property.Name))
                        continue;

                    var date = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(date))
                        continue;

                    // A claim only counts on the day it was made.
                    if (!date.Equals(today, StringComparison.OrdinalIgnoreCase))
                        continue;

                    _claimed[property.Name.Trim()] = date;
                }
            }
        }
        catch (Exception)
        {
            // A corrupt file must never stop the app from starting.
            _claimed.Clear();
        }
    }

    /// <summary>
    /// True when the profile was checked in TODAY.
    ///
    /// The date is compared here, not only at load time, so a claim made
    /// yesterday stops counting the moment the day changes - even if the app
    /// has been open the whole time.
    /// </summary>
    public bool IsClaimed(string directoryName)
    {
        return _claimed.TryGetValue(directoryName, out var date) &&
               date.Equals(Today, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Marks a profile as checked in, stamped with today's date.</summary>
    public void SetClaimed(string directoryName, bool isClaimed)
    {
        if (isClaimed)
            _claimed[directoryName.Trim()] = Today;
        else
            _claimed.Remove(directoryName);
    }

    /// <summary>
    /// Forgets every claim. Used by the "Clear claimed" button.
    /// </summary>
    public void ClearAll()
    {
        _claimed.Clear();
    }

    /// <summary>
    /// Replaces the stored set with the given folder names.
    ///
    /// A profile that is already in the store keeps the date it had, so a
    /// save that happens after midnight cannot turn yesterday's claim into
    /// today's.
    /// </summary>
    public void Replace(IEnumerable<string> directoryNames)
    {
        var today = Today;
        var kept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in directoryNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var trimmed = name.Trim();

            kept[trimmed] = _claimed.TryGetValue(trimmed, out var existing)
                ? existing
                : today;
        }

        _claimed.Clear();
        foreach (var pair in kept)
            _claimed[pair.Key] = pair.Value;

        Save();
    }

    /// <summary>Writes the claims to disk.</summary>
    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var json = JsonSerializer.Serialize(
                _claimed,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception)
        {
            // Saving is a convenience, never a reason to interrupt the user.
        }
    }
}
