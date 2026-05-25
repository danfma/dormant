using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Ports;

/// <summary>Driver port: a connection bound to a transaction, executing prebuilt statements.</summary>
public interface IDbSession : IAsyncDisposable
{
    /// <summary>Begins the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the transaction has begun.</returns>
    ValueTask BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when committed.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when rolled back.</returns>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes a query and streams materialized rows without boxing.</summary>
    /// <typeparam name="TRow">The row type.</typeparam>
    /// <param name="statement">The prebuilt, parameterized statement.</param>
    /// <param name="materialize">The generated row materializer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of materialized rows.</returns>
    IAsyncEnumerable<TRow> QueryAsync<TRow>(
        PreparedStatement statement,
        RowMaterializer<TRow> materialize,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a non-query statement (insert/update/delete) and returns affected rows.</summary>
    /// <param name="statement">The prebuilt, parameterized statement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    ValueTask<int> ExecuteAsync(PreparedStatement statement, CancellationToken cancellationToken = default);
}