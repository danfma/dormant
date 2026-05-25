# Contract: Public API (reduced session)

The public API is a compatibility surface (Constitution II), tracked by PublicApiAnalyzers baselines. This
fork **reduces** the `001` session API (removes the mutable/change-tracking members).

## Session surface (`Dormant.Abstractions.Sessions`)

```csharp
public interface ISessionFactory : IAsyncDisposable
{
    ValueTask<ISession> OpenSessionAsync(CancellationToken cancellationToken = default);
}

public interface ISession : IAsyncDisposable
{
    // Transaction boundary
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);

    // Read by primary key (returns an immutable instance; read identity map within the session)
    ValueTask<TEntity?> GetAsync<TEntity>(object key, CancellationToken cancellationToken = default)
        where TEntity : class;

    // Generated command/query methods are C# 14 extension members on ISession (one per authored
    // command/query), added by the source generator — not declared here.
}
```

- **Removed from `001`**: `AddAsync`, `Remove`, and the snapshot/change-tracking machinery. There is **no**
  mutate-and-save path.
- **Generated extension methods** (in `{Module}Commands` / `{Module}Queries`) are the write/read surface;
  they appear as `session.CreateUser(…)`, `session.UsersByEmail(…)`, etc.
- On-demand relationship load (if offered) returns a **new** loaded value (immutable), never mutating an
  existing entity.

## Execution + provider (carried from `001`)

```csharp
public static class DormantPostgres
{
    public static ISessionFactory CreateSessionFactory(string connectionString);
    public static IDataSource CreateDataSource(string connectionString);
    public static ValueTask EnsureCreatedAsync(string connectionString, CancellationToken ct = default); // schema-qualified DDL apply
    public static ISqlDialect Dialect { get; }
}

public sealed class CompiledQuery<TResult> { /* prebuilt statement + no-boxing materializer (reused) */ }
public sealed class CompiledCommand<TResult> { /* prebuilt (CTE) statement + binder + materializer (reused) */ }
public sealed class ConcurrencyConflictException : Exception { }
```

- `CompiledQuery<TResult>` carried from `001`; **`CompiledCommand<TResult>`** is the symmetric write handle
  (prebuilt CTE SQL + no-boxing binder + result materializer), reused per generated method (SC-007).
- `ConcurrencyConflictException` surfaced by `update`/`delete` commands on a stale-token (zero-rows) result.

## Stability rules

- Additive within a MAJOR; the reduction from `001` is a **pre-1.0 fork** reset, not a released-surface break.
- Async surface is **ValueTask-first** (await-once discipline); multi-row reads stream via
  `IAsyncEnumerable<T>`.
- Public API changes update the PublicApiAnalyzers baseline in the same change.
