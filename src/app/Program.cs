using System;
using System.Windows.Forms;
using ChromeProfileLauncher.Simulation;
using ChromeProfileLauncher.UI;
using ChromeProfileLauncher.Features.Update;

namespace ChromeProfileLauncher;

/// <summary>
/// Entry point for the standalone MiniMax app.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string Required(string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing updater argument: " + name);
            return args[index + 1];
        }
        if (args.Length > 0 && args[0] == "--apply-update")
            return MiniMaxUpdateApplyRunner.Apply(int.Parse(Required("--wait-pid")), Required("--source"),
                Required("--target"), Required("--backup"));
        if (args.Length > 0 && args[0] == "--cleanup-update")
            MiniMaxUpdateApplyRunner.StartCleanup(int.Parse(Required("--wait-pid")), Required("--source"), Required("--backup"));

        // --simulate runs the check-in flow against a fake page. No Chrome is
        // opened, no mouse moves, no real account is touched.
        if (args.Length > 0 && args[0] == "--simulate")
        {
            Console.WriteLine("Dry simulation of the check-in flow (no browser, no real account).");
            Console.WriteLine();

            var ok = DrySimulation.RunAll();

            return ok ? 0 : 1;
        }

        Application.SetColorMode(SystemColorMode.Dark);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        Application.Run(new MiniMaxForm());

        return 0;
    }
}
