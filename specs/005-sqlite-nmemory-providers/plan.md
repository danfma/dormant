# Implementation Plan: SQLite Provider + Dialect Framework

**Branch**: `005-sqlite-nmemory-providers` | **Date**: 2026-05-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-sqlite-nmemory-providers/spec.md`

## Summary

Generalize Dormant's currently PostgreSQL-only SQL emission into a **multi-variant dialect
framework** (NHibernate-style) and add a second real relational provider, **SQLite**, primarily to
exercise the ORM across providers with a fast, Docker-free target.

The existing `SqlIr` (structured statement nodes) is already provider-neutral; the leak is
`SqlRenderer` — a single static renderer hard-wired to PostgreSQL syntax (`"`-quoting, `$n`
placeholders, `::jsonb` casts, `RETURNING`, PG type names, `now()`). The technical core of this
feature is: **(1)** turn `SqlRenderer` into a set of per-dialect renderers over the same IR; **(2)**
make the generator render **one SQL variant per target dialect at build time** and emit generated code
that **selects the variant by the session's dialect at runtime** (a branch over compile-time-constant
strings — no runtime SQL compilation, Constitution build-time-SQL rule preserved); **(3)** add the
`Dormant.Provider.Sqlite` adapter (data source, session, dialect identity, field reader / parameter
writer) that stays Native-AOT + full-trimming clean; **(4)** prove cross-provider parity with a single
parameterized conformance suite (the same authored DQL run against PostgreSQL via Testcontainers and
SQLite in-memory).

PostgreSQL stays the reference provider and its emitted SQL is byte-identical to today. NMemory is
**out of v1 scope** (deferred); v1 only shapes the execution boundary so a future non-SQL strategy can
plug in without core rework.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (generator targets `netstandard2.0` as a Roslyn incremental
source generator).

**Primary Dependencies**:
- Generator: Microsoft.CodeAnalysis (Roslyn incremental generators) — no new deps.
- PostgreSQL provider (existing): Npgsql (slim, AOT path).
- **SQLite provider (new)**: `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3`, with
  explicit `SQLitePCL.Batteries_V2.Init()` at provider init (mirrors the Npgsql-slim AOT discipline).

**Storage**: PostgreSQL (reference) + SQLite (file and `:memory:`). SQLite bundled `e_sqlite3` ships a
modern engine (≥ 3.44), so `RETURNING` (≥ 3.35) is available on both providers.

**Testing**: TUnit (source-generated, AOT-native, Microsoft.Testing.Platform). Real engines, never
mocks: PostgreSQL via Testcontainers (Docker), SQLite in-memory (no Docker). Generator tests use Verify
snapshots (`Verify.SourceGenerators`) + cacheability checks. New: a **parameterized cross-provider
conformance suite** (FR-007).

**Target Platform**: .NET 10 server/CLI (Linux/macOS/Windows). Native AOT + full trimming is a gate for
the core, the dialect framework, and the SQLite provider (FR-006). (iOS/dlopen constraints do not apply
to this feature's targets.)

**Project Type**: Managed .NET library (ORM) built on a Roslyn source generator + a provider/dialect
boundary. Feature-first layout, dependencies pointing inward (Abstractions ← Core ← provider adapters).

**Performance Goals**: Variant selection adds at most one branch over compile-time-constant SQL strings
per generated call — zero added allocation/boxing, no first-call warm-up. SQLite in-memory CRUD+query
round-trip completes in a fraction of the Testcontainers PostgreSQL time (SC-005).

**Constraints**:
- Build-time SQL only; no runtime query compilation on the hot path (Constitution).
- Zero library-originated AOT/trim warnings for core + dialect framework + SQLite (FR-006, SC-002).
- **0 core (Abstractions/Core runtime) changes are required to *add a dialect*** beyond the one-time
  framework seam (SC-003); adding a provider touches only new adapter packages + the generator's dialect
  set (SC-006). The DSL and the consumer-facing generated method signatures are unchanged (FR-008).

**Scale/Scope**: v1 = 2 SQL dialects (PostgreSQL + SQLite) over the core authored-DQL surface (schema
apply, queries incl. optional filters, insert/update/delete, `returning`, `with`-block). Not in scope:
advanced PG-only capabilities on SQLite (advanced JSON, spatial), a third-party provider SDK, NMemory.

### Resolved unknowns (detail in [research.md](./research.md))

All Technical-Context unknowns were resolved; **no `NEEDS CLARIFICATION` remain**. Highest-risk item:
SQLite Native-AOT cleanliness (Microsoft.Data.Sqlite has historical trim/AOT warnings) — mitigated via
`Microsoft.Data.Sqlite.Core` + `bundle_e_sqlite3` + explicit `Batteries_V2.Init()`, **gated by the AOT
smoke publish** extended to include SQLite (research D11).

## Constitution Check

*GATE: evaluated against Constitution v2.0.1. Re-checked after Phase 1 design — still passing.*

| Principle | Assessment | Verdict |
|-----------|-----------|---------|
| **I. DX First** | `DormantSqlite` entry point mirrors `DormantPostgres` (one-call factory, `EnsureCreatedAsync`). Unsupported-capability paths surface a clear, provider-named diagnostic/runtime error (FR-009). | PASS |
| **II. Interface & Compatibility Stability** | DSL unchanged. Consumer-facing **generated method signatures unchanged** — only method *bodies* change (variant-selecting `switch`) + 3 internal-shape additions (`DialectId` on the session, `IEntityBinding.CreateTableSql`/`SelectByKey` gain a dialect arg). These are within-MAJOR, additive to the binding surface; PostgreSQL's emitted SQL is byte-identical. Compatibility baseline (generated-code contract) updated in the same change. | PASS (baseline update required) |
| **III. Statically-Known Data Access** | Result types are independent of dialect; variant selection never changes the result type. No new runtime-typed paths. | PASS |
| **IV. First-Class Tooling** | AOT smoke gate extended to core+SQLite; conformance suite + generator snapshots run in CI via the single entry point. | PASS |
| **V. Performance by Default** | Variant selection = one branch over `const` strings; no reflection, no runtime codegen, no warm-up, no boxing. SQLite stays in the AOT gate (FR-006) — the explicit-init slim path keeps it warning-free; **verified by the smoke publish**. | PASS (AOT verified by gate) |
| **VI. Quality & Testing** | Parameterized real-engine conformance suite proves parity (no mocks); generator snapshot/cacheability tests updated for variants; AOT gate green before merge. | PASS |

**Provider boundary mandate**: Compatibility & Performance Standards explicitly require the provider
boundary to admit additional relational providers without breaking consumers — this feature *implements*
that mandate. No principle violation; **Complexity Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/005-sqlite-nmemory-providers/
├── plan.md              # This file
├── research.md          # Phase 0 — dialect/AOT/test decisions (D1–D12)
├── data-model.md        # Phase 1 — DialectId, IR, dialect renderer, type map, binding shape
├── quickstart.md        # Phase 1 — run authored DQL against SQLite in-memory
├── contracts/
│   ├── dialect-boundary.md   # The build-time renderer + runtime dialect-identity contract
│   └── provider-sqlite.md    # The SQLite adapter surface (entry point, session, IO)
├── checklists/          # (existing)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/
├── Dormant.Abstractions/
│   └── Providers/
│       ├── DialectId.cs            # NEW: closed enum { PostgreSql, Sqlite } — runtime variant key
│       ├── ISqlDialect.cs          # reshaped: gains DialectId Id; runtime quoting/placeholder for dynamic path
│       ├── IDbSession.cs           # gains DialectId Dialect { get; }
│       ├── IDataSource.cs          # (unchanged)
│       └── ... (IFieldReader/IParameterWriter already neutral)
│   └── Entities/IEntityBinding.cs  # CreateTableSql(DialectId) + SelectByKey(DialectId, key)
├── Dormant.Core/
│   ├── Persistence/Session.cs      # exposes Dialect; passes it to bindings (GetAsync)
│   └── Migrations/SchemaInitializer.cs  # dialect-aware CREATE SCHEMA (no-op on SQLite) + CreateTableSql(dialect)
├── Dormant.SourceGeneration/
│   ├── Ir/
│   │   ├── SqlIr.cs                # IR nodes kept neutral; ParamCast generalized to a neutral type tag
│   │   └── Dialects/               # NEW: per-dialect renderers over the IR
│   │       ├── ISqlDialectRenderer.cs
│   │       ├── PostgreSqlRenderer.cs   # current SqlRenderer behavior (byte-identical output)
│   │       ├── SqliteRenderer.cs       # ?-placeholders, schema-prefix tables, TEXT/INTEGER/REAL/BLOB, LIKE, no ::cast
│   │       └── DialectTypeMap.cs       # per-dialect DSL→SQL type maps (PG map moves here)
│   ├── Query/QueryEmitter.cs       # emit per-dialect variants + a session.Dialect switch (static + dynamic paths)
│   ├── Command/CommandEmitter.cs   # same: variant switch for insert/update/delete/returning/with
│   └── Schema/EntityBindingEmitter.cs  # emit CreateTableSql(DialectId) + SelectByKey(DialectId, key)
└── Dormant.Provider.Sqlite/        # NEW adapter package
    ├── DormantSqlite.cs            # entry point: CreateSessionFactory / CreateDataSource / EnsureCreatedAsync / Dialect
    ├── SqliteDataSource.cs         # opens SqliteSession; owns connection lifetime (incl. :memory: keep-alive)
    ├── SqliteSession.cs            # IDbSession over SqliteConnection+transaction; Dialect => DialectId.Sqlite
    ├── SqliteDialect.cs            # ISqlDialect: Id, QuoteIdentifier, Placeholder (runtime/dynamic path), Supports
    └── Io/
        ├── SqliteFieldReader.cs    # IFieldReader over SqliteDataReader
        └── SqliteParameterWriter.cs# IParameterWriter over SqliteParameterCollection (positional add-order)

tests/
├── Dormant.SourceGeneration.Tests/    # snapshots updated: now assert per-dialect variants + cacheability
├── Dormant.Providers.ConformanceTests/# NEW: parameterized [postgres|sqlite] suite; one authored schema/DQL source
│   └── schema/                        # shared catalog.dqls + catalog.dql (single source of truth, FR-007)
├── Dormant.Provider.PostgreSql.Tests/ # retained for PG-specific behavior (jsonb, etc.)
├── Dormant.Provider.Sqlite.Tests/     # NEW: SQLite-specific behavior (in-memory lifetime, affinity edge cases)
└── Dormant.Aot.SmokeTests/            # extended: publish includes Dormant.Provider.Sqlite (FR-006 gate)
```

**Structure Decision**: Keep the established feature-first layout and the inward dependency direction.
The dialect *renderers* live **inside the generator** (rendering is a build-time concern over the IR);
the runtime `ISqlDialect`/`DialectId` in `Abstractions` is reduced to a variant-selection *identity*
(plus the quote/placeholder helpers the runtime dynamic-filter path needs). The SQLite adapter is a new
sibling of `Dormant.Provider.PostgreSql`, depending only inward on Abstractions + Core — **zero changes
to the runtime core are required to add the dialect itself** beyond the one-time framework seam.

## Complexity Tracking

> No Constitution violations. The dialect layer is the constitutionally-mandated provider-boundary
> extension point, not added complexity. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
