using Dormant.Abstractions.Entities;
using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Sessions;

/// <summary>
/// Unit of work over a single transaction: owns the identity map and per-entity snapshots, tracks
/// changes, and persists only modified state on commit (spec FR-005/FR-014). A recognizable subset of
/// the NHibernate-style session model.
/// </summary>
public interface ISession : IAsyncDisposable
{
    /// <summary>Registers a new entity to be inserted on commit.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tracked entity.</returns>
    ValueTask<TEntity> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>Marks a tracked entity for deletion on commit.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>Loads a full entity by its primary key, tracking it in the identity map (spec FR-014).</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="key">The primary-key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity, or <see langword="null"/> when no row matches.</returns>
    ValueTask<TEntity?> GetAsync<TEntity>(object key, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>Executes a build-time-compiled query and streams its statically-known result type.</summary>
    /// <typeparam name="TResult">The result type (full entity or projection).</typeparam>
    /// <param name="query">The compiled query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of results.</returns>
    IAsyncEnumerable<TResult> QueryAsync<TResult>(
        CompiledQuery<TResult> query,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a query expecting at most one result.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="query">The compiled query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The single result, or <see langword="null"/> when none match.</returns>
    ValueTask<TResult?> QuerySingleOrDefaultAsync<TResult>(
        CompiledQuery<TResult> query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a build-time-compiled write command (authored DQL <c>insert</c>/<c>update</c>/<c>delete</c>)
    /// within the session transaction and returns its single result (spec FR-002/FR-005). Generated command
    /// methods on <c>{Module}Commands</c> call this.
    /// </summary>
    /// <typeparam name="TResult">The statically-known result type.</typeparam>
    /// <param name="command">The compiled command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command's result (e.g. the inserted row via <c>RETURNING</c>).</returns>
    ValueTask<TResult> ExecuteCommandAsync<TResult>(
        CompiledCommand<TResult> command,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an unloaded single reference on demand, returning a loaded one (spec FR-009).</summary>
    /// <typeparam name="TTarget">The related entity type.</typeparam>
    /// <param name="reference">The (typically unloaded) reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A loaded reference.</returns>
    ValueTask<Ref<TTarget>> LoadAsync<TTarget>(Ref<TTarget> reference, CancellationToken cancellationToken = default)
        where TTarget : class?;

    /// <summary>Loads an unloaded set on demand, returning a loaded one (spec FR-009).</summary>
    /// <typeparam name="TTarget">The related entity type.</typeparam>
    /// <param name="references">The (typically unloaded) set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A loaded set.</returns>
    ValueTask<RefSet<TTarget>> LoadAsync<TTarget>(RefSet<TTarget> references, CancellationToken cancellationToken = default)
        where TTarget : class;

    /// <summary>Loads an unloaded list on demand, returning a loaded one (spec FR-009).</summary>
    /// <typeparam name="TTarget">The related entity type.</typeparam>
    /// <param name="list">The (typically unloaded) list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A loaded list.</returns>
    ValueTask<RefList<TTarget>> LoadAsync<TTarget>(RefList<TTarget> list, CancellationToken cancellationToken = default)
        where TTarget : class;

    /// <summary>Loads an unloaded bag on demand, returning a loaded one (spec FR-009).</summary>
    /// <typeparam name="TTarget">The related entity type.</typeparam>
    /// <param name="bag">The (typically unloaded) bag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A loaded bag.</returns>
    ValueTask<RefBag<TTarget>> LoadAsync<TTarget>(RefBag<TTarget> bag, CancellationToken cancellationToken = default)
        where TTarget : class;

    /// <summary>Loads an unloaded map on demand, returning a loaded one (spec FR-009).</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TTarget">The related entity (value) type.</typeparam>
    /// <param name="map">The (typically unloaded) map.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A loaded map.</returns>
    ValueTask<RefMap<TKey, TTarget>> LoadAsync<TKey, TTarget>(RefMap<TKey, TTarget> map, CancellationToken cancellationToken = default)
        where TKey : notnull
        where TTarget : class;

    /// <summary>Commits the unit of work, writing only changed columns (spec FR-014).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when committed.</returns>
    /// <exception cref="ConcurrencyConflictException">An optimistic concurrency conflict occurred.</exception>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Discards pending changes and rolls back the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when rolled back.</returns>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}