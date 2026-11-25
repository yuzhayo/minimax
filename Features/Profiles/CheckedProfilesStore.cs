using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Remembers which profiles the user ticked in the MiniMax table, so the
/// ticks come back the next time the app runs. The ticked profiles are stored by
/// their Chrome folder name ("Profile 3", "Default"), which is stable even if
/// the profile is renamed.
/// </summary>
internal sealed class CheckedProfilesStore
{
    private readonly string _path;
    private readonly HashSet<string> _checked;

    public CheckedProfilesStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromeProfileLauncher",
            "checked-profiles.json");
        _checked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public string FilePath => _path;

    /// <summary>The folder names of the ticked profiles.</summary>
    public IReadOnlyCollection<string> Checked => _checked;

    public void Load()
    {
        _checked.Clear();

        if (!File.Exists(_path))
            return;

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<List<string>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (loaded == null)
                return;

            foreach (var name in loaded)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    _checked.Add(name.Trim());
            }
        }
        catch (Exception)
        {
            // A corrupt file must never stop the app from starting. Start unticked.
            _checked.Clear();
        }
    }

    public bool IsChecked(string directoryName) => _checked.Contains(directoryName);

    public void SetChecked(string directoryName, bool isChecked)
    {
        if (isChecked)
            _checked.Add(directoryName);
        else
            _checked.Remove(directoryName);
    }

    /// <summary>
    /// Replaces the stored set with the given folder names. Anything that is no
    /// longer ticked is dropped, and profiles that no longer exist are not kept.
    /// </summary>
    public void Replace(IEnumerable<string> directoryNames)
    {
        _checked.Clear();
        foreach (var name in directoryNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _checked.Add(name.Trim());
        }
        Save();
    }

    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var json = JsonSerializer.Serialize(
                new List<string>(_checked),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception)
        {
            // Saving is a convenience, never a reason to interrupt the user.
        }
    }
}
