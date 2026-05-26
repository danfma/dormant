# Tasks: Project Documentation & Developer README

**Input**: Design documents from `/specs/006-project-documentation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/documentation-set.md](./contracts/documentation-set.md), [quickstart.md](./quickstart.md)

**Tests**: No automated test suite is required for this documentation-only feature. Validation tasks are included for internal links, DormantQL example grammar, capability status labels, and traceability.

**Organization**: Tasks are grouped by user story so each documentation increment can be reviewed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency on an incomplete task.
- **[Story]**: User story label for story phases only.
- Every task includes an exact file path.

## Phase 1: Setup (Shared Documentation Structure)

**Purpose**: Create the documentation surface and empty page targets so all planned links have destinations.

- [ ] T001 Create the documentation directory structure at `docs/` and `docs/guides/`
- [ ] T002 Create empty documentation page targets in `docs/index.md`, `docs/getting-started.md`, `docs/status.md`, `docs/architecture.md`, `docs/design-decisions.md`, and `docs/speckit-sources.md`
- [ ] T003 Create empty guide page targets in `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, and `docs/guides/naming-and-generated-code.md`

---

## Phase 2: Foundational (Source Mapping & Shared Conventions)

**Purpose**: Establish the shared source-of-truth map and terminology that all user-facing docs must follow.

**CRITICAL**: Complete this phase before writing story-specific documentation so public claims stay consistent.

- [ ] T004 Draft the SpecKit source inventory table in `docs/speckit-sources.md` from `.specify/memory/constitution.md`, `specs/001-orm-aot-sourcegen/`, `specs/002-immutable-command-dml/`, `specs/003-linq-dql-grammar/`, `specs/004-raw-string-sql/`, `specs/005-sqlite-nmemory-providers/spec.md`, `samples/Dormant.Sample.Quickstart/schema/app.dqls`, and `samples/Dormant.Sample.Quickstart/schema/app.dql`
- [ ] T005 Define the shared capability status vocabulary in `docs/status.md` using Implemented, Planned, Deferred, and Illustrative labels from `specs/006-project-documentation/contracts/documentation-set.md`
- [ ] T006 Record current implementation status for PostgreSQL, SQLite, NMemory, raw string SQL literals, `with` value-flow, and deferred `||`/`!` operators in `docs/status.md`
- [ ] T007 Add the reader-path table of contents skeleton in `docs/index.md` linking to `docs/getting-started.md`, `docs/status.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, `docs/guides/naming-and-generated-code.md`, `docs/architecture.md`, `docs/design-decisions.md`, and `docs/speckit-sources.md`

**Checkpoint**: Documentation targets, source map, status vocabulary, and docs navigation skeleton exist.

---

## Phase 3: User Story 1 - Evaluator Understands the Project from the README (Priority: P1) MVP

**Goal**: A first-time developer can read only the root README and understand what Dormant is, why it exists, its differentiators, current status, prerequisites, and where to go next.

**Independent Test**: Read only `README.md` and confirm the reader can state Dormant's purpose, at least three differentiators, current maturity/status, and links for next steps without opening source files.

### Implementation for User Story 1

- [ ] T008 [US1] Write the root overview, value proposition, and maturity/status summary in `README.md`
- [ ] T009 [US1] Add the differentiators section covering AOT-first design, build-time SQL, no hot-path runtime reflection/query compilation, statically-known result types, DormantQL, immutable command-driven writes, explicit `Ref*` load state, and PostgreSQL-primary provider in `README.md`
- [ ] T010 [US1] Add a minimal illustrative DormantQL schema/query/mutation snippet consistent with `samples/Dormant.Sample.Quickstart/schema/app.dqls` and `samples/Dormant.Sample.Quickstart/schema/app.dql` in `README.md`
- [ ] T011 [US1] Add prerequisites and local workflow notes for .NET 10 SDK, `./build.sh`, and Docker-required PostgreSQL provider tests in `README.md`
- [ ] T012 [US1] Add next-step links from `README.md` to `docs/index.md`, `docs/getting-started.md`, `docs/status.md`, `docs/architecture.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, and `docs/guides/naming-and-generated-code.md`
- [ ] T013 [US1] Add README source traceability entries for overview, differentiators, status, prerequisites, and examples in `docs/speckit-sources.md`
- [ ] T014 [US1] Validate the README-only evaluator test from `specs/006-project-documentation/spec.md` against `README.md`

**Checkpoint**: User Story 1 is independently reviewable as the MVP documentation increment.

---

## Phase 4: User Story 2 - Integrator Follows the Docs to First Success (Priority: P2)

**Goal**: A developer can follow the docs to define a minimal schema, write one read query and one write mutation, understand generated naming, and know how generation/build/run fits together.

**Independent Test**: Follow only `docs/getting-started.md` plus the linked language guides and confirm the reader can author a minimal `.dqls` schema, one `.dql` query, one `.dql` mutation, and predict the generated C# method/entity naming.

### Implementation for User Story 2

- [ ] T015 [P] [US2] Write the ordered first-success path in `docs/getting-started.md` covering prerequisites, local project reference approach, schema definition, authored units, build/generation, and run/check expectations
- [ ] T016 [P] [US2] Write the DormantQL schema guide in `docs/guides/dormantql-schema.md` covering modules, entities, `name: TypeExpr[?]`, scalar values, single refs, `Set/List/Bag/Map` collections, primary keys, concurrency members, required-by-default behavior, and optionality
- [ ] T017 [P] [US2] Write the query and mutation guide in `docs/guides/queries-and-mutations.md` covering `query`, `mutation`, aliases, alias-qualified members, clause order, lowercase parameter types, supported operators, `returning`, result inference, removed `002` forms, and status caveats for planned/deferred constructs
- [ ] T018 [P] [US2] Write the naming and generated-code guide in `docs/guides/naming-and-generated-code.md` covering snake_case unit names to PascalCase methods, PascalCase entities, namespace derivation, mutation result inference, projection types, and raw string SQL literal readability
- [ ] T019 [US2] Add cross-links from `docs/getting-started.md` to `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, `docs/guides/naming-and-generated-code.md`, `docs/status.md`, and `docs/speckit-sources.md`
- [ ] T020 [US2] Add source traceability entries for getting started, schema grammar, query/mutation grammar, naming, and generated-code examples in `docs/speckit-sources.md`
- [ ] T021 [US2] Validate every `.dqls`, `.dql`, and C# snippet in `docs/getting-started.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, and `docs/guides/naming-and-generated-code.md` against `specs/003-linq-dql-grammar/contracts/dql-grammar.md` and the sample files
- [ ] T022 [US2] Validate the first-success independent test from `specs/006-project-documentation/spec.md` against `docs/getting-started.md`

**Checkpoint**: User Story 2 can be reviewed by following the getting-started guide and linked usage guides without source-code lookup.

---

## Phase 5: User Story 3 - Curious Developer or Contributor Explores Design and Rationale (Priority: P3)

**Goal**: A contributor or evaluator can understand the architecture, dependency direction, generator pipeline, provider boundary, constitution principles, and design history from SpecKit features `001` through `005`.

**Independent Test**: Read `docs/architecture.md` and `docs/design-decisions.md` and confirm the reader can reproduce the component map, dependency direction, build-time SQL pipeline, provider status, and governing principles without contradictions against the constitution or SpecKit plans.

### Implementation for User Story 3

- [ ] T023 [P] [US3] Write the component map and package/layer overview in `docs/architecture.md` covering `src/Dormant.Abstractions/`, `src/Dormant.Core/`, `src/Dormant.SourceGeneration/`, `src/Dormant.Provider.PostgreSql/`, `src/Dormant.Spatial.PostgreSql/`, `src/Dormant.Tool/`, `samples/Dormant.Sample.Quickstart/`, and `tests/`
- [ ] T024 [P] [US3] Write the design rationale page in `docs/design-decisions.md` covering AOT-first design, build-time SQL, no runtime reflection/query compilation on hot paths, immutable command-driven writes, the `003` grammar replacement, raw string SQL literals, and the `005` SQLite/dialect direction with deferred NMemory
- [ ] T025 [US3] Add generator pipeline and provider-boundary sections to `docs/architecture.md` covering `.dqls`/`.dql` parsing, structured IR, per-dialect rendering direction, generated C# method surfaces, and runtime provider execution
- [ ] T026 [US3] Add constitution principle summaries to `docs/design-decisions.md` covering Developer Experience First, Interface & Compatibility Stability, Statically-Known Safe Data Access, First-Class Tooling, Performance by Default, and Quality & Testing Discipline
- [ ] T027 [US3] Add architecture and design-decision source traceability entries in `docs/speckit-sources.md`
- [ ] T028 [US3] Validate the architecture independent test from `specs/006-project-documentation/spec.md` against `docs/architecture.md` and `docs/design-decisions.md`

**Checkpoint**: User Story 3 can be reviewed independently by a contributor using the architecture and design-decision pages.

---

## Phase 6: Polish & Cross-Cutting Validation

**Purpose**: Verify the complete documentation set against feature success criteria and clean up reader experience.

- [ ] T029 [P] Normalize headings, page introductions, and final navigation links across `README.md`, `docs/index.md`, `docs/getting-started.md`, `docs/status.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, `docs/guides/naming-and-generated-code.md`, `docs/architecture.md`, `docs/design-decisions.md`, and `docs/speckit-sources.md`
- [ ] T030 [P] Review all documentation for English-only wording and remove Portuguese phrases from `README.md` and `docs/`
- [ ] T031 Validate all internal Markdown links in `README.md` and `docs/` using the `rg` command from `specs/006-project-documentation/quickstart.md`
- [ ] T032 Validate capability labels and shipped/planned/deferred claims across `README.md`, `docs/status.md`, `docs/getting-started.md`, `docs/guides/queries-and-mutations.md`, `docs/architecture.md`, and `docs/design-decisions.md`
- [ ] T033 Validate traceability coverage for every major claim in `docs/speckit-sources.md` against `README.md` and `docs/`
- [ ] T034 Run `./build.sh build` from the repository root and record any documentation-relevant build caveats in `docs/getting-started.md`
- [ ] T035 Perform the final success-criteria review from `specs/006-project-documentation/spec.md` for SC-001 through SC-006 against `README.md` and `docs/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup** has no dependencies.
- **Phase 2: Foundational** depends on Phase 1 and blocks all user-story writing.
- **Phase 3: US1** depends on Phase 2 and is the MVP.
- **Phase 4: US2** depends on Phase 2; it can start after the shared source/status conventions are done and should align with US1 links.
- **Phase 5: US3** depends on Phase 2; it can proceed in parallel with US2 after shared source/status conventions are done.
- **Phase 6: Polish** depends on the user stories selected for delivery.

### User Story Dependencies

- **US1 (P1)**: No dependency on US2 or US3 after Foundational; delivers the README MVP.
- **US2 (P2)**: No hard dependency on US3; should preserve README link destinations and status labels from US1.
- **US3 (P3)**: No hard dependency on US2; should reuse the shared source map and status vocabulary.

### Within Each User Story

- Draft page content before validation tasks for that story.
- Add traceability entries before final story validation.
- Validate examples and status labels before cross-cutting polish.

---

## Parallel Opportunities

- T002 and T003 can be done after T001 and touch different files.
- T015, T016, T017, and T018 can run in parallel for US2 because each writes a different page.
- T023 and T024 can run in parallel for US3 because each writes a different page.
- T029 and T030 can run in parallel during polish if edits are coordinated by file.

---

## Parallel Example: User Story 2

```text
Task: "Write the ordered first-success path in docs/getting-started.md"
Task: "Write the DormantQL schema guide in docs/guides/dormantql-schema.md"
Task: "Write the query and mutation guide in docs/guides/queries-and-mutations.md"
Task: "Write the naming and generated-code guide in docs/guides/naming-and-generated-code.md"
```

---

## Parallel Example: User Story 3

```text
Task: "Write the component map and package/layer overview in docs/architecture.md"
Task: "Write the design rationale page in docs/design-decisions.md"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational source/status conventions.
3. Complete Phase 3: README MVP.
4. Stop and validate the README-only independent test.

### Incremental Delivery

1. Add US1 for evaluator comprehension.
2. Add US2 for first-success adoption.
3. Add US3 for architecture and contributor confidence.
4. Run Phase 6 validation across whichever stories are included in the delivery.

### Documentation Quality Strategy

Keep examples small, status-labeled, and traceable. Prefer copying or lightly adapting current sample syntax over inventing new examples. When a capability is specified but not clearly implemented, label it Planned or Illustrative rather than available.

## Notes

- `[P]` tasks are safe to work on in parallel only when file ownership is coordinated.
- This feature intentionally avoids new docs tooling dependencies.
- Do not change product code, generator behavior, or the DormantQL grammar while completing these tasks.
- Full provider test runs may require Docker; the documentation feature itself is validated through content, link, grammar, and traceability checks.
