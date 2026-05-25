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
- [ ] T105 [US5] Schema-qualified DDL/SQL + `CREATE SCHEMA IF NOT EXISTS <module>` in migration generation (`src/Dormant.Core/Migrations/` + `src/Dormant.Provider.PostgreSql/Migrations/DdlGenerator.cs`); integration test asserts tables created in the module schema (FR-045)
- [X] T106 [P] [US1] Update generator tests + sample for the Ref model (`SchemaEmitTests` asserts Ref/RefSet/Unloaded/equality; `RefTests` renamed from LinkTests; `app.dqls` uses `Set<Post>`); builds + runs green
- [ ] T107 [US1] Replace `[UnsafeAccessor]` materialization (fragile backing-field hack) with a generated `[SetsRequiredMembers] internal {Entity}(IFieldReader reader)` ctor on the entity partial (ordinary setters) + retained public parameterless ctor (`EntityEmitter`); binding `Materialize` → `new {Entity}(reader)`, INSERT/snapshot reads via public getters, drop field accessors + `Create()` (`EntityBindingEmitter`); update SchemaEmitTests + re-run the Testcontainers round-trip (FR-048)

**Checkpoint**: Kernel + generator on the Ref model (collections, Unloaded sentinel, PK equality, record projections); sample builds + runs; generator tests green. Resume US2 after this.

---

## Phase 4: User Story 2 - Persist and load full entities through a session (Priority: P1)

**Goal**: Session CRUD on PostgreSQL with identity map, snapshot-diff change tracking (only changed columns), optimistic concurrency.

**Independent Test**: Insert+commit→row exists; load+modify one field+commit→only that column written; delete→gone; two sessions same row→conflict. (FR-014/015, SC)

### Tests for User Story 2

- [X] T034 [P] [US2] Integration (Testcontainers): insert + commit → row exists in `tests/Dormant.Provider.PostgreSql.Tests/CrudTests.cs` _(insert→get-by-key round-trip against real PostgreSQL via generated binding + session; 2/2 green)_
- [ ] T035 [P] [US2] Integration: modify one field + commit → only that column changed in `tests/Dormant.Provider.PostgreSql.Tests/ChangeTrackingTests.cs`
- [ ] T036 [P] [US2] Integration: delete + commit → row gone in `tests/Dormant.Provider.PostgreSql.Tests/CrudTests.cs`
- [ ] T037 [P] [US2] Integration: two sessions same row → `ConcurrencyConflictException` in `tests/Dormant.Provider.PostgreSql.Tests/ConcurrencyTests.cs`

### Implementation for User Story 2

- [X] T038 [US2] `IDataSource`/`IDbSession` over `NpgsqlSlimDataSourceBuilder` (connection + transaction) in `src/Dormant.Provider.PostgreSql/PostgreSqlDataSource.cs` _(+ PostgreSqlSession + DormantPostgres factory; verified by a real Testcontainers round-trip)_
- [X] T039 [US2] No-boxing parameter writer (`NpgsqlParameter<T>.TypedValue`) + reader (`GetFieldValue<T>`) in `src/Dormant.Provider.PostgreSql/Io/`
- [X] T040 [US2] `PostgreSqlDialect` (positional `$n`, identifier quoting, DDL type names) in `src/Dormant.Provider.PostgreSql/PostgreSqlDialect.cs` _(quoting + `$n` + Supports; DDL type-name mapping added with US5 migrations)_
- [X] T041 [P] [US2] Built-in scalar `ITypeBinding<T>` set in `src/Dormant.Provider.PostgreSql/Bindings/ScalarBindings.cs` _(satisfied by the generic no-boxing IO path — `IFieldReader.GetValue<T>`/`IParameterWriter.Write<T>` route built-in scalars through Npgsql directly; a per-type `ITypeBindingRegistry` is only needed for custom handlers, deferred to US7)_
- [X] T042 [US2] Session / Unit of Work + identity map in `src/Dormant.Core/Persistence/Session.cs` (depends T038, T013) _(+ `SessionFactory`, `DormantPostgres.CreateSessionFactory`, and the `IEntityBinding<T>`/`EntityBindings` registry bridging generated code↔session; AddAsync/GetAsync/CommitAsync wired. Remove/Query/Load deferred to later slices)_
- [ ] T043 [US2] Emit per-entity snapshot struct + diff comparer in `src/Dormant.SourceGeneration/Schema/SnapshotEmitter.cs`
- [X] T044 [US2] Emit per-entity materializer (`Create()` via `[UnsafeAccessor]` ctor past `required`, field accessors, `Materialize(IFieldReader)` reading value columns by ordinal, no boxing/reflection) in `src/Dormant.SourceGeneration/Schema/MaterializerEmitter.cs` _(value columns only; references left Unloaded — link materialization with US3 shapes)_
- [ ] T045 [US2] Change-tracking commit: INSERT / UPDATE(changed columns only) / DELETE in `src/Dormant.Core/Persistence/ChangeTracker.cs` (depends T043)
- [ ] T046 [US2] Optimistic concurrency token check + conflict surfacing in `src/Dormant.Core/Persistence/ChangeTracker.cs`
- [ ] T047 [US2] DSL DML (insert/update/delete) parse + SQL emit in `src/Dormant.SourceGeneration/Query/DmlEmitter.cs`

**Checkpoint**: Full-entity persistence round-trip works.

---

## Phase 5: User Story 3 - Query exact result types: entity or projection, nested links (Priority: P1)

**Goal**: DSL query → full entity or distinct projection type; nested links in one round-trip; non-fetched field access is a compile error; SQL at build time.

**Independent Test**: Projection `{ id, name, posts: { title } }` exposes exactly those members, `posts` populated in one round-trip, referencing `email` fails to compile. (FR-006/007/008/010/013, SC-002/003)

### Tests for User Story 3

- [ ] T048 [P] [US3] Verify snapshot: projection → distinct type with exactly requested members in `tests/Dormant.SourceGeneration.Tests/ProjectionEmitTests.cs`
- [ ] T049 [P] [US3] Integration: nested-link fetch executes in exactly one round-trip (statement count) in `tests/Dormant.Provider.PostgreSql.Tests/RoundTripTests.cs`
- [ ] T050 [P] [US3] Negative compile test: referencing a non-fetched field fails to compile in `tests/Dormant.SourceGeneration.Tests/ProjectionNegativeTests.cs`
- [ ] T051 [P] [US3] Integration: full-entity query populates all mapped columns in `tests/Dormant.Provider.PostgreSql.Tests/EntityQueryTests.cs`

### Implementation for User Story 3

- [ ] T052 [US3] Query parser (select, shape, path nav, filter, order by, limit/offset) → query AST in `src/Dormant.SourceGeneration/Parsing/QueryParser.cs`
- [ ] T053 [US3] Projection type emitter (distinct types, nested shapes) in `src/Dormant.SourceGeneration/Query/ProjectionEmitter.cs`
- [ ] T054 [US3] Select SQL builder incl. single-round-trip nested links in `src/Dormant.SourceGeneration/Query/SelectSqlBuilder.cs`
- [ ] T055 [US3] Typed query method + `CompiledQuery<T>` emit in `src/Dormant.SourceGeneration/Query/QueryMethodEmitter.cs`
- [ ] T056 [US3] Result materialization for entities + projections (no boxing) in `src/Dormant.Core/Querying/Materialization.cs`
- [ ] T057 [US3] Query execution streaming via `IDbSession.QueryAsync` → `IAsyncEnumerable<T>` (+ `QuerySingleOrDefaultAsync`) in `src/Dormant.Core/Querying/QueryExecutor.cs`
- [ ] T058 [US3] Link load-state population + explicit on-demand `LoadAsync` in `src/Dormant.Core/Persistence/LinkLoader.cs` (depends T042)

**Checkpoint**: 🎯 MVP complete (US1+US2+US3 = minimum usable ORM).

---

## Phase 6: User Story 5 - Evolve the schema with migration tooling (Priority: P2)

**Goal**: CLI create/apply/rollback/status; incremental diffs; destructive ops flagged.

**Independent Test**: Initial migration apply → schema matches; change → incremental migration = diff only; rollback restores prior state; destructive op flagged. (FR-020/021/022, SC-010)

### Tests for User Story 5

- [ ] T059 [P] [US5] Integration: initial migration apply → DB schema matches in `tests/Dormant.Provider.PostgreSql.Tests/MigrationApplyTests.cs`
- [ ] T060 [P] [US5] Integration: incremental migration contains only the diff in `tests/Dormant.Provider.PostgreSql.Tests/MigrationDiffTests.cs`
- [ ] T061 [P] [US5] Integration: rollback restores prior state in `tests/Dormant.Provider.PostgreSql.Tests/MigrationRollbackTests.cs`
- [ ] T062 [P] [US5] Integration: destructive (data-loss) op flagged, not auto-applied in `tests/Dormant.Provider.PostgreSql.Tests/MigrationSafetyTests.cs`

### Implementation for User Story 5

- [ ] T063 [US5] Migration model + schema snapshot + diff engine in `src/Dormant.Core/Migrations/`
- [ ] T064 [US5] DDL generation (create/alter/drop) via dialect in `src/Dormant.Provider.PostgreSql/Migrations/DdlGenerator.cs`
- [ ] T065 [US5] `IMigrationStore` impl (applied/pending tracking) in `src/Dormant.Provider.PostgreSql/Migrations/MigrationStore.cs`
- [ ] T066 [US5] CLI commands `migrations add/apply/rollback/status` + `schema validate` in `src/Dormant.Tool/Commands/`
- [ ] T067 [US5] Destructive-op detection + explicit confirm gate in `src/Dormant.Core/Migrations/DestructiveGuard.cs`

**Checkpoint**: Migration workflow usable end-to-end via CLI only.

---

## Phase 7: User Story 4 - Dynamic queries via optional parameters (Priority: P2)

**Goal**: One query with optional/conditional params; executed SQL varies, result type fixed.

**Independent Test**: Two optional filters; run with none/one/both → identical result type, correctly filtered rows. (FR-012/031, SC-005)

### Tests for User Story 4

- [ ] T068 [P] [US4] Integration: optional params none/one/both → same result type, correct rows in `tests/Dormant.Provider.PostgreSql.Tests/OptionalParamsTests.cs`
- [ ] T069 [P] [US4] Verify: single result type generated once for all parameter combinations in `tests/Dormant.SourceGeneration.Tests/OptionalParamTypeTests.cs`

### Implementation for User Story 4

- [ ] T070 [US4] Extend query parser: required/optional params + coalesce (`??`) in `src/Dormant.SourceGeneration/Parsing/QueryParser.cs`
- [ ] T071 [US4] Prebuilt SQL fragments + runtime fragment-toggle assembler (no query compilation) in `src/Dormant.Core/Querying/FragmentAssembler.cs`
- [ ] T072 [US4] Optional-parameter binding (omit clause when absent) in `src/Dormant.Core/Querying/QueryExecutor.cs`

**Checkpoint**: Conditional queries with stable result types.

---

## Phase 8: User Story 8 - Database-native types and functions (JSONB, GIS) (Priority: P2)

**Goal**: Per-provider native types/functions (typed catalog + raw typed escape), explicitly non-portable with build diagnostics; JSONB in core, GIS via companion.

**Independent Test**: `jsonb` round-trip + native containment query (build-time-known type, zero AOT warnings); PostGIS spatial query via companion; unsupported-provider target → located diagnostic. (FR-038..044, SC-012/013/014)

### Tests for User Story 8

- [ ] T073 [P] [US8] Integration: `jsonb` round-trip + containment operator, result type build-time-known in `tests/Dormant.Provider.PostgreSql.Tests/JsonbTests.cs`
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

- [ ] T084 [P] [US6] AOT smoke: publish US2/US3 scenarios `PublishAot=true`+`TrimMode=full`, assert zero library-originated warnings in `tests/Dormant.Aot.SmokeTests/CoreAotTests.cs`
- [ ] T085 [P] [US6] AOT smoke: results identical to non-trimmed run in `tests/Dormant.Aot.SmokeTests/ParityTests.cs`
- [ ] T086 [P] [US6] AOT smoke: cold start, first query no warm-up step in `tests/Dormant.Aot.SmokeTests/ColdStartTests.cs`

### Implementation for User Story 6

- [ ] T087 [US6] Confirm `IsAotCompatible` across all shipped libs; resolve any trim/AOT warnings in `src/*`
- [ ] T088 [US6] CI gate: fail build on any library-originated AOT/trim warning in `.github/workflows/ci.yml`

**Checkpoint**: AOT deliverable proven end-to-end.

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
