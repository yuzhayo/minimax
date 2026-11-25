using System;
using System.Linq;
using AppFoundation.Areas.ActionBar;
using AppFoundation.Areas.BottomText;
using AppFoundation.Shared.UI;
using AppFoundation.Window;
using ChromeProfileLauncher.Areas.Body;
using ChromeProfileLauncher.UI;

internal static class FoundationArchitectureTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert)
    {
        var assembly = typeof(MiniMaxForm).Assembly;
        test("MiniMax window is a thin Foundation wrapper with no feature workflow methods", () =>
        {
            var declared = typeof(MiniMaxForm).GetMethods(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.DeclaredOnly).Select(method => method.Name).ToArray();
            assert(typeof(MiniMaxForm).BaseType == typeof(FoundationWindow) &&
                !declared.Any(name => name.StartsWith("Start", StringComparison.Ordinal) || name.StartsWith("Build", StringComparison.Ordinal) || name == "SetStatus"),
                "MiniMax wrapper still owns feature/UI workflows");
        });
        test("MiniMax declares exactly three discoverable window areas", () =>
            assert(assembly.GetTypes().Count(type => !type.IsAbstract && typeof(IFoundationWindowArea).IsAssignableFrom(type) &&
                ((IFoundationWindowArea)Activator.CreateInstance(type, nonPublic: true)!).Scope == "MiniMax") == 3,
                "MiniMax area set is not exactly ActionBar/Body/BottomText"));
        test("ActionBar and Body children are additive owner-prefixed declarations", () =>
        {
            var actions = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IActionBarItem).IsAssignableFrom(type)).ToArray();
            var columns = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IBodyProfileColumn).IsAssignableFrom(type)).ToArray();
            assert(actions.Length == 6 && actions.All(type => type.Name.StartsWith("ActionBar", StringComparison.Ordinal)) &&
                columns.Length == 5 && columns.All(type => type.Name.StartsWith("Body", StringComparison.Ordinal)),
                "Action/column children lost owner naming or discovery");
        });
        test("all visible bottom text is owned by two BottomText declarations", () =>
        {
            var text = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IBottomTextItem).IsAssignableFrom(type)).ToArray();
            assert(text.Length == 2 && text.All(type => type.Name.StartsWith("BottomText", StringComparison.Ordinal)),
                "Bottom text ownership is incomplete");
        });
        test("Shared UI lives in Foundation and Foundation references no MiniMax assembly", () =>
            assert(typeof(SharedActionButton).Assembly == typeof(FoundationWindow).Assembly &&
                !typeof(FoundationWindow).Assembly.GetReferencedAssemblies().Any(name => name.Name is "MiniMax" or "ChromeProfileLauncher"),
                "Foundation Shared/UI depends on MiniMax"));
    }
}
