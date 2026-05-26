using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Sessions;

/// <summary>
/// A thin unit of work (002 fork): a transaction boundary + a read identity map + an executor for authored
/// DQL queries and commands. It does **not** track changes, hold snapshots, or auto-persist a mutable graph
/// — writes are explicit authored commands (FR-002/FR-003/FR-010). Generated query/command methods are C# 14
/// extension members on this interface.
/// </summary>
public interface ISession : IAsyncDisposable
{
    /// <summary>Loads an immutable entity by primary key; one instance per key within the session (FR-010).</summary>
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
    /// <returns>An async stream of immutable results.</returns>
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
    /// within the session transaction and returns its single result (FR-002/FR-005). Generated command
    /// methods on <c>{Module}Commands</c> call this.
    /// </summary>
    /// <typeparam name="TResult">The statically-known result type.</typeparam>
    /// <param name="command">The compiled command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command's result (e.g. the inserted row via <c>RETURNING</c>).</returns>
    ValueTask<TResult> ExecuteCommandAsync<TResult>(
        CompiledCommand<TResult> command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a write command that returns an affected-row count (authored <c>update</c>/<c>delete</c>)
    /// within the session transaction (FR-002/FR-011). A zero count on a concurrency-token-matched command
    /// signals a conflict to the caller.
    /// </summary>
    /// <param name="statement">The prebuilt, parameter-bound statement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    ValueTask<int> ExecuteWriteAsync(
        PreparedStatement statement,
        CancellationToken cancellationToken = default);

    /// <summary>Commits the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when committed.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when rolled back.</returns>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
