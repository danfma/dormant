# Tasks: Configure CI

**Input**: Design documents from `/specs/007-configure-ci/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md

**Tests**: CI validation requires running existing tests and adding specialized smoke test execution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create GitHub Actions directory structure in `.github/workflows/`
- [x] T002 [P] Configure environment variables (`DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`) in `.github/workflows/ci.yml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

- [x] T003 Configure base pipeline triggers (push/PR to main) in `.github/workflows/ci.yml`
- [x] T004 Setup .NET 10 SDK installation step in `.github/workflows/ci.yml`
- [x] T005 [P] Implement NuGet restore and caching strategy in `.github/workflows/ci.yml`
- [x] T006 Implement solution-wide build step (`dotnet build Dormant.slnx`) in `.github/workflows/ci.yml`

**Checkpoint**: Foundation ready - basic build pipeline operational.

---

## Phase 3: User Story 1 - Developer Validation (Priority: P1) 🎯 MVP

**Goal**: Automate build, linting, and unit testing for every PR.

**Independent Test**: Create a PR and verify that Build, Lint, and Core Tests pass or fail correctly.

### Implementation for User Story 1

- [x] T007 [US1] Implement strict linting enforcement (`dotnet format --verify-no-changes`) in `.github/workflows/ci.yml`
- [x] T008 [US1] Add core test execution (`dotnet test Dormant.slnx`) with GitHubActions logger in `.github/workflows/ci.yml`
- [x] T009 [P] [US1] Add source generator integration tests execution in `.github/workflows/ci.yml`
- [x] T010 [US1] Configure job failure conditions to block PR merges in `.github/workflows/ci.yml`

**Checkpoint**: User Story 1 complete - developers now get immediate feedback on PRs.

---

## Phase 4: User Story 3 - Provider Consistency (Priority: P2)

**Goal**: Support database integration tests using real PostgreSQL containers.

**Independent Test**: Verify that `Dormant.Provider.PostgreSql.Tests` runs and passes against the service container.

### Implementation for User Story 3

- [x] T011 [US3] Configure PostgreSQL service container (image: `postgres:latest`) in `.github/workflows/ci.yml`
- [x] T012 [US3] Setup database health check and port mapping (5432:5432) in `.github/workflows/ci.yml`
- [x] T013 [US3] Configure connection strings or environment variables for integration tests in `.github/workflows/ci.yml`

**Checkpoint**: User Story 3 complete - database-dependent tests are now validated in CI.

---

## Phase 5: User Story 2 - AOT Integrity (Priority: P3)

**Goal**: Guarantee AOT compatibility via native smoke tests.

**Independent Test**: Introduce a reflection-based breaking change and verify CI fails the AOT smoke test.

### Implementation for User Story 2

- [x] T014 [US2] Implement AOT publish step for `tests/Dormant.Aot.SmokeTests/Dormant.Aot.SmokeTests.csproj` in `.github/workflows/ci.yml`
- [x] T015 [US2] Add native binary execution step in `.github/workflows/ci.yml`
- [x] T016 [US2] Ensure AOT warnings fail the build step in `.github/workflows/ci.yml`

**Checkpoint**: All user stories complete - AOT integrity is now guaranteed.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T017 [P] Add test result summary upload (artifacts) in `.github/workflows/ci.yml`
- [x] T018 [P] Update `CLAUDE.md` and `GEMINI.md` with CI usage instructions
- [x] T019 Final run and validation of `specs/007-configure-ci/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on T001-T002.
- **User Story 1 (P1)**: Depends on Phase 2 completion.
- **User Story 3 (P2)**: Depends on Phase 2 completion. Can run in parallel with US1.
- **User Story 2 (P3)**: Depends on Phase 2 completion.

### User Story Dependencies

- **US1 (P1)**: High priority, core PR validation.
- **US3 (P2)**: Medium priority, requires Docker infrastructure.
- **US2 (P3)**: AOT specific, can be the last piece of the CI puzzle.

### Parallel Opportunities

- T002 and T005 (Parallelizable configuration)
- US1 and US3 can be implemented as parallel jobs if resource usage allows.
- T017 and T018 (Final documentation/artifacts)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Setup basic GitHub Action.
2. Build solution.
3. Lint code.
4. Run core tests.
5. **STOP**: Verify PRs are protected.

### Incremental Delivery

1. Foundation -> US1 (MVP)
2. Add US3 (DB Integration)
3. Add US2 (AOT Smoke Tests)
4. Add Artifacts/Polish
