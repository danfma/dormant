# Quickstart: Run authored DQL against SQLite (no Docker)

This shows the SQLite provider exercising the same authored DQL the PostgreSQL provider runs — the MVP
of feature 005 (User Story 1). It assumes the same schema/units already used by the quickstart sample.

## 1. Reference the provider

```xml
<ProjectReference Include="src/Dormant.Provider.Sqlite/Dormant.Provider.Sqlite.csproj" />
<!-- or, once published: <PackageReference Include="Dormant.Provider.Sqlite" /> -->
```

## 2. Apply the schema + run units against an in-memory database

```csharp
using Dormant.Provider.Sqlite;

// In-memory, kept alive for the lifetime of the factory's sessions.
const string connectionString = "Data Source=quickstart;Mode=Memory;Cache=Shared";

await DormantSqlite.EnsureCreatedAsync(connectionString);          // CREATE TABLE ... (no CREATE SCHEMA on SQLite)

var factory = DormantSqlite.CreateSessionFactory(connectionString);
await using var session = await factory.OpenAsync();

// Authored insert ... returning (same DQL unit as PostgreSQL; the SQLite SQL variant is selected by session.Dialect).
var id = await session.CreateWidget(name: "bolt", quantity: 12);

// Authored query.
await foreach (var w in session.FindWidgetsByName(name: "bolt"))
    Console.WriteLine($"{w.Id}: {w.Name} x{w.Quantity}");

// with-block (parent → child FK flow) executes each binding as its own statement in the transaction.
await session.CreateAuthorWithArticle(authorName: "ada", title: "notes");

await session.CommitAsync();
```

Nothing about the authored DQL changed; `session.Dialect == DialectId.Sqlite` makes the generated
methods pick the SQLite SQL variant rendered at build time.

## 3. Same code, PostgreSQL (for contrast / parity)

```csharp
using Dormant.Provider.PostgreSql;

var factory = DormantPostgres.CreateSessionFactory(pgConnectionString);
// identical session.CreateWidget / FindWidgetsByName / CreateAuthorWithArticle calls
```

## 4. Prove parity (the conformance suite)

`tests/Dormant.Providers.ConformanceTests` holds the schema + units once and runs each behavior twice:

```csharp
[Test]
[Arguments("postgres")]
[Arguments("sqlite")]
public async Task Insert_then_query_round_trips(string provider)
{
    await using var session = await OpenSession(provider);   // postgres → Testcontainers, sqlite → :memory:
    var id = await session.CreateWidget(name: "bolt", quantity: 12);
    var found = await session.GetWidget(id);
    await Assert.That(found!.Name).IsEqualTo("bolt");
}
```

- `sqlite` cases need **no Docker** and finish in a fraction of the PostgreSQL time (SC-005).
- Each SQLite case gets a **fresh** in-memory store (unique name / held-open connection).

## 5. Prove AOT integrity (the gate)

```bash
dotnet publish tests/Dormant.Aot.SmokeTests -c Release -r <rid> \
  -p:PublishAot=true -p:TrimMode=full
# MUST report 0 library-originated AOT/trim warnings (core + PostgreSQL + SQLite) — FR-006 / SC-002.
```

The smoke project now references `Dormant.Provider.Sqlite` and calls a SQLite round-trip, so the publish
exercises the SQLite path under Native AOT. A non-zero warning count blocks the feature (research D11).

## Dialect differences you'll observe (same DQL, different SQL)

| Aspect | PostgreSQL | SQLite |
|--------|-----------|--------|
| Identifier / table | `"app"."widget"` | `"app_widget"` (schema folded into a prefix) |
| Placeholder | `$1, $2` | `?, ?` |
| `CREATE SCHEMA` | emitted | skipped (no-op) |
| Column types | `text / jsonb / uuid / timestamptz / bigint / bytea` | `TEXT / TEXT / TEXT / TEXT / INTEGER / BLOB` |
| JSON param | `$1::jsonb` | `?` (JSON stored as TEXT) |
| Case-insensitive match | `ILIKE` | `LIKE` (ASCII NOCASE) |
| `RETURNING` | yes | yes (SQLite ≥ 3.35) |
