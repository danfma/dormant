# Tasks: LINQ-Style DQL Grammar

**Feature**: 003-linq-dql-grammar | **Branch**: `003-linq-dql-grammar`
**Spec**: spec.md | **Plan**: plan.md | **Inputs**: research.md, data-model.md, contracts/, quickstart.md

Front-end grammar replacement in the generator. Runtime (SQL IR/renderer, entities, session, provider) is
NOT modified. Tests: TUnit generator (Verify + cacheability) + provider against real Docker PostgreSQL + AOT
smoke. All `.dql` paths below are the canonical unit extension (queries + mutations); `.dqls` schema grammar
unchanged.

## Phase 1: Setup

- [X] T001 Establish the pre-change baseline: build `Dormant.slnx` and run the existing generator + core + provider test suites so regressions from the grammar swap are detectable; record the green baseline in the PR notes (no code change).

## Phase 2: Foundational (blocking — MUST complete before any user story)

- [X] T002 [P] Add lexer tokens `==` (EqualEqual), `!=` (BangEqual), `&&` (AmpAmp), `||` (PipePipe), `!` (Bang) — TokenKind + two-char lookahead lexing — in src/Dormant.SourceGeneration/Parsing/Lexer.cs
- [X] T003 Repurpose `=` as the assignment operator and retire the removed-form operators `:=`/`::`/`->` from the accepted token stream (kept only if needed to emit migration diagnostics) in src/Dormant.SourceGeneration/Parsing/Lexer.cs
- [X] T004 [P] Add grammar diagnostic descriptors (removed-`002`-syntax, missing/duplicate/undeclared alias, unqualified member, wrong clause order, unknown entity/member/parameter, missing required insert member, returning/select member-not-in-shape) in src/Dormant.SourceGeneration/Diagnostics/DiagnosticDescriptors.cs
- [X] T005 [P] Add a `SnakeToPascal` unit-name helper (inverse of the existing `ToSnakeCase`) for generated method names in src/Dormant.SourceGeneration/Emit/NamingConvention.cs
- [X] T006 Define shared parsed-model nodes — Subject, Alias, MemberRef, Predicate (Comparison/And/Or/Not), CompareOp, Assignment, ValueExpr, Parameter, OrderTerm, ResultShape, WithBinding — reused by both query and mutation parsing, in src/Dormant.SourceGeneration/Parsing/ (e.g. GrammarNodes.cs)

## Phase 3: User Story 1 — Read query in the LINQ grammar (P1)

**Goal**: `query name(...) { from E a where … order by … select a }` → typed full-entity method.
**Independent test**: author `users_by_email`, build, query real DB, rows match predicate + order.

- [X] T007 [US1] Rewrite QueryModel: alias-bound Subject, optional Predicate tree, OrderBy MemberRefs, ResultShape (entity vs projection) in src/Dormant.SourceGeneration/Parsing/QueryModel.cs
- [X] T008 [US1] Rewrite QueryParser to the brace grammar `query snake_name(params) { from E a [where pred] [order by a.f asc|desc]* select a }`, enforcing canonical clause order, binding aliases, emitting located diagnostics, in src/Dormant.SourceGeneration/Parsing/QueryParser.cs
- [X] T009 [US1] Update QueryEmitter to map alias-qualified members + operators (`==`→`=`, `!=`→`<>`, `&&`→AND, `||`→OR, `!`→NOT) onto the existing SelectStatement IR and to name the method via SnakeToPascal, in src/Dormant.SourceGeneration/Query/QueryEmitter.cs
- [X] T010 [US1] Confirm the DormantGenerator `.dql` unit glob parses `query` blocks through the rewritten parser (pipeline shape unchanged) in src/Dormant.SourceGeneration/DormantGenerator.cs
- [X] T011 [P] [US1] Generator tests: full-entity query emits expected schema-qualified SELECT + method; diagnostics for missing/undeclared alias, unqualified member, unknown entity/member, wrong clause order, in tests/Dormant.SourceGeneration.Tests/QueryEmitTests.cs and a new GrammarDiagnosticsTests.cs

## Phase 4: User Story 2 — Insert mutation with inferred id + returning + multi-command (P1)

**Goal**: `mutation name(...) { insert E a { a.f = p } [returning …] }` → method; default returns id; `returning`/trailing read shapes richer results; multi-command flows values via `with`.
**Independent test**: `create_user` returns id; `… returning u` returns the entity; a two-command block with a `with`-bound id returns the trailing read's shape.

- [X] T012 [US2] Rewrite CommandModel/MutationUnit: alias Subject, Assignments (`MemberRef = ValueExpr`), Commands[] sequence, WithBinding[], ResultShape inference + optional Returning, in src/Dormant.SourceGeneration/Parsing/CommandModel.cs
- [X] T013 [US2] Rewrite CommandParser for `mutation snake_name(params) { insert E a { a.f = expr } [returning expr] }`, located diagnostics (missing required member, unknown entity/member/param), in src/Dormant.SourceGeneration/Parsing/CommandParser.cs
- [X] T014 [US2] Update CommandEmitter: insert → InsertStatement IR, default result = the entity's PK value (`ValueTask<PkType>`), method named via SnakeToPascal, in src/Dormant.SourceGeneration/Command/CommandEmitter.cs
- [X] T015 [US2] CommandEmitter: `returning <expr>` shaping mirroring `select` — `returning a` (entity via binding Materialize), `returning { … }` (distinct projection), `returning a.f` (scalar) — in src/Dormant.SourceGeneration/Command/CommandEmitter.cs
- [ ] T016 [US2] CommandParser + CommandEmitter: multi-command sequence + `with x = expr` bindings compiled to C# locals threaded into later commands; the trailing read/`returning` determines the unit result, in src/Dormant.SourceGeneration/Parsing/CommandParser.cs and src/Dormant.SourceGeneration/Command/CommandEmitter.cs
  - **BLOCKED (validated 2026-05-26)**: the compelling use case (flow a parent's id into a child's foreign key, e.g. `with u = insert User …; insert Post p { p.author = u }`) requires **`002` FR-019 (ref → `<ref>_id` FK column in DDL + ref assignment in commands)**, which is NOT implemented — `EntityBindingEmitter` builds `CREATE TABLE` from value properties only (`entity.Properties`), refs are emitted as read-side `Ref<T>` nav but have no FK column, and commands cannot assign a ref. Prerequisite: implement FR-019 (FK column + `alias.ref = expr`), then layer multi-command/`with` (inline statement sequence in the generated method; `with u` binds the inserted PK; trailing read/`returning` sets the result). Mirrors `002` US2 (nested writes) which was likewise blocked on FK/PK. Recommend a dedicated `/speckit-clarify` + `/speckit-plan` pass before implementing.
- [X] T017 [P] [US2] Generator tests: insert→id default; `returning` entity/projection/scalar shapes; missing-required-member diagnostic; multi-command trailing-read result type, in tests/Dormant.SourceGeneration.Tests/CommandEmitTests.cs
- [X] T018 [US2] Provider tests (real Docker PostgreSQL): insert returns id; `insert … returning u` materializes the row; multi-command `with`-bound id flows to a dependent command, in tests/Dormant.Provider.PostgreSql.Tests/CommandInsertTests.cs

## Phase 5: User Story 3 — Update/delete with where + affected-count (P2)

**Goal**: `update E a where … set { … }` and `delete E a where …` → affected count; optimistic concurrency via a version match in `where`.
**Independent test**: `set_user_name` returns 1 on match, 0 on stale version; delete returns the count.

- [X] T019 [US3] CommandParser: `update E a where pred set { a.f = expr }` and `delete E a where pred`, canonical clause order enforced, in src/Dormant.SourceGeneration/Parsing/CommandParser.cs
- [X] T020 [US3] CommandEmitter: update → UpdateStatement, delete → DeleteStatement; predicate logical operators → AND/OR/NOT; default result = affected count (`ValueTask<int>`) unless `returning`; concurrency = `where` match on the version field, in src/Dormant.SourceGeneration/Command/CommandEmitter.cs
- [X] T021 [P] [US3] Generator tests: update emits correct UPDATE (SET + WHERE); delete emits DELETE; operator mapping; count result type, in tests/Dormant.SourceGeneration.Tests/CommandEmitTests.cs
- [X] T022 [US3] Provider tests: update affects 1 on match / 0 on stale version; delete returns count — migrate the existing concurrency/delete tests to the new grammar, in tests/Dormant.Provider.PostgreSql.Tests/CommandConcurrencyTests.cs

## Phase 6: User Story 4 — Projection select block (P2)

**Goal**: `select { a.x, a.y }` → distinct projection type exposing exactly those members.
**Independent test**: `user_contacts` result type exposes only id + email; accessing another member fails to compile.

- [X] T023 [US4] QueryEmitter: `select { a.f, … }` → distinct projection type, reusing the `002` projection materialization machinery, in src/Dormant.SourceGeneration/Query/QueryEmitter.cs
- [X] T024 [P] [US4] Generator test: projection emits a distinct type with exactly the selected members; a non-selected member access is a compile error (negative/Verify snapshot), in tests/Dormant.SourceGeneration.Tests/QueryEmitTests.cs

## Phase 7: User Story 5 — Replace grammar across samples/tests + removed-syntax diagnostics (P3)

**Goal**: the removed `002` forms no longer parse; all sample/test units use the new grammar; full suite green.
**Independent test**: a unit in the old syntax yields a migration diagnostic; the migrated suite + AOT publish pass.

- [X] T025 [US5] Emit located migration diagnostics for the removed `002` forms (`command …`, `… = …;`, leading-dot `.field`, `:=`, `and`/`or`) instead of generic parse errors, in src/Dormant.SourceGeneration/Parsing/QueryParser.cs and src/Dormant.SourceGeneration/Parsing/CommandParser.cs
- [X] T026 [P] [US5] Generator tests: each removed form produces its specific migration diagnostic, in tests/Dormant.SourceGeneration.Tests/GrammarDiagnosticsTests.cs
- [X] T027 [US5] Migrate the sample units to the new grammar and rename samples/Dormant.Sample.Quickstart/schema/app.query → app.dql; update the sample Program.cs to call the PascalCase generated methods, in samples/Dormant.Sample.Quickstart/
- [X] T028 [US5] Migrate the provider test `.dql` units (e.g. schema/catalog.dql) to `query`/`mutation` grammar, in tests/Dormant.Provider.PostgreSql.Tests/schema/
- [X] T029 [US5] Migrate the AOT smoke `.dql` + Program.cs to the new grammar and re-verify a zero-warning publish, in tests/Dormant.Aot.SmokeTests/
- [X] T030 [US5] Verification gate: build `Dormant.slnx` and run generator + core + provider (Docker) suites + AOT publish — all green on the new grammar (no `002`-syntax remaining)

## Phase 8: Polish & Cross-Cutting

- [X] T031 [P] Cacheability + determinism tests for the new unit pipeline (LoadUnitFiles/ParseQueries/ParseMutations cached on unchanged rerun) — adapt the existing cacheability test — in tests/Dormant.SourceGeneration.Tests/GrammarCacheabilityTests.cs
- [X] T032 [P] Refresh Verify snapshots — **N/A**: the generator tests are assertion-based (`.Contains`), there are no Verify `.verified.txt` snapshots in this project, so nothing to refresh.
- [X] T033 [P] Re-verify the PublicApiAnalyzers baseline — **N/A (deferred to 001 T017)**: PublicApiAnalyzers is version-pinned but not wired per shipped project and no `PublicAPI.*.txt` baselines exist yet (see src/Directory.Build.props). Public surface unchanged by 003/004 (slnx build 0/0). Wiring the analyzer+baselines is a 001 foundational task, out of scope here.
- [X] T034 [P] Verify quickstart.md examples against the built sample — sample (schema/app.dql + Program.cs) uses the new grammar and builds green (slnx 0/0); quickstart examples are valid grammar; no docs reference the removed 002 forms.
- [X] T035 Confirm contracts/dql-grammar.md matches the implemented grammar and record it as the DSL compatibility baseline (Constitution II) — added an "Implementation status (003 baseline)" table marking implemented vs deferred (`||`/`!`, multi-command/`with`).

## Dependencies & order

- Setup (T001) → Foundational (T002–T006) → US1 (T007–T011) → US2 (T012–T018) → US3 (T019–T022) → US4 (T023–T024) → US5 (T025–T030) → Polish (T031–T035).
- US2 depends on US1 (trailing-read + projection reuse); US4 depends on US1 (select infra); US5 depends on US1–US4. Foundational blocks everything.
- [P] = parallelizable (distinct files, no incomplete dep). Within a phase, non-[P] tasks touching the same file run in order.

## Parallel examples

- Foundational: T002, T004, T005 in parallel (Lexer tokens / Diagnostics / Naming — distinct files); T003 after T002 (same Lexer file); T006 independent.
- US1: T011 [P] tests authored alongside T007–T009 implementation.
- Polish: T031–T034 all [P].

## MVP

US1 + US2 (both P1): a read query and an insert mutation authored in the new grammar, round-tripping against
real PostgreSQL. US3/US4 complete the write/read surface; US5 makes the replacement total.

## Phase 9: FK columns + `with`-block (FR-020/021/022) — supersedes the blocked T016

Per the 2026-05-26 clarification (plan.md "Post-clarify design"): T016's "multi-command sequence" is replaced
by a **`with name = (expr)` block + single terminal `select`**, and its prerequisite is the FK column
(FR-020). Everything in Phases 1–8 (cutover + `returning`) is done and green. Implement FR-020 first
(standalone, independently valuable), then the `with`-block.

- [X] T036 [US2] FR-020: in `EntityBindingEmitter`, add a `<ref>_id` column (typed as the target entity's primary-key type, nullable iff the ref is optional) to the `CREATE TABLE` for each single `ReferenceKind.Ref` member; value-column SELECT/materialize unchanged — in src/Dormant.SourceGeneration/Schema/EntityBindingEmitter.cs
- [ ] T037 [US2] FR-020: allow `alias.ref = <expr>` assignment in a command — parse the ref member (resolve to its `<ref>_id` column) and validate it against the entity's references — in src/Dormant.SourceGeneration/Parsing/UnitParser.cs and src/Dormant.SourceGeneration/Parsing/CommandModel.cs
- [ ] T038 [US2] FR-020: in `CommandEmitter`, write the `<ref>_id` column from a ref-assignment value (parameter or literal), with the target PK type — in src/Dormant.SourceGeneration/Command/CommandEmitter.cs
- [ ] T039 [P] [US2] FR-020 tests: generator asserts the `<ref>_id` column appears in `CREATE TABLE` and that an `insert` assigning the ref writes it; provider test inserts an entity with a ref and confirms the FK column is persisted — in tests/Dormant.SourceGeneration.Tests/ and tests/Dormant.Provider.PostgreSql.Tests/
- [ ] T040 [US2] FR-022: parse a `with name = ( insert|update|delete|select … )` block (zero or more) followed by a single terminal `select` (entity/projection) — extend the unit model with the bindings + terminal shape — in src/Dormant.SourceGeneration/Parsing/UnitParser.cs and src/Dormant.SourceGeneration/Parsing/CommandModel.cs
- [ ] T041 [US2] FR-022: emit each binding as its own SQL statement executed in declaration order within the session transaction (`with`-bound results → C# locals), then the terminal `select` composes the unit result — provider-portable, no CTE — in src/Dormant.SourceGeneration/Command/CommandEmitter.cs
- [ ] T042 [US2] FR-021: a `with`-bound name carries the expression's result object — `name.field` projects, and a reference/FK context (`alias.ref = name`) resolves it to the target's primary key (id) — in src/Dormant.SourceGeneration/Parsing/UnitParser.cs and src/Dormant.SourceGeneration/Command/CommandEmitter.cs
- [ ] T043 [P] [US2] tests: generator asserts the `with`-block emits an ordered statement sequence + terminal projection; provider test proves the parent→child id flow (`with u = (insert User …); insert Post p { p.author = u } returning p`) sets `post.author_id = u.id` — in tests/Dormant.SourceGeneration.Tests/ and tests/Dormant.Provider.PostgreSql.Tests/
- [ ] T044 [US2] Verification gate: build `Dormant.slnx` 0/0, generator + provider (Docker) suites green, AOT smoke 0-warning, and cacheability holds for the `with`-block pipeline

**Dependencies**: T036 → T037 → T038 → T039 (FR-020 FK column, shippable on its own); then T040 → T041 → T042 → T043 (`with`-block, depends on FR-020 for the FK-flow); T044 gates the phase. **MVP of this phase**: T036–T039 (FK column) — independently valuable even before the `with`-block.
