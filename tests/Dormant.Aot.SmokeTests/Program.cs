// Native-AOT smoke harness (US6, SC-001/SC-006; 005 US3/T039): exercises the public ORM surface — generated
// entities + bindings, schema apply, session CRUD, and generated DSL queries (entity + projection, required
// + optional params) — across BOTH providers, so PublishAot=true + full trimming analyze the whole stack
// (core + dialect framework + PostgreSQL + SQLite) and must report zero library-originated warnings. The
// PostgreSQL path is rooted but skips at runtime when no database is present; the SQLite path runs for real
// against an in-memory database (no Docker), proving the SQLite provider executes under Native AOT.
using System;
using Dormant.Aot.SmokeTests.Schema.Smoke;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.Sqlite;

const string connectionString =
    "Host=localhost;Port=5432;Database=smoke;Username=postgres;Password=postgres;Timeout=2;Command Timeout=2";

try
{
    await DormantPostgres.EnsureCreatedAsync(connectionString);

    await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
    await using var session = await factory.OpenSessionAsync();

    var id = Guid.NewGuid();
    await session.CreateItem(id, "smoke", 1, "{\"k\":1}", 0);
    await session.CommitAsync();

    var loaded = await session.GetAsync<Item>(id);
    Console.WriteLine(loaded?.Name);

    await foreach (var item in session.ItemsByName("smoke"))
    {
        Console.WriteLine(item.Name);
    }

    await foreach (var row in session.ItemNames(0))
    {
        Console.WriteLine(row.Name);
    }
}
catch (Exception ex)
{
    // No database in the AOT publish/run environment — the point is that the rooted code paths are
    // AOT-clean, not that they connect.
    Console.WriteLine(
        $"Dormant AOT smoke (PostgreSQL): surface rooted; runtime skipped ({ex.GetType().Name})."
    );
}

// SQLite (in-memory, no Docker): runs for real under Native AOT, so the SQLite provider's connection,
// schema apply, command/query, and materialization paths are exercised — not merely rooted (005 FR-006).
const string sqliteConnectionString = "Data Source=smoke_aot;Mode=Memory;Cache=Shared";
await using (var sqliteFactory = DormantSqlite.CreateSessionFactory(sqliteConnectionString))
{
    await DormantSqlite.EnsureCreatedAsync(sqliteConnectionString);

    var sqliteId = Guid.NewGuid();
    await using (var session = await sqliteFactory.OpenSessionAsync())
    {
        await session.CreateItem(sqliteId, "smoke-sqlite", 1, "{\"k\":1}", 0);
        await session.CommitAsync();
    }

    await using (var session = await sqliteFactory.OpenSessionAsync())
    {
        var loaded = await session.GetAsync<Item>(sqliteId);
        Console.WriteLine($"Dormant AOT smoke (SQLite): loaded '{loaded?.Name}'.");

        await foreach (var item in session.ItemsByName("smoke-sqlite"))
        {
            Console.WriteLine(item.Name);
        }
    }
}

return 0;
