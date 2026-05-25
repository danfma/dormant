using System;
using System.Collections.Generic;
using Dormant.Abstractions.Entities;
using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Querying;
using Dormant.Abstractions.Sessions;

namespace Dormant.Core.Persistence;

/// <summary>
/// Unit of work over a single <see cref="IDbSession"/> transaction (spec FR-005/FR-014/FR-015). Owns the
/// identity map and dispatches to generated <see cref="IEntityBinding"/>s with no reflection. Commit
/// flushes queued inserts, then change-tracking updates (changed columns only), then deletes; an
/// optimistic-concurrency token mismatch surfaces as <see cref="ConcurrencyConflictException"/> rather
/// than silently overwriting. DSL queries and on-demand link loading arrive in US3.
/// </summary>
internal sealed class Session(IDbSession db) : ISession
{
    private readonly List<(object Entity, IEntityBinding Binding, PreparedStatement Insert)> _added = [];
    private readonly Dictionary<(Type, object), Tracked> _tracked = [];
    private readonly List<(object Entity, IEntityBinding Binding)> _removed = [];
    private readonly HashSet<object> _removedSet = new(ReferenceEqualityComparer.Instance);

    public ValueTask<TEntity> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // The INSERT binds live values at execute time; queued and flushed at commit.
        var binding = EntityBindings.Get<TEntity>();
        _added.Add((entity, binding, binding.Insert(entity)));
        return new ValueTask<TEntity>(entity);
    }

    public async ValueTask<TEntity?> GetAsync<TEntity>(object key, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (_tracked.TryGetValue((typeof(TEntity), key), out var existing))
        {
            return (TEntity)existing.Entity;
        }

        var binding = EntityBindings.Get<TEntity>();
        await foreach (var entity in db.QueryAsync(binding.SelectByKey(key), binding.Materialize, cancellationToken)
                           .ConfigureAwait(false))
        {
            // Snapshot at load so commit can diff changed columns (FR-014).
            _tracked[(typeof(TEntity), key)] = new Tracked(entity, binding, binding.Snapshot(entity));
            return entity;
        }

        return null;
    }

    public void Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        _removed.Add((entity, EntityBindings.Get<TEntity>()));
        _removedSet.Add(entity);
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (_, _, insert) in _added)
        {
            await db.ExecuteAsync(insert, cancellationToken).ConfigureAwait(false);
        }

        foreach (var tracked in _tracked.Values)
        {
            if (_removedSet.Contains(tracked.Entity))
            {
                continue;
            }

            var update = tracked.Binding.Update(tracked.Entity, tracked.Snapshot);
            if (update is null)
            {
                continue;
            }

            var rows = await db.ExecuteAsync(update, cancellationToken).ConfigureAwait(false);
            if (tracked.Binding.TracksConcurrency && rows == 0)
            {
                throw new ConcurrencyConflictException();
            }

            // Refresh the snapshot so a later commit in this session diffs against the persisted state.
            tracked.Snapshot = tracked.Binding.Snapshot(tracked.Entity);
        }

        foreach (var (entity, binding) in _removed)
        {
            var rows = await db.ExecuteAsync(binding.Delete(entity), cancellationToken).ConfigureAwait(false);
            if (binding.TracksConcurrency && rows == 0)
            {
                throw new ConcurrencyConflictException();
            }
        }

        await db.CommitAsync(cancellationToken).ConfigureAwait(false);

        _added.Clear();
        _removed.Clear();
        _removedSet.Clear();
    }

    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        _added.Clear();
        _removed.Clear();
        _removedSet.Clear();
        return db.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => db.DisposeAsync();

    // A tracked entity plus the snapshot it is diffed against at commit (mutable: refreshed post-commit).
    private sealed class Tracked(object entity, IEntityBinding binding, object snapshot)
    {
        public object Entity { get; } = entity;

        public IEntityBinding Binding { get; } = binding;

        public object Snapshot { get; set; } = snapshot;
    }

    // --- Deferred to US3 -------------------------------------------------------------------------

    public IAsyncEnumerable<TResult> QueryAsync<TResult>(CompiledQuery<TResult> query, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DSL query execution is implemented in US3.");

    public ValueTask<TResult?> QuerySingleOrDefaultAsync<TResult>(CompiledQuery<TResult> query, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DSL query execution is implemented in US3.");

    public ValueTask<Ref<TTarget>> LoadAsync<TTarget>(Ref<TTarget> reference, CancellationToken cancellationToken = default)
        where TTarget : class?
        => throw new NotSupportedException("On-demand reference loading is implemented in a later US2/US3 slice.");

    public ValueTask<RefSet<TTarget>> LoadAsync<TTarget>(RefSet<TTarget> references, CancellationToken cancellationToken = default)
        where TTarget : class
        => throw new NotSupportedException("On-demand reference loading is implemented in a later US2/US3 slice.");

    public ValueTask<RefList<TTarget>> LoadAsync<TTarget>(RefList<TTarget> list, CancellationToken cancellationToken = default)
        where TTarget : class
        => throw new NotSupportedException("On-demand reference loading is implemented in a later US2/US3 slice.");

    public ValueTask<RefBag<TTarget>> LoadAsync<TTarget>(RefBag<TTarget> bag, CancellationToken cancellationToken = default)
        where TTarget : class
        => throw new NotSupportedException("On-demand reference loading is implemented in a later US2/US3 slice.");

    public ValueTask<RefMap<TKey, TTarget>> LoadAsync<TKey, TTarget>(RefMap<TKey, TTarget> map, CancellationToken cancellationToken = default)
        where TKey : notnull
        where TTarget : class
        => throw new NotSupportedException("On-demand reference loading is implemented in a later US2/US3 slice.");
}
