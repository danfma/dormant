# Phase 0 Research: Dormant — AOT-First, Schema-DSL ORM for .NET 10

**Feature**: `001-orm-aot-sourcegen` | **Date**: 2026-05-25

This document resolves the Technical Context unknowns and records the decisions that constrain the
design. Format per decision: **Decision / Rationale / Alternatives considered**. All decisions are
checked against the constitution (v2.0.0): AOT & performance by default, statically-known safe-by-default
data access, build-time SQL, four compatibility surfaces (API / package / generated code / DSL).

---

## 1. Runtime, language, packaging

**Decision**: Target `net10.0`, C# 14. Ship a multi-package product: `Dormant.Abstractions`,
`Dormant.Core`, `Dormant.SourceGeneration` (analyzer/generator), `Dormant.Provider.PostgreSql`,
`Dormant.Spatial.PostgreSql` (companion), `Dormant.Tool` (`dotnet tool`). Every shipped library sets
`<IsAotCompatible>true</IsAotCompatible>` (enables `IsTrimmable` + trim/AOT analyzers, .NET 10).
Central Package Management (`Directory.Packages.props`) + `Directory.Build.props` for shared settings.

**Rationale**: `IsAotCompatible` turns on the analyzers that enforce SC-001 at build time. Package split
maps to the one-directional dependency boundary (abstractions ← engine ← adapters) and lets GIS ship separately (FR-044).

**Alternatives**: Single package — rejected; couples the PostgreSQL/Npgsql adapter and GIS into the core,
violating the provider boundary (FR-024) and the "GIS not in core" decision (FR-044).

---

## 2. PostgreSQL data access (Npgsql) — AOT-safe path

**Decision**: Use the raw ADO.NET path through **`NpgsqlSlimDataSourceBuilder`** only. Opt in explicitly
to needed features (`EnableArrays()`, `EnableJsonTypes()`, etc.). Never use `NpgsqlDataSourceBuilder`
(non-slim), `GlobalTypeMapper`, `EnableDynamicJson()`, `EnableUnmappedTypes()`, or `EnableRecords*()` —
all carry `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. Bind parameters with generic
**`NpgsqlParameter<T>.TypedValue`** (no boxing) using **positional `$1,$2…` placeholders** (avoids the
named-parameter SQL rewrite). Read with generic **`reader.GetFieldValue<T>(ordinal)`** /
`GetFieldValueAsync<T>` (no boxing), guarded by `IsDBNull`/`IsDBNullAsync`. Map enums explicitly via the
generic `MapEnum<TEnum>`.

**Rationale**: Satisfies SC-001 (zero AOT warnings), SC-004 (no boxing), and "no runtime reflection on
hot paths" (FR-017). The source generator emits the per-column `NpgsqlParameter<T>` and `GetFieldValue<T>`
calls with types fixed at build time.

**Alternatives**: Convenience builder / dynamic JSON — rejected (AOT warnings, reflection). Dapper-style
reflection mapping — rejected (boxing, reflection, warm-up).

---

## 3. JSONB mapping — AOT-safe

**Decision**: Treat `jsonb` at the Npgsql boundary as `string`/UTF-8 `byte[]`, with (de)serialization via a
**source-generated `System.Text.Json` `JsonSerializerContext`** (emitted by `Dormant.SourceGeneration` for each
jsonb-mapped type). Register the type via `EnableJsonTypes()` only. Schemaless columns expose
`JsonDocument`/`JsonElement`.

**Rationale**: The only warning-free POCO↔jsonb path. Npgsql's native AOT-safe POCO API does not exist yet
(npgsql#5355 open, milestone 11.0.0), so we do not design around it.

**Alternatives**: `EnableDynamicJson()` — rejected (reflection, AOT-incompatible per Npgsql docs).

---

## 4. PostGIS / spatial (companion package) — zero-warning strategy

**Decision**: `Dormant.Spatial.PostgreSql` maps PostGIS `geometry`/`geography` through a **hand-written/
source-generated EWKB codec** over `reader.GetFieldValue<byte[]>` / a `byte[]` parameter with
`DataTypeName = "geometry"`. Default geometry type is NetTopologySuite's `Geometry` (parsed via our codec),
with the NTS dependency isolated to this package; we do **not** take the `Npgsql.NetTopologySuite` plugin in
core. If the NTS dependency trips `IL2104` (it is an unannotated `netstandard2.x` assembly), the warning is
contained to the companion package and addressed there (verified suppression or a no-NTS geometry type).

**Rationale**: The Npgsql NTS plugin itself is reflection-free, but the NTS assembly is unannotated and may
emit `IL2104`. Keeping GIS out of core (FR-044) means any residual warning never reaches a core consumer,
preserving SC-001 for the core product; SC-013 is validated for the companion package specifically.

**Alternatives**: Bundle the NTS plugin in core — rejected (FR-044, risks core AOT warnings). Pure custom
geometry type with no NTS — kept as a fallback if NTS warnings cannot be eliminated.

---

## 5. Source generation architecture

**Decision**: One `IIncrementalGenerator` pipeline (plus a companion `DiagnosticAnalyzer`):
- Root the pipeline at **`AdditionalTextsProvider`** filtered to DSL files (schema + query files);
  extract file **path + string content** at the edge (never hold `SourceText`/`Compilation`/`ISymbol`).
- Parse with a **hand-written incremental lexer/parser** producing an **equatable** AST/model
  (`record`/`readonly record struct` + a custom `EquatableArray<T>`).
- Emit via `RegisterSourceOutput`: partial entity types, per-entity **snapshot + diff comparer**, typed
  query methods + **prebuilt SQL**, native bindings, and STJ json contexts.
- Generated materialization uses a `[SetsRequiredMembers]` constructor on the entity partial (ordinary
  property setters) and public getters for reads — no reflection, no `[UnsafeAccessor]`, no
  compiler-internal backing-field names (see §10).
- **Determinism**: sort everything by `StringComparer.Ordinal`, format literals with
  `CultureInfo.InvariantCulture`, deterministic hint names, normalized newlines, no time/guid/random/paths
  (satisfies FR-004, SC-009’s determinism sibling).
- **Located diagnostics**: `Location.Create(filePath, TextSpan, LinePositionSpan)` from parser offsets
  (0-based); store equatable `DiagnosticInfo`/`LocationInfo` in the model, materialize `Diagnostic` only in
  the output stage. Hard errors from the generator; rich located warnings from the companion analyzer.

**Rationale**: Directly implements FR-003/FR-004 and the "no runtime query compilation" rule (FR-013):
SQL is text emitted at build time. Equatable models + `AdditionalTextsProvider` keep the IDE fast and the
output deterministic.

**Alternatives**: Reflection/`Reflection.Emit` mapping or `System.Linq.Expressions`-compiled queries —
rejected (AOT-incompatible, warm-up, boxing). `ISourceGenerator` (v1) — rejected (not incremental).

---

## 6. Query authoring medium & optional parameters

**Decision**: Queries are authored in **DormantQL query files** (`.dql`, alongside `.dqls` schema files) and compiled
by the generator into **typed methods** returning the generated entity/projection type plus the prebuilt
SQL — a query-file→generated-function model (one typed method + prebuilt SQL per query). **Optional parameters** (FR-012/FR-031) are
realized by emitting prebuilt SQL **fragments** whose inclusion is toggled at runtime by a tiny clause
assembler (string concatenation of pre-generated, parameterized fragments). This is fragment selection,
**not** query compilation, so FR-013 holds; the result type is fixed regardless of the chosen fragments.

**Rationale**: Keeps the DSL the primary surface (FR-002), guarantees build-time-known result types (FR-006),
and satisfies "no runtime query compilation" while still allowing conditional SQL.

**Alternatives**: Embed queries as attribute strings on C# methods — viable but less readable than dedicated
query files; kept as a possible secondary authoring form, not primary. LINQ provider — out of scope.

**v1 MVP slice (implemented)**: full-entity `select Entity` and flat scalar projection `select Entity { f… }`,
with conjunctive own-column `filter`, `order by`, and `limit`/`offset` (literal or parameter). Each query
compiles to an `ISession` extension method in a `partial static {Module}Queries` class, carrying build-time
SQL on a `CompiledQuery<TResult>` (now `{ PreparedStatement Statement; RowMaterializer<TResult> Materialize }`).
Full entities materialize via the entity's generated ctor; projections emit a distinct `record {Query}Result`
with exactly the requested members (so a non-projected field cannot compile — FR-008). Execution streams
through `ISession.QueryAsync`/`QuerySingleOrDefaultAsync`. Diagnostics: ORM010 (unknown entity), ORM011
(unknown column), ORM012 (undeclared parameter). **Deferred**: path-navigation filters, nested-link shapes,
optional-parameter fragments (US4), DSL DML, and projection into user-owned records (FR-050/T104).

**Nested-link single round-trip (FR-010) — decision (pending implementation, user-confirmed)**: build the
nested shape with PostgreSQL **JSON aggregation** (`json_build_object`/`json_agg`) in a single statement and
materialize via **System.Text.Json source generation** (AOT-safe, no cartesian blow-up). One
`JsonSerializerContext` **per assembly** (not per shape) lists every shape as a `[JsonSerializable]` type;
its `PropertyNamingPolicy` is **snake_case** to match DB column naming. Rejected: LEFT JOIN + client-side
grouping (cartesian row duplication); correlated subquery per link (N+1-like SQL, complicates streaming).

---

## 7. Async API shape (user directive: prefer ValueTask)

**Decision**: Public async surface returns **`ValueTask`/`ValueTask<T>`** by default (per the user
directive), under enforced **ValueTask discipline**: await at most once, never concurrently, never block,
`.AsTask()` to buffer. Multi-row reads return **`IAsyncEnumerable<T>`** (streaming, `[EnumeratorCancellation]`)
with a buffering `ToListAsync`/`Task<IReadOnlyList<T>>` convenience. Fall back to **`Task<T>`** only where a
result is cached/shared or callers are expected to `WhenAll`/store it. Every async method takes a trailing
`CancellationToken cancellationToken = default`, flowed downstream; library code uses `ConfigureAwait(false)`
throughout. Reserve `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]` for benchmarked,
always-async hot methods.

**Rationale**: Honors the directive while documenting the trade-off (Stephen Toub's default is `Task`).
ValueTask avoids `Task` allocations on synchronously-completing hot paths (identity-map/cache hits),
reinforcing SC-004/SC-007. Discipline is enforced by analyzers (next item), so the sharp edges are caught at
build time rather than left to convention.

**Alternatives**: `Task` everywhere (the doc default) — rejected per directive. Blanket pooling — rejected
(overhead usually exceeds gen-0 savings).

---

## 8. Tooling, testing, CI (Quality & Tooling principles)

**Decision**:
- **Analyzers/build**: `IsAotCompatible=true`; enable CA2012 (ValueTask) as warning, CA1068, CA2016, and
  `Microsoft.VisualStudio.Threading.Analyzers` (VSTHRD002/103/111); `Microsoft.CodeAnalysis.PublicApiAnalyzers`
  with `PublicAPI.Shipped/Unshipped.txt` on `Dormant.Abstractions` (the generated↔runtime contract baseline).
- **Test framework**: **TUnit** (source-generated, AOT-native, runs on Microsoft.Testing.Platform) —
  chosen over xUnit/VSTest for alignment with the AOT-first premise. TUnit's built-in assertions are the
  default; Shouldly is omitted unless they prove insufficient.
- **Generator tests**: `Verify.SourceGenerators` + `Verify.TUnit` snapshots (the committed `.verified.txt`
  files *are* the generated-code contract baseline) **plus** cacheability tests (`GeneratorDriverOptions(
  trackIncrementalGeneratorSteps: true)`, assert `Cached`/`Unchanged`).
- **Integration**: **TUnit + Testcontainers** (PostgreSQL) — verified against a real provider instance in
  ephemeral Docker, never mocks/in-memory; a Docker daemon is required locally and in CI.
- **AOT smoke**: a `tests/Dormant.Aot.SmokeTests` project published with `PublishAot=true` + `TrimMode=full`
  running the US2/US3/US8 scenarios; CI fails on any library-originated warning (SC-001/SC-006).
- **Perf budgets**: **BenchmarkDotNet** with `MemoryDiagnoser`; per-release budgets (latency/throughput/alloc)
  recorded and gated (Constitution V; SC-004/SC-007).
- **CLI tooling**: `Dormant.Tool` as a `dotnet tool` for migrations create/apply/rollback/status (FR-020),
  itself AOT-publishable (FR-023).
- Single documented entry point (build script / `Makefile`/`nuke`) running build+test+bench+pack in CI.

**Rationale**: Each principle (IV Tooling, V Performance, VI Quality) becomes an automated gate, not a
convention — required for the guarantees to be credible.

**Alternatives**: Manual perf/AOT checks — rejected (Constitution VI: skipped verification isn't "done").

---

## 9. Change tracking & materialization

**Decision**: Generated entities are mutable classes. On load, the session captures a **per-entity snapshot**
(generated `readonly record struct` of the mapped columns) read via public property getters; commit diffs
current vs snapshot with a generated comparer and writes only changed columns (FR-014). Links are generated
`Link<T>`/`LinkSet<T>` wrappers encoding loaded/unloaded state (FR-009); on-demand load transitions the
wrapper via an explicit session call.

**Rationale**: Implements the locked clarifications (mutable + snapshot diff; typed link load-state) with no
reflection/boxing.

**Alternatives**: Proxies / per-property dirty flags / immutable entities — rejected in clarification.

---

## 10. Module mapping, generated namespace & `required` members (FR-045..048)

**Decision**:
- **Module → DB schema (FR-045)**: the module name is the PostgreSQL schema; emitted DDL/SQL is
  schema-qualified and migration tooling issues `CREATE SCHEMA IF NOT EXISTS`.
- **Generated namespace (FR-046)**: the generator reads the consuming project's root namespace and the
  schema file's location from `AnalyzerConfigOptionsProvider.GlobalOptions`
  (`build_property.RootNamespace`, `build_property.ProjectDir`) and per-file options
  (`build_metadata.AdditionalFiles.RelativeDir` / deriving the path relative to the project), then
  computes `PascalCaseEachPart(rootNamespace + folderSegments + module)`. These config values are
  projected to small equatable strings before entering the pipeline (no `Compilation` capture, research §5).
- **Member syntax (FR-047)**: parser disambiguates `name: T` by checking the type token against the
  scalar `TypeMap` — a known scalar ⇒ property, otherwise ⇒ single link (validated against the entity
  set); `multi T` ⇒ multi link; trailing `?` ⇒ optional. No arrow, no `single` keyword.
- **`required` members + materialization (FR-048)**: emit `public required T Name { get; set; }` for
  non-nullable members. Materialize via a generated **`[SetsRequiredMembers] internal {Entity}(IFieldReader
  reader)` constructor on the entity partial** that assigns ordinary property setters; keep the public
  parameterless constructor so consumers still get `required`-init enforcement. Reads (INSERT params,
  snapshots) use public getters. No reflection, no `[UnsafeAccessor]`, no compiler-internal backing-field
  names.

**Rationale**: Namespaces read naturally to .NET developers while the DB schema stays named after the
module; `required` gives safe-by-default construction; the materialization constructor is idiomatic C#
(`[SetsRequiredMembers]` is exactly for this) and avoids the fragile, undocumented `<Prop>k__BackingField`
naming that `[UnsafeAccessor]` field access relied on.

**Alternatives**: bare-module namespace (poor .NET DX — rejected); **`[UnsafeAccessor]` to auto-property
backing fields** (rejected — relies on undocumented compiler field naming, fragile, and unnecessary for
mutable entities); GetX/SetX accessor pairs (rejected — pollutes the entity API);
`RuntimeHelpers.GetUninitializedObject` (skips initializers — rejected); `single` keyword for required
links (inconsistent with property optionality — rejected, see Clarifications).

---

## 11. Relationship model: Ref vocabulary, projection records, entity equality (FR-009/049/050/051)

**Decision**:
- **Rename `Link`→`Ref`; NHibernate collection kinds.** Single relationship → `Ref<T>`. Collections →
  `RefSet<T>` (unordered/unique), `RefList<T>` (ordered), `RefBag<T>` (unordered/duplicates),
  `RefMap<TKey,TValue>` (keyed). DSL: `owner: User`, `users: Set<User>`, `items: List<Item>`,
  `tags: Bag<Tag>`, `roles: Map<Role, Membership>`. Each is a generated `readonly struct` carrying an
  explicit `Unloaded` sentinel (default), so unfetched ≠ empty (FR-009). Generated relationship members
  get an `= …Unloaded` initializer (not `= []`) and are not `required` unless the schema marks them so.
- **Projection into user-owned records (FR-050).** Entities encode load state via `Ref` types (a small
  AOT, dependency-free `Dormant.Abstractions` reference). To keep domain/application code Dormant-free,
  a query may project into a **plain user-defined `record`/type** with no Dormant members — the
  generator emits a positional materializer that maps columns to the record's constructor parameters by
  name/order. Entities = persistence model; projections = the dependency-free boundary.
- **Entity identity equality (FR-051).** The generator emits `Equals`/`GetHashCode` based on the entity
  type + primary-key values; a transient entity (unset key) uses reference equality. An attribute (e.g.
  `[NoIdentityEquality]`) opts out. This is generated code (no reflection), supports the identity map and
  `RefSet` semantics. Projections are records → structural equality from the compiler.

**Rationale**: NHibernate-aligned collection semantics are more expressive and familiar than a single
"multi"; the `Unloaded` sentinel keeps the safe-by-default guarantee; projection-into-records is the
clean Clean-Architecture seam (you can't have both plain-POCO navigation and compile-time load safety on
the *same* type, so split entity vs projection); PK identity equality is the standard ORM/DDD default.

**Alternatives**: plain-POCO entities (lose load-state safety — rejected); a separate domain↔data mapping
layer (most decoupled, heavy — rejected for v1); `= []` collection default (erases unloaded≠empty —
rejected); equality opt-in only (less useful default — rejected, opt-out chosen).

---

## 12. Database naming conventions & overrides (FR-052..FR-057)

**Decision**: Database identifiers are derived from schema identifiers by a **naming convention**,
defaulting to **snake_case** (entity `RecentPost` → table `recent_post`; member `createdAt` →
`created_at`; native function names; the module's schema name). The convention is set **per project**
via an `AnalyzerConfigOptions` build property (e.g. `build_property.DormantNamingConvention`, values
`snake_case` (default) | `verbatim`), projected into the generator's equatable config alongside
`RootNamespace`/`ProjectDir`. Any single **table/column/function** carries an optional **explicit name
override** (a DSL annotation) that wins over the convention for that unit. Names are **resolved once**
into the schema model (`DbTableName`/`DbColumnName`/…), then consumed by `EntityBindingEmitter`,
`QueryEmitter`, and migration DDL — a single source of truth, so INSERT/SELECT/UPDATE/DELETE/query/DDL
never drift. Resolution is pure build-time string work (no runtime reflection/transform — FR-056). A
convention that collapses two members to the same identifier is a **source-located diagnostic** (no
ambiguous SQL — FR-057).

**Rationale**: snake_case is the PostgreSQL norm; a default + targeted overrides removes per-member
boilerplate while still mapping onto pre-existing databases. Resolving in the model (not at each emit
site) guarantees consistency and keeps emitters dumb. Build-property config matches the existing FR-046
namespace mechanism — no new config channel.

**Alternatives**: per-emit-site transformation (drift risk — rejected); attribute-only naming with no
project default (boilerplate — rejected); runtime name mapping (violates AOT/no-warm-up — rejected);
inflection/pluralization (`User`→`users`) — out of scope (transform, don't inflect).

## 13. Generated extension surface = C# 14 extension blocks (FR-058)

**Decision**: Generated query methods (and any future session-scoped extension members) are emitted
inside a **C# 14 extension block** — `partial static {Module}Queries { extension(ISession session) { … } }`
— rather than classic `this`-parameter static methods. **Rationale**: C# 14 / .NET 10 extension members
let the generated surface later add **extension properties and static members** (e.g. `session.Users`)
without changing the shape consumers bind to (Constitution II additive-only). **Alternatives**: classic
extension methods (can't grow to properties/statics — rejected); instance methods on a wrapper (extra
type, worse ergonomics — rejected). _Code drift_: the US3 MVP shipped classic extension methods; T108
applies the switch.

---

## Resolved unknowns summary

| Unknown | Resolution |
|---------|------------|
| Npgsql AOT path | Slim builder + `NpgsqlParameter<T>` + `GetFieldValue<T>` + positional params |
| jsonb AOT mapping | STJ source-generated context; bind as string/bytes; `EnableJsonTypes()` only |
| PostGIS zero-warning | Companion package, EWKB codec, NTS isolated (warning contained out of core) |
| Generator design | `IIncrementalGenerator` from `AdditionalTextsProvider`, equatable models, `[SetsRequiredMembers]` materialization ctor |
| Query medium | DSL query files → typed methods + prebuilt SQL; optional params = fragment toggling |
| Async shape | ValueTask-first (disciplined) + IAsyncEnumerable; Task only where shared/cached |
| Testing/CI | Verify + cacheability + Testcontainers + AOT smoke + BenchmarkDotNet + PublicApiAnalyzers |
