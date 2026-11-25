using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Features.Update;

internal enum MiniMaxUpdateCheckResult { DevelopmentBuild, UpToDate, UpdateStarted, Failed }

internal interface IMiniMaxUpdateService : IDisposable
{
    MiniMaxRuntimeVersion Runtime { get; }
    void StartBackgroundCheck();
    Task<MiniMaxUpdateCheckResult> CheckManuallyAsync(CancellationToken token = default);
}

internal sealed class MiniMaxUpdateCoordinator : IMiniMaxUpdateService
{
    private readonly IMiniMaxOperationCoordinator _operations;
    private readonly IWin32Window _owner;
    private readonly Action _requestClose;
    private readonly MiniMaxGitHubReleaseClient _github = new();
    private readonly SemaphoreSlim _check = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private Version? _shownVersion;
    public MiniMaxRuntimeVersion Runtime { get; } = MiniMaxVersionProvider.Read();

    public MiniMaxUpdateCoordinator(IMiniMaxOperationCoordinator operations, IWin32Window owner, Action requestClose)
    { _operations = operations; _owner = owner; _requestClose = requestClose; }

    public void StartBackgroundCheck()
    {
        if (!Runtime.IsPortable || _disposed) return;
        _ = CheckBackgroundAsync();
    }
    private async Task CheckBackgroundAsync()
    {
        try { await CheckCoreAsync(_lifetime.Token); } catch { /* Launch checks are deliberately silent. */ }
    }
    public async Task<MiniMaxUpdateCheckResult> CheckManuallyAsync(CancellationToken token = default)
    {
        if (!Runtime.IsPortable) return MiniMaxUpdateCheckResult.DevelopmentBuild;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        try { return await CheckCoreAsync(linked.Token); }
        catch (OperationCanceledException) { throw; }
        catch { return MiniMaxUpdateCheckResult.Failed; }
    }
    private async Task<MiniMaxUpdateCheckResult> CheckCoreAsync(CancellationToken token)
    {
        await _check.WaitAsync(token);
        try
        {
            var release = await _github.LatestAsync(token);
            if (release.Version <= Runtime.Version) return MiniMaxUpdateCheckResult.UpToDate;
            if (_shownVersion == release.Version) return MiniMaxUpdateCheckResult.UpdateStarted;
            _shownVersion = release.Version;
            try { await WaitForIdleAsync(token); await DownloadAndInstallAsync(release, token); }
            catch { _shownVersion = null; throw; }
            return MiniMaxUpdateCheckResult.UpdateStarted;
        }
        finally { _check.Release(); }
    }
    private async Task WaitForIdleAsync(CancellationToken token)
    {
        while (_operations.Active != null)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Changed(IMiniMaxOperationSession? active) { if (active == null) ready.TrySetResult(); }
            _operations.Changed += Changed;
            try
            {
                if (_operations.Active == null) return;
                using var registration = token.Register(() => ready.TrySetCanceled(token));
                await ready.Task;
            }
            finally { _operations.Changed -= Changed; }
        }
    }
    private async Task DownloadAndInstallAsync(MiniMaxGitHubRelease release, CancellationToken outerToken)
    {
        var operation = _operations.Begin(MiniMaxOperationKind.Update) ?? throw new InvalidOperationException("Another operation started before update.");
        using var dialog = new SharedUpdateDialog(Runtime.DisplayVersion, release.Version.ToString(3));
        if (_owner is Form ownerForm && ownerForm.Icon != null) dialog.Icon = ownerForm.Icon;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(outerToken, dialog.Token);
        var token = linked.Token;
        var versionText = release.Version.ToString(3);
        var work = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromeProfileLauncher", "updates", "v" + versionText);
        var download = Path.Combine(work, "download");
        var stage = Path.Combine(work, "stage");
        var backup = Path.Combine(work, "backup");
        var handedOff = false;
        dialog.Show(_owner);
        try
        {
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(download);
            dialog.Report("Downloading checksum…", 0);
            var declared = (await _github.ReadTextAsync(release.Sha.DownloadUrl, token)).Trim().Split(' ')[0].ToLowerInvariant();
            var zip = Path.Combine(download, release.Zip.Name);
            dialog.Report("Downloading portable release…", 0);
            await _github.DownloadAsync(release.Zip, zip,
                new Progress<int>(percent => dialog.Report($"Downloading MiniMax {versionText}…", percent)), token);
            dialog.Report("Verifying SHA-256…", 100);
            string actual;
            await using (var hashStream = File.OpenRead(zip))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, token)).ToLowerInvariant();
            if (actual != declared) throw new InvalidDataException("Downloaded update checksum does not match GitHub release.");
            dialog.Report("Extracting update…", 100);
            Directory.CreateDirectory(stage);
            using (var archive = ZipFile.OpenRead(zip))
            {
                var prefix = Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    var destination = Path.GetFullPath(Path.Combine(stage, entry.FullName));
                    if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe update archive path.");
                }
            }
            ZipFile.ExtractToDirectory(zip, stage);
            ValidateStage(stage, versionText);
            var stagedExe = Path.Combine(stage, "MiniMax.exe");
            var start = new ProcessStartInfo(stagedExe) { UseShellExecute = false, WorkingDirectory = stage };
            start.ArgumentList.Add("--apply-update");
            start.ArgumentList.Add("--wait-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add("--source"); start.ArgumentList.Add(stage);
            start.ArgumentList.Add("--target"); start.ArgumentList.Add(Runtime.BaseDirectory);
            start.ArgumentList.Add("--backup"); start.ArgumentList.Add(backup);
            using var updater = Process.Start(start) ?? throw new InvalidOperationException("Could not start the staged updater.");
            handedOff = true;
            dialog.MarkInstalling();
        }
        catch (OperationCanceledException) { dialog.Report("Update cancelled."); throw; }
        catch (Exception ex) { dialog.Report("Update failed: " + ex.Message); throw; }
        finally
        {
            _operations.End(operation);
            if (handedOff) { dialog.Close(); _requestClose(); }
            else if (Directory.Exists(work)) { try { Directory.Delete(work, true); } catch { } }
        }
    }
    internal static void ValidateStage(string stage, string version)
    {
        var manifestPath = Path.Combine(stage, "release.json");
        var executable = Path.Combine(stage, "MiniMax.exe");
        if (!File.Exists(manifestPath) || !File.Exists(executable)) throw new InvalidDataException("Update package is incomplete.");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (document.RootElement.GetProperty("version").GetString() != version ||
            document.RootElement.GetProperty("distribution").GetString() != "portable-self-contained")
            throw new InvalidDataException("Update release metadata does not match.");
        if (FileVersionInfo.GetVersionInfo(executable).FileVersion != version + ".0")
            throw new InvalidDataException("Update executable version does not match.");
    }
    public void Dispose() { _disposed = true; _lifetime.Cancel(); _github.Dispose(); _lifetime.Dispose(); }
}
