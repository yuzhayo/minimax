using System;
using System.Collections.Generic;

namespace ChromeProfileLauncher.Window;

/// <summary>Composition-root registry; areas receive only their declared contract.</summary>
public sealed class WindowConnections
{
    private readonly Dictionary<Type, object> _connections = new();

    public WindowConnections Add<T>(T connection) where T : class
    {
        if (!_connections.TryAdd(typeof(T), connection ?? throw new ArgumentNullException(nameof(connection))))
            throw new InvalidOperationException("Duplicate window connection contract: " + typeof(T).FullName);
        return this;
    }

    internal object Require(Type contract)
    {
        if (!_connections.TryGetValue(contract, out var connection))
            throw new InvalidOperationException("Missing window connection contract: " + contract.FullName);
        if (!contract.IsInstanceOfType(connection))
            throw new InvalidOperationException("Incompatible window connection: " + contract.FullName);
        return connection;
    }
}
