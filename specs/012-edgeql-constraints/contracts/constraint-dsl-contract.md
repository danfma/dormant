# Contract — Constraint DSL Surface

**Owner**: Dormant team · **Stability**: DSL grammar surface (Constitution II) · **Version impact**: MAJOR

Defines the author-facing DormantQL schema syntax added by Feature 012. This is a compatibility
surface; changes follow SemVer over the DSL.

## Grammar additions (informal)

```
scalar_decl   := 'scalar' Name 'extending' BaseScalar '{' block_stmt* '}'

entity_decl   := ['abstract'] 'entity' Name ['extending' Name (',' Name)*]
                 '{' member* entity_stmt* '}'

member        := name ':' typeExpr ['?'] [ '{' block_stmt* '}' ] ';'
                 | collection_ref ';'        # collections take no constraint/annotation block (v1)

block_stmt    := constraint_stmt | annotation_stmt          # inside a member/scalar block

constraint_stmt        := 'constraint' name [ arg_list ] [ 'as' SqlName ] ';'
annotation_stmt        := 'annotation' name [ arg_list ] ';'
entity_stmt            := 'constraint' name [ 'on' '(' member (',' member)* ')' ]
                          [ '(' check_expr ')' ] [ 'as' SqlName ] ';'
                       |  'annotation' name [ arg_list ] ';'

arg_list      := '(' arg (',' arg)* ')'      # OPTIONAL — omitted entirely for zero-arg
arg           := literal                     # positional
               | name '=' literal            # named (C#-attribute-style):  range(min = 1, max = 2)
check_expr    := boolean expression over the entity's OWN members (same grammar as query `where`,
                 alias/column-qualified, NO relationship navigation)
```

Both `constraint` and `annotation` use the same **function-call, C#-attribute-style** form: optional
parentheses, positional or named arguments. `on (…)` and `as …` are suffix clauses on constraints
(outside the parentheses). Annotations are metadata (no `as`/`on`).

## Standard constraint names (minimum set)

| Name | Args | Scope | Lowering |
|------|------|-------|----------|
| `unique` | — / `on (cols)` | member, entity | UNIQUE |
| `check` | `(expr)` | member, entity | CHECK (expr) |
| `one_of` | value list | member | CHECK (col IN (…)) |
| `max` / `min` | value | member | CHECK (col <=/>= v) |
| `max_exclusive` / `min_exclusive` | value | member | CHECK (col </> v) |
| `max_length` / `min_length` / `length` | int | string member | CHECK (length(col) …) |
| `range` | `min`, `max` (named) | ordered member | sugar → CHECK (col BETWEEN min AND max) |
| `regex` | pattern | string member | PG: `col ~ 'p'`; SQLite: fallback (R-01) |
| `primary` | — / `on (cols)` | member, entity | PRIMARY KEY |
| `concurrency` | — | member | optimistic-lock token column (DEFAULT 0, incremented on update) |

### Annotations (metadata, minimum set)

| Name | Args | Scope | Effect |
|------|------|-------|--------|
| `column` | name string | value member | sets the DB column name (replaces removed `db("…")`) |

The annotation set is extensible (`description`, `deprecated`, …) without rework.

## Rules

- A member's constraint block and the entity's constraint statements use the **same** `constraint`
  keyword; entity-level adds `on (…)` and `check (expr)` forms.
- `as {name}` pins the generated SQL constraint name; omitted ⇒ deterministic default
  (`<table>_<cols>_<kind>`), stable across builds.
- Unknown name, type mismatch, missing target, duplicate `as`, bad scalar base, inheritance
  conflict/cycle, legacy modifier syntax, and unknown/misshaped annotations each produce a
  source-located diagnostic (ORM029–ORM036).
- **Removed**: the legacy trailing modifier forms (`… primary;`, `… concurrency;`) **and** the
  `db("…")` column-name modifier. They no longer parse (clean break). The DB column name is now set
  via the `column(...)` **annotation**, orthogonal to the constraint `as` name.

## Non-goals

- Relationship/navigation paths inside `check` (v1) — the navigation operator is removed.
- Constraints/annotations on reference or collection members (v1) — value members + entity level only.
- Polymorphic/table-per-type storage for inheritance (flattening only).
- Deep scalar-extends-scalar chains.
