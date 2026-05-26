# Contract: SQLite Provider Adapter (`Dormant.Provider.Sqlite`)

A new sibling of `Dormant.Provider.PostgreSql`, depending inward only on `Dormant.Abstractions` +
`Dormant.Core`. Mirrors the PostgreSQL adapter shape so DX is identical (Constitution I).

## Package

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Data.Sqlite.Core" />
  <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" />
</ItemGroup>
```

- AOT discipline (research D11): call `SQLitePCL.Batteries_V2.Init()` once at provider init; do not rely
  on reflective auto-registration. Minimum engine: SQLite 3.35+ (for `RETURNING`); `bundle_e_sqlite3`
  ships ≥ 3.44.

## Entry point — `DormantSqlite` (mirrors `DormantPostgres`)

```csharp
public static class DormantSqlite
{
    public static ISessionFactory CreateSessionFactory(string connectionString);
    public static IDataSource CreateDataSource(string connectionString);
    public static ISqlDialect Dialect { get; }                       // SqliteDialect.Instance
    public static ValueTask EnsureCreatedAsync(string connectionString, CancellationToken ct = default);
}
```

- Connection strings: file (`Data Source=app.db`) and in-memory (`Data Source=:memory:` or
  `Data Source=...;Mode=Memory;Cache=Shared`).
- `EnsureCreatedAsync` applies the generated schema via `SchemaInitializer` (which, for
  `DialectId.Sqlite`, skips `CREATE SCHEMA` and uses the prefixed table DDL — research D5).

## Driver ports

### `SqliteDataSource : IDataSource`

```csharp
ValueTask<IDbSession> OpenAsync(CancellationToken ct = default);
```

- Owns connection creation; for `:memory:` it MUST keep the store alive for the session's lifetime
  (an in-memory DB vanishes when its last connection closes). Each opened session gets a clean store
  per test case (spec Edge Case).

### `SqliteSession : IDbSession`

```csharp
DialectId Dialect => DialectId.Sqlite;
ValueTask BeginAsync(...); ValueTask CommitAsync(...); ValueTask RollbackAsync(...);
IAsyncEnumerable<TRow> QueryAsync<TRow>(PreparedStatement, RowMaterializer<TRow>, CancellationToken);
ValueTask<int> ExecuteAsync(PreparedStatement, CancellationToken);
```

- Wraps `SqliteConnection` + a `SqliteTransaction`.
- `QueryAsync`/`ExecuteAsync` bind parameters via `SqliteParameterWriter` (positional add-order) and
  execute the `PreparedStatement.Sql` already selected for `Sqlite` by the generated code.

### `SqliteDialect : ISqlDialect`

```csharp
DialectId Id => DialectId.Sqlite;
string QuoteIdentifier(string name);   // "name" with "" escaping (dynamic-filter path)
string Placeholder(int index);         // "?"  (dynamic-filter path)
bool Supports(string providerScope);   // "sqlite"
```

## IO (mirror PostgreSql/Io)

### `SqliteFieldReader : IFieldReader`

```csharp
bool IsNull(int ordinal);
T GetValue<T>(int ordinal);            // over SqliteDataReader.GetFieldValue<T> (+ TEXT→Guid/DateTime affinity reads)
```

### `SqliteParameterWriter : IParameterWriter`

```csharp
void Write<T>(int index, T value);     // adds SqliteParameter in order; values typed via affinity (Guid/DateTime → TEXT, byte[] → BLOB)
```

## Obligations

1. **AOT-clean**: zero library-originated AOT/trim warnings; verified by the extended AOT smoke publish
   (FR-006, SC-002). This is the gate; failing it blocks the feature (research D11).
2. **Parity**: schema apply, CRUD, `returning`, optional-filter queries, and the `with`-block behave
   equivalently to PostgreSQL for the core surface (SC-001), proved by the conformance suite (FR-007).
3. **No Docker**: file + in-memory only (FR-002, SC-005).
4. **Affinity correctness**: TEXT-stored UUID/DateTime/JSON and BLOB-stored bytes round-trip without
   boxing on the hot path (Constitution V).
5. **Clear errors**: an authored capability SQLite cannot serve surfaces a clear, provider-named error,
   never silent wrong results (FR-009).
