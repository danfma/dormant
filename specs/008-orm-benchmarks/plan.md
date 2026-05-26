# Implementation Plan: Comparative ORM Benchmarks

**Branch**: `008-orm-benchmarks` | **Date**: 2026-05-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-orm-benchmarks/spec.md`

## Summary

Turn the placeholder `tests/Dormant.Benchmarks` project into a real BenchmarkDotNet
suite that measures Dormant against Dapper, EF Core, and Insight.Database across five
representative operations (read-by-key, filtered multi-row read, insert, update, delete),
all hitting one shared in-memory SQLite database with an identical schema and seed data.
Each operation is a BenchmarkDotNet group of four methods (one per library, Dormant as
baseline) with `MemoryDiagnoser` enabled so the summary reports time and allocations per
(library, operation) pair. Dormant owns the schema (its generator emits the DDL via
`DormantSqlite.EnsureCreatedAsync`); the other three libraries read/write the same table
through their idiomatic async APIs. The suite runs from a single command and a CI smoke
job executes it in BenchmarkDotNet's `Dry` job to keep it green without the full runtime.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: BenchmarkDotNet 0.14.0 (central); `Dormant.Provider.Sqlite`
(→ `Microsoft.Data.Sqlite.Core` 10.0.8 + `SQLitePCLRaw.bundle_e_sqlite3` 2.1.11);
Dapper; `Microsoft.EntityFrameworkCore.Sqlite`; Insight.Database (versions resolved in
research.md, added to `Directory.Packages.props`)

**Storage**: Embedded SQLite, in-memory shared-cache (`Data Source=bench;Mode=Memory;Cache=Shared`),
kept alive by Dormant's `SqliteDataSource` keep-alive connection for the suite lifetime

**Testing**: BenchmarkDotNet IS the harness (the project is `Exe`, run via `BenchmarkSwitcher`).
A CI smoke run uses the `Dry` job to verify the suite executes. TUnit does not apply here.

**Target Platform**: Local dev + CI runner (Linux/macOS/Windows); single-process, single-machine

**Project Type**: Benchmark harness (single existing project, reused)

**Performance Goals**: Not an absolute SLA — the deliverable is a reproducible *relative*
comparison. Stable per-operation ranking across ≥3 runs on the same machine (SC-003).

**Constraints**: Not AOT/trimmed (`IsAotCompatible=false`) — EF Core, Dapper, and
Insight.Database rely on runtime reflection. Measured regions exclude setup/seed/warmup
(FR-006). Per-library write isolation so writes don't cross-contaminate (FR-007).

**Scale/Scope**: One representative entity; seed ~1,000 rows across ~10 categories so the
filtered read returns a meaningful (~100-row) set. Five operations × four libraries = 20
measured cells.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

This feature is benchmark/tooling work; it directly advances two principles and violates
none.

- **IV. First-Class Tooling** — *Advances.* "Build, test, **benchmark**, and documentation
  generation MUST be reproducible via a single documented entry point and MUST run in CI."
  Plan delivers exactly this: one command (`dotnet run -c Release --project
  tests/Dormant.Benchmarks`) plus a CI smoke job.
- **V. Performance by Default** — *Advances.* Provides the measurement substrate for the
  "explicit, measurable performance budgets" the principle requires. (This feature
  establishes the suite; wiring a regression *gate* with budgets is a follow-up, noted in
  research.md, not in scope here.)
- **VI. Quality & Testing Discipline** — *Satisfied.* The suite runs in CI (smoke/dry).
- **V. AOT clause** — *Not violated.* The AOT-clean mandate applies to the shipped Dormant
  library, not to a benchmark harness that intentionally pulls reflection-based peers for
  comparison. `IsAotCompatible=false` on this `Exe` test project is correct and isolated.

**Gate result: PASS.** No entries in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/008-orm-benchmarks/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── benchmark-operations.md   # The 5-operation parity contract every library implements
└── tasks.md             # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
Directory.Packages.props                 # + PackageVersion: Dapper, Microsoft.EntityFrameworkCore.Sqlite, Insight.Database

tests/Dormant.Benchmarks/
├── Dormant.Benchmarks.csproj            # swap PG ref → Provider.Sqlite ref; add Dapper/EF/Insight PackageReferences; add .dql/.dqls as AdditionalFiles
├── Program.cs                           # BenchmarkSwitcher.FromAssembly(...).Run(args)
├── BenchmarkConfig.cs                   # MemoryDiagnoser, columns, default + Dry (CI) jobs, baseline ratio
├── schema/
│   ├── bench.dqls                       # entity Product (id uuid primary, name, category, price, quantity)
│   └── bench.dql                        # query products_by_category; mutations create/update/delete_product
├── Model/
│   ├── Product.cs                       # POCO for Dapper + Insight (plain, no Dormant types)
│   └── BenchDbContext.cs                # EF Core DbContext + entity config (maps to same table)
├── Infrastructure/
│   ├── SqliteBenchHarness.cs            # owns the shared in-memory DB: Dormant DataSource keep-alive, EnsureCreated, seeding
│   └── InsightSqliteProvider.cs         # Insight.Database provider registration for Microsoft.Data.Sqlite (if needed — see research)
└── Benchmarks/
    ├── ReadByKeyBenchmarks.cs           # Dormant(baseline) / Dapper / EfCore / Insight
    ├── FilteredReadBenchmarks.cs
    ├── InsertBenchmarks.cs
    ├── UpdateBenchmarks.cs
    └── DeleteBenchmarks.cs
```

**Structure Decision**: Single existing project (`tests/Dormant.Benchmarks`) is reused, not
replaced — the spec assumption. Its lone `Dormant.Provider.PostgreSql` project reference is
swapped for `Dormant.Provider.Sqlite` (the suite is SQLite-only per the user request). The
generated Dormant entity/queries come from `schema/bench.dqls` + `schema/bench.dql`
registered as generator `AdditionalFiles`; generated namespace is
`Dormant.Benchmarks.Schema.Bench`. EF, Dapper, and Insight share one hand-written `Product`
POCO / `DbContext` mapped to the same physical table Dormant's DDL creates.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
