using Dormant.Abstractions.Ports;
using Npgsql;

namespace Dormant.Provider.PostgreSql;

/// <summary>Entry point for the PostgreSQL provider adapter (spec FR-024).</summary>
public static class DormantPostgres
{
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
}
