# Dormant Comparative ORM Benchmarks

BenchmarkDotNet suite comparing **Dormant** against **Dapper**, **EF Core**, and
**Insight.Database** across five representative operations over one shared **in-memory
SQLite** database. Feature spec: `specs/008-orm-benchmarks/`.

## Run

```bash
# Full comparison (all operations, all libraries)
dotnet run -c Release --project tests/Dormant.Benchmarks

# One operation
dotnet run -c Release --project tests/Dormant.Benchmarks -- --filter '*ReadByKey*'

# One library across all operations
dotnet run -c Release --project tests/Dormant.Benchmarks -- --filter '*EfCore*'

# Fast smoke (each benchmark once — what CI runs; not a perf gate)
dotnet run -c Release --project tests/Dormant.Benchmarks -- --job dry --filter '*'
```

Each operation is one BenchmarkDotNet group with four methods — `Dormant` (baseline),
`Dapper`, `EfCore`, `Insight`. `MemoryDiagnoser` reports allocated bytes/op; the ratio and
rank columns show relative standing against the Dormant baseline.

## Operations

| Class | Operation |
|-------|-----------|
| `ReadByKeyBenchmarks` | fetch one product by primary key |
| `FilteredReadBenchmarks` | fetch all products in a category (fully materialized) |
| `InsertBenchmarks` | insert one row |
| `UpdateBenchmarks` | set one row's quantity by key |
| `DeleteBenchmarks` | delete one row by key |

## Measurement policy (read results in this light)

- **Relative, not absolute.** Results are relative library overhead on one machine, not
  production throughput, and do not generalize to networked databases.
- **One shared schema + seed.** Dormant's generator owns the DDL (`bench_product`);
  `DormantSqlite.EnsureCreatedAsync` creates it and the kept-alive session factory holds the
  shared-cache in-memory database alive. Dapper, EF Core, and Insight bind to that same
  table — there is no second hand-written schema. Seed is ~1,000 rows over ~10 categories
  with a fixed RNG (deterministic).
- **Async everywhere.** Every operation uses each library's async API.
- **Per-operation handle.** Each measured call acquires its library's per-operation handle
  (Dormant session / fresh `DbContext` / opened `SqliteConnection`), reflecting realistic
  per-request cost and avoiding Dormant identity-map cache bias on repeated reads.
- **EF tracking.** EF reads use `AsNoTracking()` (the other three never track); EF update and
  delete use the set-based `ExecuteUpdate`/`ExecuteDelete` so every library issues a single
  statement; EF insert uses the tracked `Add` + `SaveChanges` path.
- **Dormant insert returns the entity** via `RETURNING` (its idiom); the other three issue a
  bare `INSERT`.
- **Write isolation.** Setup/seed/warmup are outside the measured region. Inserts use fresh
  keys; updates use per-library scratch rows; deletes consume a per-method key pool refilled
  in `[IterationCleanup]`, so no library's writes perturb another's.
- **Provider notes.** Dapper needs Guid/decimal type handlers (SQLite stores both as TEXT —
  see `DapperSqliteSetup`). Insight is driven through its inline-SQL `*Sql` methods with
  `Dictionary` parameters (anonymous-type parameters are cached/reused incorrectly against
  Microsoft.Data.Sqlite). EF Core and Dormant handle these conversions internally.
- **Not AOT.** The harness is not AOT/trimmed — EF Core, Dapper, and Insight use runtime
  reflection. This isolates that from Dormant's AOT-clean shipped library.

## Adding a new operation

The four-library group is one class deriving from `BenchmarkBase` (which owns the shared
`Harness` lifecycle). To add an operation:

1. Create `Benchmarks/<Name>Benchmarks.cs` deriving from `BenchmarkBase`.
2. Add four `[Benchmark]` methods — mark the Dormant one `[Benchmark(Baseline = true)]`.
3. Use `Harness` for the shared database (`OpenDormantSessionAsync`, `OpenConnectionAsync`,
   `NewEfContext`, seeded keys/category). For Dormant queries/mutations, add the unit to
   `schema/bench.dql` (and any new entity member to `schema/bench.dqls`).
4. If the operation needs extra preparation, override `GlobalSetup` and call `base.GlobalSetup()`
   first (see `DeleteBenchmarks`).

BenchmarkSwitcher discovers the class automatically — it appears for all four libraries in
the next run with no other changes.
