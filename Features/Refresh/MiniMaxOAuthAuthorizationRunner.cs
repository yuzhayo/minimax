using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ChromeProfileLauncher.Services;

internal sealed class MiniMaxOAuthAuthorizationRunner
{
    // CONFIG AND CONTRACT
    private readonly IMiniMaxOAuthAuthorizationPage _page;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan ClickRetryAfter { get; init; } = TimeSpan.FromSeconds(3);
    public MiniMaxOAuthAuthorizationRunner(IMiniMaxOAuthAuthorizationPage page) => _page = page;

    // STATE AND COORDINATION
    public async Task WaitForConfirmationAsync(Func<bool> pending, CancellationToken token = default)
    {
        var watch = Stopwatch.StartNew();
        var clicks = 0;
        var clickedAt = TimeSpan.Zero;
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var snapshot = _page.Read();
            if (snapshot.Confirmed) return;
            if (snapshot.AuthorizeButtons > 1) throw new InvalidOperationException("More than one visible Authorize button; no click performed.");
            if (snapshot.AuthorizeButtons == 1 && (clicks == 0 ||
                (clicks == 1 && pending() && watch.Elapsed - clickedAt >= ClickRetryAfter)))
            {
                token.ThrowIfCancellationRequested();
                clicks++;
                clickedAt = watch.Elapsed;
                _page.PressAuthorize(token);
            }
            await Task.Delay(PollInterval, token);
        }
        throw new TimeoutException("The OAuth page did not show Authorization confirmed. Existing tokens were not replaced.");
    }
}
