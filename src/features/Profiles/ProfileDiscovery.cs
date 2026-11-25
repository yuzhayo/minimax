using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Reads Chrome's "Local State" file to build the list of available profiles.
/// Read-only: this never modifies, copies, or relocates Chrome data.
/// </summary>
internal static class ProfileDiscovery
{
    public static IReadOnlyList<ChromeProfile> Discover(out string warning)
    {
        var result = new List<ChromeProfile>();
        warning = string.Empty;
        var chromePath = ChromePaths.FindChromeExecutable();

        string localStatePath;
        try
        {
            localStatePath = ChromePaths.LocalStatePath;
        }
        catch (Exception ex)
        {
            warning = "Cannot locate Chrome user data: " + ex.Message;
            return result;
        }

        if (!File.Exists(localStatePath))
        {
            warning = "Chrome profile data was not found at:\n" + localStatePath;
            return result;
        }

        Dictionary<string, int> recentOrder;
        Dictionary<string, int> profileOrder;

        try
        {
            using var stream = File.OpenRead(localStatePath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            recentOrder = ReadStringList(root, "profile", "last_active_profiles");
            profileOrder = ReadStringList(root, "profile", "profiles_order");

            if (!root.TryGetProperty("profile", out var profileEl) ||
                !profileEl.TryGetProperty("info_cache", out var cacheEl) ||
                cacheEl.ValueKind != JsonValueKind.Object)
            {
                warning = "No profile information was found in Chrome's Local State.";
                return result;
            }

            var orderCounter = 0;
            foreach (var entry in cacheEl.EnumerateObject())
            {
                var displayName = string.Empty;
                var userName = string.Empty;
                var gaiaName = string.Empty;
                var pictureFile = string.Empty;
                var isUsingDefaultAvatar = true;
                var isSignedIn = false;
                var fill = Color.FromArgb(58, 64, 78);
                var stroke = Color.FromArgb(90, 96, 110);
                double activeTime = 0;

                if (entry.Value.ValueKind == JsonValueKind.Object)
                {
                    displayName = GetString(entry.Value, "name");
                    userName = GetString(entry.Value, "user_name");
                    gaiaName = GetString(entry.Value, "gaia_name");
                    pictureFile = GetString(entry.Value, "gaia_picture_file_name");
                    isUsingDefaultAvatar = GetBool(entry.Value, "is_using_default_avatar", true);
                    activeTime = GetDouble(entry.Value, "active_time");

                    // A profile counts as signed in when it carries a Google account
                    // address. is_consented_primary_account is only true for a subset
                    // of signed-in profiles, so it cannot be used on its own.
                    var consented = GetBool(entry.Value, "is_consented_primary_account", false);
                    isSignedIn = userName.Length > 0 || consented;

                    fill = GetColor(entry.Value, "default_avatar_fill_color", fill);
                    stroke = GetColor(entry.Value, "default_avatar_stroke_color", stroke);
                }

                // The real Google account picture lives inside the profile's own folder.
                string avatarPath = string.Empty;
                bool hasAvatarFile = false;
                if (pictureFile.Length > 0)
                {
                    var candidate = Path.Combine(
                        ChromePaths.UserDataDirectory, entry.Name, pictureFile);
                    if (File.Exists(candidate))
                    {
                        avatarPath = candidate;
                        hasAvatarFile = true;
                    }
                }

                result.Add(new ChromeProfile
                {
                    DirectoryName = entry.Name,
                    Name = displayName,
                    UserName = userName,
                    GaiaName = gaiaName,
                    ChromePath = chromePath,
                    AvatarPath = avatarPath,
                    HasAvatarFile = hasAvatarFile,
                    IsUsingDefaultAvatar = isUsingDefaultAvatar,
                    AvatarFillColor = fill,
                    AvatarStrokeColor = stroke,
                    IsSignedIn = isSignedIn,
                    ActiveTime = activeTime,
                    RecentRank = recentOrder.TryGetValue(entry.Name, out var rank) ? rank : -1,
                    OrderIndex = profileOrder.TryGetValue(entry.Name, out var order) ? order : orderCounter
                });

                orderCounter++;
            }
        }
        catch (Exception ex)
        {
            warning = "Could not read Chrome profile data: " + ex.Message;
            return result;
        }

        if (result.Count == 0)
        {
            warning = "Chrome is installed but no profiles were found.";
        }
        else if (string.IsNullOrEmpty(chromePath))
        {
            warning = "Profiles were found, but chrome.exe could not be located.";
        }

        return result;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static bool GetBool(JsonElement element, string name, bool fallback)
    {
        if (element.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
        }
        return fallback;
    }

    private static double GetDouble(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                return number;
            if (value.ValueKind == JsonValueKind.String &&
                double.TryParse(value.GetString(), out var parsed))
                return parsed;
        }
        return 0;
    }

    /// <summary>
    /// Chrome stores some colours as signed 32-bit ARGB integers, e.g. -14933977.
    /// </summary>
    private static Color GetColor(JsonElement element, string name, Color fallback)
    {
        if (!element.TryGetProperty(name, out var value))
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return Color.FromArgb(unchecked((int)number));

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            if (text.StartsWith("#", StringComparison.Ordinal))
                text = text.Substring(1);
            if (text.Length == 6)
                text = "FF" + text;
            if (text.Length == 8 && int.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return Color.FromArgb(unchecked((int)parsed));
        }

        return fallback;
    }

    /// <summary>Reads a JSON array of strings and maps each value to its index.</summary>
    private static Dictionary<string, int> ReadStringList(JsonElement root, string section, string name)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(section, out var sectionEl))
            return map;
        if (!sectionEl.TryGetProperty(name, out var listEl) || listEl.ValueKind != JsonValueKind.Array)
            return map;

        var index = 0;
        foreach (var item in listEl.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrEmpty(text) && !map.ContainsKey(text))
                    map[text] = index;
            }
            index++;
        }
        return map;
    }
}
