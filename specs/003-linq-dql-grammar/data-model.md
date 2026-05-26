# Phase 1 Data Model: LINQ-Style DQL Grammar

This feature changes the **build-time parsed model (AST)** produced by the generator's front end. The
**runtime model is unchanged** from `002` (immutable entities, distinct projection types, `Ref*` read-side
types, the thin session, `IEntityBinding`). Shapes below are the equatable records the parsers emit and the
emitters consume; field names are indicative, not final.

## Unit envelope

- **UnitFile** — one parsed `.dql` file: `Module` (schema), `Queries: QueryUnit[]`, `Mutations:
  MutationUnit[]`, `Diagnostics[]`. (Replaces `002`'s separate `QueryFile`/`CommandFile`; a file may hold both
  kinds.)
- **Identifier casing**: a unit's authored name is `snake_case`; the model carries both `DslName`
  (`users_by_email`) and the resolved `MethodName` (`UsersByEmail`).

## QueryUnit (read)

- `DslName` / `MethodName`
- `Parameters: Parameter[]`
- `Subject: Subject` — the `from Entity alias` (entity + bound alias)
- `Where: Predicate?` — optional boolean expression
- `OrderBy: OrderTerm[]` — each `MemberRef` + asc/desc
- `Result: ResultShape` — `select alias` (Entity) or `select { … }` (Projection)

## MutationUnit (write)

- `DslName` / `MethodName`
- `Parameters: Parameter[]`
- `Bindings: WithBinding[]` — zero or more `with x = <expr>` introduced before/between commands
- `Commands: WriteCommand[]` — one or more, in authored order
- `Result: ResultShape` — the **trailing statement's** shape: a `returning <expr>`, a trailing read, or the
  default (id for a single trailing `insert`; affected count for `update`/`delete`)

### WriteCommand (one of)

- **InsertCommand**: `Subject` (`insert Entity alias`), `Assignments: Assignment[]`
  (`alias.column = ValueExpr`), optional `Returning: ResultShape`
- **UpdateCommand**: `Subject` (`update Entity alias`), `Where: Predicate` (required),
  `Assignments: Assignment[]` (the `set { … }` block), optional `Returning: ResultShape`
- **DeleteCommand**: `Subject` (`delete Entity alias`), `Where: Predicate` (required),
  optional `Returning: ResultShape`

## Shared nodes

- **Subject** — `EntityName` (PascalCase) + `Alias` (the range variable). Validated against the schema entity
  set; unknown entity → diagnostic.
- **Alias** — declared per subject; referenced by `MemberRef`. Missing/undeclared/duplicate → diagnostic.
- **MemberRef** — `Alias` + `Member` (snake_case in DQL) → resolves to the entity column (reusing the existing
  naming/override resolution). Unknown member → diagnostic.
- **Predicate** — a boolean expression tree over comparisons and logical connectives:
  - `Comparison(MemberRef, CompareOp, ValueExpr)` — `CompareOp ∈ { Eq(==), NotEq(!=), Lt(<), LtEq(<=),
    Gt(>), GtEq(>=) }`
  - `And(Predicate, Predicate)` (`&&`), `Or(Predicate, Predicate)` (`||`), `Not(Predicate)` (`!`), with
    C#/TS precedence and parentheses
  - An optional-parameter comparison is omitted when its parameter is unsupplied (carried `002` FR-012)
- **Assignment** — `MemberRef (=) ValueExpr`; the `=` token is assignment (not comparison).
- **ValueExpr** — a parameter reference, a literal (string/number/bool), a `with`-bound name, or a
  provider-native call (e.g. `now()`), reusing `002`'s command value kinds + the `json` `::jsonb` write cast.
- **Parameter** — `Name`, `DslType` (lowercase keyword), `ClrType`, `IsOptional` (`optional T`).
  `DslType ∈ { string, bool, int, long, double, decimal, uuid, datetime, date, json }`.
- **OrderTerm** — `MemberRef` + `Descending`.
- **WithBinding** — `Name` + bound `Expr` (a value or a command result); compiled to a C# local; referable by
  later commands/clauses and by `Returning`.
- **ResultShape** — one of:
  - `EntityResult(Subject)` — the full immutable entity element type
  - `ProjectionResult(Member[])` — a distinct generated projection type with exactly these members
  - `ScalarResult(MemberRef | id)` — a single value (e.g. `returning u.id`, or the default insert id)
  - `AffectedCount` — `int` (default for `update`/`delete` with no `returning`)

## Type vocabulary (DslType → ClrType)

| DslType | ClrType | Notes |
|---------|---------|-------|
| `string` | `string` | |
| `bool` | `bool` | |
| `int` | `int` | |
| `long` | `long` | |
| `double` | `double` | |
| `decimal` | `decimal` | |
| `uuid` | `System.Guid` | language-neutral scalar keyword |
| `datetime` | `System.DateTime` (timestamptz) | |
| `date` | `System.DateOnly` | |
| `json` | `string` (PG `jsonb`, `::jsonb` write cast) | carried from `002` |
| `optional T` | `T?` / omitted predicate | carried `002` FR-012 |

## Validation rules (→ located diagnostics, FR-009)

- Unknown entity, unknown member, unknown parameter.
- Missing / undeclared / duplicate alias; unqualified member reference.
- Wrong clause order (e.g. `set` before `where`, `select` before `from`).
- Removed `002` syntax (`command`, `= …;`, leading-dot, `:=`, `and`/`or`) → migration-pointed diagnostic.
- `insert` omitting a required (non-optional, non-defaulted) member.
- `returning`/`select` referencing a member not produced by its shape (fixed-shape guarantee).

## Unchanged runtime model (carried from `002`)

Immutable entity types (`{ get; init; }` + `[SetsRequiredMembers]` materializing ctor), distinct projection
types, `Ref*` read-side relationship types, `IEntityBinding` (Schema/CreateTableSql/Materialize/SelectByKey),
the thin `Session` (transaction + read identity map + executor), `CompiledCommand<T>`, and the SQL IR +
renderer. None of these are modified by this feature.
