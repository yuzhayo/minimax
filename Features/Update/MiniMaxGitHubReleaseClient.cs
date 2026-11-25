using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChromeProfileLauncher.Features.Update;

internal sealed record MiniMaxReleaseAsset(string Name, Uri DownloadUrl, long Size);
internal sealed record MiniMaxGitHubRelease(Version Version, string Tag, MiniMaxReleaseAsset Zip, MiniMaxReleaseAsset Sha);

internal sealed class MiniMaxGitHubReleaseClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    public MiniMaxGitHubReleaseClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient(); _ownsClient = http == null;
        if (!_http.DefaultRequestHeaders.UserAgent.Any()) _http.DefaultRequestHeaders.UserAgent.ParseAdd("MiniMax-Updater/1.0");
        if (!_http.DefaultRequestHeaders.Accept.Any()) _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    public async Task<MiniMaxGitHubRelease> LatestAsync(CancellationToken token)
    {
        using var response = await _http.GetAsync("https://api.github.com/repos/yuzhayo/minimax/releases/latest", token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("Latest GitHub release is not stable.");
        var tag = root.GetProperty("tag_name").GetString() ?? throw new InvalidDataException("Release tag is missing.");
        if (!Version.TryParse(tag.TrimStart('v'), out var version) || version.Revision > 0)
            throw new InvalidDataException("Release tag is not three-part semantic versioning.");
        MiniMaxReleaseAsset Asset(string name)
        {
            var matches = root.GetProperty("assets").EnumerateArray().Where(asset => asset.GetProperty("name").GetString() == name).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Release asset is missing or ambiguous: " + name);
            return new(name, new Uri(matches[0].GetProperty("browser_download_url").GetString()!), matches[0].GetProperty("size").GetInt64());
        }
        var zipName = $"MiniMax-v{version.Major}.{version.Minor}.{version.Build}-win-x64-portable.zip";
        return new(version, tag, Asset(zipName), Asset(zipName + ".sha256"));
    }
    public async Task<string> ReadTextAsync(Uri uri, CancellationToken token) => await _http.GetStringAsync(uri, token);
    public async Task DownloadAsync(MiniMaxReleaseAsset asset, string path, IProgress<int> progress, CancellationToken token)
    {
        using var response = await _http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength ?? asset.Size;
        await using var source = await response.Content.ReadAsStreamAsync(token);
        await using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await source.ReadAsync(buffer, token)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), token); total += read;
            if (length > 0) progress.Report((int)Math.Min(99, total * 100 / length));
        }
        await destination.FlushAsync(token); progress.Report(100);
    }
    public void Dispose() { if (_ownsClient) _http.Dispose(); }
}
