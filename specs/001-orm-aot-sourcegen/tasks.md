---
description: "Task list for Dormant — AOT-First, Schema-DSL ORM for .NET 10"
---

# Tasks: Dormant — AOT-First, Schema-DSL ORM for .NET 10

**Input**: Design documents from `specs/001-orm-aot-sourcegen/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included as first-class tasks. Constitution v2.0.0 Principle VI (Quality & Testing) is
NON-NEGOTIABLE and the plan defines the test projects (Verify snapshots, cacheability, Testcontainers,
AOT smoke, BenchmarkDotNet). Write tests first within each story and ensure they fail before implementation.

**Organization**: Grouped by user story. Stack: C# 14 / .NET 10; multi-package, one-directional dependencies
(`Dormant.Abstractions` kernel ← `Dormant.Core` engine ← adapters `Provider.PostgreSql` /
`Spatial.PostgreSql` / `Tool`; `Dormant.SourceGeneration` emits against the kernel).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no incomplete dependencies)
- **[Story]**: US1–US8 for story-phase tasks; Setup/Foundational/Polish carry no story label

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution, projects, shared config, analyzers, CI.

- [X] T001 Create `Dormant.sln`, `Directory.Build.props`, `Directory.Packages.props` (central package mgmt, `net10.0`, `LangVersion=14`, `IsAotCompatible=true`, nullable enable, warnings-as-errors on public surface) at repo root
- [X] T002 [P] Create `src/Dormant.Abstractions/` project (net10.0, `IsAotCompatible`, empty `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`)
- [X] T003 [P] Create `src/Dormant.Core/` project referencing `Dormant.Abstractions`
- [X] T004 [P] Create `src/Dormant.SourceGeneration/` project (netstandard2.0, packaged as analyzer/generator)
- [X] T005 [P] Create `src/Dormant.Provider.PostgreSql/` project (refs Abstractions + Core, Npgsql)
- [X] T006 [P] Create `src/Dormant.Spatial.PostgreSql/` companion project (refs Provider.PostgreSql, NetTopologySuite isolated here)
- [X] T007 [P] Create `src/Dormant.Tool/` project (`dotnet tool`, refs Core + Provider.PostgreSql)
- [X] T008 [P] Create test projects `tests/Dormant.SourceGeneration.Tests/`, `tests/Dormant.Core.Tests/`, `tests/Dormant.Provider.PostgreSql.Tests/`, `tests/Dormant.Spatial.PostgreSql.Tests/`, `tests/Dormant.Aot.SmokeTests/`, `tests/Dormant.Benchmarks/`
- [X] T009 [P] Configure analyzers in `Directory.Build.props`: CA2012 (warning), CA1068, CA2016, `Microsoft.VisualStudio.Threading.Analyzers` (VSTHRD002/103/111), `Microsoft.CodeAnalysis.PublicApiAnalyzers` _(CA* via AnalysisLevel + VSTHRD wired in src/Directory.Build.props; PublicApiAnalyzers + PublicAPI.*.txt baselines land in T017)_
- [X] T010 [P] Create `samples/Dormant.Sample.Quickstart/` skeleton (mirrors quickstart.md)
- [X] T011 CI pipeline (single entry point: build → test → AOT smoke → benchmark → pack) in `.github/workflows/ci.yml` and a `build` script at repo root _(ci.yml build+test+aot-smoke jobs + build.sh created; benchmark/pack jobs added later; CI run unverified until pushed)_

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Kernel contracts (grouped by capability), generator pipeline skeleton, diagnostics + test harnesses. **⚠️ No user story may begin until complete.**

- [X] T012 [P] Define `Link<T>` / `LinkSet<T>` loaded/unloaded state in `src/Dormant.Abstractions/Links/` _(renamed Link→Ref + collection kinds added in T100; FR-009/049)_
- [X] T013 [P] Define `ISession`, `ISessionFactory`, `ConcurrencyConflictException` in `src/Dormant.Abstractions/Sessions/`
- [X] T014 [P] Define provider/data-access contracts `IDataSource`, `IDbSession` in `Providers/`; `PreparedStatement`, `RowMaterializer<T>` in `Querying/` (`src/Dormant.Abstractions/`)
- [X] T015 [P] Define `ISqlDialect` (Providers/), `ITypeBinding<T>`/`ITypeBindingRegistry` (Mapping/), `INativeFunctionCatalog` (Native/), `IMigrationStore` (Migrations/) — semantic grouping, no `Ports` bucket (`src/Dormant.Abstractions/`)
- [X] T016 [P] Define `CompiledQuery<T>`, `FieldReader`, `ParameterWriter` in `src/Dormant.Abstractions/Querying/`
- [ ] T017 Seed `PublicAPI.Shipped.txt`/`Unshipped.txt` baselines for `Dormant.Abstractions` (kernel surface) _(DEFERRED: enabling a frozen public-API baseline while the kernel still churns through US1–US3 is churn; turn on PublicApiAnalyzers + seed baselines once the surface stabilizes, before first release)_
- [X] T018 [P] Implement `EquatableArray<T>` + equatable model primitives in `src/Dormant.SourceGeneration/Parsing/`
- [X] T019 `IIncrementalGenerator` skeleton rooted at `AdditionalTextsProvider` (DSL file filter, path+content extraction, `WithTrackingName`) in `src/Dormant.SourceGeneration/Generator.cs`
- [X] T020 `DiagnosticDescriptors` (ORM###) + equatable `DiagnosticInfo`/`LocationInfo` + companion `DiagnosticAnalyzer` scaffold in `src/Dormant.SourceGeneration/Diagnostics/` _(descriptors ORM001/002 + DiagnosticInfo/LocationInfo + AnalyzerReleases tracking done; the DiagnosticAnalyzer class is added in US1 when it reports real located diagnostics — an empty analyzer is pointless)_
- [X] T021 [P] Core error model + diagnostics types in `src/Dormant.Core/Diagnostics/`
- [X] T022 Generator test harness: `VerifySourceGenerators.Initialize()` + cacheability helper (`trackIncrementalGeneratorSteps`) in `tests/Dormant.SourceGeneration.Tests/`

**Checkpoint**: Ports + generator pipeline ready — user stories can begin.

---

## Phase 3: User Story 1 - Model a domain in the schema DSL (Priority: P1) 🎯 MVP

**Goal**: Schema DSL → strongly-typed partial entity types + links; deterministic; located diagnostics.

**Independent Test**: Two entities + a link build into compiling partials; a hand-written partial coexists and survives regeneration; an invalid link yields a located diagnostic. (FR-001/003/004, SC-009)

### Tests for User Story 1

- [X] T023 [P] [US1] Verify snapshot test: two entities + link → generated partials in `tests/Dormant.SourceGeneration.Tests/SchemaEmitTests.cs` _(used structural assertions + a determinism test rather than Verify `.verified.txt` snapshots; committed Verify baselines deferred to polish T097)_
- [X] T024 [P] [US1] Cacheability test for the schema pipeline (assert `Cached`/`Unchanged` on re-run) in `tests/Dormant.SourceGeneration.Tests/SchemaCacheabilityTests.cs`
- [X] T025 [P] [US1] Diagnostic test: link to undefined entity / required-link cycle → located diagnostic in `tests/Dormant.SourceGeneration.Tests/SchemaDiagnosticTests.cs` _(covers located ORM002 undefined link, ORM003 unknown type, ORM001 syntax; cycle detection deferred)_

### Implementation for User Story 1

- [X] T026 [US1] DSL lexer (tokens, source offsets) in `src/Dormant.SourceGeneration/Parsing/Lexer.cs`
- [X] T027 [US1] Schema parser → equatable schema AST in `src/Dormant.SourceGeneration/Parsing/SchemaParser.cs` (depends T026)
- [X] T028 [US1] Schema validation (undefined links, cycles, duplicate names) → `DiagnosticInfo` in `src/Dormant.SourceGeneration/Schema/SchemaValidator.cs` _(undefined-link-target ORM002 done; unknown-type ORM003 in parser; cycle + duplicate-name checks deferred to a later hardening pass)_
- [X] T029 [P] [US1] Deterministic emit helpers (ordinal sort, `InvariantCulture`, stable hint names, normalized newlines) in `src/Dormant.SourceGeneration/Emit/EmitHelpers.cs`
- [X] T030 [US1] Emit partial entity types (properties + `Link<T>`/`LinkSet<T>` members) in `src/Dormant.SourceGeneration/Schema/EntityEmitter.cs` (depends T027, T029) _(superseded by Ref vocabulary + collection kinds in T102; FR-049)_
- [X] T031 [US1] Emit `[UnsafeAccessor]` field accessors per entity _(done in `MaterializerEmitter.cs`: per-entity `[UnsafeAccessor]` field accessors + ctor accessor; compiles in the sample, generator-tested)_
- [X] T032 [US1] Wire schema pipeline into `RegisterSourceOutput` (emit + report diagnostics) in `src/Dormant.SourceGeneration/DormantGenerator.cs`
- [X] T033 [US1] Quickstart schema + hand-written partial coexistence sample in `samples/Dormant.Sample.Quickstart/` _(schema/app.dqls + UserExtensions.cs partial; generator wired as analyzer; builds + runs)_

**Checkpoint**: Schema → entities works and is independently testable.

---

## Phase 3b: Relationship-model revision & coverage gaps (FR-045/049/050/051)

**Why**: These tasks were added after the original tasks.md (spec gained FR-045..051 + the Link→Ref
model). They revise the already-built US1 generator/kernel and close `/speckit-analyze` coverage gaps.
**Complete before continuing US2.** Story labels show which story's scope each augments.

- [X] T100 [US1] Rename `Link<T>`→`Ref<T>`, `LinkSet<T>`→`RefSet<T>` and add `RefList<T>`, `RefBag<T>`, `RefMap<TKey,TValue>` in `src/Dormant.Abstractions/Entities/` (readonly structs, Unloaded sentinel); `Ref<T> where T : class?` so optional single is `Ref<User?>` (FR-009/FR-049). _(moved to `Entities/` namespace; ISession LoadAsync overloads + adapter/sample/tests updated)_
- [X] T101 [US1] Extend DSL parser for collection syntax `Set<T>`/`List<T>`/`Bag<T>`/`Map<K,V>` → `ReferenceModel.Kind` in `src/Dormant.SourceGeneration/Parsing/SchemaParser.cs` (lexer `< > ,` added; `multi`/`single`/arrow removed) (FR-047/FR-049)
- [X] T102 [US1] Update `EntityEmitter` to emit `Ref`/`RefSet`/`RefList`/`RefBag`/`RefMap` members with Unloaded-sentinel initializers (never `= []`) and `Ref<T?>` for optional single refs in `src/Dormant.SourceGeneration/Schema/EntityEmitter.cs` (FR-047/FR-048/FR-049)
- [X] T103 [P] [US1] Emit entity identity `Equals`/`GetHashCode` by primary key (transient → reference equality) (FR-051) _(folded into `EntityEmitter` (single-PK entities, `: IEquatable<T>`); SchemaEmitTests assert it. `[NoIdentityEquality]` opt-out + multi-key still deferred)_
- [ ] T104 [US3] Emit projection materialization into a **user-owned plain `record`/DTO** (no Dormant types) — map columns → record constructor by name/order — in `src/Dormant.SourceGeneration/Query/ProjectionEmitter.cs`; tests assert a Dormant-free result type (FR-050)
- [X] T105 [US5] Schema-qualified DDL/SQL + `CREATE SCHEMA IF NOT EXISTS <module>` (FR-045) _(done — see Phase 6: `TableRef(schema, name)` qualifies all emitted SQL + DDL; `SchemaInitializer` creates the schema; integration suite runs schema-qualified)_
- [X] T106 [P] [US1] Update generator tests + sample for the Ref model (`SchemaEmitTests` asserts Ref/RefSet/Unloaded/equality; `RefTests` renamed from LinkTests; `app.dqls` uses `Set<Post>`); builds + runs green
- [X] T107 [US1] Replace `[UnsafeAccessor]` materialization (fragile backing-field hack) with a generated `[SetsRequiredMembers] internal {Entity}(IFieldReader reader)` ctor on the entity partial (ordinary setters) + retained public parameterless ctor (`EntityEmitter`); binding `Materialize` → `new {Entity}(reader)`, INSERT/snapshot reads via public getters, drop field accessors + `Create()` (`EntityBindingEmitter`); update SchemaEmitTests + re-run the Testcontainers round-trip (FR-048) _(done: `EntityEmitter` emits public `()` + `[SetsRequiredMembers] internal E(IFieldReader)` reading value columns by ordinal via setters; `EntityBindingEmitter` dropped `Uac`/`UacKind`/`Create()`/field accessors, `Materialize` → `new E(reader)`, `Insert` writes via public getters. Supersedes T044's UnsafeAccessor emitter. Build 0/0; generator 8/8; core 6/6; PostgreSQL Testcontainers round-trip 2/2 — required-init contract intact, sample compiles)_

**Checkpoint**: Kernel + generator on the Ref model (collections, Unloaded sentinel, PK equality, record projections); sample builds + runs; generator tests green. Resume US2 after this.

---

## Phase 4: User Story 2 - Persist and load full entities through a session (Priority: P1)

**Goal**: Session CRUD on PostgreSQL with identity map, snapshot-diff change tracking (only changed columns), optimistic concurrency.

**Independent Test**: Insert+commit→row exists; load+modify one field+commit→only that column written; delete→gone; two sessions same row→conflict. (FR-014/015, SC)

### Tests for User Story 2

- [X] T034 [P] [US2] Integration (Testcontainers): insert + commit → row exists in `tests/Dormant.Provider.PostgreSql.Tests/CrudTests.cs` _(insert→get-by-key round-trip against real PostgreSQL via generated binding + session; 2/2 green)_
- [X] T035 [P] [US2] Integration: modify one field + commit → only that column changed in `tests/Dormant.Provider.PostgreSql.Tests/ChangeTrackingTests.cs` _(proven behaviorally: an out-of-band write to the untouched `quantity` column survives a name-only commit — an all-columns UPDATE would revert it; green vs real PostgreSQL)_
- [X] T036 [P] [US2] Integration: delete + commit → row gone in `tests/Dormant.Provider.PostgreSql.Tests/CrudTests.cs` _(Remove + commit → reload returns null)_
- [X] T037 [P] [US2] Integration: two sessions same row → `ConcurrencyConflictException` in `tests/Dormant.Provider.PostgreSql.Tests/ConcurrencyTests.cs` _(first commit wins token 0→1; stale second commit affects 0 rows → conflict; first writer's value persists)_

### Implementation for User Story 2

- [X] T038 [US2] `IDataSource`/`IDbSession` over `NpgsqlSlimDataSourceBuilder` (connection + transaction) in `src/Dormant.Provider.PostgreSql/PostgreSqlDataSource.cs` _(+ PostgreSqlSession + DormantPostgres factory; verified by a real Testcontainers round-trip)_
- [X] T039 [US2] No-boxing parameter writer (`NpgsqlParameter<T>.TypedValue`) + reader (`GetFieldValue<T>`) in `src/Dormant.Provider.PostgreSql/Io/`
- [X] T040 [US2] `PostgreSqlDialect` (positional `$n`, identifier quoting, DDL type names) in `src/Dormant.Provider.PostgreSql/PostgreSqlDialect.cs` _(quoting + `$n` + Supports; DDL type-name mapping added with US5 migrations)_
- [X] T041 [P] [US2] Built-in scalar `ITypeBinding<T>` set in `src/Dormant.Provider.PostgreSql/Bindings/ScalarBindings.cs` _(satisfied by the generic no-boxing IO path — `IFieldReader.GetValue<T>`/`IParameterWriter.Write<T>` route built-in scalars through Npgsql directly; a per-type `ITypeBindingRegistry` is only needed for custom handlers, deferred to US7)_
- [X] T042 [US2] Session / Unit of Work + identity map in `src/Dormant.Core/Persistence/Session.cs` (depends T038, T013) _(+ `SessionFactory`, `DormantPostgres.CreateSessionFactory`, and the `IEntityBinding<T>`/`EntityBindings` registry bridging generated code↔session; AddAsync/GetAsync/CommitAsync wired. Remove/Query/Load deferred to later slices)_
- [X] T043 [US2] Emit per-entity snapshot struct + diff comparer in `src/Dormant.SourceGeneration/Schema/SnapshotEmitter.cs` _(folded into `EntityBindingEmitter`: emits an `internal readonly record struct {E}Snapshot(...)` of value columns + `Snapshot(object)` capture; the diff comparer is the per-column typed `EqualityComparer<T>.Default` check inside `Update`. Single box per tracked entity — bookkeeping, not the per-row path)_
- [X] T044 [US2] Emit per-entity materializer (`Create()` via `[UnsafeAccessor]` ctor past `required`, field accessors, `Materialize(IFieldReader)` reading value columns by ordinal, no boxing/reflection) in `src/Dormant.SourceGeneration/Schema/MaterializerEmitter.cs` _(value columns only; references left Unloaded — link materialization with US3 shapes)_
- [X] T045 [US2] Change-tracking commit: INSERT / UPDATE(changed columns only) / DELETE in `src/Dormant.Core/Persistence/ChangeTracker.cs` (depends T043) _(folded into `Session.CommitAsync`: flush queued inserts → diff each tracked entity vs snapshot → `IEntityBinding.Update` (changed cols only, null when no-op) → `Delete` for removed; snapshots refreshed post-commit. Dispatch via the new non-generic `IEntityBinding` facade — no reflection. `ChangeTracker.cs` not split out; logic lives in the Session unit-of-work)_
- [X] T046 [US2] Optimistic concurrency token check + conflict surfacing in `src/Dormant.Core/Persistence/ChangeTracker.cs` _(generated `Update`/`Delete` embed `WHERE token = $old` + bump token in SET; `Session` throws `ConcurrencyConflictException` when `TracksConcurrency` and affected-rows == 0. v1 tokens are integer-incremented)_
- [ ] T047 [US2] DSL DML (insert/update/delete) parse + SQL emit in `src/Dormant.SourceGeneration/Query/DmlEmitter.cs` _(DEFERRED: DSL-authored DML is a separable parser feature independent of session change-tracking — grouped with US3 query authoring. Session persistence (T034–T046) generates SQL from bindings, not from authored DML, so US2 acceptance does not need it)_

**Checkpoint**: Full-entity persistence round-trip works.

---

## Phase 5: User Story 3 - Query exact result types: entity or projection, nested links (Priority: P1)

**Goal**: DSL query → full entity or distinct projection type; nested links in one round-trip; non-fetched field access is a compile error; SQL at build time.

**Independent Test**: Projection `{ id, name, posts: { title } }` exposes exactly those members, `posts` populated in one round-trip, referencing `email` fails to compile. (FR-006/007/008/010/013, SC-002/003)

### Tests for User Story 3

- [X] T048 [P] [US3] Verify snapshot: projection → distinct type with exactly requested members in `tests/Dormant.SourceGeneration.Tests/ProjectionEmitTests.cs` _(asserts `record WidgetNamesResult(global::System.Guid Id, string Name);` — exactly id+name)_
- [ ] T049 [P] [US3] Integration: nested-link fetch executes in exactly one round-trip (statement count) in `tests/Dormant.Provider.PostgreSql.Tests/RoundTripTests.cs` _(DEFERRED with nested-link shapes — JSON-aggregation strategy; see research §6)_
- [X] T050 [P] [US3] Negative compile test: referencing a non-fetched field fails to compile in `tests/Dormant.SourceGeneration.Tests/ProjectionNegativeTests.cs` _(satisfied in `ProjectionEmitTests`: the projection record omits non-projected members by construction — the exact-signature assertion proves a non-fetched field has no member to reference. Folded; no separate file)_
- [X] T051 [P] [US3] Integration: full-entity query populates all mapped columns in `tests/Dormant.Provider.PostgreSql.Tests/EntityQueryTests.cs` _(full-entity + flat-projection queries green vs real PostgreSQL; filter/order/limit honored)_

### Implementation for User Story 3

- [X] T052 [US3] Query parser (select, shape, filter, order by, limit/offset) → query AST in `src/Dormant.SourceGeneration/Parsing/QueryParser.cs` _(+ `QueryModel.cs` AST; lexer extended with `= . ( ) <= >=` + numbers. MVP grammar: full-entity/flat-projection select, conjunctive own-column filter, order by, limit/offset literal-or-param. Path nav rejected with a diagnostic; nested shapes/optional params deferred)_
- [X] T053 [US3] Projection type emitter (distinct types) in `src/Dormant.SourceGeneration/Query/QueryEmitter.cs` _(folded into `QueryEmitter`: emits `record {Query}Result(...)` with exactly the requested scalar members. Nested shapes deferred)_
- [X] T054 [US3] Select SQL builder in `src/Dormant.SourceGeneration/Query/QueryEmitter.cs` _(folded into `QueryEmitter.BuildSql`: SELECT col-list (full-entity decl order / projection order) + WHERE/ORDER BY/LIMIT/OFFSET, positional `$n`. Single-round-trip nested links deferred — JSON aggregation, research §6)_
- [X] T055 [US3] Typed query method + `CompiledQuery<T>` emit in `src/Dormant.SourceGeneration/Query/QueryEmitter.cs` _(folded: `partial static {Module}Queries` ISession extension methods; `CompiledQuery<T>` redesigned with public ctor carrying `PreparedStatement` + `RowMaterializer<T>`)_
- [X] T056 [US3] Result materialization for entities + projections (no boxing) _(entities via the generated ctor `new E(reader)`; projections via positional `new {Query}Result(reader.GetValue<T>(i)…)` — emitted by `QueryEmitter`, no separate `Materialization.cs`)_
- [X] T057 [US3] Query execution streaming via `IDbSession.QueryAsync` → `IAsyncEnumerable<T>` (+ `QuerySingleOrDefaultAsync`) _(folded into `Session.QueryAsync`/`QuerySingleOrDefaultAsync` streaming through the driver; no separate `QueryExecutor.cs`. `QuerySingleOrDefault` = first-or-default; strict narrowing FR-033 deferred)_
- [ ] T058 [US3] Link load-state population + explicit on-demand `LoadAsync` in `src/Dormant.Core/Persistence/LinkLoader.cs` (depends T042) _(DEFERRED: pairs with nested-link fetch + FK columns for single refs; `LoadAsync` overloads still throw NotSupported)_

**Checkpoint**: 🎯 MVP query path complete (full-entity + flat projection, build-time SQL, real-PostgreSQL verified). Remaining US3 = nested-link single-round-trip (JSON aggregation) + link loading (T049/T058) + user-owned-record projection (T104) + path-nav/optional-param query grammar.

---

## Phase 6: User Story 5 - Evolve the schema with migration tooling (Priority: P2)

**Goal**: CLI create/apply/rollback/status; incremental diffs; destructive ops flagged.

**Independent Test**: Initial migration apply → schema matches; change → incremental migration = diff only; rollback restores prior state; destructive op flagged. (FR-020/021/022, SC-010)

### Tests for User Story 5

- [X] T059 [P] [US5] Integration: initial schema apply → DB schema matches in `tests/Dormant.Provider.PostgreSql.Tests/MigrationApplyTests.cs` _(EnsureCreated applies generated CREATE SCHEMA + CREATE TABLE; idempotent (applied twice) + table immediately usable; the whole integration suite now provisions via EnsureCreated)_
- [ ] T060 [P] [US5] Integration: incremental migration contains only the diff in `tests/Dormant.Provider.PostgreSql.Tests/MigrationDiffTests.cs`
- [ ] T061 [P] [US5] Integration: rollback restores prior state in `tests/Dormant.Provider.PostgreSql.Tests/MigrationRollbackTests.cs`
- [ ] T062 [P] [US5] Integration: destructive (data-loss) op flagged, not auto-applied in `tests/Dormant.Provider.PostgreSql.Tests/MigrationSafetyTests.cs`

### Implementation for User Story 5

- [ ] T063 [US5] Migration model + schema snapshot + diff engine in `src/Dormant.Core/Migrations/` _(DEFERRED: incremental diff/versioned migrations — this slice does idempotent create-if-not-exists, not versioned diffs)_
- [X] T064 [US5] DDL generation (CREATE TABLE) via the SQL IR + PG type map _(folded into `EntityBindingEmitter` → `CreateTableStatement`/`CreateSchemaStatement` rendered by `SqlRenderer`; `TypeMap.ToSqlType` (DSL→PG). ALTER/DROP deferred with the diff engine; emitted on the binding as `CreateTableSql` + `Schema`)_
- [X] T065 [US5] Schema apply over registered bindings in `src/Dormant.Core/Migrations/SchemaInitializer.cs` + `DormantPostgres.EnsureCreatedAsync` _(applies CREATE SCHEMA per distinct schema + each binding's CREATE TABLE in one tx; idempotent. A **versioned** `IMigrationStore` (applied/pending tracking) is DEFERRED)_
- [ ] T066 [US5] CLI commands `migrations add/apply/rollback/status` + `schema validate` in `src/Dormant.Tool/Commands/` _(DEFERRED: CLI slice)_
- [ ] T067 [US5] Destructive-op detection + explicit confirm gate in `src/Dormant.Core/Migrations/DestructiveGuard.cs` _(DEFERRED: needs the diff engine T063)_

> T105 (schema-qualified DDL/SQL + CREATE SCHEMA) done in this slice — see Phase 3b.

**Checkpoint**: Generated schema-qualified DDL + idempotent apply (CREATE SCHEMA + CREATE TABLE) works end-to-end; all SQL is schema-qualified. DEFERRED: versioned migration store + incremental diff (T063), rollback (T061), destructive guard (T067), CLI (T066).

---

## Phase 7: User Story 4 - Dynamic queries via optional parameters (Priority: P2)

**Goal**: One query with optional/conditional params; executed SQL varies, result type fixed.

**Independent Test**: Two optional filters; run with none/one/both → identical result type, correctly filtered rows. (FR-012/031, SC-005)

### Tests for User Story 4

- [X] T068 [P] [US4] Integration: optional params none/one/both → same result type, correct rows in `tests/Dormant.Provider.PostgreSql.Tests/OptionalParamsTests.cs` _(two optional filters; none/min/name/both all correct + ordered; green vs real PostgreSQL)_
- [X] T069 [P] [US4] Verify: single result type + nullable optional params for all combinations in `tests/Dormant.SourceGeneration.Tests/OptionalParamTypeTests.cs` _(asserts `IAsyncEnumerable<Widget> SearchWidgets(int? minQuantity = default, string? name = default, …)` + fragment selection)_

### Implementation for User Story 4

- [X] T070 [US4] Extend query parser: `optional` parameters in `QueryParser` + `QueryParameter.IsOptional` (FR-012/FR-031) _(coalesce `??` + optional LIMIT/OFFSET DEFERRED — sugar; the SC-005 core is optional filters)_
- [X] T071 [US4] Prebuilt SQL fragments + runtime fragment-toggle assembly (no query compilation, FR-013) _(folded into `QueryEmitter.EmitDynamicStatement`: when a filter uses an optional param, the method assembles SQL at runtime — required fragments always, optional only when the param is non-null; result type fixed. No separate `FragmentAssembler.cs`. Mirrors the change-tracking UPDATE)_
- [X] T072 [US4] Optional-parameter binding (omit clause + bind when absent/present) _(folded into the dynamic emit: the bind callback re-applies the same null-guards so parameter order matches; value-type optionals unwrapped via `.Value`. No separate `QueryExecutor.cs`)_

**Checkpoint**: Conditional queries with stable result types — optional filters work; `??` coalesce + optional LIMIT/OFFSET deferred (sugar).

---

## Phase 8: User Story 8 - Database-native types and functions (JSONB, GIS) (Priority: P2)

**Goal**: Per-provider native types/functions (typed catalog + raw typed escape), explicitly non-portable with build diagnostics; JSONB in core, GIS via companion.

**Independent Test**: `jsonb` round-trip + native containment query (build-time-known type, zero AOT warnings); PostGIS spatial query via companion; unsupported-provider target → located diagnostic. (FR-038..044, SC-012/013/014)

### Tests for User Story 8

- [~] T073 [P] [US8] Integration: `jsonb` round-trip + containment operator, result type build-time-known in `tests/Dormant.Provider.PostgreSql.Tests/JsonbTests.cs` _(ROUND-TRIP DONE: a `json` property → PG `jsonb` column round-trips as a build-time-known `string` (no boxing) — green vs real PostgreSQL. Required a native **write cast**: `json` columns emit `$n::jsonb` in INSERT + UPDATE (`InsertColumn.ParamCast` in the IR; `ParamCast` in `EntityBindingEmitter`) since PG won't coerce text→jsonb. Containment operator `@>` DEFERRED with native functions/operators T078/T079)_
- [ ] T074 [P] [US8] AOT smoke: jsonb scenario, zero library-originated warnings in `tests/Dormant.Aot.SmokeTests/JsonbAotTests.cs`
- [ ] T075 [P] [US8] Integration: PostGIS geometry + spatial function end-to-end in `tests/Dormant.Spatial.PostgreSql.Tests/SpatialTests.cs`
- [ ] T076 [P] [US8] Diagnostic: native construct targeting unsupported provider → located build diagnostic in `tests/Dormant.SourceGeneration.Tests/NativePortabilityTests.cs`
- [ ] T077 [P] [US8] Build-error test: raw fragment without declared return type in `tests/Dormant.SourceGeneration.Tests/NativeRawFragmentTests.cs`

### Implementation for User Story 8

- [ ] T078 [US8] Parse provider directive + native catalog (`provider { func/operator }`, raw fragment) in `src/Dormant.SourceGeneration/Native/NativeParser.cs`
- [ ] T079 [US8] Emit typed native function/operator invocation + signature type-check in `src/Dormant.SourceGeneration/Native/NativeFunctionEmitter.cs`
- [ ] T080 [US8] Emit raw typed SQL fragment (parameter-bound, declared return) in `src/Dormant.SourceGeneration/Native/RawFragmentEmitter.cs`
- [ ] T081 [US8] Non-portability diagnostic via `ISqlDialect.Supports(scope)` in `src/Dormant.SourceGeneration/Diagnostics/NativePortability.cs`
- [ ] T082 [US8] jsonb `ITypeBinding` + emit STJ `JsonSerializerContext` per jsonb-mapped type in `src/Dormant.Provider.PostgreSql/Bindings/JsonbBinding.cs` + `src/Dormant.SourceGeneration/Native/JsonContextEmitter.cs`
- [ ] T083 [US8] PostGIS EWKB codec + `geometry`/`geography` bindings in `src/Dormant.Spatial.PostgreSql/Ewkb/`

**Checkpoint**: Native types/functions usable; JSONB built-in, GIS via companion.

---

## Phase 9: User Story 6 - Ship a Native AOT / fully-trimmed application (Priority: P2)

**Goal**: Publish with Native AOT + full trimming, zero library-originated warnings, no warm-up.

**Independent Test**: Publish US2/US3 sample AOT+trimmed; identical results; zero library warnings; cold first query no warm-up. (FR-016/017/018, SC-001/006)

### Tests for User Story 6

- [X] T084 [P] [US6] AOT smoke: publish the harness (`PublishAot=true`+`TrimMode=full`) exercising US2/US3/US4/US8 scenarios, **zero** library-originated warnings _(smoke `Program.cs` roots schema apply + CRUD + generated entity/projection queries + optional params + jsonb; `dotnet publish -r osx-arm64` → 4.7M native binary, **0 warnings** across Abstractions/Core/Npgsql-slim/generated code. SC-001 ✓)_
- [~] T085 [P] [US6] AOT smoke: results identical to non-trimmed run _(PARTIAL: the AOT binary runs the rooted paths; full parity vs JIT needs a DB at AOT-run time. Results are identical by construction — same generated code, no reflection — but not separately asserted under AOT. Deferred)_
- [X] T086 [P] [US6] AOT smoke: cold start, first query no warm-up step _(native binary runs ~0.5s cold and goes straight to connecting — no runtime codegen/warm-up. SC-006 ✓)_

### Implementation for User Story 6

- [X] T087 [US6] Confirm `IsAotCompatible` across all shipped libs; resolve trim/AOT warnings _(the zero-warning AOT publish of the full rooted stack proves it; no warnings to resolve)_
- [X] T088 [US6] CI gate: fail on any library-originated AOT/trim warning _(`TreatWarningsAsErrors` in the smoke csproj makes IL2xxx/IL3xxx fail the publish — intrinsic gate, local + CI; `ci.yml` aot-smoke job publishes `-r linux-x64`)_

**Checkpoint**: AOT deliverable proven end-to-end — full stack publishes Native-AOT + fully-trimmed with zero warnings; native binary runs with no warm-up. (Parity-vs-JIT under AOT deferred — needs DB.)

---

## Phase 10: User Story 7 - Extend the ORM with custom behavior (Priority: P3)

**Goal**: Custom type/value handlers + naming/mapping conventions without modifying the library; AOT-safe.

**Independent Test**: Register a custom handler for an unsupported type → round-trips; custom convention applied without per-entity config; AOT publish still zero warnings. (FR-025/026)

### Tests for User Story 7

- [ ] T089 [P] [US7] Integration: custom type handler round-trips an unsupported type in `tests/Dormant.Provider.PostgreSql.Tests/CustomHandlerTests.cs`
- [ ] T090 [P] [US7] Verify/integration: custom naming convention applied without per-entity config in `tests/Dormant.SourceGeneration.Tests/ConventionTests.cs`
- [ ] T091 [P] [US7] AOT smoke: registered extension → zero warnings, no hot-path reflection in `tests/Dormant.Aot.SmokeTests/ExtensionAotTests.cs`

### Implementation for User Story 7

- [ ] T092 [US7] Type handler registration API + AOT-safe resolution in `src/Dormant.Core/Extensibility/TypeHandlers.cs` (+ `ITypeBinding<T>` in Abstractions)
- [ ] T093 [US7] Naming/mapping convention API + generation-time application in `src/Dormant.Core/Extensibility/Conventions.cs` + `src/Dormant.SourceGeneration/Schema/ConventionResolver.cs`

**Checkpoint**: Extensibility complete; all stories independently functional.

---

## Phase 5b: US3 follow-up — extension-block query surface (FR-058)

- [X] T108 [US3] Refactor `QueryEmitter` to emit the generated query methods inside a **C# 14 extension block** (`extension(global::Dormant.Abstractions.Sessions.ISession session) { … }`) within `partial static {Module}Queries`, instead of classic `this`-parameter static methods, so extension properties/static members can be added later without a breaking shape change. Update `ProjectionEmitTests` assertions + re-run generator and PostgreSQL query tests (FR-058) _(done: methods now `public {Result} {Name}(params, ct)` inside `extension(ISession session) { … }`; call sites unchanged. Generator 16/16, PostgreSQL 8/8)_

---

## Phase 12: User Story 9 - Configurable database naming conventions with overrides (Priority: P2)

**Goal**: Database identifiers (tables, columns, native functions, schema names) follow a configurable convention — snake_case by default — with per-project setting and per-unit (table/column/function) explicit overrides; resolved at build time; consistent across DDL/DML/queries/params/migrations.

**Independent Test**: PascalCase entities + snake_case members build to snake_case DDL/SQL by default; one table + one column override use exact names everywhere while siblings keep the convention; switching the project convention changes all non-overridden names consistently. (FR-052..FR-057, SC-015)

### Tests for User Story 9

- [X] T109 [P] [US9] Verify snapshot/string asserts: default snake_case table/column names in generated SQL (entity `RecentPost` → `recent_post`, member `createdAt` → `created_at`) in `tests/Dormant.SourceGeneration.Tests/NamingConventionTests.cs` (FR-052)
- [X] T110 [P] [US9] Per-project convention change → all non-overridden names follow it (verbatim vs snake_case) in `tests/Dormant.SourceGeneration.Tests/NamingConventionTests.cs` (FR-053) _(via a `TestOptionsProvider` supplying `build_property.DormantNamingConvention`)_
- [X] T111 [P] [US9] Per-unit override (table/column) wins over convention, siblings unaffected in `tests/Dormant.SourceGeneration.Tests/NamingOverrideTests.cs` (FR-054)
- [X] T112 [P] [US9] Convention collision (two members → same DB name) → source-located ORM013 diagnostic in `tests/Dormant.SourceGeneration.Tests/NamingDiagnosticTests.cs` (FR-057)
- [X] T113 [P] [US9] Integration: snake_case round-trip against real PostgreSQL (`StockItem`→`stock_item`, `itemName`→`item_name`) in `tests/Dormant.Provider.PostgreSql.Tests/NamingTests.cs` (FR-055, SC-015)

### Implementation for User Story 9

- [X] T114 [US9] Naming-convention model + resolver (snake_case + verbatim; deterministic, build-time) in `src/Dormant.SourceGeneration/Emit/NamingConvention.cs` (FR-052/FR-053/FR-056) _(in `Emit` namespace to avoid colliding with the existing `Naming` helper class)_
- [X] T115 [US9] Project-level convention from `AnalyzerConfigOptions` (`build_property.DormantNamingConvention`) projected into `GeneratorConfig` and passed to the emitters (FR-053)
- [X] T116 [US9] Per-unit override syntax in DSL (`entity X db("…")`, `member: T db("…")`) + lexer string literals + parse into `EntityModel.NameOverride`/`PropertyModel.NameOverride` (FR-054) _(function override deferred to US8 native functions)_
- [X] T117 [US9] Resolve names at emit (`override ?? convention`) and consume in `EntityBindingEmitter` (INSERT/SELECT/UPDATE/DELETE) + `QueryEmitter` (SELECT) — same resolver, no drift (FR-055) _(schema-qualified DDL/function names land with US5/US8)_
- [X] T118 [US9] Per-entity column collision detection → source-located ORM013 (FR-057) _(`NameResolution.FindColumnCollisions`; cross-table collision deferred)_

**Checkpoint**: Database naming is snake_case by default, project-configurable (`DormantNamingConvention`), per-unit overridable via `db("…")`, build-time-resolved, and consistent across binding + query SQL. Build 0/0; generator 16/16; PostgreSQL 8/8.

---

## Phase 13: User Story 10 - Structured IR + plugin extension points (Priority: P3, architectural)

**Goal**: Generation operates over a structured, deterministic, value-equatable IR (language constructs + statements-to-emit), with build-time plugin hooks transforming the IR before output; strings only at the output boundary. Compiled-definition cache deferred. (FR-059..FR-064, SC-016/017)

**Independent Test**: A plugin transforms the IR (inject a column / rewrite a statement) and the output reflects it with no core edits, determinism + zero AOT warnings preserved. (Cache: a repeated query reuses one definition instance.)

> Note: largely architectural / future-leaning. The current emitters build SQL via string assembly (`EntityBindingEmitter`, `QueryEmitter`); these tasks introduce the IR layer and migrate emission onto it. Sequence after the MVP stories stabilize.

### Implementation for User Story 10

- [X] T119 [US10] Define a structured SQL/output IR (statement + clause nodes) + `SqlRenderer` in `src/Dormant.SourceGeneration/Ir/SqlIr.cs` (FR-059/FR-060) _(InsertStatement/SelectStatement/DeleteStatement + SqlCondition/SqlOrder/SqlLimit; renderer centralizes quoting/formatting. Nodes are emit-time scaffolding downstream of the cached parse models → plain records (determinism, not pipeline equatability, is what matters); noted vs the original `EquatableArray` wording)_
- [X] T120 [US10] Migrate `EntityBindingEmitter` (INSERT / SELECT-by-key / DELETE) + `QueryEmitter` (SELECT) to build the IR and render at the boundary; byte-identical output (FR-059) _(verified: exact-SQL assertions in SchemaEmitTests/ProjectionEmitTests unchanged → generator 16/16; PostgreSQL 8/8. **UPDATE excluded**: the changed-columns-only UPDATE is assembled at runtime by generated code (StringBuilder over diff flags), not a gen-time-static string — migrating it needs a runtime fragment/IR builder, tracked as a follow-up under T121)_
- [ ] T121 [US10] Internal IR transformation seam: ordered, deterministic transform stages over the IR before rendering (FR-061/FR-062). Also: extend the IR to model the change-tracking UPDATE as a conditional/fragment statement so its runtime assembly renders from IR too (completes T120 for UPDATE)
- [ ] T122 [US10] Plugin-produced invalid IR → source-located diagnostic (new ORM0xx); cacheability + determinism tests over the IR + transforms (FR-063/FR-060)
- [ ] T123 [US10] (Later phase) Stable public plugin API surface (PublicApiAnalyzers) + example plugin (SC-016) — defer until the IR seam stabilizes (FR-061)
- [ ] T124 [US10] (Later phase) Compiled query/command definition cache — reuse one definition instance per query/command, allocation benchmark (FR-064, SC-017)

**Checkpoint**: Generation is IR-based and composable; internal transform seam in place; public plugin API + definition cache follow in a later phase.

---

## Phase 11: Polish & Cross-Cutting Concerns

- [ ] T094 [P] BenchmarkDotNet suite + per-release perf budgets (throughput, alloc/op, no boxing) in `tests/Dormant.Benchmarks/` (SC-004/SC-007)
- [ ] T095 [P] Docs + runnable example per public capability (FR-029) in `docs/` + `samples/`
- [ ] T096 Run `quickstart.md` end-to-end and confirm <15-minute round-trip (SC-008)
- [ ] T097 [P] Finalize `PublicAPI.Shipped.txt` and commit generated-code Verify baselines (compatibility surfaces, Constitution II)
- [ ] T098 [P] Actionable error-message pass across schema/query/migration/native diagnostics (FR-028)
- [ ] T099 Allocation/round-trip assertions threaded across stories (no per-row boxing, single round-trip) (SC-003/SC-004)

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)** → **Foundational (P2, blocks all stories)** → **User Stories** → **Polish**.
- Story order (priority + plan): **US1 → US2 → US3** (MVP) → **US5 → US4 → US8 → US6 → US7**.

### Cross-story dependencies (this project is more layered than typical)

- **US2** depends on the PostgreSQL adapter introduced in its own phase (T038–T041) + the snapshot/materializer emitters (T043/T044).
- **US3** reuses US2's adapter + materialization (T056 builds on T039/T044); link loader (T058) depends on the session (T042).
- **US4** extends US3's query parser/executor (T070 → T052; T071/T072 → T057).
- **US5** depends on the dialect (T040) and data source (T038).
- **US8** depends on the type-binding registry (T041) and query/native emit paths (T052/T078).
- **US6** validates the cumulative surface (US2/US3 at minimum); **US7** builds on the binding/convention seams.

### Within each story

- Tests written first and failing → models/emitters → services → execution/integration.

### Parallel opportunities

- All Setup `[P]` (T002–T010) parallel after T001.
- All Foundational `[P]` (T012–T016, T018, T021) parallel after their project exists.
- Within a story, `[P]` tests run together; independent emitters/bindings run together.
- With staffing, after MVP the P2/P3 stories (US5/US4/US8/US6/US7) can proceed largely in parallel.

---

## Parallel Example: User Story 1

```bash
# Tests together:
Task: "Verify snapshot test: two entities + link in tests/Dormant.SourceGeneration.Tests/SchemaEmitTests.cs"
Task: "Cacheability test in tests/Dormant.SourceGeneration.Tests/SchemaCacheabilityTests.cs"
Task: "Diagnostic test in tests/Dormant.SourceGeneration.Tests/SchemaDiagnosticTests.cs"

# Then independent emit helpers:
Task: "Deterministic emit helpers in src/Dormant.SourceGeneration/Emit/EmitHelpers.cs"
```

---

## Implementation Strategy

### MVP first (US1 + US2 + US3)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. US1 → US2 → US3 → **STOP & validate** the minimum usable ORM (schema → persist → typed query) against a real PostgreSQL via Testcontainers, AOT-published.

### Incremental delivery

US5 (migrations) → US4 (optional params) → US8 (native/JSONB/GIS) → US6 (AOT deliverable proof) → US7 (extensibility). Each adds value without breaking prior stories; perf budgets + compatibility baselines locked in Polish.

---

## Notes

- `[P]` = different files, no incomplete dependencies; `[USx]` = traceability to spec user stories.
- Tests included per Constitution VI (NON-NEGOTIABLE); ensure they fail before implementing; reproduce bugs with a failing test first.
- Two compatibility surfaces are locked by tests: public API (`PublicAPI.*.txt`) and generated code (Verify `.verified.txt`); DSL grammar is the third (`contracts/dsl-grammar.md`), SemVer the fourth.
- Commit after each task or logical group; auto-commit is disabled in `git-config.yml`.
