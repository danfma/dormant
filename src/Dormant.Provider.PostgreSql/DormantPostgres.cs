using System.Threading;
using System.Threading.Tasks;
using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Sessions;
using Dormant.Core.Migrations;
using Dormant.Core.Persistence;
using Npgsql;

namespace Dormant.Provider.PostgreSql;

/// <summary>Entry point for the PostgreSQL provider adapter (spec FR-024).</summary>
public static class DormantPostgres
{
    /// <summary>Creates a session factory over an AOT-safe PostgreSQL data source.</summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>A session factory.</returns>
    public static ISessionFactory CreateSessionFactory(string connectionString) =>
        new SessionFactory(CreateDataSource(connectionString));

    /// <summary>
    /// Creates an AOT-safe PostgreSQL <see cref="IDataSource"/> over the Npgsql slim builder
    /// (<see cref="NpgsqlSlimDataSourceBuilder"/>) — no dynamic JSON, no reflection-based mapping
    /// (research §2).
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>An open-able data source.</returns>
    public static IDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlSlimDataSourceBuilder(connectionString);
        return new PostgreSqlDataSource(builder.Build());
    }

    /// <summary>The PostgreSQL SQL dialect (identifiers, positional placeholders, capabilities).</summary>
    public static ISqlDialect Dialect => PostgreSqlDialect.Instance;

    /// <summary>
    /// Applies the generated initial schema (CREATE SCHEMA + CREATE TABLE for every registered entity)
    /// to the target database (spec FR-020/FR-045). Idempotent.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the schema has been applied.</returns>
    public static async ValueTask EnsureCreatedAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        await using var dataSource = CreateDataSource(connectionString);
        await using var db = await dataSource.OpenAsync(cancellationToken).ConfigureAwait(false);
        await SchemaInitializer.EnsureCreatedAsync(db, cancellationToken).ConfigureAwait(false);
    }
}
