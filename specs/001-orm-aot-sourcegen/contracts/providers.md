# Contract: Provider & engine abstractions

The abstraction interfaces consumed by `Dormant.Core` and implemented by adapters
(`Dormant.Provider.PostgreSql`, `Dormant.Spatial.PostgreSql`, `Dormant.Tool`). **Dependencies point
one direction inward**: adapters depend on `Dormant.Abstractions`; nothing in `Dormant.Abstractions`
or `Dormant.Core` references Npgsql/NetTopologySuite. There is no `Ports` bucket — interfaces are
grouped by capability with semantic names. Interfaces are kept minimal (Constitution I).

## Data access — namespace `Dormant.Abstractions.Providers`

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

    IAsyncEnumerable<TRow> QueryAsync<TRow>(
        PreparedStatement statement, RowMaterializer<TRow> materialize,
        CancellationToken cancellationToken = default);

    ValueTask<int> ExecuteAsync(PreparedStatement statement, CancellationToken cancellationToken = default);
}

public interface ISqlDialect
{
    string QuoteIdentifier(string name);
    string Placeholder(int index);                 // "$1", "$2", ...
    bool Supports(string providerScope);            // drives FR-042 non-portability diagnostic
}
```

`PreparedStatement` uses positional `$1..$n` placeholders; `RowMaterializer<TRow>` and the no-boxing
`IFieldReader`/`IParameterWriter` it binds against live in `Dormant.Abstractions.Querying`.

## Value mapping — namespace `Dormant.Abstractions.Mapping`

```csharp
public interface ITypeBinding<T>                    // read/write a column value, no boxing (FR-019/FR-025)
{
    T Read(IFieldReader reader, int ordinal);
    void Write(IParameterWriter writer, int index, T value);
}

public interface ITypeBindingRegistry { ITypeBinding<T> Resolve<T>(); }
```

## Native functions — namespace `Dormant.Abstractions.Native`

```csharp
public sealed record NativeFunctionSignature(
    string ProviderScope, string Name, IReadOnlyList<string> ParameterTypes, string ReturnType);

public interface INativeFunctionCatalog            // FR-039
{
    bool TryGet(string providerScope, string name, out NativeFunctionSignature? signature);
}
```

## Migrations — namespace `Dormant.Abstractions.Migrations`

```csharp
public sealed record MigrationRecord(string Id, string Name);

public interface IMigrationStore                    // FR-020/FR-021
{
    ValueTask<IReadOnlyList<MigrationRecord>> GetAppliedAsync(CancellationToken cancellationToken = default);
    ValueTask ApplyAsync(MigrationRecord migration, CancellationToken cancellationToken = default);
    ValueTask RevertAsync(MigrationRecord migration, CancellationToken cancellationToken = default);
}
```

**Adapter responsibilities (PostgreSQL)**: implement `IDataSource`/`IDbSession` over
`NpgsqlSlimDataSourceBuilder` + `NpgsqlParameter<T>` + `GetFieldValue<T>`; `ISqlDialect` for PostgreSQL;
`jsonb` binding; `Supports("postgres")` true. **Spatial adapter**: `geometry`/`geography` bindings
(EWKB codec) under scope `postgres`. **Command-line tool**: drives `IMigrationStore` + generation diff.
