---

description: "Task list for Shape Selection (EdgeQL-style) + Flat Immutable Entities"
---

# Tasks: Shape Selection (EdgeQL-style) + Flat Immutable Entities

**Input**: Design documents from `/specs/009-shape-selection/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/query-shape-grammar.md, quickstart.md

**Tests**: Included. This is generator/DSL work; correctness is proven by Verify snapshots (generated SQL/code) and the cross-dialect conformance suite (PostgreSQL via Testcontainers + SQLite in-memory). Constitution VI + plan require them.

**Organization**: Grouped by user story. Phase 2 (Foundational) is heavy — it lands the breaking entity flatten AND the net-new relational IR + navigation that every shape depends on. US1 (root-object shape) is the MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: different files, no incomplete-task dependency
- **[Story]**: US1 / US2 / US3
- Exact file paths included

## Path Conventions

Generator: `src/Dormant.SourceGeneration/`. Runtime: `src/Dormant.Abstractions/`, `src/Dormant.Core/`, `src/Dormant.Provider.{Sqlite,PostgreSql}/`. Tests: `tests/Dormant.SourceGeneration.Tests/` (Verify), `tests/Dormant.Providers.ConformanceTests/` (cross-dialect).

---

## Phase 1: Setup

**Purpose**: Fixtures and diagnostics the rest of the work builds on.

- [ ] T001 Add relationship fixtures to `tests/Dormant.Providers.ConformanceTests/schema/` — entities `Author { id, name, articles: Set<Article> }`, `Article { id, title, writer: Author }`, `Tag { id, label, article: Article }` (to-one + to-many + backlink) for use across all stories
- [ ] T002 [P] Add diagnostic descriptors in `src/Dormant.SourceGeneration/Diagnostics/DiagnosticDescriptors.cs`: undeclared-relationship, ambiguous-backlink, shape-cycle, into-mismatch, duplicate-member, shape-limit-exceeded

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Flatten entities (breaking) + build the relational IR and navigation that all shapes need.

**⚠️ CRITICAL**: No user-story shape can be implemented until this phase is complete.

### Entity flatten (P-A — breaking, MAJOR)

- [X] T003 Flatten `src/Dormant.SourceGeneration/Schema/EntityEmitter.cs`: stop emitting `Ref/RefSet/RefList/RefBag/RefMap` members; emit the to-one FK id scalar property (`WriterId` required / `ManagerId` optional); to-many collection ⇒ no entity member
- [X] T004 Update `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`: materializer ctor reads the FK id scalar ordinal; keep flat `SelectByKey` and FK column DDL
- [X] T005 Delete wrapper types `Ref.cs`, `RefSet.cs`, `RefList.cs`, `RefBag.cs`, `RefMap.cs` from `src/Dormant.Abstractions/Entities/`
- [X] T006 Migrate all consumers off wrappers (conformance schema/tests, `samples/Dormant.Sample.Quickstart`, any source reading `Ref`/`RefSet`); restore green build of `Dormant.slnx`
- [ ] T007 [P] Extend `src/Dormant.SourceGeneration/Parsing/SchemaModel.cs` + `SchemaParser.cs`: parse `Set<T>` collection declarations as metadata and record inverse/backlink info (no runtime member)
- [X] T008 Update Verify snapshots for flattened entities in `tests/Dormant.SourceGeneration.Tests/` (SchemaEmitTests)

### Relational IR + navigation (P-B)

- [X] T009 Extend `src/Dormant.SourceGeneration/Ir/SqlIr.cs` with the relational/expression core: `FromItem`, `Join`, `QualifiedColumn`, `SqlExpr` (`ColumnExpr`/`ParamExpr`/`BinaryExpr`/`FuncExpr`), `ShapedSelect`, `Cte` (keep the existing flat `SelectStatement` path intact)
- [X] T010 Render joins, qualified columns, and base expressions in `src/Dormant.SourceGeneration/Ir/Dialects/SqlDialectRendererBase.cs`, `SqliteRenderer.cs`, `PostgreSqlRenderer.cs` (depends T009)
- [X] T011 Parse navigation paths (`alias.ref.field`) in `where`/`order by`/expressions in `src/Dormant.SourceGeneration/Parsing/UnitParser.cs` + add `NavigationPath` to `Parsing/QueryModel.cs`
- [X] T012 Resolve navigation paths to join chains in `src/Dormant.SourceGeneration/Schema/SchemaValidator.cs`; emit undeclared-relationship diagnostic (depends T007, T011)
- [X] T013 Emit joins from navigation (predicate/order) in `src/Dormant.SourceGeneration/Query/QueryEmitter.cs` via the new IR (depends T009, T012)
- [X] T014 Snapshot (`tests/Dormant.SourceGeneration.Tests`) + conformance (`tests/Dormant.Providers.ConformanceTests`) for `where a.writer.name == p` on PostgreSQL + SQLite (depends T013)

**Checkpoint**: Entities are flat (FK scalars, no wrappers); navigation generates joins; relational IR + renderers exist. Shapes can now be built.

---

## Phase 3: User Story 1 - Fetch an object with its related objects in one shot (Priority: P1) 🎯 MVP

**Goal**: Root-object shape (`select a { title, writer: { name }, tags: { label } }`) returning a nested immutable projection in one round-trip.

**Independent Test**: Author a query selecting a root with a nested to-one and a nested to-many; run it; confirm the tree matches the shape, the collection is fully materialized, and exactly one DB command is issued.

- [X] T015 Tokenize the select shape block (`{ } : ,`) in `src/Dormant.SourceGeneration/Parsing/Lexer.cs`
- [X] T016 [US1] Parse root-object shape select into the `SelectShape`/`ShapeNode` AST (scalar / to-one / to-many + inner `order by`) in `src/Dormant.SourceGeneration/Parsing/UnitParser.cs` + `Parsing/QueryModel.cs` (depends T015)
- [X] T017 [US1] Resolve shape nodes in `src/Dormant.SourceGeneration/Schema/SchemaValidator.cs`: scalar/to-one/to-many kinds, to-many backlink resolution + ambiguous-backlink diagnostic, shape-cycle guard diagnostic (depends T007, T016)
- [X] T018 [US1] Add `JsonObjectExpr` + `ScalarSubquery` to `src/Dormant.SourceGeneration/Ir/SqlIr.cs` (to-one shape) (depends T009)
- [X] T019 [US1] Render `JsonObjectExpr` + scalar subquery per dialect (`jsonb_build_object` / `json_object`) in `src/Dormant.SourceGeneration/Ir/Dialects/*` (depends T018)
- [X] T020 [US1] In `src/Dormant.SourceGeneration/Query/QueryEmitter.cs`: build the shaped `ShapedSelect` (single JSON column) for to-one shapes; emit nested projection records; emit a `Utf8JsonReader` parser; wire the `CompiledQuery<T>` materializer to read the JSON column (depends T017, T019)
- [X] T021 [US1] Add `JsonArrayAggExpr` (to-many) to `Ir/SqlIr.cs` + render per dialect (`coalesce(jsonb_agg(... order by ...), '[]')` / `coalesce(json_group_array(...), json('[]'))`) in `Ir/Dialects/*` (depends T018, T019)
- [X] T022 [US1] Extend `QueryEmitter` + emitted parser for to-many shape nodes → `IReadOnlyList<TNested>` (empty not null), honoring inner `order by` (depends T020, T021)
- [X] T023 [US1] Verify snapshots in `tests/Dormant.SourceGeneration.Tests/` for shaped query SQL (both dialects) + generated nested records + parser (depends T022)
- [X] T024 [US1] Conformance tests in `tests/Dormant.Providers.ConformanceTests/`: shaped read (to-one + to-many) issues one command; empty to-many ⇒ empty list, absent to-one ⇒ null; PostgreSQL + SQLite parity (depends T022)

**Checkpoint**: MVP — fetch an object and its related objects in one typed, single-round-trip query.

---

## Phase 4: User Story 2 - Assemble a new response object from several sources (Priority: P2)

**Goal**: Free-composition select (`select { x = a.f, y = b.g, nested = c { … } }`) over multiple sources, with cascading read-side `with` (CTEs).

**Independent Test**: Author a query with two in-scope sources and a free-composition select naming fields from both; run it; confirm one composed result type, single round-trip.

- [ ] T025 [US2] Parse free-composition select (named members), multiple `from` sources, and cascading read-side `with name = (query)` in `src/Dormant.SourceGeneration/Parsing/UnitParser.cs` + `Parsing/QueryModel.cs`
- [ ] T026 [US2] Resolve free-composition members across sources + duplicate-member diagnostic in `src/Dormant.SourceGeneration/Schema/SchemaValidator.cs` (depends T025)
- [ ] T027 [US2] Emit CTEs (`WITH …`) + multi-source composition as a single JSON object in `src/Dormant.SourceGeneration/Query/QueryEmitter.cs` using `Cte`/`ShapedSelect` (depends T020, T026)
- [ ] T028 [US2] Render `Cte`/`WITH` per dialect in `src/Dormant.SourceGeneration/Ir/Dialects/*` (depends T009)
- [ ] T029 [US2] Snapshot + conformance for free composition from ≥2 sources with cascading `with`, PostgreSQL + SQLite (depends T027, T028)

**Checkpoint**: Compose new DTOs from multiple, independently-filtered sources in one query.

---

## Phase 5: User Story 3 - Project into a user-owned record (Priority: P3)

**Goal**: `select a { … } into MyDto` materializes into a user-owned record by structural match.

**Independent Test**: Author a query with `into UserRecord`; confirm results are that type and a structural mismatch is a build-time error.

- [ ] T030 [US3] Parse the `into <UserRecord>` form in `src/Dormant.SourceGeneration/Parsing/UnitParser.cs` + `Parsing/QueryModel.cs`
- [ ] T031 [US3] Structural match (member name case-insensitive + assignable type) and into-mismatch diagnostic in `SchemaValidator.cs`; target the emitted `Utf8JsonReader` parser at the user type in `Query/QueryEmitter.cs` (depends T020, T030)
- [ ] T032 [US3] Snapshot + conformance: `into` projection materializes the user record; mismatch produces the diagnostic (depends T031)

**Checkpoint**: Results can land in user-owned Clean-Architecture record types.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T033 [P] Add a shaped-read perf check (extend `tests/Dormant.Benchmarks` or a budget) confirming single round-trip + no-reflection materialization (Constitution V)
- [X] T034 Amend Constitution Principle III via `/speckit-constitution`: supersede the "links loaded/unloaded" clause (no-partial-data now via projection/shape types)
- [ ] T035 [P] Update `tests/Dormant.Benchmarks` / `samples` docs + the feature migration notes (wrapper → FK scalar / shape)
- [ ] T036 Full gate: `dotnet build Dormant.slnx -c Release` (0 warnings), `dotnet csharpier check .`, `dotnet format --verify-no-changes`, `dotnet test --solution Dormant.slnx` (PG Testcontainers + SQLite) green

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: T001, T002 — no deps (T002 [P]).
- **Foundational (P2)**: depends on Setup. Two tracks:
  - Flatten: T003 → T004 → T005 → T006; T007 [P]; T008 after T003.
  - Relational IR/nav: T009 → T010; T011 → T012 (needs T007) → T013 (needs T009) → T014.
  - **BLOCKS all user stories.**
- **US1 (P3)**: depends on Foundational. T015 → T016 → T017; T018 → T019; T020 (needs T017,T019); T021 → T022 (needs T020); T023/T024 after T022. MVP.
- **US2 (P4)**: depends on Foundational + T020 (shaped emit). Independent of US1's to-many otherwise.
- **US3 (P5)**: depends on T020 (shaped emit) + T030.
- **Polish (P6)**: after the stories it covers.

### Parallel Opportunities

- T002 [P] alongside T001.
- T007 [P] alongside the flatten chain (different files).
- The relational-IR track (T009–T014) can proceed alongside the flatten chain (T003–T008) — different files — then both must complete before US1.
- T018/T019 (to-one IR+render) can proceed alongside T015/T016 (lexer/parser).
- T033/T035 [P] in Polish.

### Within a story

- Parser → validator/resolution → IR → renderer → emitter → snapshot → conformance.

---

## Parallel Example: Phase 2 Foundational (two tracks)

```bash
# Track A (entity flatten):
Task: "EntityEmitter flatten + FK scalar"          # T003
Task: "SchemaModel/SchemaParser collection metadata" # T007 [P]
# Track B (relational IR), concurrently:
Task: "SqlIr relational/expression core"            # T009
Task: "Renderers: joins + qualified cols"           # T010
```

---

## Implementation Strategy

### MVP (first shippable MAJOR)

1. Phase 1 Setup → Phase 2 Foundational (flatten + navigation + relational IR).
2. Phase 3 US1 (root-object shape, to-one + to-many).
3. **STOP and VALIDATE**: shaped read, one round-trip, both dialects. This is a coherent MAJOR release ("009a": flat entities + navigation + root shapes).

### Incremental delivery

1. Foundational → flat entities + navigation usable on their own.
2. US1 → root-object shapes (MVP).
3. US2 → free composition + `with` CTEs ("009b" candidate).
4. US3 → `into` user records.
5. Polish → perf, constitution amendment, docs.

### Possible split (per plan Complexity Tracking)

- **009a** = Phase 1 + Phase 2 + US1 (flatten + navigation + root shapes). Self-contained MAJOR.
- **009b** = US2 + US3 + Polish (free composition, `with`, `into`).
- Decide at implementation start; one branch can still deliver all if capacity allows.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- Tests are part of the deliverable (snapshots + conformance), not optional, per Constitution VI.
- Keep the existing flat (non-shape) query path byte-identical — only the shaped path is new.
- T034 (constitution amendment) is governance; do it before declaring the feature done.
- Highest risk: T020–T022 (shaped emit + `Utf8JsonReader` parser + to-many JSON aggregation). De-risk with a thin to-one slice (T018–T020) before to-many (T021–T022).
