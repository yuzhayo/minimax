using System;

namespace ChromeProfileLauncher.Models;

internal sealed class NineRouterOAuthSession
{
    // CONFIG AND CONTRACT: keep secrets out of generated record ToString().
    public required Uri AuthorizationUrl { get; init; }
    public required string DeviceCode { get; init; }
    public required string CodeVerifier { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required TimeSpan PollInterval { get; init; }
    public override string ToString() => "MiniMax Global OAuth session (credentials redacted)";
}

internal sealed record NineRouterOAuthPoll(Guid? ConnectionId, bool SlowDown);
