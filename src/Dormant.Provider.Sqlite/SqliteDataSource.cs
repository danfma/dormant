using System;
using Dormant.Abstractions.Providers;
using Microsoft.Data.Sqlite;

namespace Dormant.Provider.Sqlite;

/// <summary>
/// Opens SQLite <see cref="IDbSession"/> instances over fresh connections (spec 005 FR-002). For a
/// shared in-memory database (<c>Mode=Memory;Cache=Shared</c>) it holds one keep-alive connection open for
/// its lifetime, so the database persists across the sessions it hands out (an in-memory DB is dropped when
/// its last connection closes — 005 D12). File and server-less disk databases need no keep-alive.
/// </summary>
internal sealed class SqliteDataSource : IDataSource
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _keepAlive;

    public SqliteDataSource(string connectionString)
    {
        _connectionString = connectionString;
        if (IsInMemory(connectionString))
        {
            _keepAlive = new SqliteConnection(connectionString);
            _keepAlive.Open();
        }
    }

    public async ValueTask<IDbSession> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SqliteSession(connection);
    }

    public async ValueTask DisposeAsync()
    {
        if (_keepAlive is not null)
        {
            await _keepAlive.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool IsInMemory(string connectionString) =>
        connectionString.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase);
}
