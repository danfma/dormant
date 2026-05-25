# Implementation Plan: Dormant — AOT-First, Schema-DSL ORM for .NET 10

**Branch**: `001-orm-aot-sourcegen` | **Date**: 2026-05-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-orm-aot-sourcegen/spec.md`

## Summary

Dormant is a managed, AOT-first .NET 10 ORM whose primary surface is DormantQL, its own schema/query DSL.
A Roslyn **incremental source generator** compiles DormantQL (from `AdditionalFiles`) into partial entity
types, distinct projection types, change-tracking snapshots, and typed query methods carrying **build-time
SQL** — so every query's result type is known at compile time and only values/predicates vary at runtime.
Persistence is an NHibernate-subset session with an identity map and snapshot-diff change tracking
(write only changed columns). Links carry explicit `Link<T>`/`LinkSet<T>` loaded/unloaded state with explicit
on-demand loading (no implicit lazy). PostgreSQL is the reference provider via the AOT-safe Npgsql slim path;
JSONB is built in; GIS ships as a companion package. Per-provider native types/functions are a deliberate,
explicitly non-portable escape hatch. The public async API prefers **`ValueTask`** (per direction) with
enforced discipline, the codebase is organized **feature-first** behind a **Ports & Adapters** boundary, and
tests run on **TUnit** verifying against **real providers in ephemeral Docker (Testcontainers)**.

Technical approach is fixed in [research.md](./research.md); design artifacts are
[data-model.md](./data-model.md), [contracts/](./contracts/), and [quickstart.md](./quickstart.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`); generators/analyzers target `netstandard2.0`.

**Primary Dependencies**: Roslyn (`Microsoft.CodeAnalysis.CSharp`) incremental generators; Npgsql (slim
data source path); `System.Text.Json` source generators (jsonb); NetTopologySuite (isolated to the GIS
companion package). Test/tooling: **TUnit** (on Microsoft.Testing.Platform), **Verify.TUnit** +
Verify.SourceGenerators, **Testcontainers** (PostgreSQL), BenchmarkDotNet,
`Microsoft.CodeAnalysis.PublicApiAnalyzers`, `Microsoft.VisualStudio.Threading.Analyzers`.

**Storage**: PostgreSQL (primary/reference provider; FR-024).

**Testing**: **TUnit** (source-generated, AOT-native) for unit/generator/integration tests; Verify
(`Verify.TUnit` + `Verify.SourceGenerators`) snapshots + cacheability tests for the generator;
**Testcontainers** provisioning a **real PostgreSQL in ephemeral Docker** (never mocks) for
connectivity/CRUD/query/migration verification (spec Clarifications/Assumptions); AOT publish smoke tests
(`PublishAot=true`, `TrimMode=full`); BenchmarkDotNet budgets with `MemoryDiagnoser`. Built-in TUnit
assertions are the default (Shouldly added only if insufficient). A Docker daemon is required locally + CI.

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

*GATE: must pass before Phase 0 and re-checked after Phase 1.* Constitution v2.0.1.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | DormantQL is the primary surface; every public capability has a doc + runnable example (FR-029); diagnostics source-located and actionable (FR-028). Quickstart proves <15 min (SC-008). | PASS |
| II. Interface & Compatibility Stability | Four surfaces have explicit contracts: public API (`contracts/public-api.md` + PublicApiAnalyzers baseline), generated code (`contracts/generated-code.md` + Verify snapshots), DSL (`contracts/dsl-grammar.md`), package/SemVer. Additive-only within MAJOR. | PASS |
| III. Statically-Known, Safe-by-Default | Build-time-known result types (FR-006); distinct projections (FR-007/008); `Link<T>` load-state (FR-009); no implicit lazy. Enforced by generated types. | PASS |
| IV. First-Class Tooling | CLI migrations (FR-020), DSL diagnostics analyzer, compatibility verification (PublicApiAnalyzers + Verify), single CI entry point (`build.sh` + `ci.yml`). | PASS |
| V. Performance by Default | Slim Npgsql path, `NpgsqlParameter<T>`/`GetFieldValue<T>` (no boxing), `[UnsafeAccessor]` (no reflection), build-time SQL, `ValueTask`-first, AOT smoke + BenchmarkDotNet budgets in CI. | PASS |
| VI. Quality & Testing (NON-NEGOTIABLE) | TUnit generator snapshot + cacheability tests, real-provider Testcontainers integration, AOT smoke, perf budgets, baselines — all CI-gated; repro-test-before-fix. | PASS |

**Result: no violations.** Complexity Tracking empty. The Ports & Adapters split is justified separation,
not gratuitous indirection; ports kept minimal to honor Principle I.

## Project Structure

### Documentation (this feature)

```text
specs/001-orm-aot-sourcegen/
├── plan.md              # This file
├── research.md          # Phase 0 — resolved decisions
├── data-model.md        # Phase 1 — build-time + runtime models
├── quickstart.md        # Phase 1 — <15 min round-trip
├── contracts/           # Phase 1 — public-api, ports, dsl-grammar, generated-code, cli
└── tasks.md             # Phase 2 — created by /speckit-tasks
```

### Source Code (repository root) — scaffolded & building (Phase 1 / T001–T011 ✅)

Feature-first folders inside each package; Ports & Adapters across packages (dependency rule inward).

```text
Dormant.sln
Directory.Build.props · Directory.Packages.props · global.json · nuget.config   # shared build config
build.sh · .github/workflows/ci.yml                                             # single entry point + CI

src/
├── Dormant.Abstractions/        # PORTS + stable kernel (compatibility surface). No external deps.
│   ├── Sessions/  Links/  Querying/  Ports/  + PublicAPI.Shipped/Unshipped.txt
├── Dormant.Core/                # provider-agnostic engine (depends only on Abstractions)
│   ├── Schema/ Modeling/ Querying/ Persistence/ Migrations/ Native/ Extensibility/ Diagnostics/
├── Dormant.SourceGeneration/    # Roslyn incremental generator + analyzer (netstandard2.0)
│   ├── Parsing/ Schema/ Query/ Native/ Diagnostics/
├── Dormant.Provider.PostgreSql/ # ADAPTER: Npgsql slim, dialect, scalar + jsonb bindings, command exec
├── Dormant.Spatial.PostgreSql/  # ADAPTER (companion): PostGIS geometry/geography, EWKB codec
└── Dormant.Tool/                # ADAPTER: dotnet tool `dormant` — migrations + schema validate

tests/
├── Dormant.SourceGeneration.Tests/     # TUnit + Verify snapshots + cacheability
├── Dormant.Core.Tests/                 # TUnit unit
├── Dormant.Provider.PostgreSql.Tests/  # TUnit + Testcontainers (real PostgreSQL)
├── Dormant.Spatial.PostgreSql.Tests/   # TUnit + Testcontainers (GIS; SC-013)
├── Dormant.Aot.SmokeTests/             # PublishAot + TrimMode=full, zero-warning assert
└── Dormant.Benchmarks/                 # BenchmarkDotNet perf budgets

samples/
└── Dormant.Sample.Quickstart/          # the quickstart.md app (doc + example, FR-029)
```

**Structure Decision**: Multi-package Ports & Adapters — `Dormant.Abstractions` holds ports + the stable
kernel, `Dormant.Core` is the provider-agnostic engine (feature-first folders), adapters
(`Provider.PostgreSql`, `Spatial.PostgreSql`, `Tool`) implement ports without leaking Npgsql/NTS inward,
`Dormant.SourceGeneration` emits code against the kernel. Maps the four compatibility surfaces to concrete,
independently-versionable artifacts and keeps GIS out of core (FR-044). **This structure is scaffolded and
builds clean** (13 projects, 0 warnings; TUnit harness green) — see tasks.md Phase 1.

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Phase notes

- **Phase 0 (done)**: [research.md](./research.md) — all unknowns resolved (Npgsql AOT path, jsonb,
  PostGIS, generator architecture, query medium, ValueTask policy, TUnit/Testcontainers tooling/CI).
- **Phase 1 design (done)**: [data-model.md](./data-model.md), [contracts/](./contracts/),
  [quickstart.md](./quickstart.md); agent context (`CLAUDE.md`) references this plan.
- **Implementation Phase 1 setup (done)**: tasks T001–T011 — solution scaffolded, builds 0/0, TUnit green.
- **Next (`/speckit-implement`)**: Phase 2 Foundational (T012–T022: kernel/ports, generator pipeline
  skeleton, diagnostics, `EquatableArray<T>`, test harnesses) → user stories in priority order
  US1 → US2 → US3 (MVP) → US5 → US4 → US8 → US6 → US7, with testing/CI gates threaded through.
