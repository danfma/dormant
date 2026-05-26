# Feature Specification: Configure CI

**Feature Branch**: `007-configure-ci`

**Created**: 2026-05-26

**Status**: Draft

**Input**: User description: "Configurar CI para o projeto."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Developer Validation (Priority: P1)

As a developer, I want to have my changes automatically validated (build + test) on every pull request to ensure I don't break the codebase.

**Why this priority**: Immediate feedback for developers is the core value of CI. It prevents regressions from reaching the main branch.

**Independent Test**: Can be tested by creating a pull request and verifying that the CI pipeline starts and reports success/failure based on code quality.

**Acceptance Scenarios**:

1. **Given** a pull request with valid code, **When** it is opened or updated, **Then** the CI pipeline builds the project and passes all tests.
2. **Given** a pull request with a compile error, **When** it is opened, **Then** the CI pipeline fails at the build step.
3. **Given** a pull request with failing tests, **When** it is opened, **Then** the CI pipeline fails at the test step.

---

### User Story 2 - AOT Integrity (Priority: P2)

As an architect, I want to ensure that the project remains AOT-compatible by running specific AOT smoke tests in the CI pipeline.

**Why this priority**: Dormant is an AOT-first ORM. Breaking AOT compatibility is a critical failure that might not be caught by standard JIT-based tests.

**Independent Test**: Can be tested by introducing an AOT-incompatible change (e.g., non-AOT-safe reflection) and verifying the AOT smoke tests fail.

**Acceptance Scenarios**:

1. **Given** the current codebase, **When** the CI runs, **Then** the AOT smoke tests (Dormant.Aot.SmokeTests) are executed and pass.

---

### User Story 3 - Provider Consistency (Priority: P3)

As a maintainer, I want to ensure that all database providers are tested against the core logic to maintain feature parity.

**Why this priority**: Ensures that cross-provider abstractions (like the dialect framework) work correctly across all supported engines.

**Independent Test**: Verified by checking that provider-specific test projects are included in the test run.

**Acceptance Scenarios**:

1. **Given** multiple provider test projects, **When** CI runs, **Then** tests for PostgreSql (and others as they are added) are executed.

### Edge Cases

- **Build-time Source Generation Failures**: How does CI handle cases where the Roslyn source generator fails but the rest of the build succeeds (or vice versa)?
- **Environment Dependencies**: What happens if tests require a database (e.g., PostgreSql) that isn't available in the CI runner?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST build the `Dormant.slnx` solution using the .NET 10 SDK.
- **FR-002**: System MUST run all unit tests in the `Dormant.Core.Tests` project.
- **FR-003**: System MUST run source generator integration tests in `Dormant.SourceGeneration.Tests`.
- **FR-004**: System MUST execute the `Dormant.Aot.SmokeTests` project.
- **FR-005**: System MUST fail the entire pipeline if any individual build or test step fails.
- **FR-006**: System MUST trigger automatically on push to `main` and on any pull request targeting `main`.
- **FR-007**: System MUST provide a clear summary of test results (pass/fail count).
- **FR-008**: System MUST enforce code style and linting rules defined in `.editorconfig` and fail the build on any violations.
- **FR-009**: System MUST support database integration tests by spinning up real PostgreSQL containers (using Docker/Service Containers) during the test execution phase.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of pull requests targeting `main` must complete CI validation before they can be merged.
- **SC-002**: Total pipeline execution time (build + all tests) should be under 7 minutes for a clean build.
- **SC-003**: All build artifacts and test logs must be available for inspection for at least 30 days.
- **SC-004**: Zero "flaky" tests allowed (CI must be 100% deterministic given the same code).

## Assumptions

- GitHub Actions is used as the CI provider.
- Ubuntu is the primary runner OS for cost-efficiency.
- The project is fully compatible with .NET 10.
- `Dormant.slnx` is the source of truth for the build scope.
- Integration tests requiring external databases are currently out of scope or will use mocks unless Docker is specified.
