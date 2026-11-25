using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace ChromeProfileLauncher.Services;

/// <summary>Private MiniMax-only transaction; never changes 9Router application code.</summary>
internal sealed class NineRouterDatabaseBridge
{
    // CONFIG AND CONTRACT
    private readonly string _path;
    private readonly int _timeoutSeconds;
    internal sealed record MergeRequest(Guid SourceId, Guid TargetId, string DesiredName,
        DateTimeOffset StartedAt, int OriginalAccountCount);
    internal sealed record RollbackRequest(Guid SourceId, Guid TargetId, DateTimeOffset StartedAt, bool CreateNew);
    private sealed record Row(string Provider, string AuthType, string Name, JsonObject Data, DateTimeOffset CreatedAt);

    public NineRouterDatabaseBridge(string path, int timeoutSeconds = 2)
    {
        _path = Path.GetFullPath(path);
        _timeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
    }

    private SqliteConnection Open(SqliteOpenMode mode = SqliteOpenMode.ReadWrite)
    {
        if (!File.Exists(_path)) throw new FileNotFoundException("9Router database is missing.");
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _path, Mode = mode, Pooling = false, DefaultTimeout = _timeoutSeconds
        }.ToString());
        connection.Open();
        return connection;
    }

    // LOGIC AND CALCULATIONS
    private static void ValidateSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT key,value FROM _meta WHERE key IN ('schemaVersion','backupSchemaVersion','appVersion')";
        var meta = new Dictionary<string, string>();
        using (var reader = command.ExecuteReader()) while (reader.Read()) meta[reader.GetString(0)] = reader.GetString(1);
        if (meta.GetValueOrDefault("schemaVersion") != NineRouterPaths.SupportedSchemaVersion ||
            meta.GetValueOrDefault("backupSchemaVersion") != NineRouterPaths.SupportedBackupSchemaVersion ||
            meta.GetValueOrDefault("appVersion") != NineRouterPaths.SupportedAppVersion)
            throw new InvalidDataException("Unsupported 9Router database version; no changes made.");
        command.CommandText = "PRAGMA table_info(providerConnections)";
        var columns = new Dictionary<string, string>();
        using (var reader = command.ExecuteReader()) while (reader.Read()) columns[reader.GetString(1)] = reader.GetString(2).ToUpperInvariant();
        foreach (var column in new[] { "id", "provider", "authType", "name", "data", "createdAt", "updatedAt" })
            if (columns.GetValueOrDefault(column) != "TEXT") throw new InvalidDataException("Unsupported 9Router connection schema.");
        foreach (var column in new[] { "priority", "isActive" })
            if (columns.GetValueOrDefault(column) != "INTEGER") throw new InvalidDataException("Unsupported 9Router connection schema.");
    }

    private static Row? ReadRow(SqliteConnection connection, SqliteTransaction transaction, Guid id, bool optional = false)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT provider,authType,name,data,createdAt FROM providerConnections WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            if (optional) return null;
            throw new InvalidDataException("Source or target connection no longer exists.");
        }
        if (reader.GetString(0) != NineRouterPaths.Provider || reader.GetString(1) != "oauth")
            throw new InvalidDataException("Only MiniMax Global OAuth connections may be merged.");
        JsonObject data;
        try { data = JsonNode.Parse(reader.GetString(3)) as JsonObject ?? throw new InvalidDataException("Invalid connection data."); }
        catch (System.Text.Json.JsonException) { throw new InvalidDataException("Connection data is corrupt; no changes made."); }
        if (!DateTimeOffset.TryParse(reader.GetString(4), out var created)) throw new InvalidDataException("Invalid connection creation date.");
        return new Row(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2), data, created);
    }

    // STATE AND COORDINATION
    public void ValidateReadOnly()
    {
        using var connection = Open(SqliteOpenMode.ReadOnly);
        ValidateSchema(connection, null);
    }

    public Guid Merge(MergeRequest request, CancellationToken token = default, Action? beforeDelete = null)
    {
        var createNew = request.TargetId == Guid.Empty;
        if (request.SourceId == Guid.Empty || request.SourceId == request.TargetId ||
            string.IsNullOrWhiteSpace(request.DesiredName) || request.OriginalAccountCount < 0)
            throw new InvalidDataException("Invalid token replacement request.");
        token.ThrowIfCancellationRequested();
        using var connection = Open();
        // Immediate transaction acquires the write lock before loading either row.
        // Microsoft.Data.Sqlite retries busy/locked operations up to DefaultTimeout.
        using var transaction = connection.BeginTransaction(deferred: false);
        ValidateSchema(connection, transaction);
        var source = ReadRow(connection, transaction, request.SourceId, optional: !createNew);
        var target = createNew ? source! : ReadRow(connection, transaction, request.TargetId)!;
        var finalId = createNew ? request.SourceId : request.TargetId;
        // A crash after commit but before mapping persistence can retry safely.
        // Only our own exact source marker proves the earlier merge committed.
        if (source == null)
        {
            if (target.Data["minimaxBridgeLastSourceId"] is not JsonValue marker ||
                !marker.TryGetValue<string>(out var markerId) || markerId != request.SourceId.ToString())
                throw new InvalidDataException("Temporary source is missing and no matching completed merge was recorded.");
            transaction.Commit();
            return finalId;
        }
        if (source.CreatedAt < request.StartedAt.AddSeconds(-2) || (!createNew && source.CreatedAt <= target.CreatedAt))
            throw new InvalidDataException("The source is not a new connection from this authorization job.");
        string? StringValue(string key) => source.Data[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        if (string.IsNullOrWhiteSpace(StringValue("accessToken")) || string.IsNullOrWhiteSpace(StringValue("refreshToken")) ||
            !DateTimeOffset.TryParse(StringValue("expiresAt"), out var expires) || expires <= DateTimeOffset.UtcNow)
            throw new InvalidDataException("New OAuth credentials are missing or expired; old connection preserved.");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM providerConnections WHERE provider=$provider";
        command.Parameters.AddWithValue("$provider", NineRouterPaths.Provider);
        if (Convert.ToInt32(command.ExecuteScalar()) != request.OriginalAccountCount + 1)
            throw new InvalidDataException("9Router account count changed during authorization. Review before merging.");

        foreach (var key in new[] { "accessToken", "refreshToken", "expiresAt", "expiresIn", "tokenType", "scope", "idToken" })
        {
            if (source.Data.ContainsKey(key)) target.Data[key] = source.Data[key]?.DeepClone();
            else if (key is "idToken" or "tokenType" or "scope") target.Data.Remove(key);
        }
        target.Data["lastRefreshAt"] = DateTimeOffset.UtcNow.ToString("O");
        target.Data["testStatus"] = "active";
        foreach (var key in new[] { "lastError", "lastErrorAt", "errorCode", "rateLimitedUntil" }) target.Data[key] = null;
        target.Data["backoffLevel"] = 0;
        target.Data["minimaxBridgeLastSourceId"] = request.SourceId.ToString();
        foreach (var key in target.Data.Select(p => p.Key).Where(k => k.StartsWith("modelLock_", StringComparison.Ordinal)).ToArray()) target.Data[key] = null;
        command.CommandText = "UPDATE providerConnections SET name=$name,data=$data,updatedAt=$updated WHERE id=$target";
        command.Parameters.AddWithValue("$name", request.DesiredName);
        command.Parameters.AddWithValue("$data", target.Data.ToJsonString());
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$target", finalId.ToString());
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Target connection update failed.");
        token.ThrowIfCancellationRequested();
        beforeDelete?.Invoke(); // Test seam for proving rollback after update, before deletion.
        if (!createNew)
        {
            command.CommandText = "DELETE FROM providerConnections WHERE id=$source";
            command.Parameters.AddWithValue("$source", request.SourceId.ToString());
            if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Temporary connection deletion failed.");
        }
        token.ThrowIfCancellationRequested();
        transaction.Commit();
        return finalId;
    }

    /// <summary>Deletes only the exact temporary OAuth connection created by one failed attempt.</summary>
    public bool Rollback(RollbackRequest request)
    {
        if (request.SourceId == Guid.Empty || (!request.CreateNew && request.TargetId == Guid.Empty) ||
            (request.TargetId != Guid.Empty && request.SourceId == request.TargetId))
            throw new InvalidDataException("Invalid refresh rollback request.");

        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        ValidateSchema(connection, transaction);
        var source = ReadRow(connection, transaction, request.SourceId, optional: true);
        if (source == null)
        {
            transaction.Commit();
            return false;
        }
        if (source.CreatedAt < request.StartedAt.AddSeconds(-2))
            throw new InvalidDataException("Refusing to remove a connection that predates this refresh attempt.");
        if (!request.CreateNew)
            _ = ReadRow(connection, transaction, request.TargetId);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM providerConnections WHERE id=$source";
        command.Parameters.AddWithValue("$source", request.SourceId.ToString());
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Temporary refresh connection was not removed.");
        transaction.Commit();
        return true;
    }
}
