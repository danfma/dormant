# Data Model: EdgeQL-Style Constraints (Feature 012)

Schema-model and IR additions in the source generator. Existing records keep their current shape
unless noted. All new records are `internal sealed record` and use `EquatableArray<T>` for
collections (incremental-generator cacheability).

## Schema model (`Parsing/SchemaModel.cs`)

### New: `ConstraintModel`

```
ConstraintModel(
  ConstraintKind Kind,                 // Unique, Check, OneOf, Max, Min, MaxExclusive,
                                       //   MinExclusive, MaxLength, MinLength, Length, Range, Regex,
                                       //   Primary, Concurrency
  EquatableArray<string> Targets,      // member names: 1 for member-level, N for entity-level (on (…))
  EquatableArray<ConstraintArg> Args,  // function-call args (positional or named), C#-attribute-style
  string? CheckExpression,             // raw boolean expr for Kind=Check (parsed → expr IR in P-B)
  string? SqlName,                     // `as {name}` — null ⇒ deterministic default
  LocationInfo Location                // diagnostics
)

ConstraintArg(string? Name, string Value)   // Name=null ⇒ positional; Name set ⇒ named (min = 1)
```

`ConstraintKind` enum carries metadata for validation: which base types it applies to, arg arity +
accepted named-arg keys, and member-vs-entity scope. Parentheses are optional for zero-arg kinds
(`unique`, `primary`, `concurrency`). `Range` (`range(min=, max=)`) is sugar lowering to two CHECKs.

### New: `AnnotationModel`

```
AnnotationModel(
  string Name,                         // e.g. "column" (DB column-name override; replaces db("…"))
  EquatableArray<ConstraintArg> Args,  // same function-call arg shape as constraints
  LocationInfo Location
)
```

Annotations are metadata (not validation). The `column` annotation supplies the DB column name
(the old `db("…")` modifier and `PropertyModel.NameOverride` are removed in favor of it). The set is
extensible (`description`, `deprecated`, …) without rework.

### New: `ScalarTypeModel`

```
ScalarTypeModel(
  string Name,                         // e.g. "Username"
  string BaseDslType,                  // e.g. "str" (must resolve via TypeMap)
  EquatableArray<ConstraintModel> Constraints,
  LocationInfo Location
)
```

### Extended: `EntityModel`

```
EntityModel(
  string Name,
  EquatableArray<PropertyModel> Properties,
  EquatableArray<ReferenceModel> References,
  string? NameOverride = null,
  // NEW:
  bool IsAbstract = false,
  EquatableArray<string> Extends = default,            // base entity names (composition)
  EquatableArray<ConstraintModel> EntityConstraints = default  // entity-level (multi-field/check)
)
```

### Extended: `PropertyModel`

```
PropertyModel(
  string Name, string DslType, string ClrType, bool IsNullable,
  bool IsPrimary, bool IsConcurrency,
  // NEW:
  EquatableArray<ConstraintModel> Constraints = default,  // member-level constraint block
  EquatableArray<AnnotationModel> Annotations = default   // member-level annotations (incl. column)
)
```

> `NameOverride` (the old `db("…")` column name) is **removed**; the DB column name now comes from a
> `column(...)` annotation in `Annotations`. `EntityModel` likewise gains
> `EquatableArray<AnnotationModel> EntityAnnotations` for entity-level annotations.

> `IsPrimary`/`IsConcurrency` are retained as derived convenience flags (computed from a
> `primary`/`concurrency` constraint) so downstream PK/concurrency code paths keep working; the
> source of truth becomes the constraint list.

### Extended: `SchemaModel` / `ParseResult`

Add `EquatableArray<ScalarTypeModel> Scalars`. Validator runs after parse; abstract entities are
retained in the model but excluded from table emission.

## Validation (`Parsing/SchemaValidator.cs`) → diagnostics

| ID | Condition |
|----|-----------|
| ORM029 | Unknown constraint name |
| ORM030 | Constraint applied to an incompatible member type (e.g. `max_length` on `int`) |
| ORM031 | Constraint references a missing member (member-level target / `unique on (…)`) |
| ORM032 | Duplicate constraint `as` name within a module |
| ORM033 | Unknown / non-scalar base in `scalar … extending <base>` |
| ORM034 | Inheritance conflict (incompatible member/constraint from two bases) or cycle |
| ORM035 | Removed legacy modifier syntax used (`… primary;`/`… concurrency;`/`db("…")`) — dedicated descriptor with a migration message pointing to the new form |
| ORM036 | Unknown annotation name, wrong annotation argument shape, or constraint/annotation on a reference/collection member (out of scope v1) |

(Exact final numbering pinned during implementation; ORM029 is the next free id. ORM035 is a
dedicated removed-syntax descriptor — NOT a reuse of the generic ORM020.)

## IR additions (`Ir/SqlIr.cs`)

### New: `ConstraintDef` (neutral)

```
ConstraintDef(
  ConstraintIrKind Kind,               // Unique, Check, PrimaryKey, NotNull-handled-elsewhere
  EquatableArray<string> Columns,      // resolved DB column names
  string? CheckSql,                    // dialect-neutral expr IR lowered to SQL by renderer
  string Name                          // resolved (explicit `as` or deterministic default)
)
```

Length/range/pattern/one_of lower to `Check` `ConstraintDef`s (e.g. `max_length(10)` →
`CHECK (length(col) <= 10)`, `one_of('a','b')` → `CHECK (col IN ('a','b'))`, `min(0)` →
`CHECK (col >= 0)`); `unique`→`Unique`; `primary`→`PrimaryKey`. The check expression reuses the
existing query-expression IR (R-03).

### Extended: `CreateTableStatement`

```
CreateTableStatement(
  TableRef Table,
  EquatableArray<ColumnDef> Columns,
  EquatableArray<ConstraintDef> TableConstraints   // NEW: table-level (multi-col unique, checks, composite PK)
)
```

`ColumnDef` keeps `(Name, DslType, NotNull, PrimaryKey)`; single-column constraints may render inline
or as table constraints depending on dialect/name presence (decided by the renderer).

## Rendering (`Ir/Dialects/*`)

- `SqlDialectRendererBase.RenderCreateTable()` extended to append `RenderConstraints(tableConstraints)`.
- New overridable hooks: `RenderUnique`, `RenderCheck`, `RenderPrimaryKey`, `RenderConstraintName`.
- `PostgreSqlRenderer`: full named `CONSTRAINT`, `CHECK`, `UNIQUE`; `regex` → `col ~ 'p'`.
- `SqliteRenderer`: named inline `CONSTRAINT`, `CHECK`, `UNIQUE`; `regex` fallback per R-01
  (GLOB/LIKE where faithful, else omit + warn).

## Binding emit (`Schema/EntityBindingEmitter.cs`)

- Resolve inheritance (flatten base members + constraints, R-05) and scalars (lower to base CLR +
  inherited constraints, R-06) **before** building `ColumnDef`s.
- Build `ConstraintDef`s from member + entity constraints; assign names (R-02).
- Resolve each column's DB name from a `column(...)` annotation when present (else the existing naming
  convention) — replaces the removed `NameOverride`/`db("…")` path.
- Lower `range(min=, max=)` and other multi-arg sugar to the primitive CHECK `ConstraintDef`s.
- Abstract entities: emit no `CreateTableStatement`.

## Out of scope for the model

- Table-per-type / polymorphic storage (flattening only, R-05).
- Navigation/relationship paths inside `check` expressions (v1, R-03).
- Scalar-extends-scalar deep chains beyond one base level (R-06).
