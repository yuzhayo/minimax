using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Services;

internal sealed record MiniMaxOAuthBatchProgress(ProfileRow Row, int Index, int Total, string Status);
internal sealed record MiniMaxOAuthBatchResult(int Total, int Completed, int Succeeded, int Failed, int Warnings);

/// <summary>Chains the provided existing row operation; owns no OAuth/browser logic.</summary>
internal sealed class MiniMaxOAuthRefreshBatchRunner
{
    // CONFIG AND CONTRACT
    private sealed class RowProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
    private int _running;

    // STATE AND COORDINATION
    public async Task<MiniMaxOAuthBatchResult> RunAsync(IReadOnlyList<ProfileRow> rows,
        Func<ProfileRow, IProgress<string>, CancellationToken, Task> refreshRow,
        IProgress<MiniMaxOAuthBatchProgress> progress, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("Refresh all is already running.");
        try
        {
            var completed = 0; var succeeded = 0; var failed = 0; var warnings = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var row = rows[i];
                var index = i + 1;
                var lastStatus = "Refresh";
                var rowProgress = new RowProgress(status =>
                {
                    lastStatus = status;
                    progress.Report(new(row, index, rows.Count, status));
                });
                rowProgress.Report("Starting refresh");
                try
                {
                    await refreshRow(row, rowProgress, token);
                    completed++;
                    if (lastStatus.StartsWith("Done —", StringComparison.Ordinal)) warnings++; else succeeded++;
                    rowProgress.Report(lastStatus == "Starting refresh" ? "Done" : lastStatus);
                }
                catch (OperationCanceledException)
                {
                    rowProgress.Report("Stopped");
                    rowProgress.Report("Refresh");
                    throw;
                }
                catch (Exception ex)
                {
                    completed++; failed++;
                    rowProgress.Report("Failed: " + ex.Message);
                    rowProgress.Report("Refresh");
                }
            }
            token.ThrowIfCancellationRequested();
            return new(rows.Count, completed, succeeded, failed, warnings);
        }
        finally { Interlocked.Exchange(ref _running, 0); }
    }
}
