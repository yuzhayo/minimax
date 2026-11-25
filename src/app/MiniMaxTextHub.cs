using System;

namespace ChromeProfileLauncher.App;

internal sealed class MiniMaxTextHub
{
    public event Action<string>? StatusChanged;
    public event Action<string>? ProgressChanged;
    public void Status(string value) => StatusChanged?.Invoke(value);
    public void Progress(string value) => ProgressChanged?.Invoke(value);
}
