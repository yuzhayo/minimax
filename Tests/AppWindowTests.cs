using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromeProfileLauncher.Window;

internal static class AppWindowTests
{
    private interface IFixtureConnection { string Name { get; } }
    private sealed class FixtureConnection : IFixtureConnection { public string Name => "fixture"; }
    private abstract class Area(string scope, string id, WindowAreaPlacement placement) : IWindowArea
    {
        public static int Initialized;
        public string Scope => scope;
        public string Id => id;
        public WindowAreaPlacement Placement => placement;
        public Type ConnectionContract => typeof(IFixtureConnection);
        public Control Build(WindowAreaBuildContext context, object connection) =>
            new Label { Text = ((IFixtureConnection)connection).Name + ":" + Id, Dock = DockStyle.Fill };
        public Task InitializeAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Initialized++; return Task.CompletedTask; }
        public void Dispose() { }
    }
    private sealed class FixtureActionArea() : Area("WindowFixture", "action", WindowAreaPlacement.ActionBar);
    private sealed class FixtureBodyArea() : Area("WindowFixture", "body", WindowAreaPlacement.Body);
    private sealed class FixtureBottomArea() : Area("WindowFixture", "bottom", WindowAreaPlacement.BottomText);
    private sealed class DuplicateActionAreaA() : Area("WindowDuplicate", "same", WindowAreaPlacement.ActionBar);
    private sealed class DuplicateActionAreaB() : Area("WindowDuplicate", "same", WindowAreaPlacement.Body);
    private sealed class DuplicateBottomArea() : Area("WindowDuplicate", "bottom", WindowAreaPlacement.BottomText);
    private sealed class MissingActionArea() : Area("WindowMissing", "action", WindowAreaPlacement.ActionBar);

    public static void Run(Action<string, Action> test, Action<bool, string> assert)
    {
        WindowConnections Connections() => new WindowConnections().Add<IFixtureConnection>(new FixtureConnection());
        AppWindowOptions Options(string scope) => new(scope, "Fixture", new Size(300, 200), new Size(400, 300));
        test("window discovers and places one scoped child per area without a registration list", () =>
        {
            Area.Initialized = 0;
            using var window = new AppWindow(typeof(AppWindowTests).Assembly, Connections(), Options("WindowFixture"));
            assert(window.Areas.Count == 3 && window.Areas[0].Placement == WindowAreaPlacement.ActionBar &&
                window.Areas[1].Placement == WindowAreaPlacement.Body && window.Areas[2].Placement == WindowAreaPlacement.BottomText,
                "Scoped areas were not discovered/ordered");
            window.InitializeAreasAsync().GetAwaiter().GetResult();
            assert(Area.Initialized == 3, "Area lifecycle was not relayed");
        });
        test("window rejects duplicate area IDs", () =>
        {
            var rejected = false;
            try { using var _ = new AppWindow(typeof(AppWindowTests).Assembly, Connections(), Options("WindowDuplicate")); }
            catch (InvalidOperationException) { rejected = true; }
            assert(rejected, "Duplicate area ID was accepted");
        });
        test("window rejects missing required area placement", () =>
        {
            var rejected = false;
            try { using var _ = new AppWindow(typeof(AppWindowTests).Assembly, Connections(), Options("WindowMissing")); }
            catch (InvalidOperationException) { rejected = true; }
            assert(rejected, "Incomplete area set was accepted");
        });
        test("window rejects missing connection contract", () =>
        {
            var rejected = false;
            try { using var _ = new AppWindow(typeof(AppWindowTests).Assembly, new WindowConnections(), Options("WindowFixture")); }
            catch (InvalidOperationException) { rejected = true; }
            assert(rejected, "Missing connection contract was accepted");
        });
        SynchronizationContext.SetSynchronizationContext(null);
    }
}
