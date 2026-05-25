# Contract: Ports & Adapters

Clean Architecture / Ports & Adapters boundary. **Ports** live in `Dormant.Abstractions`; `Dormant.Core`
depends only on ports; **adapters** (`Dormant.Provider.PostgreSql`, `Dormant.Spatial.PostgreSql`,
`Dormant.Tool`) implement them. The dependency rule is one-way inward: no core/abstractions type references
Npgsql/NTS. Ports are kept deliberately minimal (Constitution I — avoid over-abstraction).

## Driver port (data access)

```csharp
public interface IDataSource : IAsyncDisposable
{
    ValueTask<IDbSession> OpenAsync(CancellationToken cancellationToken = default);
}

public interface IDbSession : IAsyncDisposable   // wraps connection + transaction
{
    ValueTask BeginAsync(CancellationToken cancellationToken = default);
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);

    // Execute prebuilt, parameterized SQL. Reads are no-boxing (GetFieldValue<T> under the hood).
    IAsyncEnumerable<TRow> QueryAsync<TRow>(
        PreparedStatement statement, RowMaterializer<TRow> materialize,
        CancellationToken cancellationToken = default);

    ValueTask<int> ExecuteAsync(PreparedStatement statement, CancellationToken cancellationToken = default);
}

// statement uses positional $1..$n placeholders; parameters bound via no-boxing typed writers.
public readonly record struct PreparedStatement(string Sql, IReadOnlyList<BoundParameter> Parameters);
public delegate TRow RowMaterializer<TRow>(in FieldReader reader);  // generated, no reflection
```

## Dialect port

```csharp
public interface ISqlDialect
{
    string QuoteIdentifier(string name);
    string Placeholder(int index);                 // "$1", "$2", ...
    string ColumnType(ValueTypeRef type);           // DDL type name for a value type
    bool Supports(string providerScope);            // drives FR-042 non-portability diagnostic
}
```

## Type-binding & native ports

```csharp
public interface ITypeBindingRegistry
{
    ITypeBinding<T> Resolve<T>();                    // scalar/collection/native; no boxing
}

public interface INativeFunctionCatalog
{
    bool TryGet(string providerScope, string name, out NativeFunctionSignature signature);  // FR-039
}
```

## Migration port

```csharp
public interface IMigrationStore
{
    ValueTask<IReadOnlyList<MigrationRecord>> GetAppliedAsync(CancellationToken cancellationToken = default);
    ValueTask ApplyAsync(MigrationRecord migration, CancellationToken cancellationToken = default);
    ValueTask RevertAsync(MigrationRecord migration, CancellationToken cancellationToken = default);
}
```

**Adapter responsibilities (PostgreSQL)**: implement `IDataSource`/`IDbSession` over
`NpgsqlSlimDataSourceBuilder` + `NpgsqlParameter<T>` + `GetFieldValue<T>`; `ISqlDialect` for PostgreSQL;
register built-in scalar + `jsonb` `ITypeBinding<T>`s; `Supports("postgres")` true. **Spatial adapter**:
register `geometry`/`geography` bindings (EWKB codec) under scope `postgres`. **Command-line tool adapter**: drives
`IMigrationStore` + generation diff.
