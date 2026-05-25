# Feature Specification: Immutable, Command-Driven ORM (DQL writes, no change-tracking)

**Feature Branch**: `refactor/new-way`

**Created**: 2026-05-25

**Status**: Draft

**Input**: User description: Architectural fork from `001-orm-aot-sourcegen`. An **immutable** ORM whose
data manipulation (insert/update/delete) is **100% authored as named DQL commands** (EdgeQL/Gel-inspired),
eliminating the mutable session, snapshot change-tracking, and unit-of-work-diff model. Writes are explicit
commands compiled to build-time SQL — never inferred. Combines NHibernate's rich relationship representation
(read side) + Gel/EdgeQL's command-language flexibility + Dapper/Insight.Database's lightness and code
generation. `001` remains the return point.

## Clarifications

### Session 2026-05-25

- Q: How is data written? → A: **Only** through named DQL commands (`insert`/`update`/`delete`) authored in
  `.dql` files, each compiled to a typed method carrying build-time SQL. There is no auto-INSERT from a
  tracked entity and no snapshot-diff UPDATE. This **supersedes** the mutable session + change-tracking model
  of `001` (US2 / FR-014 / FR-015 there).
- Q: Are entities mutable? → A: No. Materialized entities and query/command results are **immutable** value
  types (records). No in-place mutation, no dirty state, no snapshot. State changes happen only by executing
  a command and reading its result.
- Q: How are relationships written? → A: Inside the command — by a **nested write** (`author := (insert User
  {…})`) or by binding to a value/parameter (an existing id, or a variable declared in a `with` block).
  There is no shadow foreign-key property the user mutates on an entity.
- Q: How is optimistic concurrency expressed? → A: In the `update`/`delete` command itself (e.g. a filter on
  a concurrency token), not via a session-held snapshot. Conflicts surface as a zero-rows-affected result the
  command method reports.
- Q: How much of EdgeQL is adopted? → A: A pragmatic subset (Tier A): `insert`/`update`/`delete` with nested
  writes, `with` variable/reference declarations, path/set expressions, parameters, and native functions
  (e.g. `datetime::now()`). Advanced EdgeQL (polymorphism, backlinks, complex set algebra, `group`) is later.
- Q: How does a nested write reference its parent (e.g. a child inserted under a collection needing the
  parent's id)? → A: Via an **explicit `with` binding** of the parent write, referenced where needed (e.g.
  `with u := (insert User {…}) insert Post { author := u, … }`). v1 has **no implicit auto-link** (the child
  is not silently linked by the assigned link) and **no special back-reference token** (the provisional
  `..id` is dropped). Explicit `with` handles the majority of cases and keeps references unambiguous.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Write data through an authored command (Priority: P1)

A developer needs to insert a row. They author a named `insert` command in DQL; the build generates a typed
method that executes prebuilt SQL. There is no implicit "add entity then save" — the command is the only way
to write, and exactly the authored command runs.

**Why this priority**: This is the core of the fork. Without command-authored writes there is no write path
at all (the implicit/mutable path is removed). It is the smallest end-to-end slice that delivers value.

**Independent Test**: Author `insert User { email := <param>, created_at := datetime::now() }`, build, call
the generated method against a real database, and confirm exactly one row is inserted with the given values
and an immutable result is returned. No tracked-entity API exists.

**Acceptance Scenarios**:

1. **Given** a `.dql` file with a named `insert` command, **When** the project builds, **Then** a typed
   method is generated whose result type is known at compile time and which carries build-time SQL.
2. **Given** the generated insert method, **When** it is invoked with parameters, **Then** exactly the
   authored row is written and the returned value is immutable (no setters, no tracked state).
3. **Given** no authored command for an operation, **When** the developer looks for a write API, **Then**
   there is none — writes exist only as authored commands (no `AddAsync`/`Remove`/auto-save).

---

### User Story 2 - Write related data in one nested command, one round-trip (Priority: P1)

A developer inserts an entity together with a related entity (or a parent with children) in a single authored
command, executed as one database round-trip.

**Why this priority**: Relationships are the main reason an ORM exists; nested writes are how this model
expresses them (there is no mutable graph to flush). Single-round-trip keeps the performance promise.

**Independent Test**: Author `insert Post { title := <param>, author := (insert User { … }) }`; invoke it;
confirm both rows are written, the child correctly references the parent, and the database is contacted
exactly once (statement count = 1).

**Acceptance Scenarios**:

1. **Given** a nested `insert` (related entity inside the parent command), **When** invoked, **Then** both
   rows are written with the correct foreign relationship and the operation is a single round-trip.
2. **Given** a parent with a collection of children authored in one command, **When** invoked, **Then** all
   children are written referencing the parent, in one round-trip.
3. **Given** any nested write, **When** the project builds, **Then** the SQL is produced at build time (no
   runtime query assembly/compilation) and the result type is statically known.

---

### User Story 3 - Reuse references via `with` variable declarations (Priority: P1)

A developer declares a named value/reference once (a parameter, a sub-result, an expression) and reuses it
across a command or query, instead of repeating or re-fetching it.

**Why this priority**: `with` bindings are how EdgeQL composes commands cleanly (e.g. reference the same
inserted row twice, or name a sub-query). Without them, nested/relational writes get awkward and duplicative.

**Independent Test**: Author a command using `with u := (insert User { … })` and reference `u` in two places
(e.g. set a field and link a child); invoke it; confirm the single declared row is used consistently.

**Acceptance Scenarios**:

1. **Given** a `with x := <expr>` declaration, **When** the command/query is built, **Then** `x` is usable as
   a reference in the body and resolves to a single, consistently-bound value at runtime.
2. **Given** a `with`-bound inserted row referenced by a child write, **When** invoked, **Then** the child
   links to that exact row in one round-trip.

---

### User Story 4 - Read with immutable, statically-typed results (Priority: P2)

A developer runs an authored query and receives immutable results — full entities or distinct projections —
with relationship members represented by the read-side reference types. Results cannot be mutated or
"saved back"; any change is a separate authored command.

**Why this priority**: Reads must keep the safe-by-default, build-time-known-type guarantees already proven,
now with immutability so there is no accidental mutate-then-expect-persistence.

**Independent Test**: Run an authored query; confirm the result type is exactly the requested shape, members
are read-only, and there is no API to mutate-and-persist the returned object.

**Acceptance Scenarios**:

1. **Given** an authored `select`, **When** invoked, **Then** the result is an immutable type whose shape is
   fixed at build time; accessing a non-selected field does not compile.
2. **Given** a query result, **When** the developer tries to change and persist it, **Then** there is no such
   API — persistence is only via authored commands.

---

### User Story 5 - Session as a thin transaction + read-cache boundary (Priority: P2)

A developer opens a session to scope a transaction, run commands and queries, and benefit from a read
identity map (one instance per key within the session). The session does **not** track changes or flush a
mutable graph.

**Why this priority**: A transaction boundary and read identity map are still needed for correctness and
consistency, but the heavy change-tracking machinery is removed for lightness and predictability.

**Independent Test**: Open a session, run two commands and a query in one transaction, commit; confirm
atomicity and that loading the same key twice returns the same instance — with no dirty-tracking involved.

**Acceptance Scenarios**:

1. **Given** an open session, **When** multiple authored commands run before commit, **Then** they share one
   transaction and either all apply or none do.
2. **Given** a session, **When** the same entity key is read twice, **Then** the same immutable instance is
   returned (read identity map), with no change-tracking cost.

---

### User Story 6 - Optimistic concurrency expressed in the command (Priority: P2)

A developer writes an `update`/`delete` command that matches on a concurrency token; when the token no longer
matches (another writer won), the command affects zero rows and the caller is told.

**Why this priority**: Concurrency safety must survive the removal of the snapshot model; expressing it in the
command keeps it explicit and build-time-known.

**Independent Test**: Two callers load the same row's token; the first update succeeds; the second (stale
token) affects zero rows and the method surfaces a conflict.

**Acceptance Scenarios**:

1. **Given** an `update` filtered by a concurrency token, **When** the token is stale at execution, **Then**
   zero rows change and the method reports a conflict (no silent overwrite).

---

### User Story 7 - Pre-compiled, reused command/query definitions (Priority: P3)

Repeatedly executing the same command/query reuses one compiled definition (the "pre-saved command as a
function") rather than re-allocating it per call.

**Why this priority**: Performance/lightness refinement (Dapper/Insight-like). Correctness does not depend on
it, so it is lower priority.

**Independent Test**: Execute the same command many times; confirm its compiled definition is allocated once
and reused (allocation measurement), result type unchanged.

**Acceptance Scenarios**:

1. **Given** a generated command, **When** executed N times, **Then** its definition is created once and
   reused for all executions.

---

### Edge Cases

- A write is attempted without an authored command → there is no API for it (compile-time impossibility), not
  a runtime error.
- A nested insert's child references the parent before the parent id exists → resolved within the single
  command (parent written first, id flowed to the child); never two round-trips.
- An `update`/`delete` matches zero rows (including a stale concurrency token) → defined, surfaced result; no
  silent success.
- A `with`-declared name is referenced but never bound, or shadows another → build-time located diagnostic.
- A command references an undefined entity/field/parameter → build-time located diagnostic (no masking output).
- A query result is treated as mutable/persistable → impossible by construction (immutable type, no save API).
- A nested write cycle (A inserts B which inserts A) → build-time diagnostic, not infinite SQL.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Materialized entities and all query/command results MUST be **immutable** (no public setters,
  no in-place mutation, no snapshot/dirty state). State transitions occur only by executing a command.
- **FR-002**: All data manipulation (`insert`/`update`/`delete`) MUST be authored as **named DQL commands**
  in `.dql` files; each MUST compile to a typed method whose SQL is produced at build time. There MUST be no
  implicit/auto write path (no add-and-save, no snapshot-diff update).
- **FR-003**: The system MUST NOT perform runtime change-tracking, snapshotting, or unit-of-work diffing. This
  supersedes the mutable session model of feature `001` (its FR-014/FR-015).
- **FR-004**: Commands MUST support **nested writes** — a related `insert`/`update`/`delete` embedded in a
  parent command (single ref and collection forms) — executed as a **single database round-trip**.
- **FR-005**: A command's result type MUST be statically known at build time; only parameter values vary at
  runtime (no runtime SQL compilation).
- **FR-006**: The DSL MUST support **`with` declarations** binding a name to a value/reference/sub-expression
  (including a nested write's result) for reuse within a command or query. An explicit `with` binding is the
  **only** mechanism in v1 for a nested/related write to reference another write's result (e.g. a parent's
  generated id); there is no implicit auto-link and no special back-reference token.
- **FR-007**: The DSL MUST support **parameters** (required and optional) on commands and queries, with a
  fixed result type across parameter combinations.
- **FR-008**: The DSL MUST support invoking **native functions** in commands and queries (e.g.
  `datetime::now()`), provider-scoped and build-time type-checked.
- **FR-009**: Reads MUST return **full entities or distinct projections** with build-time-known shape;
  accessing a non-selected field MUST be a compile error. Relationship members on read results use the
  read-side reference types (`Ref`/`RefSet`/`RefList`/`RefBag`/`RefMap`).
- **FR-010**: A session MUST provide a **transaction boundary**, **read identity map** (one immutable instance
  per key within the session), and **execution** of authored commands/queries — and nothing else (no
  tracking, no flush).
- **FR-011**: Optimistic concurrency MUST be expressible **within an `update`/`delete` command** (e.g. a
  token-matched filter); a stale match MUST affect zero rows and surface a conflict to the caller (no silent
  overwrite).
- **FR-012**: Compiled command/query **definitions MUST be reused** across executions (allocated once), with
  no change to the result type.
- **FR-013**: The DSL surface for commands MUST adopt a pragmatic EdgeQL-aligned subset (Tier A): nested
  writes, `with`, path/set expressions, parameters, native functions. Advanced EdgeQL (polymorphism,
  backlinks, broad set algebra, grouping) is explicitly out of scope for this fork's v1.
- **FR-014**: All generated SQL/DDL MUST remain **schema-qualified** (module → DB schema) and follow the
  configurable **naming convention** (snake_case default) with per-unit overrides — carried over from `001`.
- **FR-015**: The library MUST remain **Native AOT + full-trimming compatible** with zero library-originated
  warnings and no runtime reflection on hot paths — carried over from `001`.
- **FR-016**: Schema definition (`.dqls`, module → DB schema), schema-qualified DDL apply, configurable
  naming, and provider-native value types (e.g. `jsonb`) MUST be preserved from `001`.
- **FR-017**: Provider connectivity and provider-specific behavior MUST be verified against a **real provider
  in ephemeral Docker (Testcontainers)** — never mocks — carried over from `001`.

### Key Entities *(include if feature involves data)*

- **Command (DQL)**: A named, authored write (`insert`/`update`/`delete`), possibly nested, with parameters
  and `with` bindings; compiled to a typed method + build-time SQL.
- **Query (DQL)**: A named authored read returning an immutable entity or projection (carried from `001`).
- **Immutable Entity / Projection**: A read-only materialized result; relationship members use the read-side
  reference types; never mutated or "saved".
- **`with` Binding**: A named value/reference/sub-expression reused within a command/query.
- **Session**: A transaction boundary + read identity map + command/query executor (no change-tracking).
- **Compiled Definition**: The reusable, pre-built representation of a command/query (build-time SQL +
  materializer), allocated once and reused.
- **Concurrency Token**: A field matched within an `update`/`delete` command to detect conflicting writes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of writes go through an authored command; there is **no** implicit/auto write API
  (verifiable: the public surface exposes no add-track-save and no snapshot-diff update).
- **SC-002**: A nested write (parent + related/child) executes in **exactly one** database round-trip
  (verifiable by statement count).
- **SC-003**: Every command/query result type is fixed at build time; accessing a non-selected field fails to
  compile **100%** of the time.
- **SC-004**: Materialized results are immutable — **0** public mutators and **0** ways to persist a mutated
  result (verifiable by API inspection).
- **SC-005**: A stale-token `update`/`delete` affects **0** rows and surfaces a conflict **100%** of the time
  (no silent overwrite).
- **SC-006**: A consuming application publishes Native AOT + fully-trimmed with **zero** library-originated
  warnings and no first-call warm-up (carried from `001`).
- **SC-007**: A repeated command/query reuses one compiled definition (no per-execution definition
  allocation), verifiable by allocation measurement.
- **SC-008**: A developer can author a schema, an insert command, and a query, then complete a write-and-read
  round-trip following the quickstart in under **15 minutes**, using only the DSL and documented APIs.

## Assumptions

- This fork **replaces** the mutable session / change-tracking model of `001` (Session.AddAsync/Remove +
  snapshot-diff). The `001-orm-aot-sourcegen` branch remains the return point if the direction is abandoned.
- Reuses the proven `001` foundation: Roslyn incremental generator + structured SQL IR + renderer; authored
  query model (`.dql` → ISession extension methods); configurable naming + overrides; schema-qualified DDL +
  apply; Native AOT; provider-native `jsonb`; the `Ref*` types (now read-side only); the schema DSL (`.dqls`).
- PostgreSQL is the primary/reference provider; nested-write single-round-trip relies on its data-modifying
  CTE capability (`WITH … RETURNING …`).
- Back-references between writes (e.g. a child referencing a parent's generated id) use an **explicit `with`
  binding** of the parent write (FR-006). v1 deliberately excludes implicit auto-linking and special
  back-reference tokens (the provisional `..id` is dropped); `with` covers the majority of cases.
- C# 14 / .NET 10; immutable results are realized as records (or equivalent), consistent with `001`.
- Target user is a .NET application developer; there is no end-user UI for the ORM itself.

## Out of Scope (v1 of this fork)

- The mutable session, change-tracking, snapshot diffing, and auto-INSERT/auto-UPDATE of `001` (removed).
- Advanced EdgeQL: polymorphic/type-intersection queries, backlinks, link properties, broad set algebra
  (union/distinct beyond basics), aggregates beyond simple counts, `group … by`, free-object results.
- A migrations CLI / versioned migration store (still future; schema apply is the idempotent create path).
- Relational providers other than PostgreSQL.
