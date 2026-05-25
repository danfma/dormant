using System.Collections.Generic;
using Dormant.Abstractions.Entities;
using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Querying;
using Dormant.Abstractions.Sessions;

namespace Dormant.Core.Persistence;

/// <summary>
/// Unit of work over a single <see cref="IDbSession"/> transaction (spec FR-005/FR-014). Owns the
/// identity map and dispatches to generated <see cref="IEntityBinding{TEntity}"/>s. v1 slice: tracked
/// inserts + primary-key load; change-tracking update/delete, concurrency, DSL queries and on-demand
/// link loading arrive in later US2/US3 slices.
/// </summary>
internal sealed class Session(IDbSession db) : ISession
{
    private readonly List<PreparedStatement> _pendingInserts = [];
    private readonly Dictionary<(System.Type, object), object> _identityMap = [];

    public ValueTask<TEntity> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // The INSERT (values read now via no-boxing accessors) is queued and executed at commit.
        _pendingInserts.Add(EntityBindings.Get<TEntity>().Insert(entity));
        return new ValueTask<TEntity>(entity);
    }

    public async ValueTask<TEntity?> GetAsync<TEntity>(object key, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (_identityMap.TryGetValue((typeof(TEntity), key), out var tracked))
        {
            return (TEntity)tracked;
        }

        var binding = EntityBindings.Get<TEntity>();
        await foreach (var entity in db.QueryAsync(binding.SelectByKey(key), binding.Materialize, cancellationToken)
                           .ConfigureAwait(false))
        {
            _identityMap[(typeof(TEntity), key)] = entity;
            return entity;
        }

        return null;
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        foreach (var insert in _pendingInserts)
        {
            await db.ExecuteAsync(insert, cancellationToken).ConfigureAwait(false);
        }

        _pendingInserts.Clear();
        await db.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        _pendingInserts.Clear();
        return db.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => db.DisposeAsync();

    // --- Deferred to later US2/US3 slices --------------------------------------------------------

    public void Remove<TEntity>(TEntity entity)
        where TEntity : class
        => throw new System.NotSupportedException("Delete is implemented in a later US2 slice.");

    public IAsyncEnumerable<TResult> QueryAsync<TResult>(CompiledQuery<TResult> query, CancellationToken cancellationToken = default)
        => throw new System.NotSupportedException("DSL query execution is implemented in US3.");

    public ValueTask<TResult?> QuerySingleOrDefaultAsync<TResult>(CompiledQuery<TResult> query, CancellationToken cancellationToken = default)
        => throw new System.NotSupportedException("DSL query execution is implemented in US3.");

    public ValueTask<Ref<TTarget>> LoadAsync<TTarget>(Ref<TTarget> reference, CancellationToken cancellationToken = default)
        where TTarget : class?
        => throw new System.NotSupportedException("On-demand reference loading is implemented in a later US2 slice.");

    public ValueTask<RefSet<TTarget>> LoadAsync<TTarget>(RefSet<TTarget> references, CancellationToken cancellationToken = default)
        where TTarget : class
        => throw new System.NotSupportedException("On-demand reference loading is implemented in a later US2 slice.");

    public ValueTask<RefList<TTarget>> LoadAsync<TTarget>(RefList<TTarget> list, CancellationToken cancellationToken = default)
        where TTarget : class
        => throw new System.NotSupportedException("On-demand reference loading is implemented in a later US2 slice.");

    public ValueTask<RefBag<TTarget>> LoadAsync<TTarget>(RefBag<TTarget> bag, CancellationToken cancellationToken = default)
        where TTarget : class
        => throw new System.NotSupportedException("On-demand reference loading is implemented in a later US2 slice.");

    public ValueTask<RefMap<TKey, TTarget>> LoadAsync<TKey, TTarget>(RefMap<TKey, TTarget> map, CancellationToken cancellationToken = default)
        where TKey : notnull
        where TTarget : class
        => throw new System.NotSupportedException("On-demand reference loading is implemented in a later US2 slice.");
}
