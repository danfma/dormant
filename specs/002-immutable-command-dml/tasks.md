# Tasks: Immutable, Command-Driven ORM (DQL writes, no change-tracking)

**Feature**: `002-immutable-command-dml` (fork of `001-orm-aot-sourcegen`) | **Branch**: `refactor/new-way`
**Input**: [plan.md](./plan.md) · [spec.md](./spec.md) · [research.md](./research.md) · [data-model.md](./data-model.md) · [contracts/](./contracts/)

Tests are included (Constitution VI is non-negotiable; spec has acceptance scenarios + Testcontainers).
The branch already carries the `001` code; this fork **reshapes** it (immutable entities + authored
commands; removes the mutable session + change-tracking). Paths below are reused `001` source paths.

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Confirm the `001` foundation builds on `refactor/new-way` (`dotnet build Dormant.slnx` 0/0) and record the reused pieces (generator, SQL IR, query path, naming, DDL/EnsureCreated, AOT, jsonb, `Ref*`, `.dqls`) as the baseline for the fork
- [X] T002 [P] Add `specs/002` design docs to the agent context (CLAUDE.md already points to 002/plan.md) and verify `feature.json` = `specs/002-immutable-command-dml`

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: shift the core model to immutable + command-driven. Blocks all user stories.

> **Sequencing note (2026-05-25)**: implemented the **additive command path first** (US1) to keep the build
> green, deferring the *breaking* reshape (T003 immutable entities, T004 binding trim, T005/T006 session
> reduction, T030 test migration) to the next pass. Rationale: removing the mutable API breaks every existing
> test/sample with no replacement until commands exist; landing the additive command path first lets the
> reshape replace the mutable API cleanly. Transitional state: mutable API + commands coexist until the next
> pass removes the former.

- [ ] T003 **[DEFERRED next pass — see sequencing note]** Emit **immutable** entities in `src/Dormant.SourceGeneration/Schema/EntityEmitter.cs`: init-only/positional members, **no public setters**, no snapshot; retain the no-reflection materialization ctor + PK identity equality + `Ref*` read-side members (FR-001)
- [ ] T004 **[DEFERRED next pass]** Remove the change-tracking write/snapshot members from the entity binding in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs` + `src/Dormant.Abstractions/Entities/IEntityBinding.cs` (drop `Insert`/`Update`/`Delete`/`Snapshot`/`TracksConcurrency`); keep `Materialize` + `SelectByKey` (reads) + `Schema`/`CreateTableSql` (DDL) (FR-003)
- [ ] T005 **[DEFERRED next pass]** Reduce `ISession` to transaction + `GetAsync<T>` + `DisposeAsync`; **remove** `AddAsync`/`Remove` + mutable members (per contracts/public-api.md) (FR-010) _(additive `ExecuteCommandAsync` already added)_
- [ ] T006 **[DEFERRED next pass]** Reduce `src/Dormant.Core/Persistence/Session.cs` to a thin unit-of-work: delete `_added`/`_tracked`/`_removed`, snapshot diff, UPDATE/DELETE-by-diff (FR-003/FR-010)
- [X] T007 Add `CompiledCommand<TResult>` in `src/Dormant.Abstractions/Querying/CompiledCommand.cs` (prebuilt statement + no-boxing binder + result materializer) (FR-005/FR-012) _(+ `ISession.ExecuteCommandAsync` + `Session` impl)_
- [ ] T008 Extend the SQL IR in `src/Dormant.SourceGeneration/Ir/SqlIr.cs` with `UpdateStatement`, `DeleteStatement`, a `Returning` clause, and a `CteStatement` (ordered `WITH` steps + final) (FR-004) _(DEFERRED to US2/US6: US1's INSERT…RETURNING SQL is built directly in `CommandEmitter`; CTE nodes land with nested writes)_
- [X] T009 Add the command AST in `src/Dormant.SourceGeneration/Parsing/CommandModel.cs` (`CommandFile`, `CommandModel`, `Assignment`, `CommandValue`) equatable for caching _(WriteNode tree + WithBinding land with nested/`with` in US2/US3)_

**Checkpoint**: immutable entities emit; session is thin; binding is read-only; command IR + AST exist. Build 0/0.

## Phase 3: User Story 1 - Write via an authored command (Priority: P1) 🎯 MVP

**Goal**: a named `insert` command → typed `ISession` method with build-time SQL; immutable result; no auto-DML.
**Independent Test**: author `insert User {…}`, build, call the method vs real PostgreSQL → exactly one row, immutable result, no Add/Save API exists.

- [X] T010 [P] [US1] Integration: authored `insert` command writes exactly one row + returns immutable result in `tests/Dormant.Provider.PostgreSql.Tests/CommandInsertTests.cs` (Testcontainers)
- [X] T011 [P] [US1] Generator: `insert` command → `partial static {Module}Commands` extension method + `CompiledCommand<T>`, exact SQL asserted in `tests/Dormant.SourceGeneration.Tests/CommandEmitTests.cs`
- [X] T012 [US1] Parse `command Name(params) = insert Entity { field := expr, … };` (params, literals, native calls) in `src/Dormant.SourceGeneration/Parsing/CommandParser.cs` (FR-002/FR-008)
- [~] T013 [US1] _(folded into CommandEmitter; INSERT…RETURNING built directly — CTE IR deferred to US2)_ Build a single `InsertStatement` (with `Returning`) from the command AST via the IR in `src/Dormant.SourceGeneration/Command/CommandSqlBuilder.cs` (FR-005)
- [X] T014 [US1] Emit the command method + reused `CompiledCommand<T>` (C# 14 extension block on `ISession`) in `src/Dormant.SourceGeneration/Command/CommandEmitter.cs` (FR-002/FR-012)
- [X] T015 [US1] Wire the command path into `src/Dormant.SourceGeneration/DormantGenerator.cs` (glob `.dql` commands, combine with schemas, emit) + execute via `Session` (FR-002)

**Checkpoint**: 🎯 MVP — author + run an insert command against real PostgreSQL; immutable result.

## Phase 4: User Story 2 - Nested write, one round-trip (Priority: P1)

**Goal**: related write nested in a command → single PostgreSQL data-modifying CTE.
**Independent Test**: `insert Post { author := (insert User {…}) }` → both rows written, child references parent, statement count = 1.

- [ ] T016 [P] [US2] Integration: nested insert writes both rows in exactly one round-trip (statement count) in `tests/Dormant.Provider.PostgreSql.Tests/NestedWriteTests.cs`
- [ ] T017 [US2] Parse nested `WriteNode` (an `insert`/`update`/`delete` as an assignment value) in `CommandParser.cs` (FR-004)
- [ ] T018 [US2] Build a `CteStatement` (parent `INSERT … RETURNING id` → child `INSERT … SELECT …, parent.id`) from a nested write tree in `CommandSqlBuilder.cs`; render via `SqlRenderer` (FR-004)
- [ ] T019 [US2] Bind nested-write parameters across CTE steps (no boxing) in `CommandEmitter.cs` (FR-005)

**Checkpoint**: nested writes execute as one statement; verified by statement count.

## Phase 5: User Story 3 - `with` bindings & back-references (Priority: P1)

**Goal**: `with x := <expr>` reusable references (incl. a nested write's result); the only back-reference mechanism.
**Independent Test**: `with u := (insert User {…})` referenced in two child writes → single declared row used consistently, one round-trip.

- [ ] T020 [P] [US3] Integration: parent + children via `with` back-reference → children link to the one parent in one round-trip in `tests/Dormant.Provider.PostgreSql.Tests/WithBindingTests.cs`
- [ ] T021 [P] [US3] Generator: undefined/unreferenced `with` name + write-reference cycle → located diagnostics in `tests/Dormant.SourceGeneration.Tests/CommandDiagnosticTests.cs` (new ORM codes)
- [ ] T022 [US3] Parse `with name := <expr>` declarations + name references in commands/queries in `CommandParser.cs` (and share with the query parser) (FR-006)
- [ ] T023 [US3] Map each `with` binding to a CTE step / bound value; resolve references to the step's `RETURNING` column in `CommandSqlBuilder.cs` (FR-006)
- [ ] T024 [US3] Validation + located diagnostics: unknown/unreferenced `with`, write-reference cycle, unknown entity/field/param in `src/Dormant.SourceGeneration/Diagnostics/` (FR-006, edge cases)

**Checkpoint**: explicit `with` back-references work; no implicit auto-link / no `..id`.

## Phase 6: User Story 4 - Immutable, statically-typed reads (Priority: P2)

**Goal**: authored queries return immutable entities / distinct projections; no mutate-and-persist.
**Independent Test**: run an authored `select`; result is immutable + exactly the requested shape; accessing a non-selected field does not compile.

- [ ] T025 [P] [US4] Integration: query returns immutable entity + flat projection; field round-trips in `tests/Dormant.Provider.PostgreSql.Tests/ImmutableReadTests.cs`
- [ ] T026 [US4] Confirm/adjust the carried-over query emit (`QueryEmitter`) to return the immutable entity/record types (no setters) and reuse a `CompiledQuery<T>` definition (FR-009/FR-012)
- [ ] T027 [US4] Confirm `GetAsync<T>` returns the immutable instance via the read identity map (one instance per key) in `src/Dormant.Core/Persistence/Session.cs` (FR-010)

**Checkpoint**: reads are immutable + build-time-typed; no save-back API.

## Phase 7: User Story 5 - Thin session (transaction + read cache) (Priority: P2)

**Goal**: session = transaction + read identity map + executor; no change-tracking.
**Independent Test**: run two commands + a query in one transaction, commit atomically; same key read twice returns the same instance; no dirty-tracking.

- [ ] T028 [P] [US5] Integration: multi-command transaction atomicity (commit all / rollback none) + read identity map in `tests/Dormant.Provider.PostgreSql.Tests/SessionTransactionTests.cs`
- [ ] T029 [US5] Finalize `SessionFactory`/`Session` lifecycle (open → execute → commit/rollback → dispose) + identity-map population on read in `src/Dormant.Core/Persistence/` (FR-010)
- [ ] T030 [US5] Delete obsolete `001` change-tracking tests and rewrite the CRUD-shaped ones as command-based (`tests/Dormant.Provider.PostgreSql.Tests/`: remove ChangeTracking/Concurrency-by-snapshot; migrate Crud→command) (FR-003)

**Checkpoint**: session is thin; no change-tracking remains; obsolete tests removed/migrated.

## Phase 8: User Story 6 - Optimistic concurrency in the command (Priority: P2)

**Goal**: `update`/`delete` match a concurrency token; stale token → 0 rows → surfaced conflict.
**Independent Test**: two callers, stale-token second update affects 0 rows and surfaces a conflict; first writer's value persists.

- [ ] T031 [P] [US6] Integration: stale-token `update` → 0 rows → `ConcurrencyConflictException`; first write persists in `tests/Dormant.Provider.PostgreSql.Tests/CommandConcurrencyTests.cs`
- [ ] T032 [US6] Parse `update Entity filter … set { … }` and `delete Entity filter …` (incl. token match + bump) in `CommandParser.cs` (FR-002/FR-011)
- [ ] T033 [US6] Build `UpdateStatement`/`DeleteStatement` (WHERE incl. token, optional `Returning`) in `CommandSqlBuilder.cs`; surface zero-rows as a conflict in the executor (FR-011)

**Checkpoint**: authored update/delete + optimistic concurrency work end-to-end.

## Phase 9: User Story 7 - Reused compiled definitions (Priority: P3)

**Goal**: one compiled definition per command/query, reused across executions.
**Independent Test**: execute the same command N times → its definition allocated once (allocation measurement).

- [ ] T034 [P] [US7] Benchmark: repeated command/query reuses one definition (alloc/op) in `tests/Dormant.Benchmarks/DefinitionReuseBenchmarks.cs` (SC-007)
- [ ] T035 [US7] Emit each `CompiledCommand<T>`/`CompiledQuery<T>` as a reused (static readonly) definition; bind parameters per call without re-allocating in `CommandEmitter.cs`/`QueryEmitter.cs` (FR-012)

**Checkpoint**: definitions reused; allocation budget met.

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T036 [P] Carry naming (snake_case + overrides), schema-qualified DDL + `EnsureCreatedAsync`, and jsonb (`::jsonb` cast) forward; confirm green for commands too (FR-014/FR-016)
- [ ] T037 [P] AOT smoke: extend `tests/Dormant.Aot.SmokeTests` to root commands (insert/nested/update) → zero library-originated warnings (FR-015/SC-006)
- [ ] T038 [P] Update `samples/Dormant.Sample.Quickstart` (immutable entities + authored commands) and validate the quickstart end-to-end vs a live container (SC-008)
- [ ] T039 [P] PublicApiAnalyzers baselines for the reduced public surface (`ISession`, `CompiledCommand`/`CompiledQuery`, `DormantPostgres`) (Constitution II)
- [ ] T040 Generator determinism + cacheability tests for the command path (Verify snapshots + `WithTrackingName`) in `tests/Dormant.SourceGeneration.Tests/`
- [ ] T041 Remove dead `001` code (snapshot structs, change-tracker remnants) and run the full suite (`./build.sh test`) green

---

## Dependencies & Execution Order

### Phase order
Setup (P1) → Foundational (P2, blocks all) → US1 → US2 → US3 → US4 → US5 → US6 → US7 → Polish.

### Cross-story dependencies
- **Foundational (T003–T009)** blocks everything (immutable entities, thin session, command IR + AST).
- **US1** (command insert pipeline) precedes **US2** (nested = CTE on the insert pipeline) and **US3** (`with` reuses CTE) and **US6** (update/delete reuse the command pipeline).
- **US4/US5** depend on Foundational (immutable entities + thin session); independent of US1's write path but share the session.
- **US7** is cross-cutting over the emitters (after US1/US6 exist).

### Parallel opportunities
- Setup `[P]` (T002) after T001.
- Within a story, `[P]` test tasks run together; integration vs generator tests are independent files.
- Polish `[P]` (T036–T039) parallel after the stories land.

## Implementation Strategy

- **MVP** = Phase 1 + Phase 2 + **US1** (author + run an insert command against real PostgreSQL, immutable result). Smallest end-to-end slice proving the fork.
- **Incremental**: add US2 (nested/CTE), US3 (`with`), US4 (immutable reads), US5 (thin session + remove change-tracking), US6 (update/delete + concurrency), US7 (definition reuse), then Polish.
- The branch already has `001`'s infra; Foundational reshapes it (the most invasive step — immutable entities + session reduction + binding trim) before commands are layered on.
