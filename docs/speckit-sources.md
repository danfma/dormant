# SpecKit Sources

This page maps documentation claims to source artifacts. It is meant to make documentation drift visible during review.

## Primary Sources

| Source | Used for |
| --- | --- |
| `.specify/memory/constitution.md` | Governing principles, compatibility surfaces, AOT/performance constraints, documentation and tooling expectations. |
| `AGENTS.md` | Current local agent context pointer to the active plan. |
| `CLAUDE.md` | Legacy/current agent-context source named by the feature spec. |
| `specs/001-orm-aot-sourcegen/` | AOT-first ORM direction, DormantQL schema model, generated entities, `Ref*` relationship model, PostgreSQL reference provider, source layout. |
| `specs/002-immutable-command-dml/` | Immutable command-driven write direction, reduced mutable change-tracking model, authored write units. |
| `specs/003-linq-dql-grammar/` | Current query/mutation grammar, generated method naming, removed forms, `returning`, operator status, `with` design history. |
| `specs/004-raw-string-sql/` | Generated SQL raw string literal decision and constraints. |
| `specs/005-sqlite-nmemory-providers/spec.md` | SQLite/dialect direction, NMemory deferral, provider status caveats. |
| `samples/Dormant.Sample.Quickstart/schema/app.dqls` | Schema examples. |
| `samples/Dormant.Sample.Quickstart/schema/app.dql` | Query/mutation examples. |
| `samples/Dormant.Sample.Quickstart/Program.cs` | Generated C# surface usage and database-optional sample flow. |
| `build.sh` | Local build/test entry point. |
| `global.json` | .NET SDK version. |
| `Dormant.slnx` | Project layout. |
| `Directory.Packages.props` | Central package versions and test/build tooling. |

## Claim Map

| Documentation topic | Main docs | Source basis |
| --- | --- | --- |
| README overview and differentiators | `README.md` | Constitution, `001`, `002`, `003`, current `src/` layout. |
| README status and prerequisites | `README.md`, `docs/status.md` | `global.json`, `build.sh`, `Dormant.slnx`, `001`, `004`, `005`, current source tree. |
| Getting started sample | `docs/getting-started.md` | `samples/Dormant.Sample.Quickstart/`, `003` grammar contract. |
| Schema language | `docs/guides/dormantql-schema.md` | `001` plan/data-model/contracts, sample `.dqls`. |
| Query and mutation language | `docs/guides/queries-and-mutations.md` | `002` write design, `003` grammar/generated-code contracts, sample `.dql`. |
| Naming and generated code | `docs/guides/naming-and-generated-code.md` | `001` plan, `003` plan/contracts, sample program. |
| Architecture | `docs/architecture.md` | `001` plan, constitution, `Dormant.slnx`, current `src/`, `samples/`, and `tests/`. |
| Provider status | `docs/status.md`, `docs/architecture.md`, `docs/design-decisions.md` | `001` PostgreSQL direction, current provider projects, `005` SQLite/NMemory spec. |
| Design decisions | `docs/design-decisions.md` | Constitution plus SpecKit features `001` through `005`. |

## Drift Review Notes

- Prefer the latest clarified design when older artifacts contain superseded wording.
- Prefer current source/tests when a SpecKit contract says a capability is "planned next" but the source has since implemented it.
- Mark designed-but-unimplemented capabilities as Planned or Deferred.
- Mark examples as Illustrative when they are not copied from current sample/test files or not end-to-end verified by this documentation feature.
