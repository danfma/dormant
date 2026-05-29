# Tasks: EdgeQL-Style Constraints

**Feature**: 012-edgeql-constraints
**Branch**: 012-edgeql-constraints
**Input**: plan.md, spec.md, research.md, data-model.md, contracts/

**Prerequisites**: All design documents above.

**Tests**: Included. Although the spec did not request TDD, Constitution Principle VI and plan.md
require cross-provider **conformance** (PostgreSQL via Testcontainers + SQLite in-memory) and
**Verify** source-generator snapshots for every compatibility-surface change. Test tasks precede or
accompany the implementation they cover.

**Organization**: Grouped by user story (US1–US5) for independent implementation/testing. Phase
mapping to plan.md: Setup+Foundational ≈ P-A; US1–US3 ≈ P-B; US4 ≈ P-C; US5 ≈ P-D; Polish ≈ P-E/P-F.

## Format Reminder

- `- [ ] T### [P?] [USx?] Description with exact file path`
- `[P]` = parallelizable (different files, no incomplete deps); story label only on US phases.

## Implementation status (Slice 2 — P-B constraint IR + DDL, 2026-05-29)

**Landed & verified** (build 0/0, generator tests 45/45 incl. new `ConstraintEmitTests`, CSharpier clean):
- T011 — `ConstraintIrKind` + `ConstraintDef` IR; `CreateTableStatement.TableConstraints`.
- T012 — `RenderCreateTable` emits named table-level constraints; `RenderConstraint` (shared, virtual)
  renders PRIMARY KEY / UNIQUE / CHECK. Both PG + SQLite produce correct DDL (verified output).
- Member-level lowering (part of T015): `unique` → UNIQUE; `max`/`min`/`max_exclusive`/`min_exclusive`/
  `max_length`/`min_length`/`length`/`range` → CHECK. `as` name honored, else deterministic default
  (`<table>_<col>_<suffix>`). Single-column `primary` stays inline on the column (no double PK).
  Covers part of T017/T018 (shared render — no dialect-specific code needed for these kinds) and
  T028/T029 (naming). Verified by `ConstraintEmitTests` (UNIQUE + CHECK + names, both dialects).

**Deferred (next slices)**: `one_of` (string-literal quoting), `regex` (PG `~` / SQLite fallback,
T017/T018 remainder), `check`-expression lowering (T016), entity-level constraint DDL (US2),
`concurrency` DEFAULT (T019), conformance tests (Docker, T021), scalars (US4), inheritance (US5),
grammar 011 (P-E), migration guide + MAJOR bump (P-F).

## Implementation status (Slice 1 — P-A front-end core, 2026-05-29)

**Landed & verified** (build 0/0, generator tests 42/42, Core 1/1, CSharpier clean):
- T003 — diagnostic descriptors ORM029–ORM036 + AnalyzerReleases rows.
- T006 — member `{ constraint…; annotation…; }` block parsing (function-call/named args, optional
  parens), legacy `primary`/`concurrency`/`db("…")` rejected via ORM035, `column(...)` annotation →
  resolved column name.
- T007 — entity-level `constraint … [on (…)] [(check …)] [as …]` + `annotation …` parsing.
- T013 — all in-repo `.dqls` (+ generator test inline schemas) migrated to the new syntax.

**Partially landed** (enough for the green slice; finish next):
- T002 — `ConstraintKind` enum done (incl. `Range`); the metadata table (arity/types/scope) not yet.
- T004 — `ConstraintModel`+`ConstraintArg`+`AnnotationModel` done; `ScalarTypeModel` deferred.
- T005 — `EntityModel`/`PropertyModel` extended with constraints/annotations; `NameOverride` kept
  (populated from `column` annotation) so the ~25 DDL/naming readers stay unchanged — full removal
  (T015a) deferred; `Scalars` on `SchemaModel`/`ParseResult` deferred.

**Deferred to later slices**: T008/T009 (scalar + abstract/extending parsing), T010 (full validator),
T011/T012 + T015–T052 (constraint IR + per-dialect DDL, scalars, inheritance, grammar 011, migration
guide, conformance, MAJOR bump). DDL still works via derived `IsPrimary`/`IsConcurrency` + column name;
constraints beyond primary/concurrency parse into the model but are not yet emitted to DDL.

---

## Phase 1: Setup (shared front-end plumbing)

- [ ] T001 Add the constraint/scalar/inheritance keyword set recognition path in `src/Dormant.SourceGeneration/Parsing/Lexer.cs` is confirmed unchanged (keywords stay identifiers) and document the reserved words (`constraint`, `scalar`, `extending`, `abstract`, `unique`, `check`, `one_of`, `max`, `min`, `max_exclusive`, `min_exclusive`, `max_length`, `min_length`, `length`, `regex`, `primary`, `concurrency`, `on`, `as`) in a comment
- [ ] T002 [P] Add a `ConstraintKind` enum (incl. `Range` sugar) + a static metadata table (arg arity, accepted named-arg keys, applicable base types, member/entity scope) in `src/Dormant.SourceGeneration/Parsing/SchemaModel.cs`
- [X] T003 [P] Register diagnostic descriptors ORM029–ORM036 (incl. ORM035 dedicated removed-syntax with a migration message, ORM036 unknown/misshaped annotation + constraint-on-reference) in `src/Dormant.SourceGeneration/Diagnostics/DiagnosticDescriptors.cs` and add rows to `src/Dormant.SourceGeneration/AnalyzerReleases.Unshipped.md`

**Checkpoint**: Keyword vocabulary fixed; constraint-kind metadata + diagnostic IDs exist.

---

## Phase 2: Foundational (model + parser + validator + IR skeleton) — BLOCKS all stories

**⚠️ No user story starts until this is substantially complete.**

- [ ] T004 Add `ConstraintModel` (with `ConstraintArg` named/positional args), `AnnotationModel`, and `ScalarTypeModel` records to `src/Dormant.SourceGeneration/Parsing/SchemaModel.cs` per data-model.md; remove `PropertyModel.NameOverride` (column name now via `column(...)` annotation)
- [ ] T005 Extend `EntityModel` (`IsAbstract`, `Extends`, `EntityConstraints`) and `PropertyModel` (`Constraints`, with `IsPrimary`/`IsConcurrency` derived) in `src/Dormant.SourceGeneration/Parsing/SchemaModel.cs`; add `Scalars` to `SchemaModel`/`ParseResult`
- [X] T006 Replace `ParseModifiers()` with member-block parsing (`{ constraint …; annotation …; }`) in `src/Dormant.SourceGeneration/Parsing/SchemaParser.cs`: parse `constraint name[(args)] [as name];` and `annotation name[(args)];` (function-call form, positional/named args, optional parens); the `column(...)` annotation supplies the DB column name (replacing `db("…")`); emit ORM035 when legacy trailing `primary`/`concurrency`/`db("…")` modifiers are encountered
- [X] T007 Add entity-level constraint parsing (`constraint … [on (…)] [(check expr)] [as name];`) in `src/Dormant.SourceGeneration/Parsing/SchemaParser.cs`
- [ ] T008 Add `scalar Name extending Base { constraint…; }` parsing in `src/Dormant.SourceGeneration/Parsing/SchemaParser.cs`
- [ ] T009 Add `abstract` entity flag + `extending Base(, …)` clause parsing in `src/Dormant.SourceGeneration/Parsing/SchemaParser.cs`
- [ ] T010 Extend `src/Dormant.SourceGeneration/Parsing/SchemaValidator.cs` with: unknown constraint (ORM029), type-incompatible constraint (ORM030), missing target member (ORM031), unknown/non-scalar base (ORM033), unknown/misshaped annotation + constraint/annotation on a reference/collection member (ORM036); leave `as`-collision (ORM032) and inheritance-conflict (ORM034) stubs for US3/US5
- [X] T011 [P] Add `ConstraintDef` IR node + extend `CreateTableStatement` with table-level constraints in `src/Dormant.SourceGeneration/Ir/SqlIr.cs` per data-model.md
- [X] T012 [P] Add `RenderConstraints()` + overridable `RenderUnique`/`RenderCheck`/`RenderPrimaryKey`/`RenderConstraintName`/`RenderRegexConstraint` hooks (default impls) in `src/Dormant.SourceGeneration/Ir/Dialects/SqlDialectRendererBase.cs` and call them from `RenderCreateTable()`
- [X] T013 Migrate ALL in-repo schemas to the new syntax (`samples/Dormant.Sample.Quickstart/schema/*.dqls`, `tests/**/schema/*.dqls`): `id: uuid primary;` → `id: uuid { constraint primary; }`, `version: int concurrency;` → `version: int { constraint concurrency; }`
- [ ] T014 [P] Add generator unit tests (Verify) asserting the parser produces the expected `ConstraintModel`/`ScalarTypeModel`/inheritance model for a representative schema in `tests/Dormant.SourceGeneration.Tests/`

**Checkpoint**: New schema syntax parses into the model; legacy syntax is rejected with a diagnostic; repo builds; IR can carry constraints (not yet rendered per kind).

---

## Phase 3: User Story 1 — Validation constraints on a field (Priority: P1) 🎯 MVP

**Goal**: Member-level constraints (`unique`, `max_length`/`min_length`/`length`, `max`/`min`/`*_exclusive`, `regex`, `one_of`, `check`) plus `primary`/`concurrency`, lowered to IR and enforced in generated DDL on both providers.

**Independent Test**: A schema member declaring `unique` + a length + a range constraint compiles, the generated DDL enforces them, and the database rejects violating rows.

- [ ] T015 [US1] Lower member `ConstraintModel`s to `ConstraintDef`s in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs` (length/range/one_of → CHECK; `unique` → Unique; `primary` → PrimaryKey; `concurrency` → column default)
- [ ] T015a [US1] Resolve each column's DB name from a `column(...)` annotation (else naming convention) in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`, and remove all references to the deleted `PropertyModel.NameOverride` (e.g. the `Col(p, convention)` path) — replacing the old `db("…")` behavior
- [ ] T016 [US1] Implement `check`/range/length/one_of expression lowering reusing the query-expression IR (research R-03); render in **literal/DDL mode** (bare column names, inline literals, NO placeholders/aliases, no navigation) for member-level `check`
- [ ] T017 [US1] Implement PostgreSQL rendering of Unique/Check/PrimaryKey + `regex` via `~` in `src/Dormant.SourceGeneration/Ir/Dialects/PostgreSqlRenderer.cs`
- [ ] T018 [US1] Implement SQLite rendering of Unique/Check/PrimaryKey + `regex` fallback (GLOB/LIKE or omit+warn, research R-01) in `src/Dormant.SourceGeneration/Ir/Dialects/SqliteRenderer.cs`
- [ ] T019 [US1] Wire `concurrency` token DDL (default) + confirm existing mutation WHERE-match path still works in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`
- [ ] T020 [P] [US1] Verify snapshot of generated DDL (PG + SQLite) for a member-constraint schema in `tests/Dormant.SourceGeneration.Tests/`
- [ ] T021 [US1] Conformance: add a schema + tests in `tests/Dormant.Providers.ConformanceTests/` asserting each member constraint kind rejects violating rows on PostgreSQL and SQLite (regex SQLite per fallback)
- [ ] T022 [P] [US1] Diagnostic unit tests for ORM029/ORM030/ORM031 in `tests/Dormant.SourceGeneration.Tests/`

**Checkpoint**: MVP — authors get the full member-level standard constraint library, enforced by the database.

---

## Phase 4: User Story 2 — Multi-field & expression constraints (Priority: P2)

**Goal**: Entity-level `constraint unique on (a, b)` and `constraint check (<expr over members>)` enforced in DDL.

**Independent Test**: An entity with `unique on (first, last)` and `check (start <= end)` rejects duplicate pairs and rows violating the expression.

- [ ] T023 [US2] Lower entity-level `EntityConstraints` (composite `unique on (…)`, `check (expr)`, composite `primary on (…)`) to table-level `ConstraintDef`s in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`
- [ ] T024 [US2] Render composite UNIQUE + table-level CHECK across both dialects (extend `RenderConstraints`/PG/SQLite renderers)
- [ ] T025 [P] [US2] Verify snapshot for an entity-level multi-field + check schema in `tests/Dormant.SourceGeneration.Tests/`
- [ ] T026 [US2] Conformance: composite-uniqueness + cross-field check rejection on PG + SQLite in `tests/Dormant.Providers.ConformanceTests/`
- [ ] T027 [P] [US2] Diagnostic test for ORM031 on a multi-field constraint referencing a missing member

**Checkpoint**: Composite uniqueness and cross-field checks enforced end to end.

---

## Phase 5: User Story 3 — Named SQL constraints via `as` (Priority: P2)

**Goal**: `as {name}` pins the DB constraint name; without it a deterministic default is generated; duplicate names error.

**Independent Test**: `constraint unique as users_email_key` yields a DB constraint named exactly that; two same `as` names in a module produce ORM032.

- [ ] T028 [US3] Implement constraint-name resolution (explicit `as` else deterministic `<table>_<cols>_<kind>`, research R-02) in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`
- [ ] T029 [US3] Render the resolved name via `RenderConstraintName` in both dialect renderers (inline `CONSTRAINT <name>`)
- [ ] T030 [US3] Implement ORM032 duplicate-`as`-name detection (per module) in `src/Dormant.SourceGeneration/Parsing/SchemaValidator.cs`
- [ ] T031 [P] [US3] Verify snapshot asserting explicit + default constraint names are stable across builds in `tests/Dormant.SourceGeneration.Tests/`
- [ ] T032 [P] [US3] Conformance: query DB catalog to assert the constraint name on PG (and SQLite where applicable) in `tests/Dormant.Providers.ConformanceTests/`
- [ ] T033 [P] [US3] Diagnostic test for ORM032 collision

**Checkpoint**: Stable, author-controllable constraint names.

---

## Phase 6: User Story 4 — Custom scalar types (Priority: P3)

**Goal**: `scalar X extending base { constraint…; }`; members typed `X` inherit its constraints.

**Independent Test**: `scalar Username extending str { constraint max_length(30); }` typed on a member enforces the constraint without restating it.

- [ ] T034 [US4] Make `TypeMap`/type resolution scalar-aware (custom scalar → base CLR type) in `src/Dormant.SourceGeneration/Emit/EmitHelpers.cs` + schema-local scalar registry
- [ ] T035 [US4] In `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`, apply a scalar's constraints to every member typed with it (member-level constraints add on top)
- [ ] T036 [US4] Extend `SchemaValidator` so scalar constraints are type-checked against the base (ORM030/ORM033)
- [ ] T037 [P] [US4] Verify snapshot for a scalar-typed schema (DDL carries the scalar's constraints) in `tests/Dormant.SourceGeneration.Tests/`
- [ ] T038 [US4] Conformance: scalar-derived constraints enforced on PG + SQLite (incl. a `one_of` enum scalar) in `tests/Dormant.Providers.ConformanceTests/`

**Checkpoint**: Reusable domain scalar types with constraints.

---

## Phase 7: User Story 5 — Inheritance & composition (Priority: P3)

**Goal**: `abstract entity` + `extending`; inherited members/constraints flattened into the concrete table; abstract entities emit no table.

**Independent Test**: An abstract base with a member + constraint, extended by a concrete entity, yields a table with the inherited member and constraint enforced.

- [ ] T039 [US5] Implement inheritance resolution (flatten base members + constraints into derived entity, dedup identical, research R-05) in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`
- [ ] T040 [US5] Skip table emission for `abstract` entities in `src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs`
- [ ] T041 [US5] Implement ORM034 inheritance conflict + cycle detection in `src/Dormant.SourceGeneration/Parsing/SchemaValidator.cs`
- [ ] T042 [P] [US5] Verify snapshot for an abstract base + extending entity (flattened DDL, no table for abstract) in `tests/Dormant.SourceGeneration.Tests/`
- [ ] T043 [US5] Conformance: inherited member + constraint enforced on the derived entity (PG + SQLite) in `tests/Dormant.Providers.ConformanceTests/`
- [ ] T044 [P] [US5] Diagnostic tests for ORM034 (conflict + cycle)

**Checkpoint**: Composition/inheritance reduces duplication; flat single-table model preserved.

---

## Phase 8: Polish & Cross-Cutting (grammar 011, migration, release)

- [ ] T045 [P] Update the Tree-sitter grammar for the new syntax (member block with `constraint` + `annotation` statements, function-call args incl. named `name = value`, optional parens, `scalar`, `extending`, `abstract`, `on`, `as`, constraint/annotation names like `column`/`range`) in `tooling/grammar/dormantql-tree-sitter/grammar.js`; run `tree-sitter generate` and commit `src/parser.c`
- [ ] T046 [P] Update `highlights.scm` (constraint names, keywords) in `tooling/grammar/dormantql-tree-sitter/src/highlights.scm` and sync the Zed copy `tooling/zed-dormantql/languages/dormantql/highlights.scm`
- [ ] T047 [P] Update the TextMate keyword list (add `constraint`, `annotation`, `scalar`, `extending`, `abstract`, `on`, `as` + constraint/annotation names) in `tooling/grammar/dormantql-textmate/dormantql.tmLanguage.json` and the vendored copy `tooling/vscode-dormantql/syntaxes/dormantql.tmLanguage.json`
- [ ] T048 Add constraint/scalar/inheritance fixtures + update real-sample parsing in `tooling/grammar/fixtures/` and run `tooling/grammar/validate-grammar.sh` (must stay green); bump the Zed grammar `commit` SHA in `tooling/zed-dormantql/extension.toml` after push
- [ ] T049 Write the migration guide (old modifiers → constraints) under `tooling/docs/` (or `docs/`) and link from `specs/012-edgeql-constraints/quickstart.md`
- [ ] T050 Update the DSL compatibility baseline + bump the package MAJOR version; record the breaking change + migration in release notes
- [ ] T051 [P] AOT smoke: confirm `tests/Dormant.Aot.SmokeTests` schema (migrated) still publishes AOT-clean with constraints
- [ ] T052 Full conformance + Verify sweep on `Dormant.slnx` (PG via Testcontainers + SQLite); `dotnet format` + CSharpier clean

**Checkpoint**: Grammar, docs, migration, release metadata, and CI all green for the MAJOR release.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational, P-A)** blocks everything.
- **US1 (P1)** depends on Phase 2; delivers the MVP.
- **US2 (P2)**, **US3 (P2)** depend on Phase 2 + the US1 IR/renderer skeleton; US2 and US3 are largely independent of each other.
- **US4 (P3)** depends on Phase 2 + US1 (reuses member-constraint lowering).
- **US5 (P3)** depends on Phase 2 + US1 (reuses constraint lowering); independent of US3/US4.
- **Phase 8 (Polish)** starts after the desired stories land; T045–T048 (grammar) can proceed in parallel once the final syntax is stable (end of Phase 2).

## Parallel Opportunities

- Setup: T002 + T003 parallel.
- Foundational: T011 + T012 (IR vs renderer base) parallel; T014 parallel with rendering once model exists.
- Each story's Verify snapshot + diagnostic tests ([P]) parallel with sibling impl tasks.
- Grammar tasks T045–T047 parallel (different files); T048 after them.

## Implementation Strategy & MVP

- **MVP = Phase 1 + Phase 2 + US1**: member-level standard constraint library enforced by the DB on
  both providers, with `primary`/`concurrency` re-expressed and legacy syntax rejected.
- Then US2 (multi-field/check) → US3 (named) → US4 (scalars) → US5 (inheritance) → Polish.
- **Risk**: P-B (constraint IR + DDL, US1/US2) is the high-risk core; land it and its conformance
  before scalars/inheritance. The MAJOR break (BC-1) means T013 (migrate repo schemas) + the grammar
  update (T045–T048) must land in the same PR so the build and editor tooling stay consistent.
