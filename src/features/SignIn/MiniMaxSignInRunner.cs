using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

internal enum MiniMaxSignInOutcome { AlreadySignedIn, SignedIn }
internal sealed record MiniMaxSignInResult(NineRouterChromeProfileSession Browser, MiniMaxSignInOutcome Outcome);

/// <summary>Owns one profile's browser opening and the recorded sign-in sequence.</summary>
internal sealed class MiniMaxSignInRunner : IDisposable
{
    // CONFIG AND CONTRACT
    private readonly NineRouterChromeProfileStarter _chrome;
    private readonly SemaphoreSlim _lease = new(1, 1);
    public MiniMaxSignInRunner(NineRouterChromeProfileStarter? chrome = null) => _chrome = chrome ?? new();

    // STATE AND COORDINATION
    public async Task<MiniMaxSignInResult> RunAsync(ChromeProfile profile, Uri miniMaxUrl,
        IProgress<string> progress, CancellationToken token = default)
    {
        if (!await _lease.WaitAsync(0, token)) throw new InvalidOperationException("A sign-in operation is already running.");
        try
        {
            progress.Report("Opening profile");
            var browser = await _chrome.OpenOrReuseAsync(profile.ChromePath, profile.DirectoryName, miniMaxUrl, token);
            var outcome = await EnsureSignedInAsync(browser.Window, miniMaxUrl, progress, token);
            progress.Report(outcome == MiniMaxSignInOutcome.SignedIn ? "Signed in" : "Already signed in");
            return new(browser, outcome);
        }
        finally { _lease.Release(); }
    }

    internal static Task<MiniMaxSignInOutcome> EnsureSignedInAsync(IntPtr window, Uri miniMaxUrl,
        IProgress<string> progress, CancellationToken token = default) =>
        new MiniMaxSignInFlow(new MiniMaxSignInPage(window, miniMaxUrl)).RunAsync(progress, token);

    public async Task CompleteStandaloneAsync(MiniMaxSignInResult result, IProgress<string> progress,
        CancellationToken token = default)
    {
        progress.Report("Closing Chrome");
        await _chrome.CloseStandaloneAsync(result.Browser, token);
        progress.Report("Done");
    }

    // LIFECYCLE AND CLEANUP
    public void Dispose() { _chrome.Dispose(); _lease.Dispose(); }
}

/// <summary>State machine for only the operator-provided MiniMax/GitHub controls.</summary>
internal sealed class MiniMaxSignInFlow
{
    // CONFIG AND CONTRACT
    private readonly IMiniMaxSignInPage _page;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(3);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public int StableSignedInReads { get; init; } = 4;
    public MiniMaxSignInFlow(IMiniMaxSignInPage page) => _page = page;

    // STATE AND COORDINATION
    public async Task<MiniMaxSignInOutcome> RunAsync(IProgress<string> progress, CancellationToken token = default)
    {
        var watch = Stopwatch.StartNew();
        var stableSignedIn = 0;
        progress.Report("Checking sign-in");
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var page = _page.Read();
            if (page.SignInButtons > 1) throw new InvalidOperationException("More than one visible Sign in button.");
            if (page.MiniMaxLoaded && page.SignInButtons == 1)
            {
                progress.Report("Signing in");
                if (_page.PressSignIn(token)) break;
            }
            else if (page.MiniMaxLoaded && page.SignInButtons == 0)
            {
                if (++stableSignedIn >= StableSignedInReads) return MiniMaxSignInOutcome.AlreadySignedIn;
            }
            else stableSignedIn = 0;
            await Task.Delay(PollInterval, token);
        }
        if (watch.Elapsed >= Timeout) throw new TimeoutException("The MiniMax page did not reach a readable sign-in state.");

        progress.Report("Waiting for sign-in options");
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var page = _page.Read();
            if (page.ConsentButtons > 1 || page.GitHubButtons > 1)
                throw new InvalidOperationException("More than one visible sign-in option was found.");
            if (page.ConsentButtons == 1 && page.GitHubButtons == 1)
            {
                if (!page.ConsentChecked)
                {
                    progress.Report("Accepting terms");
                    if (!_page.PressConsent(token)) throw new InvalidOperationException("The consent control could not be pressed.");
                    await WaitForConsent(token, watch);
                }
                progress.Report("Continue With Github");
                if (_page.PressGitHub(token)) break;
            }
            await Task.Delay(PollInterval, token);
        }
        if (watch.Elapsed >= Timeout) throw new TimeoutException("The MiniMax sign-in options did not appear.");

        progress.Report("Waiting for Continue");
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var page = _page.Read();
            if (page.ContinueButtons > 1) throw new InvalidOperationException("More than one visible Continue control was found.");
            if (page.ContinueButtons == 1 && _page.PressContinue(token)) break;
            await Task.Delay(PollInterval, token);
        }
        if (watch.Elapsed >= Timeout) throw new TimeoutException("The GitHub Continue step did not appear.");

        progress.Report("Returning to MiniMax");
        stableSignedIn = 0;
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var page = _page.Read();
            if (page.SignInButtons > 1) throw new InvalidOperationException("More than one visible Sign in button.");
            if (page.MiniMaxLoaded && page.SignInButtons == 0)
            {
                if (++stableSignedIn >= StableSignedInReads) return MiniMaxSignInOutcome.SignedIn;
            }
            else stableSignedIn = 0;
            await Task.Delay(PollInterval, token);
        }
        throw new TimeoutException("MiniMax did not return to the signed-in page.");
    }

    private async Task WaitForConsent(CancellationToken token, Stopwatch watch)
    {
        while (watch.Elapsed < Timeout)
        {
            token.ThrowIfCancellationRequested();
            var page = _page.Read();
            if (page.ConsentButtons > 1) throw new InvalidOperationException("More than one visible consent control was found.");
            if (page.ConsentButtons == 1 && page.ConsentChecked) return;
            await Task.Delay(PollInterval, token);
        }
        throw new TimeoutException("The consent control did not become checked.");
    }
}
