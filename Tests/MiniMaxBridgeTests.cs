using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;
using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
using System.IO;
using System.Net.Http;
using AppFoundation.Shared.UI;
using ChromeProfileLauncher.Areas.ActionBar;
using ChromeProfileLauncher.Areas.WindowAreas;

internal static class MiniMaxBridgeTests
{
    // CONFIG AND CONTRACT
    private static int _passed;
    private static string Root = string.Empty;

    // STATE AND COORDINATION
    [STAThread]
    private static int Main(string[] args)
    {
        Root = Path.GetFullPath(args.FirstOrDefault() ?? throw new ArgumentException("Specify a workspace scratch directory."));
        Directory.CreateDirectory(Root);
        try
        {
            if (args.Length > 1 && args[1] == "--discard-live-pending")
            {
                using var runner = MiniMaxOAuthRefreshRunner.Create(new NineRouterPaths());
                var discarded = runner.DiscardInterrupted(new ConsoleStatus());
                Console.WriteLine(discarded ? "LIVE_ROLLBACK discarded one interrupted refresh attempt." : "LIVE_ROLLBACK no interrupted attempt exists.");
                return 0;
            }
            MappingTests();
            FoundationWindowTests.Run(Test, Assert);
            FoundationArchitectureTests.Run(Test, Assert);
            AccountTests();
            DatabaseTests();
            ChromeTests();
            OAuthTests();
            AuthorizationTests();
            MiniMaxSignInTests.Run(Test, Assert);
            MiniMaxSettingsTests.Run(Root, Test, Assert);
            MiniMaxMidnightTests.Run(Root, Test, Assert);
            MiniMaxOperationUiTests.Run(Root, Test, Assert);
            UiWiringTests();
            // WinForms construction auto-installs a UI synchronization context.
            // This console runner has no message pump; async tests must not use it.
            SynchronizationContext.SetSynchronizationContext(null);
            MiniMaxBridgeEndToEndTests.Run(Root, Test, Assert);
            MiniMaxRefreshAllTests.Run(Root, Test, Assert);
            if (args.Length > 1 && args[1] == "--verify-selection") SelectionUiTest(int.Parse(args[2]));
            if (args.Length > 1 && args[1] == "--ui-settings") SettingsUiTest(Root);
            if (args.Length > 1 && args[1] == "--live-row") LiveRowTest(args[2]);
            if (args.Length > 1 && args[1] == "--open-profile")
            {
                var browser = new NineRouterChromeProfileStarter().OpenOrReuseAsync(@"C:\Program Files\Google\Chrome\Application\chrome.exe", args[2], new Uri(MiniMaxSettingsStore.DefaultCheckInUrl)).GetAwaiter().GetResult();
                Console.WriteLine($"LIVE_CHROME handle={browser.Window}, foreground={Win32Window.IsForeground(browser.Window)}, profile={args[2]}, title={Win32Window.GetText(browser.Window)}; NO OAUTH REQUEST");
            }
            if (args.Length > 1 && args[1] == "--discover")
            {
                using var http = NineRouterOAuthBridge.CreateLocalClient(new NineRouterPaths());
                var accounts = new NineRouterOAuthBridge(http).ReadAccountsAsync().GetAwaiter().GetResult();
                var profiles = System.Text.Json.JsonSerializer.Deserialize<NineRouterLinkCandidate[]>(File.ReadAllText(args[2]))!;
                var links = NineRouterProfileMapStore.Match(profiles, accounts, Array.Empty<NineRouterProfileLink>());
                new NineRouterProfileMapStore(Path.Combine(Root, "discovered-links.json")).Save(links);
                Console.WriteLine($"LIVE_READ accounts={accounts.Count}, safe links={links.Count}, unresolved profiles={profiles.Length - links.Count}");
                foreach (var account in accounts)
                    Console.WriteLine((links.Any(l => l.ConnectionId == account.Id) ? "LINKED " : "UNRESOLVED ") + account.Name);
            }
            Console.WriteLine($"{_passed} bridge tests passed, 0 failed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.GetBaseException().Message);
            return 1;
        }
    }

    private static void Test(string name, Action action)
    {
        action();
        _passed++;
        Console.WriteLine("PASS " + name);
    }
    private static void Assert(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class ConsoleStatus : IProgress<string>
    {
        public void Report(string value) => Console.WriteLine("LIVE_ROLLBACK " + value);
    }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    // LOGIC AND CALCULATIONS
    private static void MappingTests()
    {
        var path = Path.Combine(Root, "links.json");
        var store = new NineRouterProfileMapStore(path);
        var a = new NineRouterProfileLink { DirectoryName = "Profile 31", ConnectionId = Guid.NewGuid(), DisplayName = "Back Up 10" };
        var b = new NineRouterProfileLink { DirectoryName = "Default", ConnectionId = Guid.NewGuid(), DisplayName = "Default" };
        Test("missing mapping is empty", () => { if (File.Exists(path)) File.Delete(path); Assert(store.Load().Count == 0); });
        Test("empty round trip", () => { store.Save(Array.Empty<NineRouterProfileLink>()); Assert(store.Load().Count == 0); });
        Test("multiple mappings round trip", () => { store.Save(new[] { a, b }); Assert(store.Load().Contains(a) && store.Load().Contains(b)); });
        Test("duplicate Chrome folder rejected", () => Reject<InvalidDataException>(() => store.Save(new[] { a, b with { DirectoryName = "profile 31" } })));
        Test("duplicate connection rejected", () => Reject<InvalidDataException>(() => store.Save(new[] { a, b with { ConnectionId = a.ConnectionId } })));
        Test("failed validation preserves old mapping", () => Assert(store.Load().Count == 2));
        Test("folder path injection rejected", () => Reject<InvalidDataException>(() => store.Save(new[] { a with { DirectoryName = "../Default" } })));
        Test("corruption reported", () => { File.WriteAllText(path, "broken{"); Reject<InvalidDataException>(() => store.Load()); });
        Test("unknown mapping version rejected", () => { File.WriteAllText(path, "{\"Version\":2,\"Links\":[]}"); Reject<InvalidDataException>(() => store.Load()); });
        Test("atomic replacement has no leftovers", () => { store.Save(new[] { a }); store.Save(new[] { b }); Assert(store.Load().Single() == b); Assert(!Directory.EnumerateFiles(Root, "links.json.*.tmp").Any()); });
        Test("nonlocal 9Router URL rejected", () => Reject<ArgumentException>(() => new NineRouterPaths(baseUrl: new Uri("http://example.com/"))));
    }

    private static void AccountTests()
    {
        var id = Guid.NewGuid();
        var accounts = new[] { new NineRouterAccount(id, "backup10", "one@example.com", true, 1) };
        var profile = new NineRouterLinkCandidate("Profile 31", "Back Up 10", null);
        Test("exact email takes precedence", () => Assert(NineRouterProfileMapStore.Match(new[] { profile with { DisplayName = "different", Email = "ONE@example.com" } }, accounts, Array.Empty<NineRouterProfileLink>()).Single().ConnectionId == id));
        Test("normalized name matches", () => Assert(NineRouterProfileMapStore.Match(new[] { profile }, accounts, Array.Empty<NineRouterProfileLink>()).Count == 1));
        Test("ambiguous account name stays unresolved", () => Assert(NineRouterProfileMapStore.Match(new[] { profile }, new[] { accounts[0], accounts[0] with { Id = Guid.NewGuid() } }, Array.Empty<NineRouterProfileLink>()).Count == 0));
        Test("ambiguous Chrome name stays unresolved", () => Assert(NineRouterProfileMapStore.Match(new[] { profile, profile with { DirectoryName = "Profile 32" } }, accounts, Array.Empty<NineRouterProfileLink>()).Count == 0));
        Test("ordering prefix ignored without stripping suffix", () => Assert(NineRouterProfileMapStore.Match(new[] { profile with { DisplayName = "01 Back Up 10" } }, accounts, Array.Empty<NineRouterProfileLink>()).Count == 1));
        Test("no positional fallback", () => Assert(NineRouterProfileMapStore.Match(new[] { profile with { DisplayName = "Not this account" } }, accounts, Array.Empty<NineRouterProfileLink>()).Count == 0));
        Test("saved pairing survives rename", () => Assert(NineRouterProfileMapStore.Match(new[] { profile with { DisplayName = "Renamed" } }, accounts, new[] { new NineRouterProfileLink { DirectoryName = profile.DirectoryName, ConnectionId = id } }).Count == 1));
        Test("email connection name matches even without email metadata", () => Assert(NineRouterProfileMapStore.Match(new[] { profile with { Email = "one@example.com", DisplayName = "unrelated" } }, new[] { accounts[0] with { Name = "one@example.com", Email = null } }, Array.Empty<NineRouterProfileLink>()).Single().ConnectionId == id));
        Test("deleted mapping becomes unmatched rather than requiring manual relink", () => Assert(NineRouterProfileMapStore.Match(new[] { profile }, Array.Empty<NineRouterAccount>(), new[] { new NineRouterProfileLink { DirectoryName = profile.DirectoryName, ConnectionId = id } }).Count == 0));
        Test("discovery allowlists credential-free metadata", () =>
        {
            var json = "{\"connections\":[{\"id\":\"" + id + "\",\"provider\":\"minimax-code-global\",\"authType\":\"oauth\",\"name\":\"backup10\",\"accessToken\":\"do-not-return\",\"refreshToken\":\"do-not-return\"}]}";
            using var http = new HttpClient(new FixedHandler(json)) { BaseAddress = new Uri("http://127.0.0.1:20129/") };
            var list = new NineRouterOAuthBridge(http).ReadAccountsAsync().GetAwaiter().GetResult();
            Assert(list.Count == 1 && !System.Text.Json.JsonSerializer.Serialize(list).Contains("do-not-return"));
        });
    }

    private sealed class FixedHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private static void DatabaseTests()
    {
        var fixture = Path.Combine(Root, "fixture.sqlite");
        var backup = @"C:\Apps\9router\data\db\backups\minimax-bridge-baseline-20261125-202043\data.sqlite";
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = new NineRouterDatabaseBridge.MergeRequest(source, target, "Back Up 10", now, 21);
        SqliteConnection Open() { var c = new SqliteConnection($"Data Source={fixture};Pooling=False"); c.Open(); return c; }
        void Sql(string sql) { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
        string Snapshot() { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT group_concat(id||name||data||priority||isActive||createdAt) FROM (SELECT * FROM providerConnections ORDER BY id)"; return (string)cmd.ExecuteScalar()!; }
        void Reset()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(fixture + suffix)) File.Delete(fixture + suffix);
            File.Copy(backup, fixture);
            Sql("UPDATE providerConnections SET data='{}'"); // Fixture strips all real credentials immediately; never printed.
            using var c = Open();
            void Insert(Guid id, string name, string data, DateTimeOffset created)
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = "INSERT INTO providerConnections(id,provider,authType,name,priority,isActive,data,createdAt,updatedAt) VALUES($id,'minimax-code-global','oauth',$name,99,0,$data,$created,$created)";
                cmd.Parameters.AddWithValue("$id", id.ToString()); cmd.Parameters.AddWithValue("$name", name); cmd.Parameters.AddWithValue("$data", data); cmd.Parameters.AddWithValue("$created", created.ToString("O")); cmd.ExecuteNonQuery();
            }
            Insert(target, "Old name", "{\"accessToken\":\"old\",\"refreshToken\":\"old-refresh\",\"providerSpecificData\":{\"proxyPoolId\":\"preserve\"},\"custom\":123,\"lastError\":\"old-error\",\"modelLock_MiniMax-M3\":\"locked\"}", now.AddDays(-1));
            Insert(source, "Account 22", new JsonObject { ["accessToken"] = "fixture-access", ["refreshToken"] = "fixture-refresh", ["expiresAt"] = now.AddHours(12).ToString("O"), ["expiresIn"] = 43200 }.ToJsonString(), now);
        }
        var bridge = new NineRouterDatabaseBridge(fixture, timeoutSeconds: 1);
        Test("fixture atomic merge preserves ownership and count", () =>
        {
            Reset(); bridge.Merge(request);
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT name,data,priority,isActive,createdAt FROM providerConnections WHERE id='{target}'";
            using (var r = cmd.ExecuteReader())
            {
                Assert(r.Read() && r.GetString(0) == "Back Up 10" && r.GetInt32(2) == 99 && r.GetInt32(3) == 0);
                var d = JsonNode.Parse(r.GetString(1))!;
                Assert(d["accessToken"]!.GetValue<string>() == "fixture-access" && d["refreshToken"]!.GetValue<string>() == "fixture-refresh");
                Assert(d["providerSpecificData"]!["proxyPoolId"]!.GetValue<string>() == "preserve" && d["custom"]!.GetValue<int>() == 123);
                Assert(d["lastError"] == null && d["modelLock_MiniMax-M3"] == null && d["testStatus"]!.GetValue<string>() == "active");
                Assert(DateTimeOffset.Parse(r.GetString(4)) == now.AddDays(-1));
            }
            cmd.CommandText = $"SELECT COUNT(*) FROM providerConnections WHERE id='{source}'"; Assert(Convert.ToInt32(cmd.ExecuteScalar()) == 0);
            cmd.CommandText = "SELECT COUNT(*) FROM providerConnections WHERE provider='minimax-code-global'"; Assert(Convert.ToInt32(cmd.ExecuteScalar()) == 21);
        });
        foreach (var entry in new[]
        {
            ("wrong provider", $"UPDATE providerConnections SET provider='minimax-code' WHERE id='{source}'"),
            ("wrong auth type", $"UPDATE providerConnections SET authType='apikey' WHERE id='{target}'"),
            ("missing source", $"DELETE FROM providerConnections WHERE id='{source}'"),
            ("missing target", $"DELETE FROM providerConnections WHERE id='{target}'"),
            ("unknown schema", "UPDATE _meta SET value='999' WHERE key='schemaVersion'"),
            ("unknown app", "UPDATE _meta SET value='999' WHERE key='appVersion'"),
            ("missing credentials", $"UPDATE providerConnections SET data='{{}}' WHERE id='{source}'"),
            ("preexisting source", $"UPDATE providerConnections SET createdAt='2020-01-01T00:00:00Z' WHERE id='{source}'")
        }) Test(entry.Item1 + " rejected without writes", () => { Reset(); Sql(entry.Item2); var before = Snapshot(); Reject<InvalidDataException>(() => bridge.Merge(request)); Assert(Snapshot() == before); });
        Test("mid-transaction failure rolls back target and source", () => { Reset(); var before = Snapshot(); Reject<InvalidOperationException>(() => bridge.Merge(request, beforeDelete: () => throw new InvalidOperationException("Injected failure"))); Assert(Snapshot() == before); });
        Test("cancellation after target update rolls back", () => { Reset(); var before = Snapshot(); using var cts = new CancellationTokenSource(); Reject<OperationCanceledException>(() => bridge.Merge(request, cts.Token, cts.Cancel)); Assert(Snapshot() == before); });
        Test("failed refresh rollback deletes only its exact temporary source", () =>
        {
            Reset(); bridge.Rollback(new(source, target, now, false));
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM providerConnections WHERE id='{source}'"; Assert(Convert.ToInt32(cmd.ExecuteScalar()) == 0);
            cmd.CommandText = $"SELECT data FROM providerConnections WHERE id='{target}'";
            Assert(JsonNode.Parse((string)cmd.ExecuteScalar()!)!["accessToken"]!.GetValue<string>() == "old");
        });
        Test("rollback refuses an older unrelated connection without writes", () =>
        {
            Reset(); Sql($"UPDATE providerConnections SET createdAt='2020-01-01T00:00:00Z' WHERE id='{source}'"); var before = Snapshot();
            Reject<InvalidDataException>(() => bridge.Rollback(new(source, target, now, false))); Assert(Snapshot() == before);
        });
        Test("account-count drift rejected", () => { Reset(); var before = Snapshot(); Reject<InvalidDataException>(() => bridge.Merge(request with { OriginalAccountCount = 20 })); Assert(Snapshot() == before); });
        Test("busy transaction respects bounded timeout", () => { Reset(); using var c = Open(); using var tx = c.BeginTransaction(deferred: false); var watch = System.Diagnostics.Stopwatch.StartNew(); Reject<SqliteException>(() => bridge.Merge(request)); Assert(watch.Elapsed < TimeSpan.FromSeconds(5)); });
        Test("live schema validation is read only", () => new NineRouterDatabaseBridge(new NineRouterPaths().DatabasePath).ValidateReadOnly());
    }

    private sealed class FakeWindows : INineRouterChromeWindows
    {
        public int Launches;
        public int FocusCalls;
        public IntPtr Foreground = (IntPtr)1;
        public bool ExistingProfile;
        public bool Ambiguous;
        public bool NeverAppears;
        public bool Closed;
        public bool RefuseClose;
        public bool OpenTabAsWindow;
        public string WindowIdentity = "fixture-chrome:2";
        public List<IntPtr> CloseRequests = new();
        public readonly HashSet<IntPtr> Active = new() { (IntPtr)1 };
        public readonly List<Uri> Urls = new();
        public readonly Dictionary<IntPtr, Uri> Addresses = new();
        public HashSet<IntPtr> ReadWindows()
        {
            if (ExistingProfile && Launches == 0) Active.Add((IntPtr)2);
            return new(Active);
        }
        public void Launch(string executable, string directoryName, Uri url)
        {
            Launches++; Urls.Add(url);
            if (NeverAppears) return;
            if (Ambiguous && Launches == 1) { Active.Add((IntPtr)2); Active.Add((IntPtr)3); Addresses[(IntPtr)2] = url; Addresses[(IntPtr)3] = url; return; }
            if (Launches == 1)
            {
                if (!ExistingProfile) Active.Add((IntPtr)2);
                else Foreground = (IntPtr)2;
                Addresses[(IntPtr)2] = url;
            }
            else if (OpenTabAsWindow) { Active.Add((IntPtr)3); Addresses[(IntPtr)3] = url; }
            else Addresses[Foreground] = url;
        }
        public bool AddressMatches(IntPtr window, Uri url) => Addresses.TryGetValue(window, out var actual) && actual == url;
        public void Focus(IntPtr window) { FocusCalls++; Foreground = window; }
        public bool IsForeground(IntPtr window) => window == Foreground;
        public string? Identity(IntPtr window) => window == (IntPtr)2 && !Closed && Active.Contains(window) ? WindowIdentity : null;
        public string? ClaimIdentity(IntPtr window) => Identity(window);
        public void ReleaseIdentity(IntPtr window) { }
        public void RequestClose(IntPtr window) { CloseRequests.Add(window); if (!RefuseClose) { Closed = true; Active.Remove(window); } }
    }
    private static void ChromeTests()
    {
        var app = new Uri("https://agent.minimax.io/"); var oauth = new Uri("https://account.minimax.io/oauth-authorize");
        NineRouterChromeProfileStarter Starter(FakeWindows windows) => new(windows)
        { PollInterval = TimeSpan.FromMilliseconds(1), Timeout = TimeSpan.FromMilliseconds(100) };
        Test("missing profile window creates and owns one window", () => { var w = new FakeWindows(); var s = Starter(w); var session = s.OpenOrReuseAsync("fake", "Profile 31", app).GetAwaiter().GetResult(); Assert(session.Window == (IntPtr)2 && session.OwnsWindow && w.Launches == 1 && w.FocusCalls == 1); });
        Test("open profile window is reused for a new tab", () => { var w = new FakeWindows { ExistingProfile = true }; var s = Starter(w); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); Assert(session.Window == (IntPtr)2 && !session.OwnsWindow && w.Launches == 1); s.OpenTabAsync(session, oauth).GetAwaiter().GetResult(); Assert(w.Launches == 2 && w.Urls[0].Host == app.Host && w.Urls[0].Fragment.StartsWith("#minimax-launch-") && w.Urls[1] == oauth); });
        Test("OAuth tab cannot silently become another window", () => { var w = new FakeWindows { ExistingProfile = true, OpenTabAsWindow = true }; var s = Starter(w); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); Reject<InvalidOperationException>(() => s.OpenTabAsync(session, oauth).GetAwaiter().GetResult()); });
        Test("ambiguous Chrome windows rejected", () => Reject<InvalidOperationException>(() => Starter(new FakeWindows { Ambiguous = true }).OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult()));
        Test("Chrome cancellation during wait", () => { using var cts = new CancellationTokenSource(20); Reject<OperationCanceledException>(() => Starter(new FakeWindows { NeverAppears = true }).OpenOrReuseAsync("fake", "Default", app, cts.Token).GetAwaiter().GetResult()); });
        Test("only a newly created window closes", () => { var w = new FakeWindows(); var s = Starter(w); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); Assert(s.CloseIfOwnedAsync(session).GetAwaiter().GetResult() && w.CloseRequests.SequenceEqual(new[] { (IntPtr)2 })); });
        Test("reused profile window is never closed", () => { var w = new FakeWindows { ExistingProfile = true }; var s = Starter(w); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); Assert(!s.CloseIfOwnedAsync(session).GetAwaiter().GetResult() && w.CloseRequests.Count == 0); });
        Test("standalone row Sign in closes its operation-created window", () => { var w = new FakeWindows(); var s = Starter(w); using var runner = new MiniMaxSignInRunner(s); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); runner.CompleteStandaloneAsync(new(session, MiniMaxSignInOutcome.SignedIn), new Progress<string>()).GetAwaiter().GetResult(); Assert(w.CloseRequests.SequenceEqual(new[] { (IntPtr)2 })); });
        Test("standalone row Sign in closes a reused window with all its tabs", () => { var w = new FakeWindows { ExistingProfile = true }; var s = Starter(w); using var runner = new MiniMaxSignInRunner(s); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); runner.CompleteStandaloneAsync(new(session, MiniMaxSignInOutcome.SignedIn), new Progress<string>()).GetAwaiter().GetResult(); Assert(w.CloseRequests.SequenceEqual(new[] { (IntPtr)2 }) && !w.Active.Contains((IntPtr)2)); });
        Test("reused Chrome identity cannot be closed", () => { var w = new FakeWindows(); var s = Starter(w); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); w.WindowIdentity = "different-process"; Reject<InvalidOperationException>(() => s.CloseIfOwnedAsync(session).GetAwaiter().GetResult()); Assert(w.CloseRequests.Count == 0); });
        Test("Chrome close refusal times out without termination", () => { var w = new FakeWindows { RefuseClose = true }; var s = new NineRouterChromeProfileStarter(w) { CloseTimeout = TimeSpan.FromMilliseconds(10), PollInterval = TimeSpan.FromMilliseconds(1), Timeout = TimeSpan.FromMilliseconds(100) }; var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); Reject<TimeoutException>(() => s.CloseIfOwnedAsync(session).GetAwaiter().GetResult()); Assert(!w.Closed && w.CloseRequests.Count == 1); });
        Test("Stop before close leaves Chrome open", () => { var w = new FakeWindows(); var s = Starter(w); var session = s.OpenOrReuseAsync("fake", "Default", app).GetAwaiter().GetResult(); using var c = new CancellationTokenSource(); c.Cancel(); Reject<OperationCanceledException>(() => s.CloseIfOwnedAsync(session, c.Token).GetAwaiter().GetResult()); Assert(!w.Closed && w.CloseRequests.Count == 0); });
    }

    private sealed class SequenceHandler(IEnumerable<string> replies) : HttpMessageHandler
    {
        private readonly Queue<string> _replies = new(replies);
        public readonly List<string> Paths = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(_replies.Count > 0 ? _replies.Dequeue() : "{\"error\":\"authorization_pending\"}") });
        }
    }
    private static void OAuthTests()
    {
        string Start = "{\"verification_uri\":\"https://account.minimax.io/oauth-authorize?user_code=TEST\",\"device_code\":\"fixture-device\",\"codeVerifier\":\"fixture-verifier\",\"interval\":1,\"expires_in\":600}";
        var id = Guid.NewGuid();
        string Success = "{\"success\":true,\"connection\":{\"id\":\"" + id + "\",\"provider\":\"minimax-code-global\"}}";
        Test("valid device session and pending/slow-down/success", () =>
        {
            var handler = new SequenceHandler(new[] { Start, "{\"error\":\"authorization_pending\"}", "{\"error\":\"slow_down\"}", Success });
            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:20129/") };
            var bridge = new NineRouterOAuthBridge(http) { TestPollInterval = TimeSpan.FromMilliseconds(1) };
            var session = bridge.StartAsync().GetAwaiter().GetResult(); var pending = 0;
            Assert(bridge.WaitForTokensAsync(session, () => pending++).GetAwaiter().GetResult() == id && pending == 2);
            Assert(handler.Paths.First() == "/api/oauth/minimax-code-global/device-code" && handler.Paths.Skip(1).All(p => p == "/api/oauth/minimax-code-global/poll"));
            Assert(!session.ToString().Contains("fixture-verifier"));
        });
        foreach (var entry in new[]
        {
            ("missing URL", Start.Replace("https://account.minimax.io/oauth-authorize?user_code=TEST", "")),
            ("wrong OAuth host", Start.Replace("account.minimax.io", "account.minimax.cn")),
            ("missing device code", Start.Replace("fixture-device", "")),
            ("missing verifier", Start.Replace("fixture-verifier", "")),
            ("unsupported user-code fallback", Start.Replace("\"expires_in\":600", "\"expires_in\":600,\"_minimaxPollByUser\":true")),
            ("invalid interval", Start.Replace("\"interval\":1", "\"interval\":0"))
        }) Test(entry.Item1 + " rejected", () => { using var h = new HttpClient(new FixedHandler(entry.Item2)) { BaseAddress = new Uri("http://127.0.0.1:20129/") }; Reject<InvalidDataException>(() => new NineRouterOAuthBridge(h).StartAsync().GetAwaiter().GetResult()); });
        foreach (var error in new[] { "access_denied", "expired_token", "fatal_unknown" }) Test(error + " fails without token delivery", () =>
        {
            using var h = new HttpClient(new SequenceHandler(new[] { Start, "{\"error\":\"" + error + "\"}" })) { BaseAddress = new Uri("http://127.0.0.1:20129/") };
            var b = new NineRouterOAuthBridge(h) { TestPollInterval = TimeSpan.FromMilliseconds(1) }; var s = b.StartAsync().GetAwaiter().GetResult();
            Reject<InvalidOperationException>(() => b.WaitForTokensAsync(s).GetAwaiter().GetResult());
        });
        Test("OAuth wait cancellation", () => { using var h = new HttpClient(new FixedHandler(Start)) { BaseAddress = new Uri("http://127.0.0.1:20129/") }; var b = new NineRouterOAuthBridge(h); var s = b.StartAsync().GetAwaiter().GetResult(); using var c = new CancellationTokenSource(5); Reject<OperationCanceledException>(() => b.WaitForTokensAsync(s, token: c.Token).GetAwaiter().GetResult()); });
        Test("OAuth deadline enforced", () => { using var h = new HttpClient(new FixedHandler(Start)) { BaseAddress = new Uri("http://127.0.0.1:20129/") }; var b = new NineRouterOAuthBridge(h); var s = new NineRouterOAuthSession { AuthorizationUrl = new Uri("https://account.minimax.io/oauth-authorize"), DeviceCode = "d", CodeVerifier = "v", ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1), PollInterval = TimeSpan.FromSeconds(1) }; Reject<TimeoutException>(() => b.WaitForTokensAsync(s).GetAwaiter().GetResult()); });
    }

    private sealed class FakeAuthorization : IMiniMaxOAuthAuthorizationPage
    {
        public int Reads;
        public int Clicks;
        public int DelayReads;
        public bool Swallow;
        public bool NeverConfirm;
        public int ButtonCount = 1;
        public MiniMaxOAuthPageSnapshot Read() => new(++Reads > DelayReads ? ButtonCount : 0, !NeverConfirm && Clicks >= (Swallow ? 2 : 1));
        public bool PressAuthorize(CancellationToken token) { token.ThrowIfCancellationRequested(); Clicks++; return true; }
    }
    private static void AuthorizationTests()
    {
        MiniMaxOAuthElement Button = new("Button", "Authorize", "h-12 bg-bg_interaction_primary_default", true, false, 10, 10, 100, 50);
        MiniMaxOAuthElement Heading = new("Text", "Authorization confirmed", "mt-8 text-center", true, false, 10, 10, 100, 50);
        Test("exact Authorize element accepted", () => Assert(MiniMaxOAuthAuthorizationPage.Describe(new[] { Button }).AuthorizeButtons == 1));
        Test("Invoke supported means no real mouse action", () => { var mouse = 0; Assert(MiniMaxOAuthAuthorizationPage.PressMethods(() => true, () => { mouse++; return true; })); Assert(mouse == 0); });
        Test("unsupported Invoke uses mouse fallback once", () => { var mouse = 0; Assert(MiniMaxOAuthAuthorizationPage.PressMethods(() => false, () => { mouse++; return true; })); Assert(mouse == 1); });
        foreach (var entry in new[] { ("Decline", Button with { Name = "Decline" }), ("hidden", Button with { Offscreen = true }), ("offscreen clone", Button with { X = -100 }), ("disabled", Button with { Enabled = false }), ("zero-sized", Button with { Width = 0 }), ("wrong class", Button with { ClassName = "other" }) })
            Test(entry.Item1 + " never accepted for authorization", () => Assert(MiniMaxOAuthAuthorizationPage.Describe(new[] { entry.Item2 }).AuthorizeButtons == 0));
        Test("exact visible confirmation required", () => { Assert(MiniMaxOAuthAuthorizationPage.Describe(new[] { Heading }).Confirmed); Assert(!MiniMaxOAuthAuthorizationPage.Describe(new[] { Heading with { Offscreen = true } }).Confirmed); Assert(!MiniMaxOAuthAuthorizationPage.Describe(new[] { Heading with { Name = "Confirm" } }).Confirmed); });
        MiniMaxOAuthAuthorizationRunner Runner(FakeAuthorization page) => new(page) { PollInterval = TimeSpan.FromMilliseconds(1), ClickRetryAfter = TimeSpan.FromMilliseconds(3), Timeout = TimeSpan.FromMilliseconds(100) };
        Test("delayed button is detected before one click", () => { var p = new FakeAuthorization { DelayReads = 3 }; Runner(p).WaitForConfirmationAsync(() => true).GetAwaiter().GetResult(); Assert(p.Clicks == 1 && p.Reads >= 5); });
        Test("swallowed click retried once only", () => { var p = new FakeAuthorization { Swallow = true }; Runner(p).WaitForConfirmationAsync(() => true).GetAwaiter().GetResult(); Assert(p.Clicks == 2); });
        Test("absent confirmation times out without click loop", () => { var p = new FakeAuthorization { NeverConfirm = true }; Reject<TimeoutException>(() => Runner(p).WaitForConfirmationAsync(() => true).GetAwaiter().GetResult()); Assert(p.Clicks == 2); });
        Test("no retry after 9Router stops reporting pending", () => { var p = new FakeAuthorization { NeverConfirm = true }; Reject<TimeoutException>(() => Runner(p).WaitForConfirmationAsync(() => false).GetAwaiter().GetResult()); Assert(p.Clicks == 1); });
        Test("ambiguous Authorize controls fail before click", () => { var p = new FakeAuthorization { ButtonCount = 2 }; Reject<InvalidOperationException>(() => Runner(p).WaitForConfirmationAsync(() => true).GetAwaiter().GetResult()); Assert(p.Clicks == 0); });
        Test("cancel before click", () => { var p = new FakeAuthorization(); using var c = new CancellationTokenSource(); c.Cancel(); Reject<OperationCanceledException>(() => Runner(p).WaitForConfirmationAsync(() => true, c.Token).GetAwaiter().GetResult()); Assert(p.Clicks == 0); });
        Test("cancel after click", () => { var p = new FakeAuthorization { NeverConfirm = true }; using var c = new CancellationTokenSource(20); Reject<OperationCanceledException>(() => Runner(p).WaitForConfirmationAsync(() => false, c.Token).GetAwaiter().GetResult()); Assert(p.Clicks == 1); });
    }

    private static void UiWiringTests()
    {
        Test("single-job guard blocks overlapping refresh and releases after Stop", () =>
        {
            var operations = new BusyOperations();
            var journalPath = Path.Combine(Root, "busy-journal.json");
            if (File.Exists(journalPath)) File.Delete(journalPath);
            using var runner = new MiniMaxOAuthRefreshRunner(operations, new NineRouterPendingRefreshStore(journalPath));
            var link = new NineRouterProfileLink { DirectoryName = "Default", ConnectionId = operations.Id, DisplayName = "Fixture", Email = "fixture@example.com" };
            using var cts = new CancellationTokenSource();
            var first = runner.RunAsync(link, "fake", new Uri("https://agent.minimax.io/"), new Progress<string>(), cts.Token);
            operations.Entered.Task.GetAwaiter().GetResult();
            Reject<InvalidOperationException>(() => runner.RunAsync(link, "fake", new Uri("https://agent.minimax.io/"), new Progress<string>(), CancellationToken.None).GetAwaiter().GetResult());
            cts.Cancel(); Reject<OperationCanceledException>(() => first.GetAwaiter().GetResult());
            Assert(!File.Exists(journalPath));
        });
        Test("main table has sign-in and refresh buttons for only checked profiles", () =>
        {
            using var form = new ChromeProfileLauncher.UI.MiniMaxForm(); // Not shown; no Load/OAuth/browser calls.
            Assert(form.ProfileTable.Columns.Count == 5);
            Assert(form.ProfileTable.Columns[3] is System.Windows.Forms.DataGridViewButtonColumn && form.ProfileTable.Columns[3].HeaderText == "Sign in");
            Assert(form.ProfileTable.Columns[4] is System.Windows.Forms.DataGridViewButtonColumn && form.ProfileTable.Columns[4].HeaderText == "Refresh token");
            form.Rows.Add(new ProfileRow(new ChromeProfile { DirectoryName = "fixture-A", Name = "A" }) { IsChecked = true });
            form.Rows.Add(new ProfileRow(new ChromeProfile { DirectoryName = "fixture-B", Name = "B" }) { IsChecked = false });
            form.AppContext.RefreshSelected();
            var rows = (System.ComponentModel.BindingList<ProfileRow>)form.ProfileTable.DataSource!;
            Assert(rows.Count == 1 && rows[0].DirectoryName == "fixture-A");
            Assert(!System.Text.Json.JsonSerializer.Serialize(rows).Contains("accessToken"));
        });
        Test("manual Link screen removed", () => Assert(!typeof(ChromeProfileLauncher.UI.MiniMaxForm).Assembly.GetTypes().Any(t => t.Name == "MiniMaxProfileLinkDialog")));
        Test("all toolbar buttons share one spacing and height preset", () =>
        {
            using var form = new ChromeProfileLauncher.UI.MiniMaxForm();
            IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control parent)
            {
                foreach (System.Windows.Forms.Control child in parent.Controls)
                {
                    yield return child;
                    foreach (var nested in Descendants(child)) yield return nested;
                }
            }
            var buttons = Descendants(form).OfType<SharedActionButton>().ToArray();
            Assert(buttons.Length == 6);
            Assert(buttons.All(b => b.Margin == new System.Windows.Forms.Padding(6, 2, 6, 2) &&
                b.Padding == new System.Windows.Forms.Padding(8, 0, 8, 0) && b.MinimumSize.Height == 30 &&
                b.Dock == System.Windows.Forms.DockStyle.Fill));
            var toolbar = (System.Windows.Forms.TableLayoutPanel)buttons[0].Parent!;
            toolbar.Size = new System.Drawing.Size(900, 38);
            toolbar.PerformLayout();
            Assert(buttons.Select(b => b.Height).Distinct().Count() == 1);
            Assert(buttons.All(b => System.Windows.Forms.TextRenderer.MeasureText(b.Text, b.Font).Width + b.Padding.Horizontal + 2 <= b.Width), "Toolbar label is clipped by the shared padding.");
            Assert(buttons.Single(b => b.Text == "Check in").Tag as string == "Primary");
            Assert(!buttons.Any(b => b.Text == "Stop"));
        });
    }

    private sealed class BusyOperations : IMiniMaxOAuthRefreshOperations
    {
        public Guid Id = Guid.NewGuid();
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<NineRouterAccount>> Accounts(CancellationToken token) => Task.FromResult<IReadOnlyList<NineRouterAccount>>(new[] { new NineRouterAccount(Id, "Fixture", null, true, 1) });
        public void ValidateDatabase() { }
        public async Task<NineRouterChromeProfileSession> OpenProfile(string executable, string directory, Uri miniMaxUrl, CancellationToken token) { Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return new((IntPtr)1, executable, directory, false); }
        public Task<MiniMaxSignInOutcome> EnsureSignedIn(IntPtr window, Uri miniMaxUrl, IProgress<string> progress, CancellationToken token) => throw new Exception("Unexpected call");
        public Task<NineRouterOAuthSession> StartOAuth(CancellationToken token) => throw new Exception("Must not reach OAuth");
        public Task OpenLogin(NineRouterChromeProfileSession browser, Uri url, CancellationToken token) => throw new Exception("Must not open browser");
        public Task Confirm(IntPtr w, Uri u, Func<bool> p, CancellationToken t) => throw new Exception("Unexpected call");
        public Task<Guid> ReceiveTokens(NineRouterOAuthSession s, CancellationToken t) => throw new Exception("Unexpected call");
        public Guid Merge(NineRouterDatabaseBridge.MergeRequest r, CancellationToken t) => throw new Exception("Unexpected call");
        public void Rollback(NineRouterPendingRefreshStore.Pending attempt) { }
        public void SaveLink(NineRouterProfileLink link) => throw new Exception("Unexpected call");
        public Task RefreshQuota(Guid id, CancellationToken token) => throw new Exception("Unexpected call");
        public Task<bool> CloseProfile(NineRouterChromeProfileSession browser, CancellationToken token) => throw new Exception("Unexpected call");
    }

    private static void LiveRowTest(string directoryName)
    {
        HashSet<IntPtr> ChromeWindows()
        {
            var result = new HashSet<IntPtr>();
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("chrome"))
                using (process)
                    foreach (var handle in Win32Window.GetTopLevelWindows(process.Id))
                        if (Win32Window.IsWindowVisible(handle) && Win32Window.GetClass(handle) == "Chrome_WidgetWin_1") result.Add(handle);
            return result;
        }
        var beforeWindows = ChromeWindows();
        var observedNewWindows = new HashSet<IntPtr>();
        System.Windows.Forms.Application.EnableVisualStyles();
        using var form = new ChromeProfileLauncher.UI.MiniMaxForm();
        using var timer = new System.Windows.Forms.Timer { Interval = 200 };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var clicked = false;
        ProfileRow? selected = null;
        string? error = null;
        string? last = null;
        var refresh = form.Areas.OfType<MiniMaxWindowActionBarArea>().Single().Items.OfType<ActionBarRefresh>().Single();
        timer.Tick += (_, _) =>
        {
            try
            {
                if (watch.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("Live row test exceeded deadline.");
                if (!clicked)
                {
                    if (refresh.IsLoading || form.Rows.Count == 0) return;
                    var row = form.ProfileTable.Rows.Cast<System.Windows.Forms.DataGridViewRow>().SingleOrDefault(r => r.DataBoundItem is ProfileRow p && p.DirectoryName == directoryName);
                    if (row == null) throw new InvalidOperationException("Test profile is not selected in the main table.");
                    selected = row.DataBoundItem as ProfileRow ?? throw new InvalidOperationException("Test profile row disappeared.");
                    if (selected.RefreshTokenStatus != "Refresh") throw new InvalidOperationException("Test row is not ready to refresh.");
                    clicked = true;
                    form.Activate();
                    typeof(System.Windows.Forms.DataGridView).GetMethod("OnCellContentClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form.ProfileTable,
                        new object[] { new System.Windows.Forms.DataGridViewCellEventArgs(4, row.Index) });
                    Console.WriteLine("LIVE_ROW button invoked once: " + selected.DisplayName);
                }
                else if (selected != null)
                {
                    foreach (var handle in ChromeWindows().Except(beforeWindows)) observedNewWindows.Add(handle);
                    if (last != selected.RefreshTokenStatus) { last = selected.RefreshTokenStatus; Console.WriteLine("LIVE_ROW " + last); }
                    if (form.AppContext.Operations.Active == null)
                    {
                        if (last != "Refresh") throw new InvalidOperationException("Live refresh button did not restore: " + last);
                        if (observedNewWindows.Any(Win32Window.IsWindow))
                            throw new InvalidOperationException("An operation-created Chrome window remained open.");
                        if (beforeWindows.Any(h => !Win32Window.IsWindow(h)))
                            throw new InvalidOperationException("A preexisting Chrome window disappeared during the test.");
                        Console.WriteLine($"LIVE_CHROME owned windows closed={observedNewWindows.Count}; all {beforeWindows.Count} preexisting windows preserved");
                        timer.Stop(); form.Close();
                    }
                }
            }
            catch (Exception ex) { error = ex.Message; timer.Stop(); form.Close(); }
        };
        form.Shown += (_, _) => timer.Start();
        System.Windows.Forms.Application.Run(form);
        if (error != null) throw new InvalidOperationException(error);
        Console.WriteLine("LIVE_ROW PASS");
    }

    private static void SelectionUiTest(int expectedCount)
    {
        using var form = new ChromeProfileLauncher.UI.MiniMaxForm();
        using var timer = new System.Windows.Forms.Timer { Interval = 200 };
        var watch = System.Diagnostics.Stopwatch.StartNew(); string? error = null;
        var refresh = form.Areas.OfType<MiniMaxWindowActionBarArea>().Single().Items.OfType<ActionBarRefresh>().Single();
        IEnumerable<System.Windows.Forms.Control> Children(System.Windows.Forms.Control parent)
        { foreach (System.Windows.Forms.Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; } }
        timer.Tick += (_, _) =>
        {
            try
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(40)) throw new TimeoutException("Selection UI verification timed out.");
                if (form.Rows.Count == 0 || refresh.IsLoading) return;
                var visible = form.ProfileTable.Rows.Cast<System.Windows.Forms.DataGridViewRow>().Select(r => r.DataBoundItem).OfType<ProfileRow>().ToArray();
                Assert(visible.Length == expectedCount && visible.All(r => r.IsChecked), "Actual main table does not show the expected selected profiles.");
                Assert(form.ProfileTable.Columns.Count == 5 && form.ProfileTable.Columns[3].HeaderText == "Sign in" && form.ProfileTable.Columns[4].HeaderText == "Refresh token", "Actual main table is missing sign-in/refresh controls.");
                var buttons = Children(form).OfType<SharedActionButton>().ToArray();
                Assert(buttons.Length == 6 && !buttons.Any(b => b.Text == "Stop") && buttons.Any(b => b.Text == "sign in all"), "Actual toolbar still has standalone Stop or lost sign in all.");
                Assert(form.ProfileTable.CellBorderStyle == System.Windows.Forms.DataGridViewCellBorderStyle.None &&
                    form.ProfileTable.RowTemplate.Height == SharedTheme.RowHeight &&
                    form.ProfileTable.RowsDefaultCellStyle.BackColor == form.ProfileTable.AlternatingRowsDefaultCellStyle.BackColor,
                    "Actual main table did not receive unified outlined-row styling.");
                Assert(form.ProfileTable.DisplayedRowCount(false) == expectedCount &&
                    form.Bottom <= System.Windows.Forms.Screen.FromControl(form).WorkingArea.Bottom,
                    "Main window did not resize within the monitor to show every selected profile row.");
                Assert(visible.Any(r => r.UserName == "yuzz.cezh10@gmail.com") && visible.Any(r => r.UserName == "yuzz.cezh11@gmail.com"), "Original cezh10/11 profiles were not restored.");
                Assert(refresh.Runner == null);
                Console.WriteLine($"SELECTION_UI PASS: all {visible.Length} outlined rows visible; fitted window; 6-button stateful toolbar; no OAuth/check-in");
                timer.Stop(); form.Close();
            }
            catch (Exception ex) { error = ex.GetBaseException().Message; timer.Stop(); form.Close(); }
        };
        form.Shown += (_, _) => timer.Start(); System.Windows.Forms.Application.Run(form);
        if (error != null) throw new InvalidOperationException(error);
    }

    private static void SettingsUiTest(string root)
    {
        var directory = Path.Combine(root, "ui-settings"); Directory.CreateDirectory(directory);
        var settings = new MiniMaxSettingsStore(Path.Combine(directory, "settings.json"));
        using var main = new ChromeProfileLauncher.UI.MiniMaxForm(settings, new WindowLayoutStore(Path.Combine(directory, "layout.json")),
            new CheckedProfilesStore(Path.Combine(directory, "checked.json")), new ClaimedProfilesStore(Path.Combine(directory, "claimed.json")));
        using var timer = new System.Windows.Forms.Timer { Interval = 200 };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        string? error = null;
        IEnumerable<System.Windows.Forms.Control> Children(System.Windows.Forms.Control parent)
        {
            foreach (System.Windows.Forms.Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; }
        }
        var refresh = main.Areas.OfType<MiniMaxWindowActionBarArea>().Single().Items.OfType<ActionBarRefresh>().Single();
        var settingsAction = main.Areas.OfType<MiniMaxWindowActionBarArea>().Single().Items.OfType<ActionBarSettings>().Single();
        timer.Tick += (_, _) =>
        {
            try
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(40)) throw new TimeoutException("UI settings check timed out.");
                if (refresh.IsLoading) return;
                var buttons = Children(main).OfType<SharedActionButton>().ToArray();
                Assert(buttons.Length == 6 && buttons.Select(b => b.Height).Distinct().Count() == 1 && buttons.Any(b => b.Text == "sign in all") && !buttons.Any(b => b.Text == "Stop"));
                Assert(!Children(main).OfType<System.Windows.Forms.TextBox>().Any());
                buttons.Single(b => b.Text == "setting").PerformClick();
                var screen = settingsAction.Screen!;
                Assert(screen.Visible && screen.Owner == main);
                buttons.Single(b => b.Text == "setting").PerformClick();
                Assert(ReferenceEquals(screen, settingsAction.Screen));
                var input = Children(screen).OfType<System.Windows.Forms.TextBox>().Single();
                input.Text = "example.com/ui-check";
                Assert(main.CheckInUrl == "https://example.com/ui-check");
                input.Text = "";
                Assert(main.CheckInUrl == "https://example.com/ui-check");
                input.Text = "https://example.com/ui-check";
                screen.Close();
                var reloaded = new MiniMaxSettingsStore(Path.Combine(directory, "settings.json")); reloaded.Load();
                Assert(reloaded.CheckInUrl == "https://example.com/ui-check");
                Assert(refresh.Runner == null);
                Console.WriteLine("UI_SETTINGS PASS: 6 uniform stateful buttons including sign in all; no standalone Stop/main URL; NO OAUTH OR CHECK-IN");
                timer.Stop(); main.Close();
            }
            catch (Exception ex) { error = ex.GetBaseException().Message; timer.Stop(); main.Close(); }
        };
        main.Shown += (_, _) => timer.Start();
        System.Windows.Forms.Application.Run(main);
        if (error != null) throw new InvalidOperationException(error);
    }
}
