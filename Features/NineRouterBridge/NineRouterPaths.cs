using System;
using System.IO;

namespace ChromeProfileLauncher.Services;

internal sealed class NineRouterPaths
{
    // CONFIG AND CONTRACT
    public const string Provider = "minimax-code-global";
    public const string SupportedAppVersion = "0.5.104";
    public const string SupportedSchemaVersion = "1";
    public const string SupportedBackupSchemaVersion = "2";
    public Uri BaseUrl { get; }
    public string DatabasePath { get; }
    public string WalPath => DatabasePath + "-wal";
    public string ShmPath => DatabasePath + "-shm";
    public string MappingPath { get; }

    public NineRouterPaths(string? databasePath = null, string? mappingPath = null, Uri? baseUrl = null)
    {
        BaseUrl = baseUrl ?? new Uri("http://127.0.0.1:20129/");
        if (!BaseUrl.IsLoopback || BaseUrl.Scheme != Uri.UriSchemeHttp || BaseUrl.UserInfo.Length != 0)
            throw new ArgumentException("The 9Router bridge requires a loopback HTTP address.");
        DatabasePath = Path.GetFullPath(databasePath ?? @"C:\Apps\9router\data\db\data.sqlite");
        MappingPath = Path.GetFullPath(mappingPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromeProfileLauncher", "minimax-9router-links.json"));
    }
}
