# Research: SQLite Provider + Dialect Framework

Phase 0 decision log. Each decision resolves a Technical-Context unknown. Format: Decision / Rationale /
Alternatives. Code seams reference the current tree (verified 2026-05-26).

## Current state (what is already neutral vs. PG-hardcoded)

- **Neutral**: `SqlIr` statement nodes (`src/Dormant.SourceGeneration/Ir/SqlIr.cs`), `IFieldReader` /
  `IParameterWriter` (positional add-order binding), `IDataSource`/`IDbSession`, `Session`/`SessionFactory`.
- **PG-hardcoded**: `SqlRenderer` (same file) — `"`-quoting (`Quote`), `$n` placeholders, `::cast`
  (`InsertColumn.ParamCast`), `RETURNING`, schema-qualified `schema.table`. `TypeMap.SqlMap`
  (`Emit/EmitHelpers.cs`) — PG type names. `QueryEmitter.EmitDynamicStatement` (optional filters) —
  builds SQL at runtime via `StringBuilder` with literal `$` + `\"`. `CommandEmitter.ValueToken` —
  `$n` + `::jsonb`, `ILIKE`, `now()`. `SchemaInitializer` — literal `CREATE SCHEMA IF NOT EXISTS "…"`.
- **Runtime SQL today**: the generator bakes **one** SQL string into `new PreparedStatement("""…""",
  binder)`; `Session` wraps `IDbSession` and has **no dialect awareness**.

## D1 — Dialect rendering is a build-time concern inside the generator

**Decision**: Replace the static `SqlRenderer` with an `ISqlDialectRenderer` abstraction
(`Ir/Dialects/`) implemented per dialect (`PostgreSqlRenderer`, `SqliteRenderer`) over the **unchanged
`SqlIr`**. The generator renders each statement **once per target dialect at build time**.
**Rationale**: Rendering produces the C# string literals baked into generated code — it is intrinsically
build-time. Roslyn generators run on `netstandard2.0` in the compiler and cannot reference the runtime
provider assemblies, so the renderers must live in the generator, not the provider packages.
**Alternatives**: (a) Render at runtime via the provider's `ISqlDialect` — rejected: violates the
build-time-SQL rule and adds hot-path cost. (b) A shared renderer library referenced by both generator
and runtime — rejected: unnecessary coupling; the runtime never renders full statements.

## D2 — Closed `DialectId` enum is the runtime variant key

**Decision**: Add `enum DialectId { PostgreSql, Sqlite }` to `Dormant.Abstractions.Providers`. The
generator emits all known variants; runtime selects by `DialectId`.
**Rationale**: A closed enum makes variant selection a cheap `switch` over compile-time-constant strings
(no dictionary, no allocation, no boxing — AOT-clean, Constitution V). Adding a SQL dialect later is one
enum case + one renderer + one adapter (no core rework — SC-003/SC-006).
**Alternatives**: open string keys + a `Dictionary<string,string>` in `PreparedStatement` — rejected:
per-call allocation + lookup on the hot path, weaker AOT story, and no compile-time exhaustiveness.

## D3 — Runtime variant selection: `session.Dialect` switch in generated code

**Decision**: Add `DialectId Dialect { get; }` to `IDbSession` (provider supplies it) and surface it on
the unit-of-work `ISession`. Generated extension-method bodies select the pre-rendered SQL before
building the statement:
```csharp
var sql = session.Dialect switch
{
    DialectId.PostgreSql => """INSERT INTO "catalog"."widget" (...) VALUES ($1, $2) RETURNING ...""",
    DialectId.Sqlite     => """INSERT INTO "catalog_widget" (...) VALUES (@p1, @p2) RETURNING ...""",
    _ => throw Unsupported(session.Dialect),
};
var statement = new PreparedStatement(sql, writer => { writer.Write(1, a); writer.Write(2, b); });
```
The **parameter binder is dialect-neutral**: the bind callback writes values by index once for all
dialects. Npgsql binds positionally (`$n`, by add order); Microsoft.Data.Sqlite binds **named `@pN`**
parameters the writer names from the same index (order-independent — chosen for reliable binding). The
placeholder *text* is the renderer's choice; the bind sequence serves every variant. The materializer is
likewise dialect-neutral.
**Rationale**: One branch over `const` strings — no runtime compilation, no warm-up. Consumer-facing
method *signatures* are unchanged (only bodies change), so Constitution II compatibility holds. The
PostgreSQL branch is byte-identical to today's output.
**Alternatives**: (a) carry variants in `PreparedStatement` + select in `Session` — rejected: pushes
selection past the generated code and needs a variant container (allocation). (b) emit a separate method
per dialect — rejected: changes the consumer-facing surface and multiplies the API.

## D4 — `IEntityBinding` becomes dialect-aware

**Decision**: `IEntityBinding.CreateTableSql` → `string CreateTableSql(DialectId)`;
`IEntityBinding<T>.SelectByKey(object key)` → `SelectByKey(DialectId dialect, object key)`.
`Session.GetAsync` passes `db.Dialect`; `SchemaInitializer` passes the session's dialect.
**Rationale**: Bindings carry per-entity DDL + the SELECT-by-key statement, both of which now vary by
dialect. The generated binding holds all variants and returns the requested one.
**Alternatives**: a property returning all variants — rejected: same allocation concern as D2; a
parameterized method keeps it a `switch` over `const`.

## D5 — SQLite schema/namespace model: table-name prefix

**Decision**: SQLite has no PostgreSQL-style schemas. The `SqliteRenderer` renders a schema-qualified
table `("app","widget")` as a single quoted identifier `"app_widget"` (join with `_`), and
`CREATE SCHEMA` becomes a **no-op** on SQLite (`SchemaInitializer` skips it for `DialectId.Sqlite`).
PostgreSQL keeps `"app"."widget"` + real `CREATE SCHEMA`.
**Rationale**: Preserves logical table identity and parity for the authored-DQL surface with zero extra
moving parts in `:memory:` tests. Differing identifiers across dialects is exactly what dialects are for.
**Alternatives**: `ATTACH DATABASE ':memory:' AS app` per schema — rejected for v1: complicates
in-memory lifetime + shared-cache and adds little for the core surface. Recorded as a future option if
true per-schema isolation is needed.

## D6 — Per-dialect type mapping (DSL → SQL column type)

**Decision**: Move `TypeMap.SqlMap` into the per-dialect renderers (`DialectTypeMap`). SQLite uses
affinities: `string/json/uuid/datetime/date/duration/decimal/bigint → TEXT`, `bool/int16/int32/int64/
long → INTEGER`, `double/float32/float64 → REAL`, `bytes → BLOB`. PostgreSQL map is unchanged
(`text/jsonb/uuid/timestamptz/bigint/numeric/bytea/...`).
**Rationale**: Column types are dialect-specific; SQLite's affinity system stores JSON/UUID/temporal as
TEXT (the SQLite-idiomatic choice; JSON1 operates on TEXT).
**Alternatives**: store UUID as BLOB(16) — rejected for v1: TEXT keeps round-trip + equality parity with
PG's textual UUID simplest. Noted as an optimization.

## D7 — `RETURNING` is supported on both

**Decision**: Keep `RETURNING` for both dialects. `bundle_e_sqlite3` ships SQLite ≥ 3.44; `RETURNING`
exists since 3.35 (2021).
**Rationale**: No divergence needed — insert/update/delete `returning` and the `with`-block flow work
identically. **Constraint**: minimum SQLite 3.35 documented for the provider.

## D8 — JSON casts: generalize `InsertColumn.ParamCast` to a neutral type tag

**Decision**: Replace the PG-literal `ParamCast` (e.g. `"jsonb"`) on the IR with a **neutral type tag**
(the DSL type / a `needs-json` flag). Each renderer maps it: PostgreSQL → `$n::jsonb`; SQLite → plain
placeholder (JSON stored as TEXT).
**Rationale**: Removes the last PG-ism from the IR; keeps the IR dialect-neutral so a future non-SQL
strategy can consume it (SC-004).
**Alternatives**: keep `ParamCast` literal + strip it in the SQLite renderer — rejected: leaves a
PG-specific value in the neutral IR.

## D9 — `ILIKE` → `LIKE` on SQLite

**Decision**: Carry the operator neutrally in the IR; `SqliteRenderer` maps case-insensitive match to
`LIKE` (ASCII case-insensitive by default; `COLLATE NOCASE` where parity needs it). PG keeps `ILIKE`.
**Rationale**: Covers the core authored surface. **Caveat documented**: SQLite `LIKE`/`NOCASE` is
ASCII-only case-folding (no full Unicode) — out of v1 parity scope.

## D10 — Native functions per dialect (`now()`)

**Decision**: Map native functions per dialect via the existing `INativeFunctionCatalog` seam: PG
`now()` → SQLite `CURRENT_TIMESTAMP` (or `datetime('now')`).
**Rationale**: The native-function abstraction already exists; per-dialect mapping is the natural home.

## D11 — SQLite client + AOT (HIGHEST RISK)

**Decision**: Reference `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3` (static native
`e_sqlite3`), and call `SQLitePCL.Batteries_V2.Init()` explicitly at provider initialization — avoiding
the reflection-based `DbProviderFactory` auto-registration. This mirrors the Npgsql **slim** discipline
used by the PostgreSQL provider (`NpgsqlSlimDataSourceBuilder`). The **AOT smoke publish is extended to
include `Dormant.Provider.Sqlite`** and is the gate proving zero library-originated AOT/trim warnings
(FR-006, SC-002).
**Rationale**: `Microsoft.Data.Sqlite` is known to emit trim/AOT warnings on the default reflective path
([dotnet/efcore#29725](https://github.com/dotnet/efcore/issues/29725)); the `.Core` + explicit-bundle +
explicit-init path removes the dynamic-discovery surface. Our targets are server/CLI Native AOT, so the
iOS `dlopen` constraint does not apply.
**Fallback (if the gate still warns)**: pin/suppress specific warnings with a documented justification,
or evaluate `SQLitePCLRaw.bundle_green`, or a thin P/Invoke layer over `e_sqlite3` (last resort).
**Verification**: the smoke publish must report **0** warnings before this feature is "done"
(Constitution VI — skipped/failing verification is not done).
**Sources**: [dotnet/efcore#29725](https://github.com/dotnet/efcore/issues/29725),
[Microsoft Learn — Custom SQLite versions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions),
[NuGet — Microsoft.Data.Sqlite](https://www.nuget.org/packages/microsoft.data.sqlite/),
[NuGet — SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/sqlitepclraw.bundle_e_sqlite3/).

## D12 — Cross-provider parity via one parameterized conformance suite

**Decision**: A new `tests/Dormant.Providers.ConformanceTests` holds the authored schema + DQL **once**
(`schema/catalog.dqls` + `catalog.dql`) and runs each behavior parameterized over the provider (TUnit
`[Arguments("postgres")]` / `[Arguments("sqlite")]`): PostgreSQL opens a session against an ephemeral
Testcontainers database; SQLite opens an in-memory session (no Docker). Shared assertions prove parity
(SC-001). Provider-*specific* behavior (PG `jsonb`, SQLite in-memory lifetime / affinity edges) stays in
the per-provider test projects.
**Rationale**: One source of truth, run twice, is the cheapest credible parity proof and matches FR-007.
**In-memory lifetime**: each SQLite test case gets a **fresh** connection/store (a unique in-memory name
or a held-open connection) so cases don't bleed (Edge Case in spec).
**Alternatives**: duplicate test bodies per provider — rejected: drift risk, violates "one source".

## Open risks carried into tasks

1. **AOT cleanliness of Microsoft.Data.Sqlite** (D11) — gated by the smoke publish; fallback documented.
2. **Dynamic-filter path** (`QueryEmitter.EmitDynamicStatement`) must become dialect-aware without
   runtime compilation: emit the `StringBuilder` assembly under a `session.Dialect` branch (dialect tokens
   = placeholder form + quote char + table identifier), or render the dynamic block per dialect. Placeholder
   shape differs (`$` + index vs. `@p` + index) — the branch selects the right fragment builder.
3. **Generator snapshot churn** — `Dormant.SourceGeneration.Tests` assertions/snapshots now contain
   per-dialect variants; cacheability checks must still pass (variants are deterministic).
