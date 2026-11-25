using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ChromeProfileLauncher.Features.Update;

internal static class MiniMaxUpdateApplyRunner
{
    public static int Apply(int waitPid, string source, string target, string backup)
    {
        if (!WaitForExit(waitPid, TimeSpan.FromSeconds(45))) return 3;
        source = Path.GetFullPath(source); target = Path.GetFullPath(target); backup = Path.GetFullPath(backup);
        if (!File.Exists(Path.Combine(source, "MiniMax.exe")) || !File.Exists(Path.Combine(source, "release.json"))) return 2;
        var hadTarget = Directory.Exists(target);
        try
        {
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            ReplaceDirectory(source, target, backup);
            var executable = Path.Combine(target, "MiniMax.exe");
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = target };
            info.ArgumentList.Add("--cleanup-update");
            info.ArgumentList.Add("--wait-pid"); info.ArgumentList.Add(Environment.ProcessId.ToString());
            info.ArgumentList.Add("--source"); info.ArgumentList.Add(source);
            info.ArgumentList.Add("--backup"); info.ArgumentList.Add(backup);
            using var restarted = Process.Start(info) ?? throw new InvalidOperationException("Could not restart updated MiniMax.");
            return 0;
        }
        catch
        {
            try
            {
                if (Directory.Exists(backup))
                {
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                    Directory.Move(backup, target);
                }
                else if (!hadTarget && Directory.Exists(target)) Directory.Delete(target, true);
            }
            catch { }
            return 1;
        }
    }
    public static void StartCleanup(int waitPid, string source, string backup) => _ = Task.Run(() =>
    {
        if (!WaitForExit(waitPid, TimeSpan.FromSeconds(45))) return;
        TryDelete(source); TryDelete(backup);
        var parent = Path.GetDirectoryName(source);
        if (parent != null) TryDelete(parent);
    });
    internal static void CopyDirectory(string source, string target)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }
    internal static void ReplaceDirectory(string source, string target, string backup)
    {
        if (Directory.Exists(target)) Directory.Move(target, backup);
        try { Directory.CreateDirectory(target); CopyDirectory(source, target); }
        catch
        {
            if (Directory.Exists(target)) Directory.Delete(target, true);
            if (Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
    }
    private static bool WaitForExit(int pid, TimeSpan timeout)
    {
        try { using var process = Process.GetProcessById(pid); return process.WaitForExit((int)timeout.TotalMilliseconds); }
        catch (ArgumentException) { return true; }
    }
    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}
