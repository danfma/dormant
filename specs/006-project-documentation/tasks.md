# Tasks: Project Documentation & Developer README

**Input**: Design documents from `/specs/006-project-documentation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/documentation-set.md](./contracts/documentation-set.md), [quickstart.md](./quickstart.md)

**Tests**: No separate automated test suite is required for this feature. Validation tasks are included for documentation links, DormantQL grammar, sample API builds, capability status labels, and traceability.

**Organization**: Tasks are grouped by user story so each documentation and sample increment can be reviewed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency on an incomplete task.
- **[Story]**: User story label for story phases only.
- Every task includes an exact file path.

## Phase 1: Setup (Shared Documentation and Sample Structure)

**Purpose**: Create the documentation surface and sample project targets so planned links and solution entries have destinations.

- [ ] T001 Create the documentation directory structure at `docs/` and `docs/guides/`
- [ ] T002 [P] Create documentation page targets in `docs/index.md`, `docs/getting-started.md`, `docs/status.md`, `docs/architecture.md`, `docs/design-decisions.md`, and `docs/speckit-sources.md`
- [ ] T003 [P] Create guide page targets in `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, and `docs/guides/naming-and-generated-code.md`
- [ ] T004 [P] Create Todo API sample directories at `samples/Dormant.Sample.TodoApi/` and `samples/Dormant.Sample.TodoApi/schema/`
- [ ] T005 [P] Create Scheduling API sample directories at `samples/Dormant.Sample.SchedulingApi/` and `samples/Dormant.Sample.SchedulingApi/schema/`
- [ ] T006 Add Todo and Scheduling API project entries to `Dormant.slnx`

---

## Phase 2: Foundational (Source Mapping, Status, and Shared Sample Conventions)

**Purpose**: Establish shared source-of-truth mapping, status vocabulary, and sample API conventions before story-specific writing.

**CRITICAL**: Complete this phase before writing story-specific documentation or sample behavior so public claims stay consistent.

- [ ] T007 Draft the SpecKit source inventory table in `docs/speckit-sources.md` from `.specify/memory/constitution.md`, `AGENTS.md`, `CLAUDE.md`, `specs/001-orm-aot-sourcegen/`, `specs/002-immutable-command-dml/`, `specs/003-linq-dql-grammar/`, `specs/004-raw-string-sql/`, `specs/005-sqlite-nmemory-providers/spec.md`, `samples/Dormant.Sample.Quickstart/schema/app.dqls`, and `samples/Dormant.Sample.Quickstart/schema/app.dql`
- [ ] T008 Define the shared capability status vocabulary in `docs/status.md` using Implemented, Planned, Deferred, and Illustrative labels from `specs/006-project-documentation/contracts/documentation-set.md`
- [ ] T009 Record current implementation status for PostgreSQL, SQLite, NMemory, raw string SQL literals, `with` value-flow, and deferred `||`/`!` operators in `docs/status.md`
- [ ] T010 Add the reader-path table of contents skeleton in `docs/index.md` linking to `docs/getting-started.md`, `docs/status.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, `docs/guides/naming-and-generated-code.md`, `docs/architecture.md`, `docs/design-decisions.md`, and `docs/speckit-sources.md`
- [ ] T011 Define shared sample API build/run conventions in `docs/getting-started.md`, including local build expectations, default provider assumptions, and provider/container caveats

**Checkpoint**: Documentation targets, source map, status vocabulary, docs navigation skeleton, and sample API conventions exist.

---

## Phase 3: User Story 1 - Evaluator Understands the Project from the README (Priority: P1) MVP

**Goal**: A first-time developer can read only the root README and understand what Dormant is, why it exists, its differentiators, current status, prerequisites, sample availability, and where to go next.

**Independent Test**: Read only `README.md` and confirm the reader can state Dormant's purpose, at least three differentiators, current maturity/status, links for next steps, and the existence of the Todo/Scheduling sample APIs without opening source files.

### Implementation for User Story 1

- [ ] T012 [US1] Write the root overview, value proposition, and maturity/status summary in `README.md`
- [ ] T013 [US1] Add the differentiators section covering AOT-first design, build-time SQL, no hot-path runtime reflection/query compilation, statically-known result types, DormantQL, immutable command-driven writes, explicit `Ref*` load state, and PostgreSQL-primary provider in `README.md`
- [ ] T014 [US1] Add a minimal DormantQL schema/query/mutation snippet consistent with `samples/Dormant.Sample.Quickstart/schema/app.dqls` and `samples/Dormant.Sample.Quickstart/schema/app.dql` in `README.md`
- [ ] T015 [US1] Add a sample applications section linking to `samples/Dormant.Sample.TodoApi/` and `samples/Dormant.Sample.SchedulingApi/` in `README.md`
- [ ] T016 [US1] Add prerequisites and local workflow notes for .NET 10 SDK, `./build.sh`, sample API build/run expectations, and Docker-required PostgreSQL provider tests in `README.md`
- [ ] T017 [US1] Add next-step links from `README.md` to `docs/index.md`, `docs/getting-started.md`, `docs/status.md`, `docs/architecture.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, and `docs/guides/naming-and-generated-code.md`
- [ ] T018 [US1] Add README source traceability entries for overview, differentiators, status, prerequisites, examples, and sample API references in `docs/speckit-sources.md`
- [ ] T019 [US1] Validate the README-only evaluator test from `specs/006-project-documentation/spec.md` against `README.md`

**Checkpoint**: User Story 1 is independently reviewable as the MVP documentation increment.

---

## Phase 4: User Story 2 - Integrator Follows the Docs and Sample APIs to First Success (Priority: P2)

**Goal**: A developer can follow the docs to define a minimal schema, write one read query and one write mutation, understand generated naming, and inspect/build Todo and Scheduling ASP.NET Core API samples.

**Independent Test**: Follow only `docs/getting-started.md` plus the linked language guides and sample projects; confirm the reader can author a minimal `.dqls` schema, one `.dql` query, one `.dql` mutation, predict generated C# naming, and identify read/write endpoints in both sample APIs.

### Implementation for User Story 2

- [ ] T020 [P] [US2] Create `samples/Dormant.Sample.TodoApi/Dormant.Sample.TodoApi.csproj` targeting `net10.0` with ASP.NET Core API settings, local Dormant project references, source generator analyzer reference, and `schema/*.dqls` plus `schema/*.dql` as AdditionalFiles
- [ ] T021 [P] [US2] Create `samples/Dormant.Sample.SchedulingApi/Dormant.Sample.SchedulingApi.csproj` targeting `net10.0` with ASP.NET Core API settings, local Dormant project references, source generator analyzer reference, and `schema/*.dqls` plus `schema/*.dql` as AdditionalFiles
- [ ] T022 [P] [US2] Author the Todo sample schema in `samples/Dormant.Sample.TodoApi/schema/todo.dqls` with task-list fields, completion state, timestamps or ownership fields, primary key, and concurrency member
- [ ] T023 [P] [US2] Author the Scheduling sample schema in `samples/Dormant.Sample.SchedulingApi/schema/scheduling.dqls` with scheduled-task fields, planned time, status, primary key, and concurrency member without recurrence/calendar constructs
- [ ] T024 [P] [US2] Author Todo query and mutation units in `samples/Dormant.Sample.TodoApi/schema/todo.dql` with at least one read query and one write mutation using current `003` syntax
- [ ] T025 [P] [US2] Author Scheduling query and mutation units in `samples/Dormant.Sample.SchedulingApi/schema/scheduling.dql` with at least one read query and one write mutation using current `003` syntax
- [ ] T026 [US2] Implement Todo API startup, provider setup, and minimal read/write HTTP endpoints in `samples/Dormant.Sample.TodoApi/Program.cs`
- [ ] T027 [US2] Implement Scheduling API startup, provider setup, and minimal read/write HTTP endpoints in `samples/Dormant.Sample.SchedulingApi/Program.cs`
- [ ] T028 [P] [US2] Create Todo API sample notes with endpoint and provider assumptions in `samples/Dormant.Sample.TodoApi/README.md`
- [ ] T029 [P] [US2] Create Scheduling API sample notes with endpoint and provider assumptions in `samples/Dormant.Sample.SchedulingApi/README.md`
- [ ] T030 [P] [US2] Write the ordered first-success path in `docs/getting-started.md` covering prerequisites, local project reference approach, schema definition, authored units, build/generation, sample API inspection, and run/check expectations
- [ ] T031 [P] [US2] Write the DormantQL schema guide in `docs/guides/dormantql-schema.md` covering modules, entities, `name: TypeExpr[?]`, scalar values, single refs, `Set/List/Bag/Map` collections, primary keys, concurrency members, required-by-default behavior, and optionality
- [ ] T032 [P] [US2] Write the query and mutation guide in `docs/guides/queries-and-mutations.md` covering `query`, `mutation`, aliases, alias-qualified members, clause order, lowercase parameter types, supported operators, `returning`, result inference, removed `002` forms, and status caveats for planned/deferred constructs
- [ ] T033 [P] [US2] Write the naming and generated-code guide in `docs/guides/naming-and-generated-code.md` covering snake_case unit names to PascalCase methods, PascalCase entities, namespace derivation, mutation result inference, projection types, sample API generated namespaces, and raw string SQL literal readability
- [ ] T034 [US2] Add sample API sections to `docs/getting-started.md` linking to `samples/Dormant.Sample.TodoApi/` and `samples/Dormant.Sample.SchedulingApi/` with endpoint summaries and runtime caveats
- [ ] T035 [US2] Add cross-links from `docs/getting-started.md` to `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, `docs/guides/naming-and-generated-code.md`, `docs/status.md`, and `docs/speckit-sources.md`
- [ ] T036 [US2] Add source traceability entries for getting started, schema grammar, query/mutation grammar, naming, generated-code examples, Todo API sample files, and Scheduling API sample files in `docs/speckit-sources.md`
- [ ] T037 [US2] Validate every `.dqls`, `.dql`, and C# snippet in `README.md`, `docs/getting-started.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, `docs/guides/naming-and-generated-code.md`, `samples/Dormant.Sample.TodoApi/schema/todo.dqls`, `samples/Dormant.Sample.TodoApi/schema/todo.dql`, `samples/Dormant.Sample.SchedulingApi/schema/scheduling.dqls`, and `samples/Dormant.Sample.SchedulingApi/schema/scheduling.dql` against `specs/003-linq-dql-grammar/contracts/dql-grammar.md` and the sample contract
- [ ] T038 [US2] Validate the first-success and sample API independent test from `specs/006-project-documentation/spec.md` against `docs/getting-started.md`, `samples/Dormant.Sample.TodoApi/Program.cs`, and `samples/Dormant.Sample.SchedulingApi/Program.cs`

**Checkpoint**: User Story 2 can be reviewed by following the getting-started guide, inspecting both sample APIs, and checking the usage guides without source-code lookup.

---

## Phase 5: User Story 3 - Curious Developer or Contributor Explores Design and Rationale (Priority: P3)

**Goal**: A contributor or evaluator can understand the architecture, dependency direction, generator pipeline, provider boundary, constitution principles, design history, and how the sample APIs fit into the repository.

**Independent Test**: Read `docs/architecture.md` and `docs/design-decisions.md` and confirm the reader can reproduce the component map, dependency direction, build-time SQL pipeline, provider status, sample project role, and governing principles without contradictions against the constitution or SpecKit plans.

### Implementation for User Story 3

- [ ] T039 [P] [US3] Write the component map and package/layer overview in `docs/architecture.md` covering `src/Dormant.Abstractions/`, `src/Dormant.Core/`, `src/Dormant.SourceGeneration/`, `src/Dormant.Provider.PostgreSql/`, `src/Dormant.Provider.Sqlite/`, `src/Dormant.Spatial.PostgreSql/`, `src/Dormant.Tool/`, `samples/Dormant.Sample.Quickstart/`, `samples/Dormant.Sample.TodoApi/`, `samples/Dormant.Sample.SchedulingApi/`, and `tests/`
- [ ] T040 [P] [US3] Write the design rationale page in `docs/design-decisions.md` covering AOT-first design, build-time SQL, no runtime reflection/query compilation on hot paths, immutable command-driven writes, the `003` grammar replacement, raw string SQL literals, and the `005` SQLite/dialect direction with deferred NMemory
- [ ] T041 [US3] Add generator pipeline and provider-boundary sections to `docs/architecture.md` covering `.dqls`/`.dql` parsing, structured IR, per-dialect rendering direction, generated C# method surfaces, runtime provider execution, and how sample APIs consume generated surfaces
- [ ] T042 [US3] Add constitution principle summaries to `docs/design-decisions.md` covering Developer Experience First, Interface & Compatibility Stability, Statically-Known Safe Data Access, First-Class Tooling, Performance by Default, and Quality & Testing Discipline
- [ ] T043 [US3] Add architecture and design-decision source traceability entries in `docs/speckit-sources.md`
- [ ] T044 [US3] Validate the architecture independent test from `specs/006-project-documentation/spec.md` against `docs/architecture.md` and `docs/design-decisions.md`

**Checkpoint**: User Story 3 can be reviewed independently by a contributor using the architecture and design-decision pages.

---

## Phase 6: Polish & Cross-Cutting Validation

**Purpose**: Verify the complete documentation and sample API set against feature success criteria and clean up reader experience.

- [ ] T045 [P] Normalize headings, page introductions, sample references, and final navigation links across `README.md`, `docs/index.md`, `docs/getting-started.md`, `docs/status.md`, `docs/guides/dormantql-schema.md`, `docs/guides/queries-and-mutations.md`, `docs/guides/naming-and-generated-code.md`, `docs/architecture.md`, `docs/design-decisions.md`, and `docs/speckit-sources.md`
- [ ] T046 [P] Review all documentation for English-only wording and remove Portuguese phrases from `README.md` and `docs/`
- [ ] T047 Validate all internal Markdown links in `README.md` and `docs/` using the `rg` command from `specs/006-project-documentation/quickstart.md`
- [ ] T048 Validate capability labels and shipped/planned/deferred claims across `README.md`, `docs/status.md`, `docs/getting-started.md`, `docs/guides/queries-and-mutations.md`, `docs/architecture.md`, and `docs/design-decisions.md`
- [ ] T049 Validate traceability coverage for every major claim and both sample APIs in `docs/speckit-sources.md` against `README.md`, `docs/`, `samples/Dormant.Sample.TodoApi/`, and `samples/Dormant.Sample.SchedulingApi/`
- [ ] T050 Run `./build.sh build` from the repository root and record any documentation-relevant build or sample API caveats in `docs/getting-started.md`
- [ ] T051 Validate that `samples/Dormant.Sample.TodoApi/Dormant.Sample.TodoApi.csproj` and `samples/Dormant.Sample.SchedulingApi/Dormant.Sample.SchedulingApi.csproj` are included in `Dormant.slnx` and participate in the standard build workflow
- [ ] T052 Perform the final success-criteria review from `specs/006-project-documentation/spec.md` for SC-001 through SC-008 against `README.md`, `docs/`, `samples/Dormant.Sample.TodoApi/`, and `samples/Dormant.Sample.SchedulingApi/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup** has no dependencies.
- **Phase 2: Foundational** depends on Phase 1 and blocks all user-story writing and sample behavior.
- **Phase 3: US1** depends on Phase 2 and is the MVP.
- **Phase 4: US2** depends on Phase 2; it can start after shared source/status/sample conventions are done and should align with US1 links.
- **Phase 5: US3** depends on Phase 2; it can proceed in parallel with US2 after shared source/status/sample conventions are done.
- **Phase 6: Polish** depends on the user stories selected for delivery.

### User Story Dependencies

- **US1 (P1)**: No dependency on US2 or US3 after Foundational; delivers the README MVP.
- **US2 (P2)**: No hard dependency on US3; should preserve README link destinations, status labels, and sample references from US1.
- **US3 (P3)**: No hard dependency on US2 completion, but architecture docs should include final sample project paths once US2 sample files exist.

### Within Each User Story

- Draft page/project structure before validation tasks for that story.
- Add traceability entries before final story validation.
- Validate examples, sample API DormantQL files, and status labels before cross-cutting polish.

---

## Parallel Opportunities

- T002, T003, T004, and T005 can run in parallel after T001 because they touch different directories/files.
- T020, T021, T022, T023, T024, and T025 can run in parallel for US2 because they touch different sample project or schema/unit files.
- T030, T031, T032, and T033 can run in parallel for US2 because each writes a different documentation page.
- T039 and T040 can run in parallel for US3 because each writes a different page.
- T045 and T046 can run in parallel during polish if edits are coordinated by file.

---

## Parallel Example: User Story 2

```text
Task: "Create samples/Dormant.Sample.TodoApi/Dormant.Sample.TodoApi.csproj"
Task: "Create samples/Dormant.Sample.SchedulingApi/Dormant.Sample.SchedulingApi.csproj"
Task: "Author samples/Dormant.Sample.TodoApi/schema/todo.dqls"
Task: "Author samples/Dormant.Sample.SchedulingApi/schema/scheduling.dqls"
Task: "Author samples/Dormant.Sample.TodoApi/schema/todo.dql"
Task: "Author samples/Dormant.Sample.SchedulingApi/schema/scheduling.dql"
Task: "Write docs/guides/dormantql-schema.md"
Task: "Write docs/guides/queries-and-mutations.md"
Task: "Write docs/guides/naming-and-generated-code.md"
```

---

## Parallel Example: User Story 3

```text
Task: "Write docs/architecture.md"
Task: "Write docs/design-decisions.md"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational source/status/sample conventions.
3. Complete Phase 3: README MVP.
4. Stop and validate the README-only independent test.

### Incremental Delivery

1. Add US1 for evaluator comprehension.
2. Add US2 for first-success adoption and Todo/Scheduling sample APIs.
3. Add US3 for architecture and contributor confidence.
4. Run Phase 6 validation across whichever stories are included in the delivery.

### Documentation and Sample Quality Strategy

Keep examples small, status-labeled, traceable, and close to current sample syntax. Keep the Todo and
Scheduling APIs intentionally boring: read/write HTTP endpoints over authored DormantQL units, with no
authentication, recurrence engine, background worker, notifications, or calendar integration.

## Notes

- `[P]` tasks are safe to work on in parallel only when file ownership is coordinated.
- This feature intentionally avoids new docs tooling dependencies.
- Do not change product library code, provider behavior, generator behavior, or the DormantQL grammar while completing these tasks.
- Full provider test runs may require Docker; sample API runtime caveats must be documented when provider setup is required.
