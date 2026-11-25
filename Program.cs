using System;
using System.Windows.Forms;
using ChromeProfileLauncher.Simulation;
using ChromeProfileLauncher.UI;

namespace ChromeProfileLauncher;

/// <summary>
/// Entry point for the standalone MiniMax app.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
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
