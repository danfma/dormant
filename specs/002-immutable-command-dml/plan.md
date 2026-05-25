# Implementation Plan: Immutable, Command-Driven ORM (DQL writes, no change-tracking)

**Branch**: `refactor/new-way` | **Date**: 2026-05-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-immutable-command-dml/spec.md`

## Summary

An architectural fork of `001-orm-aot-sourcegen`. Dormant becomes an **immutable, command-driven** ORM:
materialized entities and all results are immutable; **all data manipulation is authored as named DQL
commands** (`insert`/`update`/`delete`, EdgeQL/Gel-inspired) compiled to build-time SQL — there is no mutable
session, no snapshot change-tracking, and no unit-of-work diff. Writes are explicit and symmetric to the
existing authored-query model. Related writes are expressed by **nesting** or by **explicit `with` bindings**
and execute in a **single round-trip** via PostgreSQL data-modifying CTEs (`WITH … RETURNING …`). The session
shrinks to a transaction boundary + read identity map + command/query executor. Compiled command/query
definitions are reused (Dapper/Insight-like lightness). Positioning: NHibernate-style relationship
representation (read side: `Ref`/`RefSet`/…) + Gel/EdgeQL command-language flexibility (`with`, nested writes)
+ Dapper-like codegen lightness, all AOT-first.

This fork **reuses** the proven `001` foundation (Roslyn incremental generator, structured SQL IR + renderer,
authored-query model, configurable naming + overrides, schema-qualified DDL + apply, Native AOT, jsonb value
type, the `Ref*` types, the `.dqls` schema DSL) and **removes** `001`'s mutable `Session.AddAsync`/`Remove` +
snapshot-diff change-tracking. `001` remains the return point.

Technical approach is fixed in [research.md](./research.md); design artifacts are
[data-model.md](./data-model.md), [contracts/](./contracts/), and [quickstart.md](./quickstart.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`); generator/analyzer target `netstandard2.0`.

**Primary Dependencies**: Roslyn incremental generators; Npgsql slim data source; `System.Text.Json`
source generators (jsonb); reused from `001`. Test/tooling: TUnit (Microsoft.Testing.Platform),
Verify.TUnit + Verify.SourceGenerators, Testcontainers (PostgreSQL), BenchmarkDotNet,
Microsoft.CodeAnalysis.PublicApiAnalyzers.

**Storage**: PostgreSQL (primary). Nested writes rely on **data-modifying CTEs** (`WITH … RETURNING …`) for
single-round-trip relational inserts. Module → DB schema; DDL/SQL schema-qualified (carried from `001`).

**Testing**: TUnit unit/generator/integration; Verify snapshots + cacheability for the generator;
Testcontainers real PostgreSQL (never mocks); AOT publish smoke; BenchmarkDotNet budgets. Docker required.

**Target Platform**: cross-platform .NET 10, Native AOT + full trimming.

**Project Type**: managed multi-package library + source generator (+ `dotnet tool` CLI, later).

**Performance Goals**: one round-trip per command incl. nested writes (SC-002); no per-row boxing; no warm-up;
compiled command/query definitions allocated once and reused (SC-007).

**Constraints**: immutable results (no mutation/snapshot/dirty state); **no change-tracking**; all SQL
produced at build time, no runtime query compilation on the core path; no runtime reflection on hot paths;
ValueTask-first; zero library-originated AOT/trim warnings; deterministic generation. Back-references between
writes are **explicit `with` bindings** only (no implicit auto-link, no special token).

**Scale/Scope**: v1 (this fork) = schema (`.dqls`) + authored queries (`.dql select`) + **authored commands**
(`.dql insert/update/delete` incl. nested + `with`) + immutable read results + thin session + configurable
naming + schema-qualified DDL/apply + AOT + jsonb value type + reused compiled definitions. Advanced EdgeQL
(polymorphism, backlinks, broad set algebra, `group`) and a migrations CLI are out of scope.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.* Constitution v2.0.1.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | DQL commands symmetric to queries; `with` bindings; immutable results remove mutate-then-expect-persist confusion; located diagnostics; <15-min quickstart. | PASS |
| II. Interface & Compatibility Stability | Four surfaces (public API + PublicApiAnalyzers, generated code + Verify, DSL grammar incl. commands, package/SemVer). This is a **pre-1.0 fork** — removing `001`'s change-tracking is not a released-surface break. | PASS |
| III. Statically-Known, Safe-by-Default | Build-time-known result types for commands AND queries; immutable results; distinct projections; `Ref` load-state (read side); no implicit lazy. **Strengthened** by immutability. | PASS |
| IV. First-Class Tooling | Generator + DSL diagnostics + Verify/PublicApi baselines + single CI entry + Testcontainers. Migration CLI deferred. | PASS |
| V. Performance by Default | Build-time SQL (incl. nested via CTE), no boxing, no reflection, single round-trip, reused compiled definitions, AOT smoke + budgets. | PASS |
| VI. Quality & Testing (NON-NEGOTIABLE) | TUnit generator + real-provider integration + AOT smoke + budgets, CI-gated; repro-test-before-fix. | PASS |

**Result: no violations.** Complexity Tracking empty. Removing the mutable/change-tracking layer **reduces**
complexity (fewer moving parts), aligning with Principle V and the simpler-is-better workflow rule.

## Project Structure

```text
specs/002-immutable-command-dml/  plan.md research.md data-model.md quickstart.md contracts/{dql-commands,generated-code,public-api}.md tasks.md

src/
├── Dormant.Abstractions/        # kernel: Sessions (reduced), Entities (Ref* read-side), Querying, Providers, Mapping, Native
├── Dormant.Core/                # engine: Querying, Persistence→(Execution), Schema apply, Diagnostics  (change-tracking removed)
├── Dormant.SourceGeneration/    # generator: Parsing (schema + query + COMMAND), Ir (SQL incl. CTE), Query, Command, Schema, Emit, Diagnostics, Naming
├── Dormant.Provider.PostgreSql/ # adapter: Npgsql slim, dialect, jsonb, IO
├── Dormant.Spatial.PostgreSql/  # companion (GIS, later)
└── Dormant.Tool/                # dotnet tool (later)
tests/  Dormant.{Core,SourceGeneration,Provider.PostgreSql}.Tests · Aot.SmokeTests · Benchmarks
samples/ Dormant.Sample.Quickstart
```

**Structure Decision**: Same multi-package, one-directional dependency layout as `001` (abstractions ← engine
← adapters), reused wholesale. The generator gains a **Command** parsing+emit path alongside the existing
Query path (symmetric); the SQL IR gains **CTE/`with`** statement nodes. `Dormant.Core` **loses**
change-tracking (snapshot/diff, `AddAsync`/`Remove`) and its `Session` is reduced to transaction +
read-identity-map + executor. Entities are emitted as **immutable** types.

## Complexity Tracking

> No constitution violations — section intentionally empty. (This fork net-removes complexity.)

## Phase notes

- **Phase 0 (research)**: [research.md](./research.md) — immutable entity emission; the command IR + nested
  write CTE strategy; `with` binding semantics; the reduced session; reused compiled definitions; what is
  carried over vs removed from `001`.
- **Phase 1 (design)**: [data-model.md](./data-model.md) (build-time command/query AST + runtime immutable
  model + reduced session), [contracts/](./contracts/) (DQL command grammar, generated-code shape, reduced
  public API), [quickstart.md](./quickstart.md). `CLAUDE.md` plan pointer updated.
- **Implementation order (for /speckit-tasks)**: reuse `001` infra on this branch → emit immutable entities →
  command parser (`insert`/`update`/`delete`, `with`, nested) → command IR + CTE SQL builder → command method
  emit → reduce Session (remove change-tracking; transaction + read identity map + execute) → optimistic
  concurrency in `update`/`delete` → reused compiled definitions → carry naming/DDL/AOT/jsonb forward →
  Testcontainers acceptance (insert, nested-insert one round-trip, update/delete, concurrency, immutable reads).
- **Deferred (future "macros" feature)**: dynamic/runtime DQL generation with dynamic mapping is **not** in
  this plan; it will be addressed later as a **macros** capability (its own spec), keeping the core
  build-time/AOT path intact.
