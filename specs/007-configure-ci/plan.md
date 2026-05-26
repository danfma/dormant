# Implementation Plan: Configure CI

**Branch**: `007-configure-ci` | **Date**: 2026-05-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-configure-ci/spec.md`

## Summary

Implement a robust GitHub Actions CI pipeline for the Dormant ORM to automate build, test (unit, integration, source generator, AOT smoke tests), linting, and PostgreSQL service integration. The pipeline will enforce strict .editorconfig compliance and utilize real PostgreSQL containers for integration tests.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0+)

**Primary Dependencies**: 
- Roslyn Source Generators (Dormant.SourceGeneration)
- PostgreSQL (npgsql)
- Docker (for Service Containers)

**Storage**: PostgreSQL (ephemeral service containers)

**Testing**: 
- xUnit/MS Test for Core and Source Generation
- Custom Smoke Tests for Native AOT validation

**Target Platform**: GitHub Actions (Runner: `ubuntu-latest`)

**Project Type**: Managed Library / ORM

**Performance Goals**: Total pipeline execution time < 7 minutes

**Constraints**: 
- No library-originated trimming/AOT warnings.
- 100% deterministic test runs.
- Strict .editorconfig enforcement.

**Scale/Scope**: Solution `Dormant.slnx` and all child projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principle I (DX)**: CI MUST provide actionable diagnostics. Source generator failures or linting errors must point to exact line numbers in GitHub annotations.
- **Principle IV (Tooling)**: CI is the central verification tool. It MUST run all build, test, and benchmark tasks defined in the repo.
- **Principle V (Performance)**: CI MUST verify AOT compatibility (zero warnings) and respect the 7-minute performance budget for execution.
- **Principle VI (Quality)**: CI MUST be green before merge. This plan implements the mechanism to enforce this principle.

## Project Structure

### Documentation (this feature)

```text
specs/007-configure-ci/
├── spec.md              # Requirement definition
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # N/A for this feature
├── quickstart.md        # N/A for this feature
├── contracts/           # N/A for this feature
└── tasks.md             # Phase 2 output (tasks-to-be-generated)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    └── ci.yml           # Main CI pipeline definition
```

**Structure Decision**: Single GitHub Actions workflow file `ci.yml` to minimize orchestration overhead, leveraging job dependencies for sequential/parallel execution (Build -> Test -> Smoke).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
