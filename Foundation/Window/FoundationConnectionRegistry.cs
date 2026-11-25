using System;
using System.Collections.Generic;

namespace AppFoundation.Window;

/// <summary>Composition-root registry; areas receive only their declared contract.</summary>
public sealed class FoundationConnectionRegistry
{
    private readonly Dictionary<Type, object> _connections = new();

    public FoundationConnectionRegistry Add<T>(T connection) where T : class
    {
        if (!_connections.TryAdd(typeof(T), connection ?? throw new ArgumentNullException(nameof(connection))))
            throw new InvalidOperationException("Duplicate foundation connection contract: " + typeof(T).FullName);
        return this;
    }

    internal object Require(Type contract)
    {
        if (!_connections.TryGetValue(contract, out var connection))
            throw new InvalidOperationException("Missing foundation connection contract: " + contract.FullName);
        if (!contract.IsInstanceOfType(connection))
            throw new InvalidOperationException("Incompatible foundation connection: " + contract.FullName);
        return connection;
    }
}
