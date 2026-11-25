using System;
using System.IO;
using System.Text.Json;

namespace ChromeProfileLauncher.Services;

/// <summary>Tracks one active attempt so failure/Stop can remove its exact temporary UUID.</summary>
internal sealed class NineRouterPendingRefreshStore
{
    // CONFIG AND CONTRACT
    internal sealed record Pending(string DirectoryName, Guid TargetId, string DesiredName, DateTimeOffset StartedAt,
        int OriginalAccountCount, Guid? SourceId = null, bool BrowserConfirmed = false,
        bool CreateNew = false, Guid? FinalConnectionId = null);
    private readonly string _path;
    public NineRouterPendingRefreshStore(string path) => _path = path;

    // STATE AND COORDINATION
    public Pending? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var value = JsonSerializer.Deserialize<Pending>(File.ReadAllText(_path));
            if (value == null || string.IsNullOrWhiteSpace(value.DirectoryName) ||
                (value.TargetId == Guid.Empty && !value.CreateNew) || value.OriginalAccountCount < 0 ||
                value.FinalConnectionId == Guid.Empty) throw new InvalidDataException("Invalid pending refresh record.");
            return value;
        }
        catch (JsonException) { throw new InvalidDataException("Pending refresh journal is corrupt. Review it before retrying."); }
    }
    public void Save(Pending value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, null); else File.Move(temporary, _path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Clear() { if (File.Exists(_path)) File.Delete(_path); }
}
