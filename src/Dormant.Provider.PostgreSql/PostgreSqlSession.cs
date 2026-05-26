using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Querying;
using Dormant.Provider.PostgreSql.Io;
using Npgsql;

namespace Dormant.Provider.PostgreSql;

/// <summary>An <see cref="IDbSession"/> over an <see cref="NpgsqlConnection"/> + transaction.</summary>
internal sealed class PostgreSqlSession(NpgsqlConnection connection) : IDbSession
{
    private NpgsqlTransaction? _transaction;

    public async ValueTask BeginAsync(CancellationToken cancellationToken = default) =>
        _transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<int> ExecuteAsync(
        PreparedStatement statement,
        CancellationToken cancellationToken = default
    )
    {
        await using var command = CreateCommand(statement);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<TRow> QueryAsync<TRow>(
        PreparedStatement statement,
        RowMaterializer<TRow> materialize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await using var command = CreateCommand(statement);
        await using var reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        var fieldReader = new PostgreSqlFieldReader(reader);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return materialize(fieldReader);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }

        await connection.DisposeAsync().ConfigureAwait(false);
    }

    private NpgsqlCommand CreateCommand(PreparedStatement statement)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement.Sql;
        command.Transaction = _transaction;
        statement.BindParameters?.Invoke(new PostgreSqlParameterWriter(command.Parameters));
        return command;
    }
}
