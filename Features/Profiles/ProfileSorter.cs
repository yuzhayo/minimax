using System;
using System.Collections.Generic;
using System.Linq;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

/// <summary>Shared sorting logic so the table and the card grid always agree.</summary>
internal static class ProfileSorter
{
    public static List<ChromeProfile> Sort(IEnumerable<ChromeProfile> profiles, ProfileSortMode mode)
    {
        var list = profiles.ToList();

        switch (mode)
        {
            case ProfileSortMode.Name:
                return list
                    .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(p => p.DirectoryName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

            case ProfileSortMode.Folder:
                return list
                    .OrderBy(p => FolderNumber(p.DirectoryName))
                    .ThenBy(p => p.DirectoryName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

            case ProfileSortMode.Recent:
            default:
                return list
                    .OrderBy(p => p.RecentRank < 0 ? int.MaxValue : p.RecentRank)
                    .ThenByDescending(p => p.ActiveTime)
                    .ThenBy(p => p.OrderIndex)
                    .ToList();
        }
    }

    /// <summary>Turns "Profile 10" into 10 and "Default" into 0.</summary>
    private static int FolderNumber(string directoryName)
    {
        if (string.Equals(directoryName, "Default", StringComparison.OrdinalIgnoreCase))
            return 0;

        var space = directoryName.LastIndexOf(' ');
        if (space >= 0 && space < directoryName.Length - 1 &&
            int.TryParse(directoryName.Substring(space + 1), out var number))
            return number;

        return int.MaxValue;
    }
}
