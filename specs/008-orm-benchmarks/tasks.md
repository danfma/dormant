---

description: "Task list for Comparative ORM Benchmarks"
---

# Tasks: Comparative ORM Benchmarks

**Input**: Design documents from `/specs/008-orm-benchmarks/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/benchmark-operations.md, quickstart.md

**Tests**: No separate test tasks. The BenchmarkDotNet suite *is* the verification; correctness/parity is validated by running it (per-story checkpoints below). No TDD requested in the spec.

**Organization**: Grouped by user story. US1 (harness + first operation) is the MVP; US2 fills the operation set; US3 makes it extensible.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3
- Exact file paths included

## Path Conventions

Single existing project reused: `tests/Dormant.Benchmarks/` at repo root. Central package versions in `Directory.Packages.props`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the project for SQLite + the three peer libraries + the runner.

- [X] T001 Add `<PackageVersion>` entries to `Directory.Packages.props`: `Dapper` 2.1.66, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.0, `Insight.Database` 6.3.10 (validate/adjust on restore)
- [X] T002 Rewire `tests/Dormant.Benchmarks/Dormant.Benchmarks.csproj`: swap the `Dormant.Provider.PostgreSql` ProjectReference for `Dormant.Provider.Sqlite`; add version-less `<PackageReference>` for Dapper, Microsoft.EntityFrameworkCore.Sqlite, Insight.Database; register `schema/*.dqls` and `schema/*.dql` as generator `AdditionalFiles` (depends T001)
- [X] T003 [P] Create `tests/Dormant.Benchmarks/BenchmarkConfig.cs`: `MemoryDiagnoser.Default`, baseline ratio column, and a `Dry`-job toggle (env var / `--job dry`) for the CI smoke
- [X] T004 Replace `tests/Dormant.Benchmarks/Program.cs` with `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)` (depends T003)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The schema, models, shared DB harness, and Insight de-risking that every operation benchmark depends on.

**⚠️ CRITICAL**: No user-story benchmark can run until this phase is complete.

- [X] T005 [P] Author `tests/Dormant.Benchmarks/schema/bench.dqls`: `module bench;` + `entity Product { id: uuid primary; name: string; category: string; price: decimal; quantity: int; }`
- [X] T006 [P] Author `tests/Dormant.Benchmarks/schema/bench.dql`: query `products_by_category(category: string)` and mutations `create_product(...)`, `update_product_quantity(id, quantity)`, `delete_product(id)` (validate against the 003 LINQ/DQL grammar)
- [X] T007 [P] Create plain `Product` POCO (no Dormant types) for Dapper + Insight in `tests/Dormant.Benchmarks/Model/Product.cs`
- [X] T008 [P] Create EF Core `BenchDbContext` + `Product` mapping (`ToTable("Product")`, `HasKey(p => p.Id)`, no `EnsureCreated`) in `tests/Dormant.Benchmarks/Model/BenchDbContext.cs`
- [X] T009 Build the project to generate the Dormant `Product` entity + `ProductsByCategory`/`CreateProduct`/`UpdateProductQuantity`/`DeleteProduct` members; resolve any generator/grammar errors (depends T002, T005, T006)
- [X] T010 Implement `tests/Dormant.Benchmarks/Infrastructure/SqliteBenchHarness.cs`: shared conn string `Data Source=bench;Mode=Memory;Cache=Shared`, Dormant `SqliteDataSource` keep-alive, `DormantSqlite.EnsureCreatedAsync`, deterministic seed (~1000 `Product` rows / ~10 categories, fixed RNG seed), and reserved per-library scratch ids for update/delete (depends T009, T007, T008)
- [X] T011 Spike to de-risk R3: validate an Insight.Database one-row round-trip against the shared DB via inline-SQL APIs; only if the generic path fails, register a minimal provider in `tests/Dormant.Benchmarks/Infrastructure/InsightSqliteProvider.cs` (depends T010)

**Checkpoint**: Generated Dormant code compiles, the shared in-memory DB seeds, and all three peers can open a connection to it — operation benchmarks can now be written.

---

## Phase 3: User Story 1 - Compare Dormant against peers (Priority: P1) 🎯 MVP

**Goal**: One operation (read-by-key) measured for all four libraries, producing a summary with time + allocation + ratio-to-Dormant. Proves the comparison harness end to end.

**Independent Test**: `dotnet run -c Release --project tests/Dormant.Benchmarks -- --filter '*ReadByKey*'` prints a table listing Dormant, Dapper, EF Core, Insight with Mean and Allocated per op.

- [X] T012 [US1] Create `tests/Dormant.Benchmarks/Benchmarks/ReadByKeyBenchmarks.cs` with `[Config(typeof(BenchmarkConfig))]`, `[GlobalSetup]` building `SqliteBenchHarness`, and the Dormant `[Benchmark(Baseline = true)]` method (`session.GetAsync<Product>(seededId)`) (depends T010)
- [X] T013 [US1] Add the Dapper (`QueryFirstOrDefaultAsync<Product>`), EF Core (`AsNoTracking().FirstOrDefaultAsync`/`FindAsync`), and Insight (`SingleAsync<Product>`) read-by-key methods to `ReadByKeyBenchmarks.cs` (same file; depends T012, T011)
- [X] T014 [US1] Run `--filter '*ReadByKey*'`; confirm the summary lists all four libraries with Mean + Allocated + ratio column (FR-004, SC-002, SC-004)

**Checkpoint**: MVP — the four-way comparison works for one operation and reports time + memory.

---

## Phase 4: User Story 2 - Cover representative operations (Priority: P2)

**Goal**: Add the remaining four operations (filtered read, insert, update, delete) for all four libraries, each idiomatic and isolated.

**Independent Test**: Full run reports all five operations × four libraries; write benchmarks don't contaminate each other.

- [X] T015 [P] [US2] `tests/Dormant.Benchmarks/Benchmarks/FilteredReadBenchmarks.cs` — 4 libs by `category`; EF `AsNoTracking().ToListAsync`; fully drain Dormant `ProductsByCategory` `IAsyncEnumerable`; Dormant baseline (depends T010, T011)
- [X] T016 [P] [US2] `tests/Dormant.Benchmarks/Benchmarks/InsertBenchmarks.cs` — 4 libs insert one row with a fresh `Guid` PK per invocation; Dormant `CreateProduct`; Dormant baseline (depends T010, T011)
- [X] T017 [P] [US2] `tests/Dormant.Benchmarks/Benchmarks/UpdateBenchmarks.cs` — 4 libs set `quantity` on a per-library scratch id (idempotent); Dormant `UpdateProductQuantity`; EF load-track-save; Dormant baseline (depends T010, T011)
- [X] T018 [P] [US2] `tests/Dormant.Benchmarks/Benchmarks/DeleteBenchmarks.cs` — 4 libs delete a per-library scratch row (re)created in `[IterationSetup]` (excluded from measurement); Dormant `DeleteProduct`; Dormant baseline (depends T010, T011)
- [X] T019 [US2] Run the full suite; confirm all five operations × four libraries report Mean + Allocated, and writes stay isolated (FR-003, FR-007, SC-005)

**Checkpoint**: Complete operation set; the comparison is trustworthy across realistic operations.

---

## Phase 5: User Story 3 - Extend the suite with new operations (Priority: P3)

**Goal**: Adding a new operation is one new class that automatically reports all four libraries.

**Independent Test**: Add a throwaway op class; it appears for all four libraries in the summary with no other changes.

- [X] T020 [US3] Extract a shared benchmark base (common `[GlobalSetup]`, harness/connection access, baseline conventions) and refactor the five operation classes onto it in `tests/Dormant.Benchmarks/Benchmarks/` (depends T015–T018)
- [X] T021 [US3] Add a "How to add an operation" section to `tests/Dormant.Benchmarks/README.md` and verify a new op class auto-appears for all four libraries (FR-011) (depends T020)

**Checkpoint**: The suite is extensible without restructuring.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, CI integration, reproducibility, and hygiene across the suite.

- [X] T022 Append the measurement-policy notes (async-everywhere, EF `AsNoTracking` on reads, write isolation, relative-not-absolute / not networked-DB) to `tests/Dormant.Benchmarks/README.md` (FR-010) (depends T021)
- [X] T023 Add a CI smoke job to `.github/workflows/ci.yml` running `dotnet run -c Release --project tests/Dormant.Benchmarks -- --job dry --filter '*'` (Constitution IV/VI)
- [ ] T024 [P] Reproducibility check: run the suite 3× on the same machine; confirm stable per-operation ranking (SC-003, FR-009)
- [X] T025 Walk through `quickstart.md` end to end and confirm every validation step passes
- [X] T026 `dotnet build Dormant.slnx` (0 warnings) and `dotnet format Dormant.slnx --verify-no-changes` clean for the benchmark project

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 → T002; T003 → T004. No external deps.
- **Foundational (Phase 2)**: depends on Setup. T005/T006/T007/T008 parallel; T009 needs T002+T005+T006; T010 needs T009+T007+T008; T011 needs T010. BLOCKS all user stories.
- **US1 (Phase 3)**: depends on Foundational. MVP.
- **US2 (Phase 4)**: depends on Foundational (harness). Independent of US1, but US1 first proves the pattern.
- **US3 (Phase 5)**: depends on US2 classes existing (refactors them).
- **Polish (Phase 6)**: after the stories it documents/validates.

### User Story Dependencies

- **US1 (P1)**: only Foundational. Independently testable (one-op comparison).
- **US2 (P2)**: only Foundational (harness). Reuses US1's pattern but each op is independently testable.
- **US3 (P3)**: depends on the US2 operation classes (extracts their shared base).

### Parallel Opportunities

- T003 parallel with T001/T002.
- Foundational authoring: T005, T006, T007, T008 in parallel.
- US2 op classes T015, T016, T017, T018 in parallel (separate files, shared read-only harness).
- T024 parallel with T023/T025.

---

## Parallel Example: Phase 2 Foundational

```bash
# Author schema + models together (different files):
Task: "Author schema/bench.dqls (entity Product)"            # T005
Task: "Author schema/bench.dql (query + 3 mutations)"        # T006
Task: "Create Model/Product.cs POCO"                         # T007
Task: "Create Model/BenchDbContext.cs"                       # T008
```

## Parallel Example: User Story 2

```bash
# The four new operation benchmark files, in parallel:
Task: "FilteredReadBenchmarks.cs"   # T015
Task: "InsertBenchmarks.cs"         # T016
Task: "UpdateBenchmarks.cs"         # T017
Task: "DeleteBenchmarks.cs"         # T018
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup → Phase 2 Foundational (CRITICAL — de-risk Insight in T011).
2. Phase 3 US1: read-by-key for all four libraries.
3. **STOP and VALIDATE**: run `--filter '*ReadByKey*'`, confirm four-way time+alloc summary.

### Incremental Delivery

1. Setup + Foundational → harness + generated Dormant code ready.
2. US1 → one-op comparison (MVP).
3. US2 → full five-op comparison.
4. US3 → extensible suite.
5. Polish → README policy, CI smoke, reproducibility.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- Benchmarks are the verification — validate by running, not by separate unit tests.
- T011 (Insight spike) is the highest-risk task; do it before building the full op set.
- Dormant owns the DDL; peers bind to the same table — never hand-write a second schema.
- Dormant method is `[Benchmark(Baseline = true)]` in every group.
- Commit after each phase or logical group.
