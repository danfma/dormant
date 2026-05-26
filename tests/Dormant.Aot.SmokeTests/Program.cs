// Native-AOT smoke harness (US6, SC-001/SC-006): exercises the public ORM surface — generated entities
// + bindings, schema apply, session CRUD, and generated DSL queries (entity + projection, required +
// optional params) — so PublishAot=true + full trimming analyze the whole stack and must report zero
// library-originated warnings. A live database is not required to root the AOT graph; the runtime call
// is guarded so the published binary exits cleanly when no database is present.
using System;
using Dormant.Aot.SmokeTests.Schema.Smoke;
using Dormant.Provider.PostgreSql;

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
    Console.WriteLine($"Dormant AOT smoke: surface rooted; runtime skipped ({ex.GetType().Name}).");
}

return 0;
