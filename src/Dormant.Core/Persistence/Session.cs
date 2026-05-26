using System;
using System.Collections.Generic;
using Dormant.Abstractions.Entities;
using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Querying;
using Dormant.Abstractions.Sessions;

namespace Dormant.Core.Persistence;

/// <summary>
/// A thin unit of work over a single <see cref="IDbSession"/> transaction (002 fork, FR-010): a transaction
/// boundary + a read identity map + an executor for authored DQL queries and commands. No change-tracking,
/// no snapshots, no auto-persist — writes are explicit authored commands. Materialized results are immutable.
/// </summary>
internal sealed class Session(IDbSession db) : ISession
{
    private readonly Dictionary<(Type, object), object> _identityMap = [];

    public async ValueTask<TEntity?> GetAsync<TEntity>(
        object key,
        CancellationToken cancellationToken = default
    )
        where TEntity : class
    {
        if (_identityMap.TryGetValue((typeof(TEntity), key), out var existing))
        {
            return (TEntity)existing;
        }

        var binding = EntityBindings.Get<TEntity>();
        await foreach (
            var entity in db.QueryAsync(
                    binding.SelectByKey(key),
                    binding.Materialize,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            _identityMap[(typeof(TEntity), key)] = entity;
            return entity;
        }

        return null;
    }

    public IAsyncEnumerable<TResult> QueryAsync<TResult>(
        CompiledQuery<TResult> query,
        CancellationToken cancellationToken = default
    ) => db.QueryAsync(query.Statement, query.Materialize, cancellationToken);

    public async ValueTask<TResult?> QuerySingleOrDefaultAsync<TResult>(
        CompiledQuery<TResult> query,
        CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var row in db.QueryAsync(query.Statement, query.Materialize, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return row;
        }

        return default;
    }

    // Authored write command executed in the session transaction; INSERT … RETURNING yields one row.
    public async ValueTask<TResult> ExecuteCommandAsync<TResult>(
        CompiledCommand<TResult> command,
        CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var row in db.QueryAsync(command.Statement, command.Materialize, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return row;
        }

        throw new InvalidOperationException("The command returned no row.");
    }

    // Authored update/delete: returns the affected-row count (0 on a stale concurrency token).
    public ValueTask<int> ExecuteWriteAsync(
        PreparedStatement statement,
        CancellationToken cancellationToken = default
    ) => db.ExecuteAsync(statement, cancellationToken);

    public ValueTask CommitAsync(CancellationToken cancellationToken = default) =>
        db.CommitAsync(cancellationToken);

    public ValueTask RollbackAsync(CancellationToken cancellationToken = default) =>
        db.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => db.DisposeAsync();
}
