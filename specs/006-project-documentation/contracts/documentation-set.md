# Contract: Documentation Set

This contract defines the public documentation surface created by the feature. It is a user-facing contract:
developers should be able to navigate and trust the docs without reading source code first.

## Required Files

| File | Required role |
|------|---------------|
| `README.md` | Root evaluator entry point. |
| `docs/index.md` | Documentation table of contents and reading paths. |
| `docs/getting-started.md` | First-success guide. |
| `docs/status.md` | Current/planned/deferred capability status. |
| `docs/guides/dormantql-schema.md` | Schema language guide. |
| `docs/guides/queries-and-mutations.md` | Query/mutation grammar guide. |
| `docs/guides/naming-and-generated-code.md` | Generated C# and naming guide. |
| `docs/architecture.md` | Architecture and component map. |
| `docs/design-decisions.md` | Design rationale and constitutional principles. |
| `docs/speckit-sources.md` | Traceability map to SpecKit artifacts. |

## README Contract

The root README MUST:

- State that Dormant is an AOT-first .NET 10 ORM using DormantQL and Roslyn source generation.
- Explain the problem: predictable, safe data access with build-time SQL and no hot-path runtime reflection or
  query compilation.
- Name at least three differentiators: build-time SQL, Native AOT/trimming focus, statically-known result
  types/no partial entities, DormantQL schema/query language, immutable command-driven writes, explicit
  `Ref*` relationship load state, PostgreSQL-primary provider.
- State maturity/status honestly.
- Link to `docs/index.md`, `docs/getting-started.md`, `docs/status.md`, `docs/architecture.md`, and the
  language guides.
- Mention prerequisites: .NET 10 SDK and Docker for PostgreSQL provider tests.
- Avoid promising NuGet availability or provider support that is only planned/deferred.

## Docs Contract

The docs folder MUST:

- Begin with `docs/index.md` that routes readers by task.
- Include a first-success path ordered as prerequisites → package/reference approach → schema → query →
  mutation → generation/build → run/check.
- Include schema and unit grammar explanations that match the current SpecKit grammar baseline.
- Include architecture/design pages covering:
  - package/layer structure: Abstractions, Core, SourceGeneration, Provider.PostgreSql, Spatial.PostgreSql,
    Tool, samples, tests;
  - one-directional inward dependency rule;
  - abstraction grouping by capability;
  - generator pipeline from `.dqls`/`.dql` through parsed models/IR/rendered SQL/generated C#;
  - provider status and planned dialect framework;
  - constitution principles in developer-facing language.
- Include `docs/speckit-sources.md` mapping each major doc topic to its source artifacts.

## Example Contract

Every DormantQL example MUST:

- Use `.dqls` schema syntax with `module`, `entity`, `name: TypeExpr[?]`, lowercase scalar type names where
  applicable, `primary`, `concurrency`, and relationship collection forms only as supported by the specs.
- Use `.dql` unit syntax with `module`, `query`, `mutation`, explicit aliases, alias-qualified members,
  lowercase parameter types, and symbolic operators.
- Avoid removed/superseded `002` syntax:
  - `command Name(...) = ...;`
  - `query Name(...) = ...;`
  - leading-dot member access such as `.email`
  - `:=`
  - single `=` as comparison
  - `and`/`or`/`not` keyword connectives
  - `::` or `->`
- Label unexecuted examples from the documentation effort as illustrative when they are not copied directly
  from current samples/tests.

## Status Label Contract

Use these exact labels when status matters:

- **Implemented**: present in current source/tests or the active compatibility baseline.
- **Planned**: specified by current SpecKit artifacts but not yet implemented.
- **Deferred**: intentionally moved out of current scope/future feature.
- **Illustrative**: valid-looking documentation example or shape that has not been end-to-end verified by
  this documentation feature.

Required status statements:

- PostgreSQL provider: **Implemented** as the primary/reference provider when current source/tests support it.
- SQLite provider/dialect framework: **Planned** from `005` unless implemented before docs are written.
- NMemory: **Deferred** and non-AOT future work.
- `||` and `!` operators: documented as **Deferred** in current `003` baseline if mentioned.
- `with` value-flow / FK assignment: status must follow the current implementation at docs-writing time;
  if uncertain, mark Planned or Illustrative rather than shipped.

## Link Contract

- Links from README to `docs/` MUST point to existing files.
- Links among docs pages MUST point to existing files.
- Relative links are preferred.
- Avoid deep anchors unless the target section is stable and checked.

## Traceability Contract

`docs/speckit-sources.md` MUST include a table mapping:

- README overview/status → constitution, `001`, `002`, `003`, `004`, `005`, source layout.
- Getting started examples → sample `.dqls`/`.dql` and `003` grammar contract.
- Schema guide → `001` plan/data-model/contracts and sample schema.
- Query/mutation guide → `003` grammar/generated-code contracts and `002` command design.
- Generated-code naming → `001`/`003` plans and contracts.
- Architecture → `001` plan, source layout, constitution, `005` provider spec.
- Design decisions → constitution plus feature plans/research `001` through `005`.
