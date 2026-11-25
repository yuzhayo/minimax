using System;

namespace ChromeProfileLauncher.Models;

/// <summary>Stable ownership link; labels can change, folder and UUID cannot.</summary>
internal sealed record NineRouterProfileLink
{
    // CONFIG AND CONTRACT
    public string DirectoryName { get; init; } = string.Empty;
    public Guid ConnectionId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public DateTimeOffset LinkedAt { get; init; } = DateTimeOffset.UtcNow;
}
