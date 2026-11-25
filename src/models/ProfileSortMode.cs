namespace ChromeProfileLauncher.Models;

/// <summary>The ways the profile list can be ordered.</summary>
internal enum ProfileSortMode
{
    /// <summary>Chrome's own ordering: recently active first, then its stored order.</summary>
    Recent,

    /// <summary>Alphabetical by profile name (A-Z).</summary>
    Name,

    /// <summary>By Chrome's internal folder number.</summary>
    Folder
}
