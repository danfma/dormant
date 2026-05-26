namespace Dormant.Abstractions.Sessions;

/// <summary>Opens <see cref="ISession"/> instances against a configured provider.</summary>
public interface ISessionFactory : IAsyncDisposable
{
    /// <summary>Opens a new session (and its transaction scope).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open session.</returns>
    ValueTask<ISession> OpenSessionAsync(CancellationToken cancellationToken = default);
}
