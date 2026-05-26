namespace Dormant.Abstractions.Providers;

/// <summary>Driver port: opens provider sessions (connection + transaction scope).</summary>
public interface IDataSource : IAsyncDisposable
{
    /// <summary>Opens a new provider session.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open <see cref="IDbSession"/>.</returns>
    ValueTask<IDbSession> OpenAsync(CancellationToken cancellationToken = default);
}
