# Implementation Plan: Dormant — AOT-First, Schema-DSL ORM for .NET 10

**Branch**: `001-orm-aot-sourcegen` | **Date**: 2026-05-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-orm-aot-sourcegen/spec.md`

## Summary

Dormant is a managed, AOT-first .NET 10 ORM whose primary surface is DormantQL, its own schema/query DSL.
A Roslyn **incremental source generator** compiles DormantQL (from `AdditionalFiles`) into partial entity
types, distinct projection types, change-tracking snapshots, and typed query methods carrying **build-time
SQL** — so every query's result type is known at compile time and only values/predicates vary at runtime.
A module maps to a **database schema**; generated types use a **.NET-friendly namespace**
(`PascalCaseEachPart(rootNamespace + folders + module)`); members use `name: TypeExpr[?]` (value ⇒
property; single ref `name: Target`; collections `Set/List/Bag/Map`). Relationships are generated
reference structs — **`Ref<T>` / `RefSet<T>` / `RefList<T>` / `RefBag<T>` / `RefMap<K,V>`** — each with an
explicit **Unloaded** sentinel (never `= []`), so unfetched ≠ empty. Non-nullable members emit C#
`required`; entities get **primary-key identity equality** by default. Persistence is an NHibernate-subset
session with an identity map and snapshot-diff change tracking. PostgreSQL is the reference provider via
the AOT-safe Npgsql slim path; JSONB built in; GIS as a companion package. Per-provider native
types/functions are a deliberate, non-portable escape hatch. The public async API prefers **`ValueTask`**;
the codebase is **feature-first** with dependencies pointing one direction inward (semantic names, no
"Ports" jargon); tests run on **TUnit** against **real providers in ephemeral Docker (Testcontainers)**.

**Clean-Architecture seam**: entities carry `Ref` types (a minimal, AOT, dependency-free
`Dormant.Abstractions` reference) and are the persistence model; **projections materialize into
user-owned plain records with zero Dormant types** so domain/application code stays Dormant-free (FR-050).

Technical approach is fixed in [research.md](./research.md); design artifacts are
[data-model.md](./data-model.md), [contracts/](./contracts/), and [quickstart.md](./quickstart.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`); generators/analyzers target `netstandard2.0`.

**Primary Dependencies**: Roslyn incremental generators; Npgsql (slim data source); `System.Text.Json`
source generators (jsonb); NetTopologySuite (GIS companion only). Test/tooling: TUnit
(Microsoft.Testing.Platform), Verify.TUnit + Verify.SourceGenerators, Testcontainers (PostgreSQL),
BenchmarkDotNet, Microsoft.CodeAnalysis.PublicApiAnalyzers, Microsoft.VisualStudio.Threading.Analyzers.

**Storage**: PostgreSQL (primary/reference; FR-024). Module → DB schema (FR-045); DDL/SQL schema-qualified.

**Testing**: TUnit unit/generator/integration; Verify snapshots + cacheability for the generator;
Testcontainers real PostgreSQL in ephemeral Docker (never mocks); AOT publish smoke; BenchmarkDotNet
budgets. Docker daemon required. Tests run via direct MTP hosts (`./build.sh test`).

**Target Platform**: cross-platform .NET 10, Native AOT + full trimming.

**Project Type**: managed multi-package library + source generator + `dotnet tool` CLI.

**Performance Goals**: one round-trip per shaped fetch (SC-003); no per-row boxing (SC-004); no warm-up
(SC-006); throughput ≥ / alloc-per-op < baseline ORM (SC-007).

**Constraints**: zero library-originated trimming/AOT warnings (SC-001); no runtime reflection or query
compilation on hot paths (FR-013/FR-017); build-time SQL; `ValueTask`-first; deterministic generation
(FR-004). Generator reads `RootNamespace`/`ProjectDir` from `AnalyzerConfigOptions` (FR-046); `required`
members materialized via a generated `[SetsRequiredMembers]` ctor on the entity partial (ordinary setters,
no `[UnsafeAccessor]`/backing-field — FR-048); relationships default to the
Unloaded sentinel (FR-009/FR-047); PK identity equality generated (FR-051); projections may target
user-owned records (FR-050).

**Scale/Scope**: v1 (Tier A) = schema + relationships (Ref + Set/List/Bag/Map) + shapes/projections +
optional params + basic query/DML + migrations CLI + AOT + JSONB native + GIS companion. Tier B per FR-035.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.* Constitution v2.0.1.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | DormantQL primary; .NET-friendly namespaces; NHibernate-familiar Set/List/Bag/Map; `required` + PK equality for ergonomic, safe construction; projection-into-records keeps domains Dormant-free; located diagnostics; <15-min quickstart. | PASS |
| II. Interface & Compatibility Stability | Four contracts (public API + PublicApiAnalyzers, generated code + Verify, DSL grammar, package/SemVer); additive within MAJOR. Ref rename is pre-1.0. | PASS |
| III. Statically-Known, Safe-by-Default | Build-time-known result types; distinct projections; `Ref`/`RefSet`… load-state with Unloaded≠empty (FR-009/049); no implicit lazy. | PASS |
| IV. First-Class Tooling | CLI migrations, DSL diagnostics analyzer, compatibility verification, single CI entry point. | PASS |
| V. Performance by Default | Npgsql slim, no boxing, no reflection (generated `[SetsRequiredMembers]` materialization + setters/getters), build-time SQL, ValueTask-first, AOT smoke + benchmarks. | PASS |
| VI. Quality & Testing (NON-NEGOTIABLE) | TUnit generator + real-provider integration + AOT smoke + budgets + baselines, CI-gated; repro-test-before-fix. | PASS |

**Result: no violations.** Complexity Tracking empty. Package split = justified one-directional separation; abstractions minimal (Principle I).

## Project Structure

```text
specs/001-orm-aot-sourcegen/   plan.md research.md data-model.md quickstart.md contracts/{public-api,providers,dsl-grammar,generated-code,cli}.md tasks.md

src/
├── Dormant.Abstractions/        # stable kernel: Sessions, Links→(Refs), Querying, Providers, Mapping, Migrations, Native
├── Dormant.Core/                # engine (Schema, Modeling, Querying, Persistence, Migrations, Native, Extensibility, Diagnostics)
├── Dormant.SourceGeneration/    # Roslyn incremental generator + analyzer (Parsing, Schema, Emit, Diagnostics)
├── Dormant.Provider.PostgreSql/ # adapter: Npgsql slim, dialect, jsonb (+ Io/)
├── Dormant.Spatial.PostgreSql/  # companion adapter: PostGIS EWKB codec
└── Dormant.Tool/                # dotnet tool `dormant`
tests/  Dormant.{Core,SourceGeneration,Provider.PostgreSql,Spatial.PostgreSql}.Tests · Aot.SmokeTests · Benchmarks
samples/ Dormant.Sample.Quickstart
```

**Structure Decision**: Multi-package, one-directional dependencies (abstractions ← engine ← adapters),
semantic names (no `Ports`). Abstraction interfaces grouped by capability (Providers/Mapping/Migrations/
Native). The `Links/` folder holds the relationship reference types (being renamed Link→Ref). Builds clean
(13 projects, 0 warnings); Foundational + US1 implemented; PostgreSQL adapter verified via Testcontainers.

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Phase notes

- **Phase 0 (done)**: [research.md](./research.md) §1–§11 — incl. §11 relationship model (Ref vocabulary,
  Unloaded sentinel, projection-into-records, PK equality).
- **Phase 1 design (done)**: data-model, contracts, quickstart, `CLAUDE.md` aligned to the Ref model.
- **Implemented so far**: Setup (T001–T011); Foundational kernel/generator (T012–T022, deferring T017);
  US1 schema→entities (T023–T033, deferring T031); US2 PostgreSQL adapter (T038–T041, Testcontainers-verified).
- **⚠️ Pending code refactor (next implement)**: rename `Link<T>`/`LinkSet<T>` → `Ref<T>` + add
  `RefSet/RefList/RefBag/RefMap` in `Dormant.Abstractions`; update generator emit (collection kinds,
  Unloaded initializers, PK identity equality, projection-into-records); update adapter/sample/tests.
- **Then**: resume US2 (Session/UoW + snapshot/materializer + change-tracking + concurrency + DML +
  Testcontainers acceptance) → US3 → US5 → US4 → US8 → US6 → US7.
