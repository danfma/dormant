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
    ValueTask<RefSet<TTarget>> LoadAsync<TTarget>(
        RefSet<TTarget> link, CancellationToken cancellationToken = default) where TTarget : class;
    ValueTask<Ref<TTarget>> LoadAsync<TTarget>(
        Ref<TTarget> link, CancellationToken cancellationToken = default) where TTarget : class;
}

public interface ISessionFactory : IAsyncDisposable
{
    ValueTask<ISession> OpenSessionAsync(CancellationToken cancellationToken = default);
}
```

## Reference load-state (FR-009)

```csharp
// Optionality is the nullability of T (orthogonal to load-state): Ref<User> = required (loaded value
// non-null); Ref<User?> = optional (loaded value may be null = no related row). Hence `class?`.
public readonly struct Ref<T> where T : class?
{
    public bool IsLoaded { get; }
    public bool TryGetLoaded(out T? value);     // false when Unloaded
    public T Value { get; }                       // T flows nullability; throws if Unloaded — prefer TryGetLoaded
    public static Ref<T> Loaded(T value);
    public static Ref<T> Unloaded { get; }
}

public readonly struct RefSet<T> where T : class      // unordered, unique (FR-049)
{
    public bool IsLoaded { get; }
    public bool TryGetLoaded(out IReadOnlyList<T> items);
    public static RefSet<T> Loaded(IReadOnlyList<T> items);
    public static RefSet<T> Unloaded { get; }
}

// Same loaded/unloaded shape, NHibernate collection semantics (FR-049):
//   RefList<T>  — ordered           RefBag<T> — unordered, allows duplicates
//   RefMap<TKey,TValue>             — keyed
// Generated relationship members default to the Unloaded sentinel (e.g. = RefSet<T>.Unloaded), never = [].
```

## Projections into user-owned records (FR-050)

A query may materialize into a **user-defined `record`/DTO with no Dormant types** — the dependency-free
boundary so domain/application code never references `Dormant.Abstractions`:

```csharp
public sealed record PostSummary(string Title, string AuthorEmail);   // user-owned, Dormant-free
// query maps columns → constructor parameters; entities (with Ref<T>) remain the persistence model.
```

## Entity equality (FR-051)

Generated entities implement identity (primary-key) equality by default; a transient (unset key) falls
back to reference equality. Opt out with `[NoIdentityEquality]`.

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
