# Contract: Generated Code (result inference & method shapes)

What the generator emits for the new grammar. The **runtime contract is unchanged from `002`** (same session
methods, `IEntityBinding`, immutable entities, `CompiledCommand<T>`); only how authored units map onto it
changes. Method bodies carry **build-time SQL** (no runtime query compilation).

## Method naming

A unit's `snake_case` name becomes a `PascalCase` method (`users_by_email` → `UsersByEmail`,
`create_user` → `CreateUser`) on the generated `{Module}Queries` / `{Module}Commands` extension blocks over
`ISession` (C# 14 extension blocks, as in `002`). Parameters map DslType → ClrType (see data-model.md).

## Query methods

| Authored | Generated result |
|----------|------------------|
| `query q(...) { from E u where … select u }` | `IAsyncEnumerable<E>` (full immutable entity) |
| `query q(...) { … select { u.a, u.b } }` | `IAsyncEnumerable<{Q}Result>` — distinct projection record exposing exactly `a`, `b` |

`optional T` parameters keep `002` behavior: the predicate condition is included only when supplied; the
result type is identical for every combination.

## Mutation methods (result inference)

| Authored | Generated result |
|----------|------------------|
| `mutation m(...) { insert E u { … } }` | the entity's primary-key type (e.g. `ValueTask<Guid>`) — the inserted id |
| `mutation m(...) { insert E u { … } returning u }` | `ValueTask<E>` — the inserted entity materialized |
| `mutation m(...) { insert E u { … } returning { u.a, u.b } }` | `ValueTask<{M}Result>` — distinct projection |
| `mutation m(...) { update E u where … set { … } }` | `ValueTask<int>` — affected-row count |
| `mutation m(...) { delete E u where … }` | `ValueTask<int>` — affected-row count |
| `mutation m(...) { update E u where … set { … } returning u }` | `ValueTask<E>` (or projection) per `returning` |
| multi-command (writes + trailing read/`returning`) | the trailing statement's shape |

The result type is **fixed at build time**. Accessing a member absent from the result (entity field not in a
projection, or a `returning`/`select` member not produced) is a **compile error** (Principle III).

## Multi-command & `with`

A multi-command `mutation` emits the commands as a **statement sequence within the session transaction**.
`with x = <expr>` compiles to a C# local; later commands and the trailing read/`returning` reference it
(e.g. the inserted parent id flowing into a child's FK parameter). No single-round-trip CTE is emitted in v1
(deferred).

## SQL mapping (unchanged IR)

Generated SQL is produced from the same `002` IR: `InsertStatement`/`UpdateStatement`/`SelectStatement`/
`DeleteStatement`, `SqlCondition` (operators per the grammar table), `SqlAssignment`, schema-qualified table
refs, `::jsonb` write cast for `json`. `returning`/trailing reads use the binding's existing `Materialize`
(entity) or the projection materializer.

## Invariants (verified by tests)

- Removed `002` syntax does not generate (produces a diagnostic) — FR-015 / SC-001.
- No `returning` authored ⇒ default inference (insert→id, update/delete→count) — SC-003.
- `returning`/trailing read ⇒ result matches that shape — SC-003.
- Materialized results are immutable; no setters, no persist-mutation path — SC-006.
- Generation is deterministic and incrementally cacheable for unit files — FR-013.
- AOT publish remains zero-warning — SC-005.
