using System;
using System.IO;
using System.Text.Json;

namespace ChromeProfileLauncher.Services;

/// <summary>Owns the check-in URL, its validation and persistent user setting.</summary>
internal sealed class MiniMaxSettingsStore
{
    // CONFIG AND CONTRACT
    public const string DefaultCheckInUrl = "https://agent.minimax.io/";
    private readonly string _path;
    public string CheckInUrl { get; private set; } = DefaultCheckInUrl;
    public string? LoadWarning { get; private set; }
    private sealed class Document
    {
        public int Version { get; set; } = 1;
        public string CheckInUrl { get; set; } = DefaultCheckInUrl;
    }
    public MiniMaxSettingsStore(string? path = null) => _path = Path.GetFullPath(path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChromeProfileLauncher", "minimax-settings.json"));

    // LOGIC AND CALCULATIONS
    private static bool Normalize(string? input, out string url)
    {
        var value = input?.Trim() ?? "";
        url = "";
        if (value.Length == 0 || (value.Contains("://", StringComparison.Ordinal) &&
            !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))) return false;
        var candidate = value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? value : ChromeLauncher.NormalizeUrl(value);
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length != 0 || !uri.IsWellFormedOriginalString()) return false;
        url = uri.AbsoluteUri;
        return true;
    }

    // STATE AND COORDINATION
    public void Load()
    {
        LoadWarning = null;
        if (!File.Exists(_path)) return;
        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (document == null || document.Version != 1 || !Normalize(document.CheckInUrl, out var url))
                throw new InvalidDataException("Invalid settings document.");
            CheckInUrl = url;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            CheckInUrl = DefaultCheckInUrl;
            LoadWarning = "Saved URL settings could not be read. The default URL is in use; open setting to fix it.";
        }
    }

    public bool TrySetCheckInUrl(string? input, out string error)
    {
        if (!Normalize(input, out var url))
        { error = "Enter a valid HTTP or HTTPS URL. The previous saved URL is unchanged."; return false; }
        try
        {
            Save(url);
            CheckInUrl = url;
            LoadWarning = null;
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { error = "The URL could not be saved. The previous saved URL is unchanged."; return false; }
    }

    private void Save(string url)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new Document { CheckInUrl = url }, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, null); else File.Move(temporary, _path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
