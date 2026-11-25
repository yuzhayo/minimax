using System;

namespace ChromeProfileLauncher.Models;

/// <summary>Allowlisted connection metadata. Credentials never enter this model.</summary>
internal sealed record NineRouterAccount(Guid Id, string Name, string? Email, bool IsActive, int Priority);

internal sealed record NineRouterLinkCandidate(string DirectoryName, string DisplayName, string? Email);
