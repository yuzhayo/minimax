using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

internal sealed record MiniMaxSignInBatchProgress(ProfileRow Row, int Index, int Total, string Status);
internal sealed record MiniMaxSignInBatchResult(int Total, int Completed, int SignedIn, int AlreadySignedIn, int Failed);

/// <summary>Chains the shared one-row sign-in operation in displayed order.</summary>
internal sealed class MiniMaxSignInBatchRunner
{
    // CONFIG AND CONTRACT
    private sealed class RowProgress(Action<string> report) : IProgress<string>
    { public void Report(string value) => report(value); }
    private int _running;

    // STATE AND COORDINATION
    public async Task<MiniMaxSignInBatchResult> RunAsync(IReadOnlyList<ProfileRow> rows,
        Func<ProfileRow, IProgress<string>, CancellationToken, Task<MiniMaxSignInOutcome>> signInRow,
        IProgress<MiniMaxSignInBatchProgress> progress, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("Sign in all is already running.");
        try
        {
            var completed = 0; var signedIn = 0; var already = 0; var failed = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var row = rows[i]; var index = i + 1;
                var rowProgress = new RowProgress(status => progress.Report(new(row, index, rows.Count, status)));
                rowProgress.Report("Starting sign in");
                try
                {
                    var outcome = await signInRow(row, rowProgress, token);
                    completed++;
                    if (outcome == MiniMaxSignInOutcome.SignedIn) signedIn++; else already++;
                }
                catch (OperationCanceledException)
                {
                    rowProgress.Report("Stopped"); rowProgress.Report("Sign in"); throw;
                }
                catch (Exception ex)
                {
                    completed++; failed++;
                    rowProgress.Report("Failed: " + ex.Message);
                    rowProgress.Report("Sign in");
                }
            }
            token.ThrowIfCancellationRequested();
            return new(rows.Count, completed, signedIn, already, failed);
        }
        finally { Interlocked.Exchange(ref _running, 0); }
    }
}
