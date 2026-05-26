# Contract: Dialect Boundary (build-time renderer + runtime identity)

The provider/dialect boundary, split into a **build-time** rendering contract (inside the generator) and
a **runtime** dialect-identity contract (in Abstractions). This is the only extension point a new SQL
provider touches (SC-003/SC-006).

## Build-time contract (Dormant.SourceGeneration)

### `ISqlDialectRenderer`

```csharp
internal interface ISqlDialectRenderer
{
    DialectId Id { get; }
    string Render(SqlStatement statement);  // IR → dialect SQL; the single string boundary, per dialect
}
```

**Obligations**:
- MUST consume only the neutral `SqlIr` (no PG-specific literals leak in or out except in the renderer).
- MUST be deterministic (ordinal comparisons, invariant culture) — generator cacheability depends on it.
- `PostgreSqlRenderer.Render` MUST be **byte-identical** to the pre-refactor `SqlRenderer` output.
- A renderer MUST map: identifier quoting, placeholder form, schema/table qualification, column types
  (via `DialectTypeMap`), `RETURNING`, JSON/param casts, and case-insensitive match (`ILIKE`/`LIKE`).

### Generator emission rule

For every authored query/command/binding, the generator renders **one SQL string per `DialectId`** and
emits a body that selects by `session.Dialect`:

```csharp
var sql = session.Dialect switch
{
    DialectId.PostgreSql => """<pg sql>""",
    DialectId.Sqlite     => """<sqlite sql>""",
    _ => throw new global::System.NotSupportedException($"Dialect {session.Dialect} is not supported by this unit."),
};
```

- The `switch` MUST be exhaustive over `DialectId`; the default arm throws a clear, dialect-named error
  (FR-009).
- The parameter binder and materializer are emitted **once** (dialect-neutral) — only the SQL string
  varies.
- The dynamic-filter path (optional parameters) selects the placeholder/quote/identifier tokens by the
  same `session.Dialect` branch before assembling the `StringBuilder` (no runtime SQL compilation).

## Runtime contract (Dormant.Abstractions.Providers)

### `DialectId`

```csharp
public enum DialectId { PostgreSql, Sqlite }
```

### `ISqlDialect`

```csharp
public interface ISqlDialect
{
    DialectId Id { get; }
    string QuoteIdentifier(string name);  // dynamic-filter path only
    string Placeholder(int index);        // dynamic-filter path only ($n vs ?)
    bool Supports(string providerScope);
}
```

### `IDbSession` (delta)

```csharp
public interface IDbSession : IAsyncDisposable
{
    DialectId Dialect { get; }            // NEW — provider-supplied
    // ... existing Begin/Commit/Rollback/QueryAsync/ExecuteAsync unchanged
}
```

### `ISession` (delta)

```csharp
public interface ISession : IAsyncDisposable
{
    DialectId Dialect { get; }            // NEW — delegates to the underlying IDbSession
    // ... existing members unchanged
}
```

### `IEntityBinding` / `IEntityBinding<T>` (delta)

```csharp
public interface IEntityBinding
{
    string Schema { get; }
    string CreateTableSql(DialectId dialect);          // was: string CreateTableSql { get; }
}

public interface IEntityBinding<TEntity> : IEntityBinding where TEntity : class
{
    TEntity Materialize(IFieldReader reader);
    PreparedStatement SelectByKey(DialectId dialect, object key);  // was: SelectByKey(object key)
}
```

## Invariants (must hold for every dialect)

1. **No runtime SQL compilation**: variant selection is a branch over compile-time-constant strings.
2. **Neutral binder**: the bind callback writes values by index once for all dialects. PostgreSQL binds
   positionally (`$n`, add order); SQLite binds named `@pN` parameters the writer names from the same index
   (order-independent). The placeholder *text* is the renderer's choice; the bind sequence is dialect-neutral.
3. **Result-type independence**: the selected dialect never changes a query/command result type.
4. **Core-change-free dialect addition**: adding a `DialectId` member + renderer + adapter requires **0**
   changes to `Session`, `SchemaInitializer` logic shape, or the public method signatures consumers call.
5. **Open to non-SQL**: the IR carries no dialect-specific literal, so a future non-SQL strategy can
   consume the same IR at build time (SC-004) — not implemented in v1.

## US2 verification (SC-003 / SC-004 / SC-006)

**SC-003 / SC-006 — adding the SQLite dialect required 0 core changes.** Once the Phase-2 framework seam
landed, introducing the SQLite *dialect* touched only:

- `src/Dormant.SourceGeneration/Ir/Dialects/SqliteRenderer.cs` — new renderer (build-time).
- `src/Dormant.SourceGeneration/Ir/Dialects/DialectRenderers.cs` — one line registering the renderer.
- `src/Dormant.Provider.Sqlite/**` — the new adapter package (data source, session, dialect identity, IO).
- `tests/**` — the conformance + SQLite tests.

**Zero** edits to `Dormant.Abstractions` runtime types, `Dormant.Core` (`Session`, `SessionFactory`,
`SchemaInitializer`), the DSL, or any consumer-facing generated method *signature*. The variant `switch`
arms for SQLite appeared automatically from the renderer registration — no per-unit emitter change.

**SC-004 — the boundary admits a non-SQL strategy.** The neutral `SqlStatement` IR is the shared seam: a
future non-SQL execution strategy consumes the same IR at build time without a SQL-text assumption blocking
it. `DialectBoundaryTests` proves neutrality empirically — one authored unit renders a PostgreSQL variant
(`"catalog"."doc"`, `$2::jsonb`) and a SQLite variant (`"catalog_doc"`, `@p2`, no cast) from the *same*
IR, so the JSON cast / placeholder form / schema qualification are renderer decisions, not IR literals.
