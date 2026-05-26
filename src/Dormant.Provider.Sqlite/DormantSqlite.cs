using System.Threading;
using System.Threading.Tasks;
using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Sessions;
using Dormant.Core.Migrations;
using Dormant.Core.Persistence;

namespace Dormant.Provider.Sqlite;

/// <summary>
/// Entry point for the SQLite provider adapter (spec 005 FR-001/FR-002). AOT-friendly: it uses
/// <c>Microsoft.Data.Sqlite.Core</c> with the static <c>e_sqlite3</c> bundle and initializes the native
/// provider explicitly via <see cref="SQLitePCL.Batteries_V2.Init"/> (no reflective auto-discovery —
/// mirroring the Npgsql-slim discipline, research D11). Supports file and shared in-memory databases; no
/// Docker. Minimum engine: SQLite 3.35 (for <c>RETURNING</c>); the bundle ships a newer build.
/// </summary>
public static class DormantSqlite
{
    static DormantSqlite() => SQLitePCL.Batteries_V2.Init();

    /// <summary>Creates a session factory over a SQLite data source.</summary>
    /// <param name="connectionString">The SQLite connection string (e.g. <c>Data Source=app.db</c>).</param>
    /// <returns>A session factory.</returns>
    public static ISessionFactory CreateSessionFactory(string connectionString) =>
        new SessionFactory(CreateDataSource(connectionString));

    /// <summary>Creates a SQLite <see cref="IDataSource"/> (keeps a shared in-memory database alive for its lifetime).</summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>An open-able data source.</returns>
    public static IDataSource CreateDataSource(string connectionString) =>
        new SqliteDataSource(connectionString);

    /// <summary>The SQLite SQL dialect (identifiers, named placeholders, capabilities).</summary>
    public static ISqlDialect Dialect => SqliteDialect.Instance;

    /// <summary>Applies the generated initial schema to a file/disk database. Idempotent.</summary>
    /// <param name="connectionString">The SQLite connection string (file/disk).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the schema has been applied.</returns>
    /// <remarks>For a shared in-memory database, use <see cref="EnsureCreatedAsync(IDataSource, CancellationToken)"/>
    /// with the same kept-alive data source the sessions are opened from, so the schema persists.</remarks>
    public static async ValueTask EnsureCreatedAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        await using var dataSource = CreateDataSource(connectionString);
        await EnsureCreatedAsync(dataSource, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies the generated initial schema using an existing (kept-alive) data source. Idempotent.</summary>
    /// <param name="dataSource">The data source the sessions will also be opened from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the schema has been applied.</returns>
    public static async ValueTask EnsureCreatedAsync(
        IDataSource dataSource,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = await dataSource.OpenAsync(cancellationToken).ConfigureAwait(false);
        await SchemaInitializer.EnsureCreatedAsync(db, cancellationToken).ConfigureAwait(false);
    }
}
