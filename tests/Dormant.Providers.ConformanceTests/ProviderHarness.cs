using Dormant.Abstractions.Sessions;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.Sqlite;
using Testcontainers.PostgreSql;

namespace Dormant.Providers.ConformanceTests;

/// <summary>
/// A provider under test, set up from one authored source (FR-007): PostgreSQL runs against an ephemeral
/// Testcontainers database; SQLite runs against a fresh shared in-memory store (no Docker). Either way the
/// schema is applied and an <see cref="ISessionFactory"/> with the generated units is returned. Each
/// instance is an isolated store; dispose tears it down.
/// </summary>
internal sealed class ProviderHarness : IAsyncDisposable
{
    private readonly PostgreSqlContainer? _container;

    private ProviderHarness(ISessionFactory factory, PostgreSqlContainer? container)
    {
        Factory = factory;
        _container = container;
    }

    public ISessionFactory Factory { get; }

    public static async Task<ProviderHarness> CreateAsync(string provider)
    {
        switch (provider)
        {
            case "postgres":
            {
                var container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
                await container.StartAsync();
                var connectionString = container.GetConnectionString();
                await DormantPostgres.EnsureCreatedAsync(connectionString);
                return new ProviderHarness(DormantPostgres.CreateSessionFactory(connectionString), container);
            }

            case "sqlite":
            {
                // A unique shared in-memory database; the factory's data source keeps it alive while the
                // schema is applied (via a sibling connection on the shared cache) and the sessions run.
                var connectionString = $"Data Source=conf_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
                var factory = DormantSqlite.CreateSessionFactory(connectionString);
                await DormantSqlite.EnsureCreatedAsync(connectionString);
                return new ProviderHarness(factory, container: null);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
