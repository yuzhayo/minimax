using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChromeProfileLauncher.Shared;

public interface IScopedDeclaration
{
    string Scope { get; }
    string Id { get; }
}

public static class ScopedDiscovery
{
    public static IReadOnlyList<T> Discover<T>(Assembly assembly, string scope) where T : class, IScopedDeclaration
    {
        var children = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(T).IsAssignableFrom(type))
            .Select(type => (T)(Activator.CreateInstance(type, nonPublic: true) ??
                throw new InvalidOperationException("Could not create scoped child: " + type.FullName)))
            .Where(child => child.Scope.Equals(scope, StringComparison.Ordinal))
            .ToList();
        var duplicate = children.GroupBy(child => child.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            foreach (var child in children) if (child is IDisposable disposable) disposable.Dispose();
            throw new InvalidOperationException("Duplicate scoped child ID: " + duplicate.Key);
        }
        return children;
    }
}
