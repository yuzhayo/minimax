using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

internal sealed class NineRouterOAuthBridge
{
    // CONFIG AND CONTRACT
    private readonly HttpClient _http;
    public TimeSpan? TestPollInterval { get; init; }
    public NineRouterOAuthBridge(HttpClient http) => _http = http;

    // LIFECYCLE AND CLEANUP: caller owns the client, preventing per-request sockets.
    public static HttpClient CreateLocalClient(NineRouterPaths paths)
    {
        var directory = Path.GetDirectoryName(paths.DatabasePath)!;
        var dataRoot = Directory.GetParent(directory)!.FullName;
        var machine = File.ReadAllText(Path.Combine(dataRoot, "machine-id")).Trim();
        var secret = File.ReadAllText(Path.Combine(dataRoot, "auth", "cli-secret")).Trim();
        if (machine.Length == 0 || secret.Length == 0) throw new InvalidDataException("9Router local CLI authentication is unavailable.");
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(machine + "9r-cli-auth" + secret))).ToLowerInvariant()[..16];
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            BaseAddress = paths.BaseUrl,
            Timeout = TimeSpan.FromSeconds(35)
        };
        client.DefaultRequestHeaders.Add("x-9r-cli-token", token);
        return client;
    }

    // STATE AND COORDINATION
    public async Task<IReadOnlyList<NineRouterAccount>> ReadAccountsAsync(CancellationToken token = default)
    {
        var result = new List<NineRouterAccount>();
        var seen = new HashSet<Guid>();
        for (var page = 1; ; page++)
        {
            using var response = await _http.GetAsync($"api/providers/client?provider={NineRouterPaths.Provider}&page={page}&pageSize=500", token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"9Router account lookup failed (HTTP {(int)response.StatusCode}).");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = document.RootElement;
            if (!root.TryGetProperty("connections", out var list) || list.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("9Router returned an invalid account list.");
            foreach (var account in list.EnumerateArray())
            {
                if (ReadString(account, "provider") != NineRouterPaths.Provider || ReadString(account, "authType") != "oauth") continue;
                if (!Guid.TryParse(ReadString(account, "id"), out var id) || !seen.Add(id))
                    throw new InvalidDataException("Invalid or repeated 9Router account identity.");
                result.Add(new NineRouterAccount(id, ReadString(account, "name") ?? "", ReadString(account, "email"),
                    !account.TryGetProperty("isActive", out var active) || active.ValueKind != JsonValueKind.False,
                    account.TryGetProperty("priority", out var priority) && priority.TryGetInt32(out var number) ? number : int.MaxValue));
            }
            var total = root.TryGetProperty("pagination", out var pagination) && pagination.TryGetProperty("totalPages", out var pages) ? pages.GetInt32() : 1;
            if (page >= total) break;
            if (page >= 100) throw new InvalidDataException("9Router pagination did not finish.");
        }
        return result;
    }

    // LOGIC AND CALCULATIONS
    public async Task<NineRouterOAuthSession> StartAsync(CancellationToken token = default)
    {
        using var response = await _http.GetAsync($"api/oauth/{NineRouterPaths.Provider}/device-code", token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"9Router OAuth start failed (HTTP {(int)response.StatusCode}).");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;
        var url = ReadString(root, "verification_uri_complete") ?? ReadString(root, "verification_uri");
        var code = ReadString(root, "device_code");
        var verifier = ReadString(root, "codeVerifier");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "account.minimax.io" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            !uri.AbsolutePath.StartsWith("/oauth", StringComparison.Ordinal))
            throw new InvalidDataException("9Router returned an unexpected MiniMax Global authorization URL.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(verifier))
            throw new InvalidDataException("9Router did not return a device code and PKCE verifier.");
        if (root.TryGetProperty("_minimaxPollByUser", out var byUser) && byUser.ValueKind == JsonValueKind.True)
            throw new InvalidDataException("This 9Router build does not forward the MiniMax user-code polling flag. Restart authorization for a device-code response.");
        var expires = root.TryGetProperty("expires_in", out var expiry) && expiry.TryGetInt32(out var seconds) ? seconds : 600;
        var interval = root.TryGetProperty("interval", out var poll) && poll.TryGetInt32(out var pollSeconds) ? pollSeconds : 5;
        if (expires <= 0 || expires > 3600 || interval < 1 || interval > 60)
            throw new InvalidDataException("9Router returned invalid OAuth timing.");
        return new NineRouterOAuthSession
        {
            AuthorizationUrl = uri, DeviceCode = code, CodeVerifier = verifier,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expires), PollInterval = TimeSpan.FromSeconds(interval)
        };
    }

    public async Task<NineRouterOAuthPoll> PollOnceAsync(NineRouterOAuthSession session, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        // Once submitted, finish reading the response even if Stop was pressed.
        // Otherwise 9Router may create a source row whose returned UUID is lost.
        using var content = new StringContent(JsonSerializer.Serialize(new { deviceCode = session.DeviceCode, codeVerifier = session.CodeVerifier }), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"api/oauth/{NineRouterPaths.Provider}/poll", content, CancellationToken.None);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"9Router OAuth poll failed (HTTP {(int)response.StatusCode}).");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True)
        {
            if (!root.TryGetProperty("connection", out var connection) || ReadString(connection, "provider") != NineRouterPaths.Provider ||
                !Guid.TryParse(ReadString(connection, "id"), out var id) || id == Guid.Empty)
                throw new InvalidDataException("9Router returned no valid Global connection ID.");
            return new NineRouterOAuthPoll(id, false);
        }
        var error = ReadString(root, "error");
        if (error is "authorization_pending" or "slow_down") return new NineRouterOAuthPoll(null, error == "slow_down");
        // Do not propagate server payloads: they may contain credentials.
        throw new InvalidOperationException(error is "access_denied" or "expired_token"
            ? "MiniMax authorization was denied or expired. Existing tokens were not replaced."
            : "9Router OAuth failed. Existing tokens were not replaced.");
    }

    public async Task<Guid> WaitForTokensAsync(NineRouterOAuthSession session, Action? pending = null, CancellationToken token = default)
    {
        var interval = TestPollInterval ?? session.PollInterval;
        while (DateTimeOffset.UtcNow < session.ExpiresAt)
        {
            await Task.Delay(interval, token);
            var result = await PollOnceAsync(session, token);
            if (result.ConnectionId is Guid id) return id;
            pending?.Invoke();
            if (result.SlowDown) interval = TestPollInterval ?? TimeSpan.FromSeconds(Math.Min(interval.TotalSeconds + 5, 30));
        }
        throw new TimeoutException("MiniMax OAuth authorization timed out. Existing tokens were not replaced.");
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
