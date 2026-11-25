using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using AppFoundation.Areas.ActionBar;
using AppFoundation.Areas.Body;
using AppFoundation.Areas.BottomText;
using AppFoundation.Shared.UI;
using AppFoundation.Window;

namespace SecondHostFixture;

internal interface IFixtureActions { }
internal interface IFixtureBody { }
internal interface IFixtureText { }
internal sealed class FixtureConnections : IFixtureActions, IFixtureBody, IFixtureText { }

internal sealed class FixtureWindowActionArea : IFoundationWindowArea
{
    public string Scope => "SecondHost"; public string Id => "action";
    public FoundationAreaPlacement Placement => FoundationAreaPlacement.ActionBar;
    public Type ConnectionContract => typeof(IFixtureActions);
    public Control Build(FoundationAreaBuildContext context, object connection) => new ActionBarArea(GetType().Assembly, Scope, context);
    public void Dispose() { }
}
internal sealed class FixtureWindowBodyArea : IFoundationWindowArea
{
    public string Scope => "SecondHost"; public string Id => "body";
    public FoundationAreaPlacement Placement => FoundationAreaPlacement.Body;
    public Type ConnectionContract => typeof(IFixtureBody);
    public Control Build(FoundationAreaBuildContext context, object connection) => new BodyArea(GetType().Assembly, Scope, context);
    public void Dispose() { }
}
internal sealed class FixtureWindowTextArea : IFoundationWindowArea
{
    public string Scope => "SecondHost"; public string Id => "text";
    public FoundationAreaPlacement Placement => FoundationAreaPlacement.BottomText;
    public Type ConnectionContract => typeof(IFixtureText);
    public Control Build(FoundationAreaBuildContext context, object connection) => new BottomTextArea(GetType().Assembly, Scope, context);
    public void Dispose() { }
}
internal sealed class FixtureAction : IActionBarItem
{
    public string Scope => "SecondHost"; public string Id => "fixture-action"; public int Order => 0; public int Width => 130;
    public Type ConnectionContract => typeof(IFixtureActions);
    public Control Build(FoundationAreaBuildContext context, object connection) => new SharedActionButton("Fixture action");
    public void Dispose() { }
}
internal sealed class FixtureBody : IBodyComponent
{
    public string Scope => "SecondHost"; public string Id => "fixture-body"; public int Order => 0;
    public Type ConnectionContract => typeof(IFixtureBody);
    public Control Build(FoundationAreaBuildContext context, object connection) => new Label { Text = "Fixture body", Dock = DockStyle.Fill };
    public void Dispose() { }
}
internal sealed class FixtureText : IBottomTextItem
{
    public string Scope => "SecondHost"; public string Id => "fixture-text"; public int Order => 0; public int Height => 32;
    public Type ConnectionContract => typeof(IFixtureText);
    public Control Build(FoundationAreaBuildContext context, object connection) => new SharedTextBlock { Text = "Fixture text" };
    public void Dispose() { }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var connections = new FixtureConnections();
        var registry = new FoundationConnectionRegistry().Add<IFixtureActions>(connections)
            .Add<IFixtureBody>(connections).Add<IFixtureText>(connections);
        using var window = new FoundationWindow(Assembly.GetExecutingAssembly(), registry,
            new FoundationWindowOptions("SecondHost", "Second Host", new Size(400, 300), new Size(700, 500)));
        SharedTheme.Apply(window);
        if (args.Contains("--verify"))
        {
            var text = Descendants(window).Select(control => control.Text).ToArray();
            var passed = window.Areas.Count == 3 && text.Contains("Fixture action") && text.Contains("Fixture body") && text.Contains("Fixture text");
            Console.WriteLine(passed ? "SECOND_HOST PASS: app-specific action/body/text discovered without Foundation edits" : "SECOND_HOST FAIL");
            return passed ? 0 : 1;
        }
        Application.EnableVisualStyles();
        Application.Run(window);
        return 0;
    }
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
}
