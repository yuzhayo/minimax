using System;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.Simulation;

/// <summary>
/// Drives the simulation's fake page through the same interface the real
/// Chrome driver uses.
/// </summary>
internal sealed class FakeSignInDriver : ISignInDriver
{
    private readonly FakePage _page;

    public FakeSignInDriver(FakePage page)
    {
        _page = page;
    }

    public SignInPageSnapshot Read() => _page.Read();

    public bool ClickMenu()
    {
        _page.ClickMenu();
        return true;
    }

    public bool HoverRow()
    {
        _page.HoverRow();
        return true;
    }

    public bool PressClaim()
    {
        _page.PressClaim();
        return true;
    }
}
