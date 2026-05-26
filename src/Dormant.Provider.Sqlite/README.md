# Dormant.Provider.Sqlite

The SQLite provider adapter for Dormant. A real, embedded relational engine for exercising authored
DormantQL across providers — **no Docker, AOT-friendly** — and a shippable second provider beside the
PostgreSQL reference.

## Native AOT

The core, the dialect framework, and this provider stay **Native-AOT + full-trimming clean** (zero
library-originated warnings — the repository's AOT smoke publish is the gate). This is achieved with:

- `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3` (the statically-linked `e_sqlite3`
  native engine) — no reflective provider discovery.
- An explicit `SQLitePCL.Batteries_V2.Init()` at provider initialization (mirrors the Npgsql-slim
  discipline), so nothing relies on runtime reflection.

> A non-AOT provider (e.g. a future NMemory adapter) would ship as a separate opt-in package; its cost
> never leaks into the core or this provider.

## Usage

```csharp
using Dormant.Provider.Sqlite;

// File database.
const string connectionString = "Data Source=app.db";
await DormantSqlite.EnsureCreatedAsync(connectionString);
await using var factory = DormantSqlite.CreateSessionFactory(connectionString);
await using var session = await factory.OpenSessionAsync();
// ... authored DQL units run unchanged: session.CreateWidget(...), session.WidgetsByName(...), etc.
await session.CommitAsync();
```

### In-memory

A shared in-memory database lives only while a connection to it is open, so create the factory (its data
source holds a keep-alive connection) and apply the schema through the same connection string:

```csharp
const string connectionString = "Data Source=mydb;Mode=Memory;Cache=Shared";
await using var factory = DormantSqlite.CreateSessionFactory(connectionString); // keep-alive opens the store
await DormantSqlite.EnsureCreatedAsync(connectionString);                       // schema persists via the keep-alive
```

## Requirements

- .NET 10+.
- **SQLite 3.35+** (for `RETURNING`); `bundle_e_sqlite3` ships a newer build.

## Dialect differences vs PostgreSQL (same authored DQL, different SQL)

| Aspect | PostgreSQL | SQLite |
|--------|-----------|--------|
| Identifier / table | `"app"."widget"` | `"app_widget"` (schema folded into the name) |
| Placeholder | `$1, $2` | `@p1, @p2` (named; bound order-independently) |
| `CREATE SCHEMA` | emitted | skipped (no schema concept) |
| Column types | `text / jsonb / uuid / timestamptz / bigint / bytea` | `TEXT / TEXT / TEXT / TEXT / INTEGER / BLOB` |
| JSON parameter | `$1::jsonb` | `@p1` (JSON stored as TEXT) |
| Case-insensitive match | `ILIKE` | `LIKE` (ASCII NOCASE) |
| `RETURNING` | yes | yes (≥ 3.35) |

The authored DormantQL is identical across providers; the generator renders one SQL variant per dialect
at build time and generated code selects it by the session's `DialectId` at runtime (no runtime SQL
compilation).
