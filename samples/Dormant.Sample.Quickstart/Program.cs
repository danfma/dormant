// Quickstart sample (mirrors specs/001-orm-aot-sourcegen/quickstart.md). The schema in schema/app.dqls
// and queries in schema/app.dql are compiled by the DormantQL generator into partial types + ISession
// query extension methods in namespace Dormant.Sample.Quickstart.Schema.App (FR-046); the hand-written
// partial in UserExtensions.cs coexists (FR-003). Non-nullable members are `required` (FR-048).
//
// Default: runs the apply + CRUD + query round-trip on SQLite in-memory (no Docker). Set DORMANT_SAMPLE_DB
// to a PostgreSQL connection string to run the same authored DQL against PostgreSQL instead (005 US1).
using Dormant.Abstractions.Sessions;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.Sqlite;
using Dormant.Sample.Quickstart.Schema.App;

var connectionString = Environment.GetEnvironmentVariable("DORMANT_SAMPLE_DB");

if (connectionString is not null)
{
    Console.WriteLine("Provider: PostgreSQL (DORMANT_SAMPLE_DB).");
    await DormantPostgres.EnsureCreatedAsync(connectionString);
    await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
    await RoundTripAsync(factory);
}
else
{
    // The same authored DQL, run against SQLite in-memory — no Docker, no PostgreSQL.
    const string sqliteConnectionString = "Data Source=quickstart;Mode=Memory;Cache=Shared";
    Console.WriteLine("Provider: SQLite (in-memory). Set DORMANT_SAMPLE_DB for PostgreSQL.");
    await using var factory = DormantSqlite.CreateSessionFactory(sqliteConnectionString);
    await DormantSqlite.EnsureCreatedAsync(sqliteConnectionString);
    await RoundTripAsync(factory);
}

// Apply already done above; CRUD + query against whichever provider's factory.
static async Task RoundTripAsync(ISessionFactory factory)
{
    var id = Guid.NewGuid();
    await using (var session = await factory.OpenSessionAsync())
    {
        await session.CreateUser(id, "ada@example.com", DateTime.UtcNow, 1);
        await session.CommitAsync();
    }

    await using (var session = await factory.OpenSessionAsync())
    {
        var ada = await session.GetAsync<User>(id);
        Console.WriteLine($"loaded by key: {ada?.Email}");

        // Generated DSL query — an ISession extension method carrying build-time SQL.
        await foreach (var user in session.UsersByEmail("ada@example.com"))
        {
            Console.WriteLine($"query result: {user.Email}, recent? {user.IsRecent()}");
        }
    }
}
