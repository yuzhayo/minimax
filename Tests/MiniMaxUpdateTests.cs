using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Features.Update;
using ChromeProfileLauncher.Services;
using ChromeProfileLauncher.UI;

internal static class MiniMaxUpdateTests
{
    private sealed class Handler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) });
    }
    public static void Run(string root, Action<string, Action> test, Action<bool, string> assert)
    {
        test("development build reports version but disables portable installation", () =>
        {
            var runtime = MiniMaxVersionProvider.Read();
            assert(runtime.Version.Major > 0 && runtime.DisplayVersion == runtime.Version.ToString(3) &&
                !runtime.IsPortable && runtime.PortableReason!.Contains("Development build"),
                "Development version/portable state is wrong");
        });
        test("GitHub release client selects exact stable portable assets", () =>
        {
            const string json = "{\"tag_name\":\"v1.2.3\",\"draft\":false,\"prerelease\":false,\"assets\":[" +
                "{\"name\":\"MiniMax-v1.2.3-win-x64-portable.zip\",\"browser_download_url\":\"https://example/zip\",\"size\":10}," +
                "{\"name\":\"MiniMax-v1.2.3-win-x64-portable.zip.sha256\",\"browser_download_url\":\"https://example/sha\",\"size\":5}]}";
            using var http = new HttpClient(new Handler(json)); using var client = new MiniMaxGitHubReleaseClient(http);
            var release = client.LatestAsync(CancellationToken.None).GetAwaiter().GetResult();
            assert(release.Version == new Version(1, 2, 3) && release.Zip.Name.EndsWith("portable.zip") && release.Sha.Name.EndsWith(".sha256"),
                "Latest release/assets were parsed incorrectly");
        });
        test("staged update validates manifest and executable version", () =>
        {
            var stage = Path.Combine(root, "update-stage"); Directory.CreateDirectory(stage);
            var version = MiniMaxVersionProvider.Read().Version.ToString(3);
            File.Copy(Environment.ProcessPath!, Path.Combine(stage, "MiniMax.exe"), true);
            File.WriteAllText(Path.Combine(stage, "release.json"), $"{{\"version\":\"{version}\",\"distribution\":\"portable-self-contained\"}}");
            MiniMaxUpdateCoordinator.ValidateStage(stage, version);
        });
        test("update payload copy replaces a complete directory tree", () =>
        {
            var source = Path.Combine(root, "update-copy-source"); var target = Path.Combine(root, "update-copy-target");
            Directory.CreateDirectory(Path.Combine(source, "runtimes", "win-x64"));
            File.WriteAllText(Path.Combine(source, "MiniMax.exe"), "fixture");
            File.WriteAllText(Path.Combine(source, "runtimes", "win-x64", "native.dll"), "fixture-native");
            Directory.CreateDirectory(target);
            MiniMaxUpdateApplyRunner.CopyDirectory(source, target);
            assert(File.Exists(Path.Combine(target, "MiniMax.exe")) && File.Exists(Path.Combine(target, "runtimes", "win-x64", "native.dll")),
                "Complete update tree was not copied");
        });
        test("update replacement backs up and replaces the complete current folder", () =>
        {
            var source = Path.Combine(root, "update-replace-source"); var target = Path.Combine(root, "update-replace-target");
            var backup = Path.Combine(root, "update-replace-backup");
            Directory.CreateDirectory(source); Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(source, "new.txt"), "new"); File.WriteAllText(Path.Combine(target, "old.txt"), "old");
            MiniMaxUpdateApplyRunner.ReplaceDirectory(source, target, backup);
            assert(File.Exists(Path.Combine(target, "new.txt")) && !File.Exists(Path.Combine(target, "old.txt")) &&
                File.Exists(Path.Combine(backup, "old.txt")), "Current folder was not backed up/replaced as one payload");
        });
        test("settings displays current version and disabled update action in development", () =>
        {
            var directory = Path.Combine(root, "update-settings"); Directory.CreateDirectory(directory);
            var runtime = MiniMaxVersionProvider.Read();
            using var form = new MiniMaxSettingsForm(new MiniMaxSettingsStore(Path.Combine(directory, "settings.json")),
                new WindowLayoutStore(Path.Combine(directory, "layout.json")));
            var controls = Descendants(form).ToArray();
            assert(controls.OfType<Label>().Any(label => label.Text == "Current version: " + runtime.DisplayVersion) &&
                controls.OfType<Button>().Single(button => button.Text == "CHECK FOR UPDATES").Enabled == false,
                "Settings version/update presentation is wrong");
        });
        SynchronizationContext.SetSynchronizationContext(null);
    }
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
}
