# Phase 0 Research: Immutable, Command-Driven ORM

**Feature**: `002-immutable-command-dml` | **Date**: 2026-05-25 | Fork of `001-orm-aot-sourcegen`

No open `NEEDS CLARIFICATION` remain (the back-reference fork was resolved in clarify: explicit `with`).
Decisions below fix the technical approach.

## 1. Immutable entities & results

**Decision**: Materialized entities and all query/command results are **immutable** — generated as C# types
with init-only/positional state and **no public setters**, no snapshot, no dirty flag. State changes only by
executing a command and reading its result. Relationship members on read results keep the `001` read-side
reference types (`Ref`/`RefSet`/`RefList`/`RefBag`/`RefMap`) with their Unloaded/Loaded state.

**Rationale**: Removes the entire class of mutate-then-forget-to-persist and partially-loaded bugs by
construction; strengthens Principle III; eliminates change-tracking machinery (Principle V — less runtime
work, fewer allocations). Materialization stays no-reflection via a generated constructor (as in `001`),
now feeding an immutable shape.

**Alternatives**: mutable entities + snapshot diff (`001`'s model) — rejected for this fork (the thing being
replaced); a separate read-model/write-model split with mapping — heavier, deferred.

## 2. Commands are authored DQL, symmetric to queries

**Decision**: `insert`/`update`/`delete` are **named commands** in `.dql` files, parsed by the generator into
a command AST and emitted as typed `ISession` methods carrying build-time SQL — exactly mirroring the
existing authored-query pipeline (parser → model → IR → render → method emit). No `AddAsync`/`Remove`/auto-DML
exists. The query and command paths share the lexer, the SQL IR, the renderer, naming resolution, and the
extension-block method emission.

**Rationale**: Symmetry reuses ~all of `001`'s query infrastructure; keeps the DSL the primary surface
(Principle I); guarantees build-time-known result types and no runtime query compilation (Principles III/V).

**Alternatives**: a fluent C# write API — rejected as primary (not DSL-first); runtime DQL→SQL — rejected
(violates build-time-SQL; relegated to the future "macros" feature).

## 3. Nested writes in one round-trip via data-modifying CTEs

**Decision**: A nested write (`insert Post { author := (insert User {…}) }`) and a parent→children write
compile to a single PostgreSQL statement using **data-modifying common table expressions**:
`WITH a AS (INSERT INTO … RETURNING id), … INSERT INTO … SELECT …, a.id FROM a`. The command AST is a tree of
writes; the SQL IR gains **CTE statement nodes** (a list of named `WITH` steps + a final statement); the
renderer emits them in dependency order. One round-trip (SC-002), SQL fixed at build time.

**Rationale**: PostgreSQL CTEs execute multiple data-modifying steps atomically in one statement, returning
generated ids forward — the exact primitive nested writes need; no client round-trips, no runtime assembly.

**Alternatives**: multiple statements in a transaction (N round-trips) — rejected (violates SC-002);
client-side id round-trip then second insert — rejected (two round-trips).

## 4. `with` bindings & back-references

**Decision**: `with name := <expr>` binds a name (a parameter, a sub-expression, or a **nested write's
result**) reusable in the command/query body. A write referencing another write's generated value (e.g. a
child needing the parent id) does so **only** via an explicit `with` binding — there is **no** implicit
auto-link and **no** special back-reference token (`..id` dropped, per clarify). Each `with` binding maps to a
CTE step or a parameter; references resolve to that step's `RETURNING` column / the bound value.

**Rationale**: One explicit, unambiguous mechanism; reuses the CTE machinery; handles the majority of cases;
avoids hidden linking semantics. EdgeQL-aligned (`with`).

**Alternatives**: implicit auto-link by assigned link (EdgeQL default) — rejected for v1 (less explicit);
special token — rejected (new grammar + scoping rules).

## 5. The session shrinks (no change-tracking)

**Decision**: `ISession` provides only: a **transaction boundary** (begin/commit/rollback), a **read identity
map** (one immutable instance per key within the session), and **execution** of generated command/query
methods. It does **not** track changes, hold snapshots, or flush a graph. The `001` `Session.AddAsync`,
`Remove`, snapshot capture, and diff-UPDATE are removed.

**Rationale**: Matches the immutable/command model; removes the heaviest runtime machinery (Principle V);
simpler mental model (Principle I). The identity map stays useful for read consistency within a unit of work.

**Alternatives**: keep a mutable session as a convenience layer — rejected (reintroduces the model being
removed; two write paths confuse).

## 6. Optimistic concurrency expressed in the command

**Decision**: Concurrency is a normal part of an authored `update`/`delete` — a filter/match on a token
column (and bumping it in `update`). A stale token ⇒ zero rows affected ⇒ the generated method surfaces a
conflict (a typed result or `ConcurrencyConflictException`, as in `001`'s adapter). No session snapshot.

**Rationale**: Keeps concurrency explicit and build-time-known; survives the removal of snapshots.

**Alternatives**: session-held snapshot tokens (`001`) — rejected (no change-tracking here).

## 7. Reused compiled definitions

**Decision**: Each generated command/query exposes a **single, reused compiled definition** (prebuilt SQL +
no-boxing parameter binder + materializer), allocated once (e.g. a static readonly per generated method) and
reused across executions. Parameters are bound per call without re-allocating the definition.

**Rationale**: Dapper/Insight-like lightness; avoids per-call definition allocation (SC-007); fits the
build-time model (the definition is fully known at build time).

**Alternatives**: per-call construction (`001`'s current query methods build a `PreparedStatement` each call)
— acceptable but allocates; this fork tightens it to reuse.

## 8. Carried over from `001` (unchanged)

Roslyn incremental generator + equatable models + `WithTrackingName` + cacheability tests; the structured SQL
IR + `SqlRenderer`; the authored-query model (`.dql select` → `ISession` extension methods in a C# 14
extension block); configurable naming convention + per-unit `db("…")` overrides (snake_case default);
schema-qualified DDL + `CREATE SCHEMA`/`EnsureCreatedAsync`; Native-AOT + full-trimming zero-warning publish;
the `jsonb` value type with the `::jsonb` write cast; the `Ref*` types (now read-side only); the `.dqls`
schema DSL (module → DB schema); Npgsql slim no-boxing IO; Testcontainers integration testing.

## Resolved unknowns summary

| Unknown | Resolution |
|---------|------------|
| Entity mutability | Immutable (init-only, no setters, no snapshot) |
| Write surface | Authored DQL commands only (no auto-DML); symmetric to queries |
| Nested write execution | Single PostgreSQL data-modifying CTE (`WITH … RETURNING …`) |
| Back-references between writes | Explicit `with` binding only (no auto-link, no `..id`) |
| Session role | Transaction + read identity map + executor; no change-tracking |
| Concurrency | Expressed in the `update`/`delete` command (token match) |
| Definition reuse | One compiled definition per command/query, reused |
| Foundation | Reuse `001` generator/IR/query/naming/DDL/AOT/jsonb/Ref/schema-DSL |
