using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarPresentation
{
    private readonly Dictionary<MiniMaxOperationKind, (Button Button, string Idle)> _operations = new();
    private readonly List<Button> _passive = new();
    public void Register(MiniMaxOperationKind kind, Button button, string idle) => _operations.Add(kind, (button, idle));
    public void RegisterPassive(Button button) => _passive.Add(button);
    public void Apply(IMiniMaxOperationSession? active)
    {
        foreach (var entry in _operations)
        {
            entry.Value.Button.Text = entry.Value.Idle;
            entry.Value.Button.Enabled = active == null || active.Kind == entry.Key;
        }
        foreach (var button in _passive) button.Enabled = active == null;
        if (active != null && _operations.TryGetValue(active.Kind, out var current))
            current.Button.Text = active.Phase == MiniMaxOperationPhase.Stopping ? "Stopping…" : "Stop";
    }
}
