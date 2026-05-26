# Data Model: Dialect Framework + SQLite Provider

This feature's "entities" are the build-time and runtime model elements of the dialect abstraction, not
database tables. Fields, relationships, and the rules that constrain them follow.

## Runtime model elements (Dormant.Abstractions / Dormant.Core)

### DialectId (enum) — NEW

The closed set of SQL dialects the generator renders and the runtime selects among.

| Member | Meaning |
|--------|---------|
| `PostgreSql` | PostgreSQL syntax (reference). |
| `Sqlite` | SQLite syntax. |

- **Rules**: closed for v1; adding a member requires a matching renderer (build-time) + adapter
  (runtime). Selection in generated code is exhaustive (`_ =>` throws a provider-named error, FR-009).
- **Lives in**: `Dormant.Abstractions.Providers`.

### ISqlDialect (reshaped)

Runtime dialect identity + the helpers the **runtime dynamic-filter path** needs (full statements are
rendered at build time, so the runtime no longer renders them).

| Member | Type | Purpose |
|--------|------|---------|
| `Id` | `DialectId` | The runtime variant key. |
| `QuoteIdentifier(string)` | `string` | Identifier quoting for the dynamic-filter `StringBuilder` path. |
| `Placeholder(int)` | `string` | Placeholder text for the dynamic-filter path (`$n` vs `?`). |
| `Supports(string providerScope)` | `bool` | Native provider-scope capability check (existing). |

- **Implementations**: `PostgreSqlDialect` (existing, gains `Id`), `SqliteDialect` (new).

### IDbSession (extended)

Driver port; gains the dialect so generated code and `Session` can select variants.

| Added member | Type | Purpose |
|--------------|------|---------|
| `Dialect` | `DialectId` | Which dialect this session executes (provider-supplied). |

Existing members (`BeginAsync`/`CommitAsync`/`RollbackAsync`/`QueryAsync`/`ExecuteAsync`) unchanged.

### ISession (unit of work, extended)

Surfaces `DialectId Dialect { get; }` (delegates to the underlying `IDbSession`) so generated extension
methods select their SQL variant. `Session.GetAsync` passes `Dialect` to `binding.SelectByKey`.

### IEntityBinding / IEntityBinding<T> (reshaped)

| Member | Before | After |
|--------|--------|-------|
| `CreateTableSql` | `string` (property) | `string CreateTableSql(DialectId)` |
| `SelectByKey` | `SelectByKey(object key)` | `SelectByKey(DialectId dialect, object key)` |
| `Schema`, `Materialize` | — | unchanged |

- **Rule**: the generated binding holds the per-dialect variants and returns the requested one via a
  `switch` over `const` strings.

### PreparedStatement (unchanged)

Still `{ string Sql; Action<IParameterWriter>? BindParameters; }`. Variant selection happens **before**
construction (in generated code, D3), so this type needs no variant container. The binder is
dialect-neutral (positional add-order).

## Build-time model elements (Dormant.SourceGeneration)

### SqlIr nodes (kept neutral; one change)

`SqlStatement` and its records (`InsertStatement`, `SelectStatement`, `UpdateStatement`,
`DeleteStatement`, `CreateSchemaStatement`, `CreateTableStatement`, `SqlCondition`, `SqlOrder`,
`SqlLimit`, `TableRef`, `ColumnDef`) stay provider-neutral.

| Node | Change |
|------|--------|
| `InsertColumn` | `ParamCast` (PG-literal, e.g. `"jsonb"`) → a **neutral type tag** (DSL type / `needs-json` flag); the renderer maps it (D8). |
| `ColumnDef.SqlType` | No longer pre-resolved to a PG type at IR-build; the **renderer** maps the DSL type per dialect via `DialectTypeMap` (D6). (Or `SqlType` holds the DSL type and the renderer maps — implementation detail for tasks.) |

### ISqlDialectRenderer — NEW (replaces static SqlRenderer)

| Member | Type | Purpose |
|--------|------|---------|
| `Id` | `DialectId` | Tags the variant this renderer produces. |
| `Render(SqlStatement)` | `string` | IR → dialect SQL text (the single string boundary, per dialect). |

- **Implementations**:
  - `PostgreSqlRenderer` — current `SqlRenderer` behavior verbatim (`"`-quoting, `$n`, `::cast`,
    `schema.table`, `RETURNING`). **Output byte-identical to today** (snapshot-verified).
  - `SqliteRenderer` — `"`-quoted single identifier `"schema_table"` (D5), `?` placeholders, no `::`
    cast, `TEXT/INTEGER/REAL/BLOB` types (D6), `LIKE` for case-insensitive (D9), `RETURNING` kept (D7),
    `CREATE SCHEMA` → empty/skip (D5).

### DialectTypeMap — NEW

Per-dialect `DSL value type → SQL column type`. PostgreSQL map = today's `TypeMap.SqlMap`. SQLite map =
affinity table (D6). `TypeMap.Map` (DSL → CLR) stays shared/neutral.

## Generated-code shape (the contract delta)

| Surface | Before | After | Compatibility |
|---------|--------|-------|---------------|
| Query/command method **signature** | `session.CreateWidget(...)` | identical | unchanged (FR-008) |
| Query/command method **body** | one `PreparedStatement("""SQL""", binder)` | `var sql = session.Dialect switch { ... }; new PreparedStatement(sql, binder)` | additive; PG branch byte-identical |
| Entity binding | `CreateTableSql` prop, `SelectByKey(key)` | `CreateTableSql(DialectId)`, `SelectByKey(DialectId, key)` | within-MAJOR additive shape; baseline updated |

## Validation rules

- Generated variant `switch` must be **exhaustive** over `DialectId`; the default arm throws a clear,
  provider-named error (FR-009).
- Renderers are **deterministic** (ordinal/invariant) so generator cacheability holds.
- The PostgreSQL renderer's output must remain byte-identical (regression guard: existing PG snapshots).
- The IR must contain **no dialect-specific literals** after the `ParamCast`/`SqlType` generalization
  (keeps the boundary open to a future non-SQL strategy, SC-004).
