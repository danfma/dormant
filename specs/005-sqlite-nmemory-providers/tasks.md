---
description: "Task list for SQLite Provider + Dialect Framework"
---

# Tasks: SQLite Provider + Dialect Framework

**Input**: Design documents from `specs/005-sqlite-nmemory-providers/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D12), data-model.md, contracts/

**Tests**: INCLUDED — the spec mandates them (FR-007 parameterized conformance suite, SC-001/SC-002),
and the Constitution (VI) requires real-engine verification + generator snapshot/cacheability checks +
the AOT gate. No mocks.

**Organization**: Tasks grouped by user story. The **dialect framework seam** (US2's abstraction) is a
blocking prerequisite for the SQLite provider (US1) and the AOT gate (US3), so it lives in Phase 2
(Foundational); the per-story phases then add SQLite, verify the boundary, and prove AOT.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 / US2 / US3 (story phases only)

## Path Conventions

Repo root: `src/` (libraries), `tests/` (test projects), `samples/`. Generator =
`src/Dormant.SourceGeneration` (netstandard2.0). Runtime core = `src/Dormant.Abstractions` +
`src/Dormant.Core`. New adapter = `src/Dormant.Provider.Sqlite`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the SQLite adapter project skeleton and wire it into the build.

- [X] T001 Create `src/Dormant.Provider.Sqlite/Dormant.Provider.Sqlite.csproj` (net10.0; `ProjectReference` to `../Dormant.Abstractions` + `../Dormant.Core`; `PackageReference` `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3`)
- [X] T002 Add `Dormant.Provider.Sqlite` to `Dormant.slnx`
- [X] T003 [P] Pin `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3` versions in `Directory.Packages.props` (Central Package Management — PG provider already uses versionless refs)

---

## Phase 2: Foundational (Blocking Prerequisites — the dialect framework seam)

**Purpose**: Generalize the PostgreSQL-only emission into a multi-dialect framework over the existing
neutral `SqlIr`, with PostgreSQL re-expressed as a dialect whose **emitted SQL stays byte-identical**.
This is the riskiest core work and blocks every story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete and PostgreSQL is green.

### Runtime contract (Dormant.Abstractions / Dormant.Core)

- [X] T004 [P] Add `enum DialectId { PostgreSql, Sqlite }` in `src/Dormant.Abstractions/Providers/DialectId.cs`
- [X] T005 [P] Reshape `ISqlDialect`: add `DialectId Id { get; }` in `src/Dormant.Abstractions/Providers/ISqlDialect.cs`
- [X] T006 [P] Add `DialectId Dialect { get; }` to `IDbSession` in `src/Dormant.Abstractions/Providers/IDbSession.cs`
- [X] T007 [P] Add `DialectId Dialect { get; }` to `ISession` in `src/Dormant.Abstractions/Sessions/ISession.cs`
- [X] T008 [P] Reshape `IEntityBinding`: `string CreateTableSql(DialectId)` + `IEntityBinding<T>.SelectByKey(DialectId, object)` in `src/Dormant.Abstractions/Entities/IEntityBinding.cs`

### Build-time renderer abstraction (Dormant.SourceGeneration)

- [X] T009 Create `ISqlDialectRenderer` (`DialectId Id`; `string Render(SqlStatement)`) in `src/Dormant.SourceGeneration/Ir/Dialects/ISqlDialectRenderer.cs`
- [X] T010 Extract the current `SqlRenderer` body verbatim into `PostgreSqlRenderer : ISqlDialectRenderer` in `src/Dormant.SourceGeneration/Ir/Dialects/PostgreSqlRenderer.cs` (output MUST be byte-identical)
- [X] T011 Create `DialectTypeMap` and move the PostgreSQL `SqlMap` out of `TypeMap` (`src/Dormant.SourceGeneration/Ir/Dialects/DialectTypeMap.cs` + edit `src/Dormant.SourceGeneration/Emit/EmitHelpers.cs`)
- [X] T012 Generalize the IR: `InsertColumn.ParamCast` → a neutral type tag; let the renderer resolve `ColumnDef.SqlType` per dialect, in `src/Dormant.SourceGeneration/Ir/SqlIr.cs` (remove the static `SqlRenderer`)
- [X] T013 Add a renderer registry to the generator that drives per-dialect emission; register `PostgreSqlRenderer` only for now (Sqlite added in US1) — in `src/Dormant.SourceGeneration/Ir/Dialects/DialectRenderers.cs`

### Per-dialect emission (generated code now selects by `session.Dialect`)

- [X] T014 Update `QueryEmitter` static path to emit `var sql = session.Dialect switch { … };` over each registered renderer's output in `src/Dormant.SourceGeneration/Query/QueryEmitter.cs`
- [X] T015 Update `QueryEmitter` dynamic-filter path (`EmitDynamicStatement`) to branch placeholder/quote/table tokens by `session.Dialect` — no runtime SQL compilation — same file (depends on T014)
- [X] T016 Update `CommandEmitter` to emit the dialect switch for insert/update/delete/`returning`/`with`, routing `ValueToken`/casts/`ILIKE`/native funcs through the renderer, in `src/Dormant.SourceGeneration/Command/CommandEmitter.cs`
- [X] T017 Update `EntityBindingEmitter` to emit `CreateTableSql(DialectId)` + `SelectByKey(DialectId, key)` switches in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`

### Runtime plumbing

- [X] T018 Update `Session` to expose `Dialect` (from `IDbSession`) and pass it to `binding.SelectByKey` in `GetAsync` — `src/Dormant.Core/Persistence/Session.cs`
- [X] T019 Update `SchemaInitializer` to skip `CREATE SCHEMA` for `DialectId.Sqlite` and call `binding.CreateTableSql(db.Dialect)` — `src/Dormant.Core/Migrations/SchemaInitializer.cs`
- [X] T020 Update PostgreSQL provider for the reshaped contract: `PostgreSqlDialect.Id => DialectId.PostgreSql`; `PostgreSqlSession.Dialect => DialectId.PostgreSql` — `src/Dormant.Provider.PostgreSql/PostgreSqlDialect.cs` + `PostgreSqlSession.cs`

### Foundational verification (PostgreSQL must stay green)

- [X] T021 Update generator snapshot/assertion tests for the new switch shape; assert the PostgreSQL variant is byte-identical and cacheability still holds, in `tests/Dormant.SourceGeneration.Tests/*` (CommandEmitTests, ProjectionEmitTests, SchemaEmitTests, *CacheabilityTests)
- [X] T022 Run `tests/Dormant.Provider.PostgreSql.Tests` (Testcontainers) — all green (regression guard: PG behavior unchanged)

**Checkpoint**: PostgreSQL is re-expressed as a dialect over the framework, output byte-identical, all
tests green. The boundary exists; SQLite can now plug in additively.

---

## Phase 3: User Story 1 — Run the ORM against SQLite without Docker (Priority: P1) 🎯 MVP

**Goal**: Authored DQL (schema apply, CRUD, `returning`, optional-filter queries, `with`-block) runs
against SQLite (file or `:memory:`) with no Docker, equivalent to PostgreSQL.

**Independent Test**: Point a session at SQLite `:memory:`, `EnsureCreatedAsync`, run the authored
units, confirm reads/writes/`returning`/`with`-block match PostgreSQL — no Docker daemon.

### Tests for User Story 1 (write first; they fail until the provider lands)

- [X] T023 [P] [US1] Create `tests/Dormant.Providers.ConformanceTests` (project + `Dormant.slnx` entry); add **shared** `schema/catalog.dqls` + `catalog.dql` (single source of truth, FR-007) and a provider-parameterized fixture (`[Arguments("postgres")]`/`[Arguments("sqlite")]`) opening the matching session factory
- [X] T024 [P] [US1] Create `tests/Dormant.Provider.Sqlite.Tests` for SQLite-specific behavior: `:memory:` clean-store-per-case lifetime + affinity round-trips (Guid/DateTime/JSON as TEXT, bytes as BLOB)

### Implementation for User Story 1

- [X] T025 [US1] Implement `SqliteRenderer : ISqlDialectRenderer` in `src/Dormant.SourceGeneration/Ir/Dialects/SqliteRenderer.cs`: `?` placeholders, `"schema_table"` prefix (D5), no `::` cast (D8), `LIKE` (D9), `RETURNING` kept (D7), `CREATE SCHEMA` → empty
- [X] T026 [US1] Add the SQLite affinity table to `DialectTypeMap` (TEXT/INTEGER/REAL/BLOB per D6) in `src/Dormant.SourceGeneration/Ir/Dialects/DialectTypeMap.cs` (depends on T011)
- [X] T027 [US1] Register `SqliteRenderer` in `DialectRenderers` so the generator emits the `DialectId.Sqlite` switch arms — `src/Dormant.SourceGeneration/Ir/Dialects/DialectRenderers.cs` (depends on T013, T025)
- [X] T028 [P] [US1] Implement `SqliteDialect : ISqlDialect` (`Id => Sqlite`, `QuoteIdentifier`, `Placeholder => "?"`, `Supports("sqlite")`) in `src/Dormant.Provider.Sqlite/SqliteDialect.cs`
- [X] T029 [P] [US1] Implement `SqliteFieldReader : IFieldReader` (TEXT→Guid/DateTime affinity reads) in `src/Dormant.Provider.Sqlite/Io/SqliteFieldReader.cs`
- [X] T030 [P] [US1] Implement `SqliteParameterWriter : IParameterWriter` (positional add-order; Guid/DateTime→TEXT, byte[]→BLOB) in `src/Dormant.Provider.Sqlite/Io/SqliteParameterWriter.cs`
- [X] T031 [US1] Implement `SqliteSession : IDbSession` (`Dialect => Sqlite`; connection + transaction; `QueryAsync`/`ExecuteAsync` via the IO writers/readers) in `src/Dormant.Provider.Sqlite/SqliteSession.cs` (depends on T029, T030)
- [X] T032 [US1] Implement `SqliteDataSource : IDataSource` with connection-lifetime ownership incl. `:memory:` keep-alive in `src/Dormant.Provider.Sqlite/SqliteDataSource.cs` (depends on T031)
- [X] T033 [US1] Implement `DormantSqlite` entry point (`CreateSessionFactory`/`CreateDataSource`/`EnsureCreatedAsync`/`Dialect`) and call `SQLitePCL.Batteries_V2.Init()` once at init in `src/Dormant.Provider.Sqlite/DormantSqlite.cs` (depends on T032)
- [X] T034 [US1] Wire the conformance fixture's sqlite path to `DormantSqlite`; run CRUD + `returning` + optional-filter + `with`-block parameterized over both providers → parity green (depends on T033, T023, T027)
- [X] T035 [US1] Make `tests/Dormant.Provider.Sqlite.Tests` green: clean store per case + affinity round-trips (depends on T033, T024)

**Checkpoint**: SQLite runs the full authored-DQL surface with PostgreSQL parity, no Docker (SC-001, SC-005).

---

## Phase 4: User Story 2 — General provider execution-strategy abstraction (Priority: P2)

**Goal**: Verify the boundary is general — a SQL provider plugs in as a dialect with **zero core
changes**, and no SQL-text assumption blocks a future non-SQL strategy.

**Independent Test**: Inspect the boundary contract + the changed-files set; confirm the IR is neutral
and the non-SQL extension point is open.

- [X] T036 [US2] Add a boundary-neutrality guard test asserting `src/Dormant.SourceGeneration/Ir/SqlIr.cs` carries no dialect-specific literal (`jsonb`, `$`, `::`, `ILIKE`) — in `tests/Dormant.SourceGeneration.Tests/DialectBoundaryTests.cs`
- [X] T037 [US2] Verify & record SC-003/SC-006: adding SQLite changed only the generator dialect set + the new adapter package (no `Session`/`SchemaInitializer`/Abstractions runtime *logic* changes beyond the Phase-2 seam) — capture the changed-files note in `specs/005-sqlite-nmemory-providers/contracts/dialect-boundary.md`
- [X] T038 [US2] Document the non-SQL extension point (a future strategy consuming the IR at build time) in `contracts/dialect-boundary.md`; add a marker/architecture test asserting `ISqlDialectRenderer` + IR shape admit it (SC-004)

**Checkpoint**: The boundary is provably general; PostgreSQL + SQLite coexist with zero core rework.

---

## Phase 5: User Story 3 — Core AOT integrity preserved (Priority: P1)

**Goal**: Core + dialect framework + SQLite publish Native AOT + full trimming with **zero**
library-originated warnings, no first-call warm-up.

**Independent Test**: The AOT smoke publish (core + PostgreSQL + SQLite) passes with 0 warnings.

- [X] T039 [US3] Extend `tests/Dormant.Aot.SmokeTests` to reference `Dormant.Provider.Sqlite` and run a SQLite `:memory:` CRUD round-trip in `tests/Dormant.Aot.SmokeTests/Program.cs`
- [X] T040 [US3] Run the AOT gate: `dotnet publish tests/Dormant.Aot.SmokeTests -c Release -r <rid> -p:PublishAot=true -p:TrimMode=full` MUST report 0 library-originated AOT/trim warnings; if any appear, apply the research-D11 fallback (explicit-init audit → suppress-with-justification → `bundle_green`) and re-run
- [X] T041 [US3] Document AOT-friendliness + minimum SQLite version (3.35 for `RETURNING`) in `src/Dormant.Provider.Sqlite/README.md`

**Checkpoint**: SQLite stays inside the AOT gate (FR-006, SC-002). "O importante é termos o nosso core AOT" upheld.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T042 [P] Update the generated-code compatibility baseline for the new `session.Dialect` switch shape + the `IEntityBinding` signature change (Constitution II / Compatibility Standards)
- [X] T043 [P] README/docs: SQLite provider usage + the dialect-differences table + the "core AOT, non-AOT providers are opt-in" framing
- [X] T044 Update the quickstart sample to optionally run on SQLite in `samples/Dormant.Sample.Quickstart`
- [X] T045 Run `specs/005-sqlite-nmemory-providers/quickstart.md` end-to-end against both PostgreSQL and SQLite
- [X] T046 [P] Add XML doc comments + at least one runnable example to every new public symbol (`DormantSqlite`, `DialectId`, reshaped interfaces) — Constitution I

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup; **BLOCKS all stories**. Must end with PostgreSQL green + byte-identical output.
- **US1 (Phase 3)**: depends on Foundational. The MVP.
- **US2 (Phase 4)**: depends on Foundational; fully verifiable once US1 has added the second dialect (the "zero core change" proof needs SQLite present).
- **US3 (Phase 5)**: depends on US1 (needs the SQLite provider to publish under AOT).
- **Polish (Phase 6)**: depends on US1–US3.

### Critical path

T001→T002 → (T004–T013 framework) → T014→T015, T016, T017 → T018→T019, T020 → T021→T022 (PG green)
→ T025→T027 + T028–T033 (SQLite) → T034/T035 (US1 green) → T039→T040 (AOT gate) → Polish.

### Within Phase 2

- T004–T008 are different files → parallel. T009→T010→T011→T012→T013 sequence (renderer abstraction before registry). T014→T015 share `QueryEmitter.cs` → sequential. T016, T017 different files → parallel with each other (after T013). T018, T019, T020 different files → parallel (after the contract edits T006–T008).

### Within US1

- Tests T023, T024 parallel (different projects). T028, T029, T030 parallel (different files). T031 needs T029+T030; T032 needs T031; T033 needs T032. T025→T026→T027 is the generator side; T034 needs both the provider (T033) and the Sqlite arms (T027) + the fixture (T023).

### Parallel Opportunities

- Phase 1: T003 alongside T001/T002.
- Phase 2: the Abstractions edits T004–T008 together; then T016/T017 and T018/T019/T020.
- US1: T023‖T024; T028‖T029‖T030.
- Polish: T042‖T043‖T046.

---

## Parallel Example: Phase 2 Abstractions

```bash
Task: "Add DialectId enum in src/Dormant.Abstractions/Providers/DialectId.cs"
Task: "Add DialectId Id to ISqlDialect.cs"
Task: "Add DialectId Dialect to IDbSession.cs"
Task: "Add DialectId Dialect to ISession.cs"
Task: "Reshape IEntityBinding CreateTableSql/SelectByKey in IEntityBinding.cs"
```

## Parallel Example: User Story 1 provider IO

```bash
Task: "Implement SqliteDialect in src/Dormant.Provider.Sqlite/SqliteDialect.cs"
Task: "Implement SqliteFieldReader in src/Dormant.Provider.Sqlite/Io/SqliteFieldReader.cs"
Task: "Implement SqliteParameterWriter in src/Dormant.Provider.Sqlite/Io/SqliteParameterWriter.cs"
```

---

## Implementation Strategy

### MVP First

1. Phase 1 Setup → 2. Phase 2 Foundational (PostgreSQL green, byte-identical) → 3. Phase 3 US1 →
**STOP & VALIDATE**: SQLite runs the authored DQL with parity, no Docker. That is the MVP.

### Incremental Delivery

Foundation → US1 (SQLite parity, MVP) → US2 (boundary proof) → US3 (AOT gate) → Polish. Each increment
is independently testable and never regresses PostgreSQL.

### Highest-risk item

T040 (the AOT gate). Microsoft.Data.Sqlite has historical AOT/trim warnings (research D11); the
explicit-init slim path is the mitigation and the gate is the proof. If it fails, the D11 fallback chain
applies before the feature is "done" (Constitution VI — failing/skipped verification is not done).

---

## Notes

- [P] = different files, no incomplete dependencies.
- PostgreSQL output byte-identity is a hard regression guard throughout (T021/T022).
- Variant selection is a branch over `const` strings — never runtime SQL compilation (Constitution).
- Commit after each task or logical group; keep PostgreSQL green at every checkpoint.
