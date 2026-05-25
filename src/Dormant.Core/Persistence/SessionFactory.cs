using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Sessions;

namespace Dormant.Core.Persistence;

/// <summary>Opens <see cref="ISession"/> instances (each over a fresh provider transaction).</summary>
public sealed class SessionFactory(IDataSource dataSource) : ISessionFactory
{
    /// <inheritdoc/>
    public async ValueTask<ISession> OpenSessionAsync(CancellationToken cancellationToken = default)
    {
        var dbSession = await dataSource.OpenAsync(cancellationToken).ConfigureAwait(false);
        await dbSession.BeginAsync(cancellationToken).ConfigureAwait(false);
        return new Session(dbSession);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
