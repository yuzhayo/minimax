using System;
using System.Threading;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarOperationSession : IMiniMaxOperationSession, IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    public MiniMaxOperationKind Kind { get; }
    public ProfileRow? Row { get; }
    public MiniMaxOperationPhase Phase { get; private set; } = MiniMaxOperationPhase.Running;
    public CancellationToken Token => _cancellation.Token;
    public bool IsRowOperation => Kind is MiniMaxOperationKind.SignInRow or MiniMaxOperationKind.RefreshRow;
    public ActionBarOperationSession(MiniMaxOperationKind kind, ProfileRow? row = null)
    {
        if ((kind is MiniMaxOperationKind.SignInRow or MiniMaxOperationKind.RefreshRow) != (row != null))
            throw new ArgumentException("Row operations require exactly one profile row.");
        Kind = kind; Row = row;
    }
    public bool RequestStop()
    {
        if (Phase == MiniMaxOperationPhase.Stopping) return false;
        Phase = MiniMaxOperationPhase.Stopping; _cancellation.Cancel(); return true;
    }
    public void Dispose() => _cancellation.Dispose();
}

internal sealed class ActionBarOperationCoordinator : IMiniMaxOperationCoordinator, IDisposable
{
    public ActionBarOperationSession? Active { get; private set; }
    IMiniMaxOperationSession? IMiniMaxOperationCoordinator.Active => Active;
    public event Action<IMiniMaxOperationSession?>? Changed;
    public IMiniMaxOperationSession? Begin(MiniMaxOperationKind kind, ProfileRow? row = null)
    {
        if (Active != null) return null;
        Active = new(kind, row); Changed?.Invoke(Active); return Active;
    }
    public bool RequestStop()
    {
        if (Active == null || !Active.RequestStop()) return false;
        Changed?.Invoke(Active); return true;
    }
    public void End(IMiniMaxOperationSession session)
    {
        if (!ReferenceEquals(Active, session)) return;
        ((ActionBarOperationSession)session).Dispose(); Active = null; Changed?.Invoke(null);
    }
    public void Dispose() { Active?.Dispose(); Active = null; }
}
