using Dormant.Abstractions.Providers;
using Npgsql;

namespace Dormant.Provider.PostgreSql;

/// <summary>An <see cref="IDataSource"/> over an AOT-safe <see cref="NpgsqlDataSource"/> (slim builder).</summary>
internal sealed class PostgreSqlDataSource(NpgsqlDataSource dataSource) : IDataSource
{
    public async ValueTask<IDbSession> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = await dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PostgreSqlSession(connection);
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
