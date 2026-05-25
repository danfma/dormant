# Phase 1 Data Model: Immutable, Command-Driven ORM

**Feature**: `002-immutable-command-dml` | **Date**: 2026-05-25

Two model layers (as in `001`): the **build-time** model the generator builds from DQL, and the
**runtime/user** model (generated immutable types + the reduced `Dormant.Abstractions` kernel). Fields are
conceptual; exact shapes live in `contracts/`.

---

## A. Build-time model (source generator)

Reuses `001`'s `SchemaModel`/`EntityModel`/`PropertyModel`/`ReferenceModel` and the query AST. **Adds** a
command AST and **extends** the SQL IR with CTE/`with` nodes.

### CommandFile / CommandModel (new)
- `CommandFile`: `ModuleName`, `FilePath`, `Commands: EquatableArray<CommandModel>`, `Diagnostics` — parsed
  from `.dql` (same files as queries; a file may hold both).
- `CommandModel`: `Name`, `Kind: { Insert, Update, Delete }`, `RootEntity`, `Parameters:
  EquatableArray<QueryParameter>`, `WithBindings: EquatableArray<WithBinding>`, `Body: WriteNode`,
  `ResultShape: ShapeModel?` (optional `returning`/projection), `Filter` (for update/delete).
- **Validation**: target entity/fields exist; parameters declared; `with` names unique and referenced;
  no write-reference cycle; result shape closed at build time; every diagnostic carries a `LocationInfo`.

### WriteNode (new, tree)
- `InsertNode`: `Entity`, `Assignments: EquatableArray<Assignment>` (where a value may be a literal,
  parameter, `with`-reference, native call, or a **nested `WriteNode`**), `CollectionWrites` (children).
- `UpdateNode`: `Entity`, `Filter`, `Assignments`, optional concurrency-token match/bump.
- `DeleteNode`: `Entity`, `Filter`.
- `Assignment`: `Column`, `ValueExpr` (literal | param | withRef | nativeCall | nestedWrite).

### WithBinding (new)
- `Name`, `Expr` (a nested write, a sub-query, a parameter, or an expression). Compiles to a CTE step or a
  bound value.

### SQL IR additions (extends `001`'s `Ir/SqlIr.cs`)
- `CteStatement`: `Steps: IReadOnlyList<CteStep>` + `Final: SqlStatement`. `CteStep`: `Name`,
  `Statement` (an `InsertStatement`/`SelectStatement`/…), `ReturningColumns`.
- `InsertStatement` gains an optional `Returning` clause and value expressions that may reference a prior
  CTE step's returning column.
- `UpdateStatement` / `DeleteStatement` (new IR nodes): table, SET (update), WHERE (incl. token match),
  optional `Returning`.
- Renderer emits `WITH a AS (…), b AS (…) <final>` deterministically; column/table names schema-qualified
  via the carried-over naming resolver.

### Emission descriptors
`ImmutableEntityEmit` (init-only/positional, no setters, no-reflection materialization ctor + read getters),
`CommandEmit` (typed method + reused compiled definition + CTE SQL), `QueryEmit` (carried from `001`),
`JsonContextEmit` (jsonb), `DdlEmit` (carried).

---

## B. Runtime / user model

### Immutable Entity (generated)
A generated **immutable** type (init-only/positional; no public setters; no tracked state). Mapped value
members + read-side relationship members (`Ref`/`RefSet`/…). Materialized via a generated no-reflection
constructor. Identity equality by primary key (carried from `001`). Never mutated or "saved".

### Ref<T> / RefSet<T> / … (kernel, read-side)
Unchanged from `001`: load-state structs with `Unloaded` sentinel; single-ref optionality via `Ref<T?>`;
collections unordered/ordered/keyed. **Read side only** — relationships are *written* via commands, not by
mutating a Ref on an entity.

### Command (generated method)
One typed `ISession` method per authored command, in a C# 14 extension block on `{Module}Commands`. Carries a
reused compiled definition (prebuilt CTE SQL + no-boxing binder + materializer for any `returning` shape).
Returns the command's result (e.g. the inserted entity / a projection / affected count), immutable.

### Query (generated method)
Carried from `001`: one typed `ISession` method per authored `select`, returning an immutable entity or a
distinct projection record. Optional parameters supported.

### Session / Unit of Work (reduced)
- Provides: **transaction boundary** (begin/commit/rollback), **read identity map** (one immutable instance
  per key within the session), and **execution** of generated command/query methods.
- Does **not**: track changes, hold snapshots, diff, or expose `AddAsync`/`Remove`.
- **Lifecycle**: `Open` → (execute commands/queries in one transaction) → `Commit`/`Rollback` → `Closed`.

### Compiled Definition
The reused, build-time representation of a command/query: prebuilt (CTE) SQL + no-boxing parameter binder +
materializer. Allocated once per generated method, reused across executions.

### Concurrency Token
A column matched within an `update`/`delete` command; on a stale match the command affects zero rows and the
method surfaces a conflict (no session snapshot).

### Native Function / jsonb (carried)
Provider-scoped native value type `jsonb` (string representation + `::jsonb` write cast) and native function
invocations (e.g. `datetime::now()`) usable in commands/queries, build-time type-checked.

---

## Relationships (overview)

```
SchemaModel 1─* EntityModel 1─* PropertyModel / ReferenceModel
CommandFile 1─* CommandModel ─► WriteNode (tree; nested WriteNode children)  ;  CommandModel *─► EntityModel
QueryFile   1─* QueryModel   ─► ShapeModel ─► (EntityModel | ProjectionType)
CommandModel/QueryModel ─► SQL IR (CteStatement | Insert/Update/Delete/Select) ─► rendered SQL
Session 1─* ImmutableEntity (read identity map)   ;   Session executes Command/Query methods
```
