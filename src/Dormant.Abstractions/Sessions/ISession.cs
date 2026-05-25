using Dormant.Abstractions.Links;
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

    /// <summary>Loads an unloaded single link on demand, returning a loaded link (spec FR-009).</summary>
    /// <typeparam name="TTarget">The related entity type.</typeparam>
    /// <param name="link">The (typically unloaded) link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A loaded link.</returns>
    ValueTask<Link<TTarget>> LoadAsync<TTarget>(Link<TTarget> link, CancellationToken cancellationToken = default)
        where TTarget : class;

    /// <summary>Loads an unloaded multi link on demand, returning a loaded link set (spec FR-009).</summary>
    /// <typeparam name="TTarget">The related entity type.</typeparam>
    /// <param name="link">The (typically unloaded) link set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A loaded link set.</returns>
    ValueTask<LinkSet<TTarget>> LoadAsync<TTarget>(LinkSet<TTarget> link, CancellationToken cancellationToken = default)
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