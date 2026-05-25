# Contract: Public API (kernel surface)

**Package**: `Dormant.Abstractions` — the stable surface users and generated code bind to. Tracked by
`PublicAPI.Shipped/Unshipped.txt`. Changes here are compatibility-surface changes (Constitution II).

Async conventions (research §7): **`ValueTask`/`ValueTask<T>` by default** with await-once discipline;
**`IAsyncEnumerable<T>`** for streaming; trailing `CancellationToken cancellationToken = default`;
`ConfigureAwait(false)` internally. Signatures are illustrative of shape, not final names.

## Session / Unit of Work

```csharp
public interface ISession : IAsyncDisposable
{
    ValueTask<TEntity> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity) where TEntity : class;

    // Query execution: generated query methods are extension/partial methods over ISession,
    // each returning its statically-known result type (entity or projection).
    IAsyncEnumerable<TResult> QueryAsync<TResult>(
        CompiledQuery<TResult> query, CancellationToken cancellationToken = default);

    ValueTask<TResult?> QuerySingleOrDefaultAsync<TResult>(
        CompiledQuery<TResult> query, CancellationToken cancellationToken = default);

    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);

    // On-demand link load (FR-009): transitions an Unloaded link to Loaded.
    ValueTask<LinkSet<TTarget>> LoadAsync<TTarget>(
        LinkSet<TTarget> link, CancellationToken cancellationToken = default) where TTarget : class;
    ValueTask<Link<TTarget>> LoadAsync<TTarget>(
        Link<TTarget> link, CancellationToken cancellationToken = default) where TTarget : class;
}

public interface ISessionFactory : IAsyncDisposable
{
    ValueTask<ISession> OpenSessionAsync(CancellationToken cancellationToken = default);
}
```

## Link load-state (FR-009)

```csharp
public readonly struct Link<T> where T : class
{
    public bool IsLoaded { get; }
    public bool TryGetLoaded(out T? value);     // false when Unloaded
    public T Value { get; }                       // throws if Unloaded — discouraged; prefer TryGetLoaded
    public static Link<T> Loaded(T? value);
    public static Link<T> Unloaded { get; }
}

public readonly struct LinkSet<T> where T : class
{
    public bool IsLoaded { get; }
    public bool TryGetLoaded(out IReadOnlyList<T> items);
    public static LinkSet<T> Loaded(IReadOnlyList<T> items);
    public static LinkSet<T> Unloaded { get; }
}
```

## Compiled query handle (build-time-produced)

```csharp
// Produced by the generator per DSL query; carries the prebuilt SQL + result-type binding.
public sealed class CompiledQuery<TResult> { /* opaque; constructed by generated code */ }
```

## Concurrency

```csharp
public sealed class ConcurrencyConflictException : Exception { /* entity key, expected/actual token */ }
```

## Extensibility (FR-025/FR-026)

```csharp
public interface ITypeBinding<T>   // no boxing; read/write a column value
{
    T Read(in FieldReader reader, int ordinal);
    void Write(in ParameterWriter writer, T value);
}

public interface INamingConvention { string ToColumnName(string memberName); string ToTableName(string entityName); }
```
