using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppFoundation.Window;

internal static class FoundationWindowTests
{
    private interface IFixtureConnection { string Name { get; } }
    private sealed class FixtureConnection : IFixtureConnection { public string Name => "fixture"; }
    private abstract class Area(string scope, string id, FoundationAreaPlacement placement) : IFoundationWindowArea
    {
        public static int Initialized;
        public string Scope => scope;
        public string Id => id;
        public FoundationAreaPlacement Placement => placement;
        public Type ConnectionContract => typeof(IFixtureConnection);
        public Control Build(FoundationAreaBuildContext context, object connection) =>
            new Label { Text = ((IFixtureConnection)connection).Name + ":" + Id, Dock = DockStyle.Fill };
        public Task InitializeAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Initialized++; return Task.CompletedTask; }
        public void Dispose() { }
    }
    private sealed class FixtureActionArea() : Area("FoundationFixture", "action", FoundationAreaPlacement.ActionBar);
    private sealed class FixtureBodyArea() : Area("FoundationFixture", "body", FoundationAreaPlacement.Body);
    private sealed class FixtureBottomArea() : Area("FoundationFixture", "bottom", FoundationAreaPlacement.BottomText);
    private sealed class DuplicateActionAreaA() : Area("FoundationDuplicate", "same", FoundationAreaPlacement.ActionBar);
    private sealed class DuplicateActionAreaB() : Area("FoundationDuplicate", "same", FoundationAreaPlacement.Body);
    private sealed class DuplicateBottomArea() : Area("FoundationDuplicate", "bottom", FoundationAreaPlacement.BottomText);
    private sealed class MissingActionArea() : Area("FoundationMissing", "action", FoundationAreaPlacement.ActionBar);

    public static void Run(Action<string, Action> test, Action<bool, string> assert)
    {
        FoundationConnectionRegistry Connections() => new FoundationConnectionRegistry().Add<IFixtureConnection>(new FixtureConnection());
        FoundationWindowOptions Options(string scope) => new(scope, "Fixture", new Size(300, 200), new Size(400, 300));
        test("foundation discovers and places one scoped child per area without a registration list", () =>
        {
            Area.Initialized = 0;
            using var window = new FoundationWindow(typeof(FoundationWindowTests).Assembly, Connections(), Options("FoundationFixture"));
            assert(window.Areas.Count == 3 && window.Areas[0].Placement == FoundationAreaPlacement.ActionBar &&
                window.Areas[1].Placement == FoundationAreaPlacement.Body && window.Areas[2].Placement == FoundationAreaPlacement.BottomText,
                "Scoped areas were not discovered/ordered");
            window.InitializeAreasAsync().GetAwaiter().GetResult();
            assert(Area.Initialized == 3, "Area lifecycle was not relayed");
        });
        test("foundation rejects duplicate area IDs", () =>
        {
            var rejected = false;
            try { using var _ = new FoundationWindow(typeof(FoundationWindowTests).Assembly, Connections(), Options("FoundationDuplicate")); }
            catch (InvalidOperationException) { rejected = true; }
            assert(rejected, "Duplicate area ID was accepted");
        });
        test("foundation rejects missing required area placement", () =>
        {
            var rejected = false;
            try { using var _ = new FoundationWindow(typeof(FoundationWindowTests).Assembly, Connections(), Options("FoundationMissing")); }
            catch (InvalidOperationException) { rejected = true; }
            assert(rejected, "Incomplete area set was accepted");
        });
        test("foundation rejects missing connection contract", () =>
        {
            var rejected = false;
            try { using var _ = new FoundationWindow(typeof(FoundationWindowTests).Assembly, new FoundationConnectionRegistry(), Options("FoundationFixture")); }
            catch (InvalidOperationException) { rejected = true; }
            assert(rejected, "Missing connection contract was accepted");
        });
        SynchronizationContext.SetSynchronizationContext(null);
    }
}
