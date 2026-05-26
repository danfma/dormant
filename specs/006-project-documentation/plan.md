# Implementation Plan: Project Documentation & Developer README

**Branch**: `006-project-documentation` | **Date**: 2026-05-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-project-documentation/spec.md`

## Summary

Create the public documentation surface for Dormant: a root `README.md` for first-time evaluators and a
structured `docs/` folder for integrators and contributors. The documentation is authored as plain Markdown,
derived from SpecKit artifacts `001` through `005`, the constitution, and current project files. It must be
honest about maturity: PostgreSQL and the implemented DormantQL grammar are documented as current, SQLite and
the generalized dialect work are documented as planned from `005`, and NMemory is documented as deferred. The
delivery emphasizes fast comprehension, valid DormantQL examples, traceable claims, and resolvable links.

## Technical Context

**Language/Version**: Markdown documentation for a C# 14 / .NET 10 (`net10.0`) project; source generator
targets `netstandard2.0`.

**Primary Dependencies**: No documentation-site generator or new runtime dependency. Source inputs are
SpecKit artifacts, `.specify/memory/constitution.md`, `AGENTS.md`, current source layout, `build.sh`,
`global.json`, solution/project files, samples, and tests.

**Storage**: N/A for the feature. Documentation files live in repository storage: root `README.md` and
repository-root `docs/`.

**Testing**: Manual documentation review plus lightweight local checks: `rg`/shell inspection for internal
link targets, example syntax review against `specs/003-linq-dql-grammar/contracts/dql-grammar.md`, and source
traceability review against SpecKit artifacts. Product verification entry point remains `./build.sh all`;
provider tests that need PostgreSQL require Docker.

**Target Platform**: GitHub/Git-compatible Markdown readers and local developer environments on .NET 10 SDK
`10.0.201` or compatible `latestFeature`.

**Project Type**: Documentation feature for a managed multi-package .NET library + source generator +
provider adapters + sample + tests.

**Performance Goals**: Reader performance, not runtime performance: README comprehension in under 5 minutes
(SC-001); integrator can author a minimal schema/query/mutation from docs without source-code lookup
(SC-002). No code/runtime performance impact.

**Constraints**: English only; documentation-only change; no product code, generator, grammar, or provider
behavior changes; no unsupported capability claims; every example must match the current DormantQL grammar and
naming conventions; internal docs links must resolve; planned/deferred capabilities must be labeled.

**Scale/Scope**: One root README plus a compact, navigable docs set. Planned structure:

```text
README.md
docs/
├── index.md
├── getting-started.md
├── status.md
├── guides/
│   ├── dormantql-schema.md
│   ├── queries-and-mutations.md
│   └── naming-and-generated-code.md
├── architecture.md
├── design-decisions.md
└── speckit-sources.md
```

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* Constitution v2.0.1.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | README + getting-started docs directly improve clarity, discoverability, and time-to-first-success; docs include examples and routing to deeper pages. | PASS |
| II. Interface & Compatibility Stability | Documentation records, but does not change, public API/generated-code/DSL/package contracts; examples are checked against current DSL baseline. | PASS |
| III. Statically-Known, Safe-by-Default Data Access | Docs must explain full-entity vs projection result types, no partial entities, explicit `Ref*` load states, and no implicit lazy loading without weakening these guarantees. | PASS |
| IV. First-Class Tooling | Docs explain the single build/test entry point and the generator/tooling workflow; no new unsupported tooling is introduced. | PASS |
| V. Performance by Default | Docs present build-time SQL, no hot-path reflection/compilation, AOT/trimming goals, and current/deferred provider status accurately; no runtime impact. | PASS |
| VI. Quality & Testing Discipline | Documentation examples, links, and claims are reviewable; tasks must include link checks and example grammar review. | PASS |

**Result: no violations.** Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/006-project-documentation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── documentation-set.md
└── tasks.md                  # Created by /speckit-tasks, not by /speckit-plan
```

### Source Code (repository root)

```text
README.md                     # New evaluator entry point
docs/
├── index.md                  # New docs landing/index page
├── getting-started.md        # New first-success path
├── status.md                 # New current/planned/deferred capability matrix
├── guides/
│   ├── dormantql-schema.md
│   ├── queries-and-mutations.md
│   └── naming-and-generated-code.md
├── architecture.md
├── design-decisions.md
└── speckit-sources.md

src/
├── Dormant.Abstractions/
├── Dormant.Core/
├── Dormant.SourceGeneration/
├── Dormant.Provider.PostgreSql/
├── Dormant.Spatial.PostgreSql/
└── Dormant.Tool/

samples/
└── Dormant.Sample.Quickstart/

tests/
├── Dormant.Aot.SmokeTests/
├── Dormant.Benchmarks/
├── Dormant.Core.Tests/
├── Dormant.Provider.PostgreSql.Tests/
├── Dormant.SourceGeneration.Tests/
└── Dormant.Spatial.PostgreSql.Tests/
```

**Structure Decision**: Keep docs as plain repository Markdown. This matches the user's requested `docs/`
folder, avoids adding a docs build dependency before content exists, works on GitHub immediately, and keeps
documentation review in the same PR surface as code/spec changes.

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Phase 0: Research

Completed in [research.md](./research.md). Key decisions:

- Plain Markdown docs, no static-site generator in this feature.
- Documentation pages organized by user journey: evaluate → first success → language guides → architecture.
- Capability status labels: **Implemented**, **Planned**, **Deferred**, and **Illustrative**.
- Examples are minimal and mirror `samples/Dormant.Sample.Quickstart/schema/app.dqls` and `.dql` while
  respecting the `003` grammar baseline and its documented deferred constructs.
- Claims cite or trace to SpecKit source artifacts rather than inventing roadmap promises.

## Phase 1: Design & Contracts

Completed artifacts:

- [data-model.md](./data-model.md) — documentation entities: pages, source artifacts, traceable claims,
  examples, links, and capability statuses.
- [contracts/documentation-set.md](./contracts/documentation-set.md) — required docs files, audiences,
  content obligations, link contract, example contract, and status-label contract.
- [quickstart.md](./quickstart.md) — implementation/check workflow for creating and validating the docs.
- Agent context updated in `AGENTS.md` to point to this plan.

## Constitution Check (Post-Design)

| Principle | Re-check | Status |
|-----------|----------|--------|
| I. Developer Experience First | Page model and contracts prioritize README comprehension and first-success docs. | PASS |
| II. Interface & Compatibility Stability | Contracts require examples to align with current DSL/generated-code surfaces and distinguish planned work. | PASS |
| III. Statically-Known, Safe-by-Default Data Access | Architecture/design docs require safe-by-default result typing and explicit `Ref*` load-state explanation. | PASS |
| IV. First-Class Tooling | Quickstart requires documenting `./build.sh`, .NET SDK, and Docker requirement for provider tests. | PASS |
| V. Performance by Default | Docs must explain build-time SQL, AOT/trimming, and raw generated SQL readability without adding runtime work. | PASS |
| VI. Quality & Testing Discipline | Link, example, and traceability checks are explicit acceptance checks. | PASS |

**Result: no violations after design.**
