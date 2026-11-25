using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Text;
using System.Globalization;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

internal sealed class NineRouterProfileMapStore
{
    // CONFIG AND CONTRACT
    private readonly string _path;
    private sealed class Document
    {
        public int Version { get; set; } = 1;
        public List<NineRouterProfileLink> Links { get; set; } = new();
    }
    public NineRouterProfileMapStore(string path) => _path = Path.GetFullPath(path);

    // LOGIC AND CALCULATIONS
    private static List<NineRouterProfileLink> Validate(IEnumerable<NineRouterProfileLink> links)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<Guid>();
        var result = new List<NineRouterProfileLink>();
        foreach (var link in links)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.DirectoryName) ||
                link.DirectoryName != link.DirectoryName.Trim() ||
                link.DirectoryName.IndexOfAny(new[] { '/', '\\', '"' }) >= 0 ||
                link.DirectoryName is "." or ".." || link.ConnectionId == Guid.Empty)
                throw new InvalidDataException("Invalid Chrome folder or 9Router connection ID in mapping.");
            if (!folders.Add(link.DirectoryName) || !ids.Add(link.ConnectionId))
                throw new InvalidDataException("Duplicate Chrome folder or 9Router connection ID in mapping.");
            result.Add(link);
        }
        result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.DirectoryName, b.DirectoryName));
        return result;
    }

    // STATE AND COORDINATION
    public static IReadOnlyList<NineRouterProfileLink> Match(
        IEnumerable<NineRouterLinkCandidate> profiles, IReadOnlyList<NineRouterAccount> accounts,
        IReadOnlyList<NineRouterProfileLink> saved)
    {
        var result = Validate(saved).Where(l => accounts.Any(a => a.Id == l.ConnectionId)).ToList();
        var used = result.Select(l => l.ConnectionId).ToHashSet();
        var candidates = profiles.Where(p => !result.Any(l => l.DirectoryName.Equals(p.DirectoryName, StringComparison.OrdinalIgnoreCase))).ToList();
        // Two passes: exact email first. Only unique one-to-one matches survive.
        foreach (var byEmail in new[] { true, false })
        {
            string Key(NineRouterLinkCandidate p) => byEmail ? (p.Email ?? "").Trim().ToLowerInvariant() : NormalizeName(p.DisplayName);
            string AccountKey(NineRouterAccount a) => byEmail ? (string.IsNullOrWhiteSpace(a.Email) ? a.Name : a.Email).Trim().ToLowerInvariant() : NormalizeName(a.Name);
            foreach (var profile in candidates.ToArray())
            {
                var key = Key(profile);
                if (key.Length == 0 || candidates.Count(p => Key(p) == key) != 1) continue;
                var matches = accounts.Where(a => !used.Contains(a.Id) && AccountKey(a) == key).ToList();
                if (matches.Count != 1) continue;
                var match = matches[0];
                result.Add(new NineRouterProfileLink { DirectoryName = profile.DirectoryName, ConnectionId = match.Id, DisplayName = profile.DisplayName, Email = profile.Email });
                used.Add(match.Id);
                candidates.Remove(profile);
            }
        }
        return Validate(result);
    }

    private static string NormalizeName(string name)
    {
        // Strip only the operator's leading ordering number, not account suffixes.
        var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (words.Count > 1 && words[0].All(char.IsDigit)) words.RemoveAt(0);
        return new string(string.Join(" ", words).Normalize(NormalizationForm.FormKC)
            .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    public IReadOnlyList<NineRouterProfileLink> Load()
    {
        if (!File.Exists(_path)) return Array.Empty<NineRouterProfileLink>();
        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path));
            if (document == null || document.Version != 1 || document.Links == null)
                throw new InvalidDataException("Unsupported profile mapping format.");
            return Validate(document.Links);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The profile mapping is corrupt. Restore or repair it before refreshing tokens.", ex);
        }
    }

    public void Save(IEnumerable<NineRouterProfileLink> links)
    {
        var data = new Document { Links = Validate(links) };
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, null);
            else File.Move(temporary, _path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
