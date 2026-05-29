# Contract — Constraint DDL Mapping (neutral IR → per-dialect)

**Owner**: Dormant team · **Stability**: generated-code (DDL) contract (Constitution II) · multi-dialect (005)

Defines how neutral constraint IR (`ConstraintDef`) renders to PostgreSQL (primary) and SQLite
(secondary) DDL inside `CREATE TABLE`. All DDL is generated at build time; constraint names are
deterministic.

## Neutral → SQL mapping

| IR `ConstraintDef.Kind` | PostgreSQL | SQLite |
|-------------------------|-----------|--------|
| `PrimaryKey` (1 col) | `… PRIMARY KEY` (column) | same |
| `PrimaryKey` (N cols) | `CONSTRAINT <name> PRIMARY KEY (cols)` | same |
| `Unique` | `CONSTRAINT <name> UNIQUE (cols)` | same |
| `Check` (incl. lowered length/range/one_of) | `CONSTRAINT <name> CHECK (<expr>)` | same (CHECK supported) |
| `Check` from `regex` | `CHECK (col ~ 'p')` | **fallback (R-01)**: GLOB/LIKE if faithful, else omit + build warning |
| NOT NULL | column `NOT NULL` (unchanged) | same |
| concurrency token | column emitted with default; matched at runtime in WHERE | same |

## Naming

- Explicit `as {name}` → exact constraint name on both dialects.
- Default name: `<table>_<col(s)>_<kindsuffix>` (e.g. `users_email_key`, `users_first_last_uniq`,
  `users_chk_1`). Deterministic and stable across builds of an unchanged schema (SC-003).
- Duplicate names within a module → ORM032 (build error), before any DDL is emitted.

## Rendering hooks (`SqlDialectRendererBase`)

- `RenderCreateTable()` appends rendered table constraints after columns.
- New overridable: `RenderUnique`, `RenderCheck`, `RenderPrimaryKey`, `RenderConstraintName`,
  `RenderRegexConstraint` (the last one is where the SQLite fallback lives).

## Guarantees

- PostgreSQL enforces every constraint kind in the minimum set.
- SQLite enforces all kinds **except** `regex` where no faithful GLOB/LIKE exists — that case is
  surfaced as a build-time warning, never silently dropped (Principle I/VI).
- Existing PG DDL for schemas that used no constraints stays byte-identical except for the added,
  explicitly-requested constraints.

## Verification

- Verify snapshots assert the generated DDL per dialect for a representative schema.
- Conformance tests (PG via Testcontainers, SQLite in-memory) insert violating rows and assert the
  database rejects them for each enforced kind.
