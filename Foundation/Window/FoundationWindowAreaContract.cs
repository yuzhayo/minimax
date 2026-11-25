using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppFoundation.Window;

public enum FoundationAreaPlacement { ActionBar, Body, BottomText }

public sealed record FoundationAreaBuildContext(IWin32Window Owner, Func<Type, object> RequireConnection,
    Action RequestLayout, Action<DataGridView, int> FitRows, Action RequestClose);

/// <summary>Explicit declaration discovered only inside one named app scope.</summary>
public interface IFoundationWindowArea : IDisposable
{
    string Scope { get; }
    string Id { get; }
    FoundationAreaPlacement Placement { get; }
    Type ConnectionContract { get; }
    Control Build(FoundationAreaBuildContext context, object connection);
    Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
    bool RequestClosing() => true;
    bool HandleWindowMessage(ref Message message) => false;
    void WindowClosed() { }
}
