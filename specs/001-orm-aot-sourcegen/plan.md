# Implementation Plan: Dormant — AOT-First, Schema-DSL ORM for .NET 10

**Branch**: `001-orm-aot-sourcegen` | **Date**: 2026-05-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-orm-aot-sourcegen/spec.md`

## Summary

Dormant is a managed, AOT-first .NET 10 ORM whose primary surface is DormantQL, its own schema/query DSL.
A Roslyn **incremental source generator** compiles DormantQL (from `AdditionalFiles`) into partial entity
types, distinct projection types, change-tracking snapshots, and typed query methods carrying **build-time
SQL** — so every query's result type is known at compile time and only values/predicates vary at runtime.
A module maps to a **database schema**; generated types are placed in a **.NET-friendly namespace**
(`PascalCaseEachPart(rootNamespace + folders + module)`); members use a unified `name: [multi] Type[?]`
syntax (required by default, `?` optional, `multi` collection) and non-nullable members are emitted with
the C# `required` modifier. Persistence is an NHibernate-subset session with an identity map and
snapshot-diff change tracking (write only changed columns). Links carry explicit `Link<T>`/`LinkSet<T>`
loaded/unloaded state with explicit on-demand loading (no implicit lazy). PostgreSQL is the reference
provider via the AOT-safe Npgsql slim path; JSONB is built in; GIS ships as a companion package.
Per-provider native types/functions are a deliberate, non-portable escape hatch. The public async API
prefers **`ValueTask`** (disciplined), the codebase is **feature-first** behind a **Ports & Adapters**
boundary, and tests run on **TUnit** against **real providers in ephemeral Docker (Testcontainers)**.

Technical approach is fixed in [research.md](./research.md); design artifacts are
[data-model.md](./data-model.md), [contracts/](./contracts/), and [quickstart.md](./quickstart.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`); generators/analyzers target `netstandard2.0`.

**Primary Dependencies**: Roslyn (`Microsoft.CodeAnalysis.CSharp`) incremental generators; Npgsql (slim
data source path); `System.Text.Json` source generators (jsonb); NetTopologySuite (isolated to the GIS
companion package). Test/tooling: **TUnit** (Microsoft.Testing.Platform), **Verify.TUnit** +
Verify.SourceGenerators, **Testcontainers** (PostgreSQL), BenchmarkDotNet,
`Microsoft.CodeAnalysis.PublicApiAnalyzers`, `Microsoft.VisualStudio.Threading.Analyzers`.

**Storage**: PostgreSQL (primary/reference provider; FR-024). Each DormantQL module maps to a DB schema
(FR-045); DDL/SQL is schema-qualified.

**Testing**: **TUnit** for unit/generator/integration; Verify (`Verify.TUnit` + `Verify.SourceGenerators`)
snapshots + cacheability for the generator; **Testcontainers** real PostgreSQL in ephemeral Docker (never
mocks) for connectivity/CRUD/query/migration; AOT publish smoke (`PublishAot`, `TrimMode=full`);
BenchmarkDotNet budgets. Docker daemon required locally + CI. Tests run via direct MTP hosts (`./build.sh
test`) since the .NET 10 SDK dropped the legacy VSTest `dotnet test` path.

**Target Platform**: cross-platform .NET 10, Native AOT + full trimming supported.

**Project Type**: managed multi-package library + source generator + `dotnet tool` CLI.

**Performance Goals**: one round-trip per shaped fetch (SC-003); no per-row boxing (SC-004); no first-use
warm-up (SC-006); throughput ≥ and alloc/op < mainstream baseline (SC-007). Per-release budgets CI-gated.

**Constraints**: zero library-originated trimming/AOT warnings (SC-001); no runtime reflection or runtime
query compilation on hot paths (FR-013/FR-017); build-time SQL; `ValueTask`-first; deterministic
generation (FR-004). Generator reads `build_property.RootNamespace` + project dir + each schema file's
relative path from `AnalyzerConfigOptions` to compute namespaces (FR-046); non-nullable members emit C#
`required` and are materialized via a `[SetsRequiredMembers]` ctor invoked through `[UnsafeAccessor]`
(FR-048).

**Scale/Scope**: v1 (Tier A) = schema + links + shapes/projections + optional params + basic query/DML +
migrations CLI + AOT + JSONB native + GIS companion. Tier B deferred per FR-035.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.* Constitution v2.0.1.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | DormantQL primary surface; .NET-friendly generated namespaces (FR-046); `required` for safe construction (FR-048); doc + example per capability (FR-029); located, actionable diagnostics (FR-028); <15-min quickstart (SC-008). | PASS |
| II. Interface & Compatibility Stability | Four contracts: public API (+PublicApiAnalyzers baseline), generated code (+Verify snapshots), DSL grammar, package/SemVer. Additive within MAJOR. | PASS |
| III. Statically-Known, Safe-by-Default | Build-time-known result types (FR-006); distinct projections (FR-007/008); `Link<T>` load-state (FR-009); required-by-default members (FR-047/048); no implicit lazy. | PASS |
| IV. First-Class Tooling | CLI migrations (FR-020), DSL diagnostics analyzer, compatibility verification, single CI entry point (`build.sh` + `ci.yml`). | PASS |
| V. Performance by Default | Npgsql slim, `NpgsqlParameter<T>`/`GetFieldValue<T>` (no boxing), `[UnsafeAccessor]` (no reflection, incl. `required` materialization), build-time SQL, `ValueTask`-first, AOT smoke + benchmarks. | PASS |
| VI. Quality & Testing (NON-NEGOTIABLE) | TUnit generator snapshot + cacheability, real-provider Testcontainers integration, AOT smoke, perf budgets, baselines — CI-gated; repro-test-before-fix. | PASS |

**Result: no violations.** Complexity Tracking empty. Ports & Adapters split is justified separation; ports kept minimal (Principle I).

## Project Structure

### Documentation (this feature)

```text
specs/001-orm-aot-sourcegen/
├── plan.md  research.md  data-model.md  quickstart.md
├── contracts/   # public-api, ports, dsl-grammar, generated-code, cli
└── tasks.md
```

### Source Code (repository root) — scaffolded & building; Foundational + US1 implemented

Feature-first folders inside each package; Ports & Adapters across packages (dependency rule inward).

```text
Dormant.sln · Directory.Build.props · Directory.Packages.props · global.json · nuget.config
build.sh · .github/workflows/ci.yml · .editorconfig

src/
├── Dormant.Abstractions/        # PORTS + stable kernel (Sessions, Links, Querying, Ports)
├── Dormant.Core/                # engine (Schema, Modeling, Querying, Persistence, Migrations, Native, Extensibility, Diagnostics)
├── Dormant.SourceGeneration/    # Roslyn incremental generator + analyzer (Parsing, Schema, Emit, Diagnostics)
├── Dormant.Provider.PostgreSql/ # ADAPTER: Npgsql slim, dialect, jsonb
├── Dormant.Spatial.PostgreSql/  # ADAPTER (companion): PostGIS EWKB codec
└── Dormant.Tool/                # ADAPTER: dotnet tool `dormant`

tests/  Dormant.{Core,SourceGeneration,Provider.PostgreSql,Spatial.PostgreSql}.Tests · Dormant.Aot.SmokeTests · Dormant.Benchmarks
samples/ Dormant.Sample.Quickstart
```

**Structure Decision**: Multi-package Ports & Adapters mapping the four compatibility surfaces to
independently-versionable artifacts; GIS out of core (FR-044). Scaffolded and building (13 projects, 0
warnings); Foundational kernel/generator + US1 schema→entities implemented and tested.

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Phase notes

- **Phase 0 (done)**: [research.md](./research.md) — all unknowns resolved, incl. §10 (module→schema,
  namespace formula via `AnalyzerConfigOptions`, `required`-member materialization via
  `[SetsRequiredMembers]` + `[UnsafeAccessor]`).
- **Phase 1 design (done)**: data-model, contracts, quickstart; `CLAUDE.md` references this plan.
- **Implementation done**: Setup (T001–T011), Foundational (T012–T022 except deferred T017), US1
  schema→entities (T023–T033 except deferred T031).
- **⚠️ US1 revision required**: the committed US1 generator predates FR-045..048 and must be updated —
  namespace formula (FR-046) + `required` (FR-048), `name: [multi] Type[?]` member syntax (FR-047), and
  the sample/tests/quickstart schemas. Do this before/with US2.
- **Next (`/speckit-implement`)**: apply the US1 revision, then US2 (persist/session: Npgsql adapter +
  snapshot-diff + materializer + `[UnsafeAccessor]`/`[SetsRequiredMembers]` (T031)) → US3 → US5 → US4 →
  US8 → US6 → US7, threading testing/CI gates through.
