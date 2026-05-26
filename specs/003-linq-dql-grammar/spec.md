# Feature Specification: LINQ-Style DQL Grammar

**Feature Branch**: `003-linq-dql-grammar`

**Created**: 2026-05-25

**Status**: Draft

**Input**: User description: "Outra grande alteração na linguagem em si. Usar `app.query` como base, mas seguir
uma linha mais LINQ com complementos da EdgeQL: mais SQL, aliases bem definidos, operadores estilo
C#/TypeScript, blocos separados por chaves. Identificadores `query`/`mutation` (estilo GraphQL). Tipos mais
C#/TS, menos Rust. Alias explícito. Inferência do tipo de retorno (sem `returning` explícito; insert retorna
um id, similar à EdgeQL)."

## Overview

This feature **redefines the DormantQL surface grammar** for authored reads and writes. It replaces the
prior `002` EdgeQL-leaning syntax (`command Name(...) = insert E { f := p };`, `query Name(...) = select E
filter .x = p;`, leading-dot member access, `:=` assignment, single-`=` comparison, `and` keyword) with a
**LINQ-/SQL-hybrid, brace-delimited grammar** that uses explicit aliases and C#/TypeScript-style operators.

It is a **front-end (grammar) change only**: the immutable, command-driven semantics established in `002`
are preserved unchanged — command-driven writes (no auto-DML/change-tracking), immutable materialized
results, app-assigned primary keys (FR-018 of `002`), `<ref>_id` foreign-key columns (FR-019 of `002`),
optimistic concurrency via a `where` filter on a version field, single-round-trip nested writes, and the
generator/IR/renderer/naming/DDL/AOT/jsonb foundation all carry over. Only the authored syntax and the unit
identifiers change.

## Clarifications

### Session 2026-05-25

- Q: What identifiers name the authored units? → A: **`query`** for reads and **`mutation`** for writes
  (GraphQL-style), each a **brace-delimited block** (`query Name(params) { … }` / `mutation Name(params)
  { … }`). This replaces `002`'s `query Name(...) = …;` and `command Name(...) = …;` forms; the keyword
  `command` is removed in favor of `mutation`.
- Q: How is a unit's result type determined? → A: **By default, inferred from the block** (no annotation
  required): `select alias` → the full immutable entity; `select { alias.f1, alias.f2 }` → a distinct
  projection type; `insert` → the inserted entity's **primary key (id)** (EdgeQL-style default shape);
  `update`/`delete` → the **affected-row count**. A mutation MAY override this default via a `returning`
  clause or a trailing read — see the two clarifications below (FR-008, FR-017).
- Q: Which scalar type names does the parameter/grammar vocabulary use? → A: **C#/TypeScript-leaning, not
  Rust**: host-friendly primitives `string`, `bool`, `int`, `long`, `double`, `decimal`; language-neutral DB
  scalars `uuid`, `datetime`, `date`, `json`; plus the `optional T` modifier. Rust-isms (`str`, `i32`,
  `i64`, `u32`, `f64`) are dropped.
- Q: Are aliases explicit and required? → A: **Yes** — every subject declares an explicit alias (`from User
  u`, `insert User u`, `update User u`, `delete User u`) and all member references are alias-qualified
  (`u.email`). The `002` leading-dot form (`.email`) is removed.
- Q: What is the clause order and statement separator? → A: Uniform order **subject → `where` →
  (`set` | `select` | `order by`)**; statements are **one per line, newline-separated, no `;`**; comments use
  `#`.
- Q: What is the identifier casing convention? → A: **`query`/`mutation` names are authored in `snake_case`
  and translated to `PascalCase` for the generated C# method** (e.g. `users_by_email` → `UsersByEmail`).
  **Entity names stay `PascalCase`** (`User`), and **primitive/scalar type keywords stay lowercase**
  (`string`, `int`, `uuid`, …). This mirrors `002`'s member/identifier casing convention (snake_case DQL →
  PascalCase C#).
- Q: Can a mutation return richer values/entities, and what forms does `returning` accept? → A: **Yes**, via
  a **`returning <expr>` clause whose expression mirrors `select`**: `returning alias` (full entity),
  `returning { alias.f, … }` (projection), `returning alias.field` (scalar). The default inference
  (insert→id, update/delete→count) applies only when no `returning`/trailing read is authored (FR-008,
  FR-017).
- Q: In a **multi-command** mutation (several writes plus an optional trailing read), what determines the
  unit's result type? → A: The **trailing statement** — the last `returning`/read (`from … select …`)
  expression sets the result type; if neither is present, the default inference applies. **`with`-bindings
  flow values between commands** (FR-017).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Author a read query in the LINQ-style grammar (Priority: P1)

A .NET developer authors a named `query` block that reads an entity using LINQ-/SQL-shaped clauses
(`from`, `where`, `order by`, `select`) with an explicit alias and C#/TypeScript operators, and gets a
generated, strongly-typed method returning the immutable entity.

**Why this priority**: Reads are the most-used surface; proving the new grammar end-to-end for a full-entity
read establishes the lexer/parser/operators/aliases foundation that every other story builds on. It is the
smallest slice that delivers a working query in the new language.

**Independent Test**: Author `query users_by_email(email: string) { from User u where u.email == email select
u }`, build, and call the generated `UsersByEmail` method against a real database — it returns the matching
users with build-time SQL and no runtime query compilation.

**Acceptance Scenarios**:

1. **Given** a `.query` unit with a `query` block using `from E alias`, a `where` predicate with `==`, and
   `select alias`, **When** the project builds, **Then** a typed method is generated whose result element is
   the full immutable entity.
2. **Given** the same query, **When** it executes against seeded data, **Then** it returns exactly the rows
   matching the predicate, honoring any `order by alias.field asc|desc`.
3. **Given** a member reference that is not alias-qualified or uses an undeclared alias, **When** the project
   builds, **Then** a located diagnostic is reported (the grammar requires explicit, declared aliases).

---

### User Story 2 - Author an insert mutation with inferred id return (Priority: P1)

A developer authors a `mutation` block containing an `insert Entity alias { alias.field = expr }` and gets a
generated method that performs the write and returns the inserted entity's **primary key (id)** — with no
explicit `returning` clause.

**Why this priority**: Writes are the other half of the MVP. The `mutation`/`insert` path proves the
GraphQL-style identifier, the brace-delimited assignment block (`alias.field = expr`, one per line), and the
EdgeQL-style return inference (id), while preserving the `002` command-driven, immutable semantics.

**Independent Test**: Author `mutation create_user(id: uuid, email: string, created_at: datetime, version:
int) { insert User u { u.id = id; u.email = email; u.created_at = created_at; u.version = version } }`, build,
call the generated `CreateUser` method, and confirm it inserts the row and returns the new id.

**Acceptance Scenarios**:

1. **Given** a `mutation` with `insert E alias { … }` and one `alias.field = parameter` per line, **When** the
   project builds, **Then** a typed method is generated whose return is the entity's primary-key type.
2. **Given** the generated insert method, **When** it executes, **Then** exactly one row is inserted with the
   supplied values and the returned id equals the assigned primary key.
3. **Given** an `insert` that omits a required (non-optional) member, **When** the project builds, **Then** a
   located diagnostic is reported.
4. **Given** an `insert` mutation ending in `returning u` (or `returning { u.id, u.email }`), **When** the
   project builds, **Then** the generated method's result is the entity (or projection) shape — not the bare
   id — and the runtime returns the inserted row materialized to that shape.
5. **Given** a multi-command mutation (e.g. two writes followed by a trailing `from … select …`) with a
   `with`-bound id flowing from an earlier command, **When** it executes, **Then** the method returns the
   trailing read's shape and the bound id resolves correctly downstream.

---

### User Story 3 - Author update/delete mutations with `where` and affected-count return (Priority: P2)

A developer authors `mutation` blocks for `update` and `delete` that filter rows with a `where` predicate
(clause order `update/delete E alias → where → set`) and gets generated methods returning the **affected-row
count**, enabling optimistic concurrency by matching a version field in `where`.

**Why this priority**: Completes the write surface and carries `002`'s count-based concurrency conflict
signal into the new grammar. Independent of projections (US4).

**Independent Test**: Author `mutation set_user_name(id: uuid, name: string) { update User u where u.id == id
set { u.name = name } }` and a delete mutation, build, and confirm the update returns 1 for a match and a
version-filtered update returns 0 for a stale write.

**Acceptance Scenarios**:

1. **Given** a `mutation` with `update E alias where <pred> set { alias.field = expr }`, **When** it executes
   against a matching row, **Then** the row is updated and the method returns the affected count.
2. **Given** an `update` whose `where` matches the current version, **When** a second writer reuses the stale
   version, **Then** the second update affects **0** rows (conflict signal) and the first writer's value
   persists.
3. **Given** a `mutation` with `delete E alias where <pred>`, **When** it executes, **Then** the matching
   rows are removed and the method returns the affected count.

---

### User Story 4 - Author a projection select block (Priority: P2)

A developer authors `select { alias.f1, alias.f2 }` in a `query` block and gets a distinct, generated
projection type exposing exactly the selected members — never the full entity.

**Why this priority**: Projections are a core read shape and the Clean-Architecture boundary (results can
target plain user-owned types). Builds on US1's read grammar.

**Independent Test**: Author `query get_user_contacts(since: datetime) { from User u where u.created_at >=
since order by u.email asc select { u.id; u.email } }`, build, and confirm the generated `GetUserContacts`
result type exposes only `id` and `email`.

**Acceptance Scenarios**:

1. **Given** a `query` ending in `select { alias.a, alias.b }`, **When** the project builds, **Then** a
   distinct projection type is generated exposing exactly those members.
2. **Given** code that accesses a member not present in the projection, **When** the project builds, **Then**
   it fails to compile.

---

### User Story 5 - Replace the prior grammar across samples and tests (Priority: P3)

The existing `002` sample and test `.query`/`.dql` units are migrated to the new grammar, and the prior
`command`/`= …;`/leading-dot/`:=`/`and` forms no longer parse, proving the replacement is complete with no
behavioral regression.

**Why this priority**: Confirms the grammar is a clean replacement (single canonical surface), not an
addition, and that all `002` semantics still pass on the new front-end. Depends on US1–US4.

**Independent Test**: Convert the quickstart and provider tests to `query`/`mutation` blocks; the full suite
(generator + core + provider) passes and the AOT smoke publishes with zero warnings.

**Acceptance Scenarios**:

1. **Given** a unit authored in the removed `002` syntax (`command …`, `… = …;`, leading-dot, `:=`, `and`),
   **When** the project builds, **Then** a located diagnostic is reported (the old forms are not accepted).
2. **Given** the migrated sample/tests, **When** the suite runs, **Then** all read/write/concurrency
   behaviors from `002` pass unchanged on the new grammar.

### Edge Cases

- A subject without an explicit alias (`from User` with no `u`) → located diagnostic.
- A member reference using an undeclared or duplicate alias → located diagnostic.
- A `where` predicate combining conditions with `&&`/`||` and grouping/negation (`!`) → parses with
  C#/TypeScript precedence.
- An `optional T` parameter left unsupplied → its `where` condition is omitted (carried from `002`), with the
  same result type for every combination.
- A statement that spans intent across lines or includes a stray `;` → defined behavior: one statement per
  line; a trailing `;` is tolerated/ignored, never required.
- An `insert` assigning a single reference (`alias.ref = expr`) → writes the `<ref>_id` foreign-key column
  (per `002` FR-019); assigning a `with`-bound write's id flows the app-assigned PK (per `002` FR-018).
- A `query`/`mutation` that mixes clause order (e.g. `set` before `where`) → located diagnostic (canonical
  order is enforced).
- A `mutation` with a `returning <expr>` → its result type is the `returning` shape (entity/projection/
  scalar), overriding the default id/count inference.
- A multi-command `mutation` (e.g. two `insert`s then a trailing `select`/`returning`) → the result type is
  the trailing statement's shape; `with`-bound ids from earlier commands are referable downstream.
- A `returning` clause referencing a member not produced by its expression → fails to compile (same fixed-
  shape guarantee as `select`).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The grammar MUST name authored units with **`query`** (reads) and **`mutation`** (writes), each
  a brace-delimited block `keyword Name(parameters) { … }`. The `002` keyword `command` and the `Name(...) =
  …;` form MUST be removed.
- **FR-002**: Every subject (`from`/`insert`/`update`/`delete`) MUST declare an **explicit alias**
  (`from User u`), and all member references MUST be **alias-qualified** (`u.email`). The leading-dot form
  (`.email`) MUST NOT be accepted.
- **FR-003**: The grammar MUST use **C#/TypeScript-style operators**: comparison `==`, `!=`, `<`, `<=`, `>`,
  `>=`; logical `&&`, `||`, `!`; assignment `=` (for member writes in `insert`/`set`). The `002` single-`=`
  comparison, `:=` assignment, and `and`/`or` keyword forms MUST be removed.
- **FR-004**: Parameter and member types MUST use the **C#/TypeScript-leaning vocabulary**: `string`, `bool`,
  `int`, `long`, `double`, `decimal`, `uuid`, `datetime`, `date`, `json`, plus the `optional T` modifier.
  Rust-style names (`str`, `i32`, `i64`, `u32`, `f64`) MUST NOT be accepted.
- **FR-005**: A `query` block MUST support the clauses **`from Entity alias` → `where <pred>` → `order by
  alias.field asc|desc` → `select …`** in that canonical order; statements are one per line (newline-
  separated, no required `;`); `#` begins a line comment.
- **FR-006**: `select alias` MUST yield the **full immutable entity** as the result element; `select {
  alias.f1, alias.f2 }` MUST yield a **distinct projection type** exposing exactly the selected members.
- **FR-007**: A `mutation` block MUST support `insert Entity alias { alias.field = expr … }`, `update Entity
  alias where <pred> set { alias.field = expr … }`, and `delete Entity alias where <pred>`, in the canonical
  order **subject → `where` → `set`**.
- **FR-008**: A mutation's result MUST be determined by its **trailing statement**: an explicit `returning
  <expr>` clause or a trailing read (`from … select …`) sets the result type; when **neither** is present,
  the result is **inferred by default** — `insert` → the inserted entity's **primary key (id)**;
  `update`/`delete` → the **affected-row count**. A `query`'s result follows FR-006.
- **FR-009**: The grammar MUST report **located diagnostics** (precise source spans) for grammar violations:
  missing/undeclared/duplicate alias, unknown entity/member/parameter, wrong clause order, removed-syntax
  use, and missing required insert members.
- **FR-010**: All **`002` semantics MUST be preserved** on the new grammar: command-driven writes with no
  auto-DML/change-tracking; immutable materialized results; app-assigned primary keys (no DB-side default);
  `<ref>_id` foreign-key columns for single references; optimistic concurrency via a `where` match on a
  version field (stale → 0 rows); the read identity map and thin session.
- **FR-011**: The grammar MUST preserve `002`'s carried-over capabilities: configurable naming + overrides,
  module → DB schema mapping with schema-qualified DDL/SQL, provider-native value types (`jsonb`), and the
  schema DSL.
- **FR-012**: `optional T` parameters MUST keep `002` behavior: each predicate condition is included only
  when its parameter is supplied, and the result type is identical for every parameter combination.
- **FR-013**: The library MUST remain **Native AOT + full-trimming compatible** with zero library-originated
  warnings, build-time SQL via the Roslyn incremental generator, and incrementally cacheable generation steps
  for the new grammar.
- **FR-014**: Provider connectivity and provider-specific behavior MUST continue to be verified against a
  **real provider in ephemeral Docker (Testcontainers)** — never mocks.
- **FR-015**: The new grammar MUST be the **single canonical surface**: the removed `002` forms MUST NOT
  coexist or be silently re-accepted (no dual grammar).
- **FR-016**: Identifier casing MUST follow: **`query`/`mutation` names authored in `snake_case`** and
  generated as `PascalCase` C# methods (`users_by_email` → `UsersByEmail`); **entity names in `PascalCase`**
  (`User`); **primitive/scalar type keywords lowercase** (`string`, `int`, `uuid`, …). Aliases and member
  names follow `002`'s existing snake_case-DQL → PascalCase-C# convention.
- **FR-017**: A `mutation` MUST support shaping a richer result via (a) a **`returning <expr>` clause** whose
  expression mirrors `select` — `returning alias` (full entity), `returning { alias.f, … }` (projection),
  `returning alias.field` (scalar) — and/or (b) **multi-command** blocks containing more than one write
  and/or a trailing read, with **`with`-bindings flowing values between commands**. The unit's result type is
  the trailing statement's shape (per FR-008).

### Key Entities *(include if feature involves data)*

- **Query (DQL)**: A named, brace-delimited read unit (`query Name(params) { from … where … order by …
  select … }`) compiled to a typed method + build-time SQL.
- **Mutation (DQL)**: A named, brace-delimited write unit (`mutation Name(params) { insert|update|delete … }`)
  compiled to a typed method. Result is the trailing statement's shape — a `returning <expr>` (mirrors
  `select`) or a trailing read — else the default (id for insert, affected count for update/delete). May be
  multi-command, with `with`-bindings flowing between commands.
- **`with` Binding**: A named value/reference/sub-expression introduced in a unit and reused by later
  commands/clauses; the mechanism that flows a write's id into a subsequent command or `returning`.
- **Alias**: An explicit, required range variable bound to a subject entity (`from User u`); the sole way to
  reference members (`u.email`).
- **Projection Type**: A distinct generated result type from a `select { … }` block exposing exactly the
  selected members.
- **Immutable Entity**: The read-only materialized result of `select alias`; relationship members use the
  read-side reference types; never mutated or "saved" (carried from `002`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of authored reads/writes use the new `query`/`mutation` brace grammar; the removed `002`
  forms (`command`, `= …;`, leading-dot, `:=`, `and`) fail to parse **100%** of the time (verifiable by
  diagnostics).
- **SC-002**: Every result type is fixed at build time — whether inferred by default or shaped by a
  `returning`/trailing read; accessing a non-selected member fails to compile **100%** of the time.
- **SC-003**: With no `returning`/trailing read authored, an `insert` returns the inserted id and an
  `update`/`delete` returns the affected count (default inference); when a `returning`/trailing read is
  authored, the result matches that shape **100%** of the time.
- **SC-004**: A stale-version `update`/`delete` affects **0** rows and surfaces a conflict **100%** of the
  time (no silent overwrite), unchanged from `002`.
- **SC-005**: The full test suite (generator + core + provider-against-real-Docker) and the AOT smoke publish
  pass with **zero** library-originated warnings after migration to the new grammar.
- **SC-006**: Materialized results remain immutable — **0** public mutators and **0** ways to persist a
  mutated result (verifiable by API inspection).
- **SC-007**: A developer can author a schema, one `query`, and one `mutation`, then complete a write-and-read
  round-trip following the quickstart in under **15 minutes**, using only the new DSL and documented APIs.
- **SC-008**: A new user reading a single `query`/`mutation` example correctly identifies the alias, the
  filter, and the result shape without external reference (familiarity check), reflecting the LINQ/SQL/C#
  design intent.

## Assumptions

- This grammar **replaces** `002`'s DQL surface syntax entirely (single canonical grammar, no back-compat
  parsing); `002`'s immutable, command-driven semantics and its generator/IR/renderer/naming/DDL/AOT/jsonb
  foundation are reused unchanged. The `002-immutable-command-dml` work remains the return point.
- The canonical reference for the grammar is the corrected `samples/Dormant.Sample.Quickstart/schema/
  app.query` (this feature's base), generalized into the rules above.
- **Default mutation return**: `insert` → the entity's primary key (id); `update`/`delete` → affected count.
  This default applies only when no `returning`/trailing read is authored. (Nuance: because primary keys are
  app-assigned per `002` FR-018, the caller already knows the inserted id — the default returned id is a
  confirmation handle and EdgeQL-parity default; richer shapes are opt-in via `returning`/multi-command.)
- A mutation's richer result (`returning`/trailing read) is shaped by the **same projection machinery as
  `select`** (FR-006), reused rather than re-invented; multi-command value flow reuses the **`with`-binding**
  mechanism (carried conceptually from `002` FR-006).
- Statements are one per line (newline-significant within a block); a trailing `;` is tolerated but never
  required; `#` begins a line comment — consistent with the corrected base file.
- Logical connectives are the symbolic C#/TypeScript forms (`&&`, `||`, `!`), not the SQL/EdgeQL keyword
  forms (`and`, `or`, `not`), per the "C#/TypeScript operators" intent.
- Scalar type keywords stay language-neutral for DB concepts (`uuid`, `datetime`, `date`, `json`) because C#
  (`Guid`/`DateTime`/`DateOnly`) and TypeScript lack a single shared name; primitives align to C# names.
- The **`with`-binding mechanism** (EdgeQL `with` keyword) is in scope for **multi-command sequencing** —
  binding a value or a command's result for reuse by later commands and the `returning`/trailing read
  (FR-017). What is deferred is **single-statement nested/multi-subject writes compiled to one round-trip**
  (the data-modifying CTE of `002` US2): in v1 a `mutation` may contain a sequence of commands, but the
  one-round-trip nesting of a parent+child in a single `insert` is a follow-up.
- C# 14 / .NET 10; target user is a .NET application developer; there is no end-user UI for the ORM itself.

## Out of Scope (v1 of this grammar)

- **Single-statement** nested/multi-subject writes compiled to one round-trip (the data-modifying CTE of
  `002` US2) — deferred to a follow-up. (Multi-**command** sequences with `with`-bindings and a trailing
  read/`returning` are **in scope** — FR-017.)
- Dynamic/runtime DQL and dynamic mapping (deferred to the future "macros" feature).
- Joins across multiple subjects, set algebra, polymorphic/backlink queries, and other advanced EdgeQL
  features beyond the single-subject read/write surface.
- Any change to the `002` runtime semantics (immutability, command-driven writes, concurrency model,
  PK/FK strategy) — this feature changes only the authored grammar.
