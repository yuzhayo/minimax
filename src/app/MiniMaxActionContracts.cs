using System;
using System.Collections.Generic;
using System.Threading;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.App;

internal enum MiniMaxOperationKind { CheckIn, SignInAll, RefreshAll, SignInRow, RefreshRow, Update }
internal enum MiniMaxOperationPhase { Running, Stopping }

internal interface IMiniMaxOperationSession
{
    MiniMaxOperationKind Kind { get; }
    ProfileRow? Row { get; }
    MiniMaxOperationPhase Phase { get; }
    CancellationToken Token { get; }
    bool IsRowOperation { get; }
}

internal interface IMiniMaxOperationCoordinator : IDisposable
{
    IMiniMaxOperationSession? Active { get; }
    event Action<IMiniMaxOperationSession?>? Changed;
    IMiniMaxOperationSession? Begin(MiniMaxOperationKind kind, ProfileRow? row = null);
    bool RequestStop();
    void End(IMiniMaxOperationSession session);
}

internal sealed record MiniMaxRowAction(string Id, string Header, int Width, string StatusProperty,
    string IdleLabel, Action<ProfileRow> Invoke);

internal interface IMiniMaxRowActions
{
    IReadOnlyCollection<MiniMaxRowAction> Actions { get; }
    event Action? Changed;
    void Register(MiniMaxRowAction action);
    bool TryGet(string id, out MiniMaxRowAction action);
}
