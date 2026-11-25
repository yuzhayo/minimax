using System;
using System.Collections.Generic;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.ActionBar;

internal sealed class ActionBarRowActions : IMiniMaxRowActions
{
    private readonly Dictionary<string, MiniMaxRowAction> _actions = new(StringComparer.Ordinal);
    public IReadOnlyCollection<MiniMaxRowAction> Actions => _actions.Values;
    public event Action? Changed;
    public void Register(MiniMaxRowAction action)
    {
        if (!_actions.TryAdd(action.Id, action)) throw new InvalidOperationException("Duplicate row action: " + action.Id);
        Changed?.Invoke();
    }
    public bool TryGet(string id, out MiniMaxRowAction action) => _actions.TryGetValue(id, out action!);
}
