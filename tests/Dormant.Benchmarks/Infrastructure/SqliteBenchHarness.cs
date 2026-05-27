using Dormant.Abstractions.Sessions;
using Dormant.Benchmarks.Model;
using Dormant.Benchmarks.Schema.Bench;
using Dormant.Provider.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Dormant.Benchmarks.Infrastructure;

/// <summary>The libraries under comparison; also indexes the per-library scratch-key arrays.</summary>
public enum OrmKind
{
    Dormant = 0,
    Dapper = 1,
    EfCore = 2,
}

/// <summary>
/// Owns one shared in-memory SQLite database for a benchmark class and seeds it identically for every
/// library (FR-002). Dormant's generator owns the DDL: <see cref="DormantSqlite.EnsureCreatedAsync(string,System.Threading.CancellationToken)"/>
/// creates <c>bench_product</c>, and the kept-alive session factory holds the shared-cache database alive
/// for the suite's lifetime. Dapper/EF/Insight bind to the same table through their own connections.
/// </summary>
/// <remarks>
/// Each benchmark class builds its own harness in <c>[GlobalSetup]</c>, so reads share seeded data and
/// writes stay isolated to per-library scratch keys (FR-007). The seed RNG is fixed, so the dataset is
/// deterministic (SC-003).
/// </remarks>
public sealed class SqliteBenchHarness : IAsyncDisposable
{
    public const int SeedCount = 1000;
    public const int CategoryCount = 10;

    private readonly ISessionFactory _dormantFactory;

    private SqliteBenchHarness(
        string connectionString,
        ISessionFactory dormantFactory,
        DbContextOptions<BenchDbContext> efOptions,
        Guid readKey,
        string readCategory,
        Guid[] updateKeys,
        Guid[] deleteKeys
    )
    {
        ConnectionString = connectionString;
        _dormantFactory = dormantFactory;
        EfOptions = efOptions;
        ReadKey = readKey;
        ReadCategory = readCategory;
        UpdateKeys = updateKeys;
        DeleteKeys = deleteKeys;
    }

    /// <summary>The shared-cache in-memory connection string every library connects to.</summary>
    public string ConnectionString { get; }

    /// <summary>EF Core options pointing at the shared database (fresh context per operation).</summary>
    public DbContextOptions<BenchDbContext> EfOptions { get; }

    /// <summary>A seeded primary key used by the read-by-key benchmarks.</summary>
    public Guid ReadKey { get; }

    /// <summary>A category present in the seed, used by the filtered-read benchmarks.</summary>
    public string ReadCategory { get; }

    /// <summary>Pre-inserted scratch rows updated by the update benchmarks, indexed by <see cref="OrmKind"/>.</summary>
    public Guid[] UpdateKeys { get; }

    /// <summary>Reserved keys for the delete benchmarks (rows are (re)created per iteration), indexed by <see cref="OrmKind"/>.</summary>
    public Guid[] DeleteKeys { get; }

    /// <summary>Opens a Dormant session (its per-operation unit of work).</summary>
    public ValueTask<ISession> OpenDormantSessionAsync() => _dormantFactory.OpenSessionAsync();

    /// <summary>Opens a fresh pooled SQLite connection (for Dapper and Insight).</summary>
    public async ValueTask<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return connection;
    }

    /// <summary>Creates a fresh EF Core context bound to the shared database.</summary>
    public BenchDbContext NewEfContext() => new(EfOptions);

    public static async ValueTask<SqliteBenchHarness> CreateAsync()
    {
        DapperSqliteSetup.EnsureRegistered();

        var connectionString = $"Data Source=bench_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        // Create the factory first so its keep-alive connection holds the in-memory database alive while the
        // schema is applied and across every session the benchmarks open (mirrors the conformance harness).
        var dormantFactory = DormantSqlite.CreateSessionFactory(connectionString);
        await DormantSqlite.EnsureCreatedAsync(connectionString).ConfigureAwait(false);

        var efOptions = new DbContextOptionsBuilder<BenchDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var categories = new string[CategoryCount];
        for (var c = 0; c < CategoryCount; c++)
        {
            categories[c] = $"cat{c}";
        }

        var rng = new Random(20260526);
        var updateKeys = new Guid[3];
        var deleteKeys = new Guid[3];
        for (var i = 0; i < updateKeys.Length; i++)
        {
            updateKeys[i] = Guid.NewGuid();
            deleteKeys[i] = Guid.NewGuid();
        }

        Guid readKey;
        await using (var session = await dormantFactory.OpenSessionAsync().ConfigureAwait(false))
        {
            readKey = Guid.NewGuid();
            await session
                .CreateProduct(readKey, "seed-0", categories[0], 9.99m, 1)
                .ConfigureAwait(false);

            for (var i = 1; i < SeedCount; i++)
            {
                await session
                    .CreateProduct(
                        Guid.NewGuid(),
                        $"seed-{i}",
                        categories[rng.Next(CategoryCount)],
                        Math.Round((decimal)(rng.NextDouble() * 1000), 2),
                        rng.Next(1, 1000)
                    )
                    .ConfigureAwait(false);
            }

            // Pre-insert the per-library update scratch rows (category outside the read sample to avoid
            // perturbing filtered-read counts).
            foreach (var key in updateKeys)
            {
                await session
                    .CreateProduct(key, "update-scratch", "scratch", 1m, 1)
                    .ConfigureAwait(false);
            }

            await session.CommitAsync().ConfigureAwait(false);
        }

        return new SqliteBenchHarness(
            connectionString,
            dormantFactory,
            efOptions,
            readKey,
            categories[0],
            updateKeys,
            deleteKeys
        );
    }

    public ValueTask DisposeAsync() => _dormantFactory.DisposeAsync();
}
