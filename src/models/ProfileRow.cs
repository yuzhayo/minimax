namespace ChromeProfileLauncher.Models;

/// <summary>
/// One row in the MiniMax profile table. It wraps a discovered profile and
/// holds the two checkbox states the table keeps:
/// <list type="bullet">
///   <item><description><see cref="IsChecked"/> - the user ticked this profile to run it.</description></item>
///   <item><description><see cref="IsClaimed"/> - this profile has already been checked in.</description></item>
/// </list>
/// </summary>
internal sealed class ProfileRow
{
    /// <summary>The discovered Chrome profile behind this row.</summary>
    public ChromeProfile Profile { get; }

    /// <summary>Whether the row's "run" checkbox is ticked.</summary>
    public bool IsChecked { get; set; }

    /// <summary>Whether this profile has already been checked in.</summary>
    public bool IsClaimed { get; set; }

    /// <summary>Operation label only; credentials never enter this model.</summary>
    public string RefreshTokenStatus { get; set; } = "Refresh";

    /// <summary>Per-row MiniMax sign-in operation label.</summary>
    public string SignInStatus { get; set; } = "Sign in";

    public ProfileRow(ChromeProfile profile)
    {
        Profile = profile;
    }

    public string DisplayName => Profile.DisplayName;

    public string DirectoryName => Profile.DirectoryName;

    public string UserName => Profile.UserName;

    public string LastActiveText => Profile.LastActiveText;
}
