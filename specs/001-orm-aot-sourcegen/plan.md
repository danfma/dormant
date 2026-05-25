# Implementation Plan: Dormant — AOT-First, Schema-DSL ORM for .NET 10

**Branch**: `001-orm-aot-sourcegen` | **Date**: 2026-05-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-orm-aot-sourcegen/spec.md`

## Summary

Dormant is a managed, AOT-first .NET 10 ORM whose primary surface is DormantQL, its own schema/query DSL.
A Roslyn **incremental source generator** compiles the DSL (from `AdditionalFiles`) into partial entity
types, distinct projection types, change-tracking snapshots, and typed query methods carrying **build-time
SQL** — so every query's result type is known at compile time and only values/predicates vary at runtime.
Persistence is an NHibernate-subset session with an identity map and snapshot-diff change tracking
(write only changed columns). Links carry explicit `Link<T>`/`LinkSet<T>` loaded/unloaded state with explicit
on-demand loading (no implicit lazy). PostgreSQL is the reference provider via the AOT-safe Npgsql slim path;
JSONB is built in; GIS ships as a companion package. Per-provider native types/functions are a deliberate,
explicitly non-portable escape hatch. The public async API prefers **`ValueTask`** (per direction) with
enforced discipline, and the codebase is organized **feature-first** behind a **Ports & Adapters** boundary.

Technical approach is fixed in [research.md](./research.md); the design artifacts are
[data-model.md](./data-model.md), [contracts/](./contracts/), and [quickstart.md](./quickstart.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`); generators/analyzers target `netstandard2.0`.

**Primary Dependencies**: Roslyn (`Microsoft.CodeAnalysis.CSharp`) incremental generators; Npgsql (slim
data source path); `System.Text.Json` source generators (jsonb); NetTopologySuite (isolated to the GIS
companion package). Test/tooling: xUnit, Verify.SourceGenerators, Testcontainers, BenchmarkDotNet,
`Microsoft.CodeAnalysis.PublicApiAnalyzers`, `Microsoft.VisualStudio.Threading.Analyzers`.

**Storage**: PostgreSQL (primary/reference provider; FR-024).

**Testing**: xUnit unit tests; Verify snapshot + cacheability tests for the generator; Testcontainers
PostgreSQL integration; AOT publish smoke tests (`PublishAot=true`, `TrimMode=full`); BenchmarkDotNet
budgets with `MemoryDiagnoser`.

**Target Platform**: cross-platform .NET 10 (Linux/Windows/macOS), Native AOT + full trimming supported.

**Project Type**: managed multi-package library + source generator + `dotnet tool` CLI.

**Performance Goals**: one round-trip per shaped fetch (SC-003); no per-row boxing of value columns
(SC-004); no first-use warm-up (SC-006); throughput ≥ and alloc/op < mainstream baseline ORM (SC-007).
Explicit per-release budgets declared and CI-gated (Constitution V).

**Constraints**: zero library-originated trimming/AOT warnings (SC-001); no runtime reflection or runtime
query compilation on hot paths (FR-013/FR-017); build-time SQL; `ValueTask`-first async; deterministic
generation (FR-004).

**Scale/Scope**: v1 (Tier A) = schema + links + shapes/projections + optional params + basic
query/DML + migrations CLI + AOT + JSONB native + GIS companion. Tier B deferred per FR-035.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.* Constitution v2.0.0.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | DSL is the primary surface; every public capability has a doc + runnable example (FR-029); diagnostics are source-located and actionable (FR-028). Quickstart proves <15 min (SC-008). | PASS — quickstart + located-diagnostic design |
| II. Interface & Compatibility Stability | Four surfaces have explicit contracts: public API (`contracts/public-api.md` + PublicApiAnalyzers baseline), generated code (`contracts/generated-code.md` + Verify snapshots), DSL (`contracts/dsl-grammar.md`), package/SemVer. Additive-only within MAJOR. | PASS — baselines defined |
| III. Statically-Known, Safe-by-Default | Build-time-known result types (FR-006); distinct projections (FR-007/FR-008); `Link<T>` load-state (FR-009); no implicit lazy. Enforced by generated types. | PASS — design enforces by construction |
| IV. First-Class Tooling | CLI migrations (FR-020), DSL diagnostics analyzer, compatibility verification (PublicApiAnalyzers + Verify), single CI entry point. | PASS — tooling projects planned |
| V. Performance by Default | Slim Npgsql path, `NpgsqlParameter<T>`/`GetFieldValue<T>` (no boxing), `[UnsafeAccessor]` (no reflection), build-time SQL, `ValueTask`-first, AOT smoke + BenchmarkDotNet budgets in CI. | PASS — measured, gated |
| VI. Quality & Testing (NON-NEGOTIABLE) | Generator snapshot + cacheability tests, Testcontainers integration, AOT smoke, perf budgets, baselines — all CI-gated; repro-test-before-fix. | PASS — CI gates planned |

**Result: no violations.** Complexity Tracking is empty. The Ports & Adapters split (Abstractions / Core /
adapters) is justified separation, not gratuitous indirection; ports are kept minimal to honor Principle I.

## Project Structure

### Documentation (this feature)

```text
specs/001-orm-aot-sourcegen/
├── plan.md              # This file
├── research.md          # Phase 0 — resolved decisions
├── data-model.md        # Phase 1 — build-time + runtime models
├── quickstart.md        # Phase 1 — <15 min round-trip
├── contracts/           # Phase 1 — public-api, ports, dsl-grammar, generated-code, cli
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

Feature-first folders inside each package; Ports & Adapters across packages (dependency rule inward).

```text
Dormant.sln
Directory.Build.props · Directory.Packages.props        # shared settings + central package mgmt

src/
├── Dormant.Abstractions/        # PORTS + stable kernel (compatibility surface). No external deps.
│   ├── Sessions/                # ISession, ISessionFactory, ConcurrencyConflictException
│   ├── Links/                   # Link<T>, LinkSet<T>
│   ├── Querying/                # CompiledQuery<T>, FieldReader, ParameterWriter
│   ├── Ports/                   # IDataSource, IDbSession, ISqlDialect, ITypeBinding<T>, IMigrationStore, INativeFunctionCatalog
│   └── PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt
├── Dormant.Core/                # provider-agnostic engine; depends only on Abstractions
│   ├── Schema/                  # schema model + validation
│   ├── Modeling/                # entity/link runtime support, snapshot infra
│   ├── Querying/                # result materialization, optional-param fragment assembly
│   ├── Persistence/             # session, unit of work, identity map, change tracking
│   ├── Migrations/              # migration model, diff, runner orchestration
│   ├── Native/                  # native type-binding + function-catalog abstractions
│   ├── Extensibility/           # type handlers, conventions
│   └── Diagnostics/             # error model
├── Dormant.SourceGeneration/           # Roslyn incremental generator + analyzer (netstandard2.0)
│   ├── Parsing/                 # lexer, parser, equatable AST, EquatableArray<T>
│   ├── Schema/                  # emit partial entities, snapshots, UnsafeAccessors
│   ├── Query/                   # emit typed methods + prebuilt SQL fragments
│   ├── Native/                  # emit native bindings + STJ JsonSerializerContext
│   └── Diagnostics/             # DiagnosticDescriptors + companion DiagnosticAnalyzer
├── Dormant.Provider.PostgreSql/ # ADAPTER: Npgsql slim, dialect, scalar + jsonb bindings, command exec
├── Dormant.Spatial.PostgreSql/  # ADAPTER (companion package): PostGIS geometry/geography, EWKB codec
└── Dormant.Tool/                 # ADAPTER: dotnet tool — migrations add/apply/rollback/status, schema validate

tests/
├── Dormant.SourceGeneration.Tests/         # Verify snapshots + cacheability (trackIncrementalGeneratorSteps)
├── Dormant.Core.Tests/              # unit
├── Dormant.Provider.PostgreSql.Tests/  # Testcontainers integration
├── Dormant.Spatial.PostgreSql.Tests/   # GIS round-trip + AOT (SC-013)
├── Dormant.Aot.SmokeTests/          # PublishAot + TrimMode=full, run scenarios, assert zero warnings
└── Dormant.Benchmarks/              # BenchmarkDotNet perf budgets

samples/
└── Dormant.Sample.Quickstart/       # the quickstart.md app (doc + example, FR-029)
```

**Structure Decision**: Multi-package solution realizing Ports & Adapters — `Dormant.Abstractions` holds
ports + the stable kernel, `Dormant.Core` is the provider-agnostic engine (feature-first folders),
adapters (`Provider.PostgreSql`, `Spatial.PostgreSql`, `Tool`) implement ports without leaking Npgsql/NTS
inward, and `Dormant.SourceGeneration` emits code against the kernel. This maps the four compatibility surfaces to
concrete, independently-versionable artifacts and keeps GIS out of core (FR-044).

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Phase notes

- **Phase 0 (done)**: [research.md](./research.md) — all unknowns resolved (Npgsql AOT path, jsonb,
  PostGIS, generator architecture, query medium, ValueTask policy, tooling/CI).
- **Phase 1 (done)**: [data-model.md](./data-model.md), [contracts/](./contracts/),
  [quickstart.md](./quickstart.md); agent context (`CLAUDE.md`) updated to reference this plan.
- **Phase 2 (next, `/speckit-tasks`)**: derive a dependency-ordered `tasks.md`. Suggested ordering follows
  the spec's user-story priorities: US1 (schema→entities generator) → US2/US3 (session + query/projection)
  → US5 (migrations CLI) → US4 (optional params) → US6 (AOT smoke) → US8 (native/JSONB/GIS) → US7
  (extensibility), with the testing/CI gates (Verify, cacheability, Testcontainers, AOT, benchmarks)
  threaded through as each capability lands.
