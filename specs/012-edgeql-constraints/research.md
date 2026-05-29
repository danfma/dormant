# Research: EdgeQL-Style Constraints (Feature 012)

**Status**: Phase 0 — decisions recorded. Source model: Gel/EdgeDB
(`https://docs.geldata.com/reference/datamodel`, `.../stdlib/constraints`).

## R-01: SQLite `regex` constraint (no native REGEXP)

- **Decision**: On PostgreSQL render `regex(p)` as `CHECK (col ~ 'p')`. On SQLite, SQLite has **no
  built-in `REGEXP`** operator (it requires an app-registered function). Render a `CHECK` only when
  the pattern is faithfully expressible via `GLOB`/`LIKE`; otherwise **omit DB-level enforcement on
  SQLite** and emit a build-time **warning diagnostic** recording the unenforced constraint. Never
  silently imply enforcement.
- **Rationale**: Honesty over false guarantees (Principle I/VI). PostgreSQL (primary provider) gets
  full enforcement; SQLite is a secondary/dev provider where the limitation is acceptable if surfaced.
- **Alternatives**: (a) Require consumers to register a `regexp` function in SQLite — rejected as it
  breaks AOT-clean zero-config and EnsureCreated. (b) Drop `regex` entirely — rejected; PG supports it.

## R-02: Named constraints + `as` across dialects

- **Decision**: Both PostgreSQL and SQLite support `CONSTRAINT <name> {UNIQUE|CHECK|PRIMARY KEY}`
  inline in `CREATE TABLE`. Use the `as {name}` value as the constraint name on both. Without `as`,
  generate a deterministic name: `<table>_<cols>_<kind>` (e.g. `users_email_key`, `users_a_b_uniq`,
  `users_chk_<n>`), stable across builds of an unchanged schema.
- **Rationale**: Stable names matter for migrations/ops (SC-003); both dialects accept inline named
  constraints, so no dialect divergence for naming.
- **Alternatives**: Auto-only names — rejected (FR-005 requires author control).

## R-03: `check` expression subset

- **Decision**: `check (…)` accepts the **same boolean expression grammar already used for query
  `where` clauses** (operators `== != < <= > >= && || !`, member references, literals,
  parenthesization), restricted to the entity's own members (the DormantQL analogue of EdgeQL
  `__subject__`). Reuse the existing expression parsing + the query expression IR; render via the
  existing per-dialect expression renderer. No navigation/relationship paths in v1.
- **Rationale**: Maximizes reuse (009 expression IR + renderers), keeps the surface familiar, avoids
  a second expression language. Cross-field checks (SC-002) are covered.
- **Alternatives**: A bespoke constraint-expression mini-language — rejected (duplication, drift).

## R-04: Standard constraint library + naming

- **Decision**: Minimum set mirrors EdgeQL `std` with semantic renames (spec mapping table):
  `unique` (`exclusive`), `check` (`expression`), `one_of`, `max`/`min` (`max_value`/`min_value`),
  `max_exclusive`/`min_exclusive` (`*_ex_value`), `max_length`/`min_length`/`length`
  (`*_len_value`/`len_value`), `regex` (`regexp`). DormantQL-only: `primary`, `concurrency`.
- **Rationale**: "Copy max from EdgeQL" + familiar SQL/validation vocabulary (FR-003/FR-011).
- **Type applicability**: length/regex → string members; max/min/exclusive → ordered scalars;
  `unique`/`check` → member or entity; `one_of` → scalar member. Mismatch → diagnostic (FR-009).

## R-05: Inheritance & composition model

- **Decision**: `abstract entity X { … }` defines a reusable base (members + constraints) that emits
  **no table**. `entity Y extending X (, Z)` **flattens** all inherited members and constraints into
  Y's single concrete table (no table-per-type, no joins — consistent with flat-entity Principle III
  and the existing single-table binding). Multiple bases compose; a member/constraint name appearing
  in two bases (or base+derived) with incompatible definition → diagnostic; identical → dedup.
- **Rationale**: Flattening keeps the statically-known, single-table model intact and avoids runtime
  polymorphism. Mirrors EdgeQL `extending` at the schema-authoring level without its multi-table
  storage.
- **Alternatives**: Table-per-type / single-table-hierarchy with discriminator — rejected for v1
  (adds query-shape complexity, conflicts with flat-entity reads).

## R-06: Custom scalar types

- **Decision**: `scalar Name extending <base> { constraint …; }` registers a schema-local scalar
  mapping `Name → base CLR type` (resolved through an extended, scalar-aware `TypeMap`) plus a set of
  inherited constraints. A member typed `Name` is a value property of the base CLR type whose DDL
  carries the scalar's constraints; member-level constraints add to (not replace) the scalar's.
  Scalars may only extend base scalars in v1 (no scalar-extends-scalar chains beyond one level unless
  trivial). `one_of` on a scalar is the idiomatic enum.
- **Rationale**: Removes duplication, names domain concepts, reuses the value-property path (no new
  CLR type emitted for a scalar — it lowers to its base).
- **Alternatives**: Emitting a distinct C# type per scalar — rejected (AOT/STJ cost, breaks flat
  mapping; not needed for highlighting/constraint purposes).

## R-07: `primary` / `concurrency` as constraints

- **Decision**: `constraint primary;` → PRIMARY KEY in DDL (single or, at entity level,
  `constraint primary on (a, b)` composite). `constraint concurrency;` → marks the optimistic-lock
  token; DDL emits the column with a sensible default and the runtime mutation path keeps matching it
  in WHERE (existing behavior). They live in the same constraint block as validation constraints.
- **Rationale**: FR-013 (one uniform mechanism). DDL wiring for PK already exists; concurrency was
  parsed-but-unused at DDL and is now first-class.

## R-08: Migration of existing syntax (BC-1)

- **Decision**: Clean break. The old modifier forms (`primary`/`concurrency`/`db("…")` trailing)
  stop parsing; encountering them yields a **removed-syntax diagnostic** (reuse the ORM020 pattern)
  pointing to the new form. Provide a written migration guide (quickstart) with before/after for each
  form. All in-repo `.dqls` (samples + test schemas) are migrated within this feature's PR.
- **Open**: whether `db("…")` column-name override also moves under the constraint block or becomes a
  separate member annotation — resolved in data-model (kept as a member-level `as`-style name on the
  column, distinct from constraint `as`). Default: member column name stays `db("…")`-style but is
  re-expressed consistently; final form pinned in data-model.md.

## Next

Phase 1: data-model.md (model + IR nodes), contracts (DSL grammar + DDL mapping), quickstart
(author guide + migration). Re-check Constitution after design.
