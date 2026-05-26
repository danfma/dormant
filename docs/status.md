# Capability Status

Dormant is early project code. This page separates what is implemented in the current repository from what is planned or deferred in the SpecKit design artifacts.

## Labels

- **Implemented**: Present in current source, tests, samples, or the active compatibility baseline.
- **Planned**: Specified by current SpecKit artifacts but not yet implemented.
- **Deferred**: Intentionally moved out of current scope or a future feature.
- **Illustrative**: Valid documentation example or shape that is not independently end-to-end verified by this documentation work.

## Current Matrix

| Capability | Status | Notes |
| --- | --- | --- |
| .NET 10 / C# 14 project baseline | **Implemented** | `global.json` pins SDK `10.0.201` with `latestFeature` roll-forward. |
| DormantQL schema files (`.dqls`) | **Implemented** | Used by the sample and generator tests. |
| DormantQL query/mutation files (`.dql`) | **Implemented** | Current syntax uses `query`, `mutation`, explicit aliases, alias-qualified members, and symbolic operators. |
| PostgreSQL provider | **Implemented** | Current reference provider and integration-test target. |
| Spatial PostgreSQL companion package | **Implemented** | Present as `src/Dormant.Spatial.PostgreSql/`. |
| `Ref<T>` and collection relationship load-state types | **Implemented** | Present under `src/Dormant.Abstractions/Entities/`. |
| Generated raw string SQL literals | **Implemented** | The generator emits static SQL through raw string literal helpers. |
| `returning` on mutations | **Implemented** | Covered in generator tests and the current grammar baseline. |
| `with` value flow and ref/FK assignment | **Implemented** | Present in current parser/emitter code and sample units. Older `003` contract notes may still describe this as planned next; current source is the tie-breaker. |
| Logical `&&` in `where` | **Implemented** | Current supported conjunction. |
| Logical `||` and `!` in `where` | **Deferred** | The current parser reports these as not supported yet. |
| SQLite provider and generalized SQL dialect framework | **Planned** | Specified by feature `005`; not documented here as shipped. |
| NMemory provider | **Deferred** | Future opt-in non-AOT provider direction. SQLite in-memory is the planned AOT-friendly in-memory path. |
| NuGet packaging | **Planned** | Do not assume public package availability from these docs. |

## Verification Expectations

- `./build.sh build` checks the repository build.
- `./build.sh all` also runs test hosts and includes PostgreSQL provider tests that require Docker.
- Runtime quickstart examples that require a database are marked as such; documentation examples are kept close to the checked-in sample files.
