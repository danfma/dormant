# Phase 0 Research: Comparative ORM Benchmarks

All Technical Context unknowns resolved below. Format per decision/rationale/alternatives.

## R1. Package versions (added to `Directory.Packages.props`)

**Decision**:
- `Dapper` — `2.1.66`
- `Microsoft.EntityFrameworkCore.Sqlite` — `10.0.0` (EF Core 10, aligns with .NET 10)
- `Insight.Database` — `6.3.10`
- Reuse existing central versions: `BenchmarkDotNet` 0.14.0, `Microsoft.Data.Sqlite.Core`
  10.0.8, `SQLitePCLRaw.bundle_e_sqlite3` 2.1.11.

**Rationale**: Central Package Management is enabled (`ManagePackageVersionsCentrally=true`),
so versions live in `Directory.Packages.props` and the csproj uses version-less
`<PackageReference>`. EF Core 10 ships its own `Microsoft.Data.Sqlite`; that's fine — it
shares the same native `e_sqlite3` and talks to the same shared-cache in-memory DB. Exact
patch numbers are confirmed/bumped at implementation time (`dotnet restore` resolves; CPM
makes adjustment a one-line edit).

**Alternatives considered**: `Microsoft.Data.Sqlite` (full meta-package) instead of `.Core`
— rejected, the repo deliberately uses `.Core` + explicit `bundle_e_sqlite3` for the
AOT-clean library; the benchmark reuses the same native bundle to avoid two SQLite natives
in one process.

## R2. One shared in-memory SQLite database across all four libraries

**Decision**: Use a single connection string `Data Source=bench;Mode=Memory;Cache=Shared`.
Dormant's `SqliteDataSource` (created once in the harness) opens a keep-alive connection
that holds the in-memory DB alive for the whole suite. Dormant's
`DormantSqlite.EnsureCreatedAsync(connStr)` emits the `CREATE TABLE` DDL once. Dapper,
EF Core, and Insight each open their own `SqliteConnection` to the *same* connection string
— shared-cache means they all see the one table + seed data.

**Rationale**: Confirmed pattern in the repo: Quickstart uses
`Data Source=quickstart;Mode=Memory;Cache=Shared` and the conformance harness uses a
GUID-keyed shared-cache DB; in-memory SQLite is dropped when the last connection closes, so
the keep-alive is mandatory. Letting Dormant own the DDL guarantees an identical schema for
every library (FR-002) — no second hand-written schema to drift.

**Alternatives considered**: (a) Temp file DB — rejected, adds disk I/O noise that swamps
per-op differences and needs cleanup. (b) Each library its own DB — rejected, can't
guarantee identical schema/seed and defeats the apples-to-apples requirement.

## R3. Insight.Database against `Microsoft.Data.Sqlite`

**Decision**: Drive Insight through its **inline-SQL** APIs (`connection.QueryAsync<Product>(sql, params)`,
`connection.SingleAsync<Product>(...)`, `connection.ExecuteAsync(sql, params)`) — *not*
stored-procedure auto-binding. With SQL text, Insight's generic `DbConnection` extensions
work on any ADO.NET provider without a registered `InsightDbProvider`. Keep a minimal
`InsightDbProvider` shim (`Infrastructure/InsightSqliteProvider.cs`) ready and only register
it if parameter binding/identity needs it at implementation time.

**Rationale**: Insight's provider model exists mainly for stored-proc parameter derivation,
bulk copy, and provider-specific unwrapping. Plain `Query`/`Execute` over command text uses
reflection-based materialization (Insight's core value) and standard `DbCommand`
parameters, which Microsoft.Data.Sqlite supports. This sidesteps the absence of an official
`Insight.Database.Providers.*` package for Microsoft.Data.Sqlite.

**Risk + mitigation**: This is the single highest-uncertainty integration. Mitigation: a
spike task in Phase 2 validates a one-row Insight round-trip against the shared DB *before*
the full op set is built; if the generic path rejects a parameter type, the provider shim is
registered (small, well-documented Insight extension point). **Primary technical risk.**

**Alternatives considered**: `System.Data.SQLite` (has community Insight support) — rejected,
would introduce a *second* SQLite native into the process alongside `e_sqlite3`, breaking
the "same engine for everyone" fairness and the AOT-bundle reuse.

## R4. Async + fairness policy across libraries

**Decision**: All operations measured **async**. Dormant: `ValueTask`-based generated
methods + `ISession.GetAsync<T>`. Dapper: `*Async` extensions. EF Core: `FindAsync` /
`ToListAsync` / `SaveChangesAsync`. Insight: `*Async` extensions. EF **read** benchmarks use
`AsNoTracking()`; EF write benchmarks use the normal tracked `SaveChanges` path.

**Rationale**: Async-everywhere matches Dormant's ValueTask-first design and the spec's
documented policy (FR-010). `AsNoTracking` on reads makes EF comparable to the other three,
which never build a change tracker — without it EF pays for tracking the other libraries
don't offer, which would misrepresent "read" cost. Tracked writes are EF's idiomatic path,
so writes keep it (FR-005: idiomatic per library). The asymmetry is documented in the
summary notes.

**Alternatives considered**: Sync everywhere — rejected, hides async overhead that real
callers pay and isn't Dormant's idiom. Tracked EF reads — rejected as unfair (above).

## R5. Per-operation isolation so writes don't contaminate (FR-007)

**Decision**:
- **Read-by-key** & **filtered-read**: run against the pre-seeded, read-only dataset. No
  mutation, fully shareable, no per-iteration cost.
- **Insert**: each invocation inserts a row with a fresh `Guid` PK. No cleanup; the table
  grows identically for all four libraries, so the comparison stays fair. `MemoryDiagnoser`
  measures alloc/op; growth doesn't bias relative results.
- **Update**: each invocation updates a dedicated per-library "scratch" row (one stable PK
  per library) — repeatedly setting the same column is idempotent and leaves no state that
  skews timing.
- **Delete**: use BenchmarkDotNet `[IterationSetup]` to insert the target scratch row
  (excluded from the measured region) and `[Benchmark]` deletes it. Per-library scratch keys
  keep libraries independent.

**Rationale**: Keeps measured regions to the operation itself (FR-006) while guaranteeing
each library's writes never touch another's rows (per-library key namespaces). `IterationSetup`
overhead is acceptable for delete (a heavier op) and is excluded from the measurement.

**Alternatives considered**: Reset the whole DB per iteration via `[IterationSetup]` for all
ops — rejected, dominates fast ops (read/insert) and wrecks signal. Transactions rolled back
per op — rejected, changes the operation being measured (commit cost differs per library).

## R6. BenchmarkDotNet configuration & runner

**Decision**: `Program.cs` → `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)`
so `dotnet run -c Release --project tests/Dormant.Benchmarks` runs everything and `--filter`
selects subsets. A `BenchmarkConfig` adds `MemoryDiagnoser.Default` (alloc/op — FR-004),
ranks via the `[Benchmark(Baseline = true)]` Dormant method per group (ratio column →
SC-004 relative standing), and exposes a `Dry` job toggled by env var / `--job dry` for the
CI smoke. One `[MemoryDiagnoser]` benchmark class per operation; four methods inside
(`Dormant` baseline / `Dapper` / `EfCore` / `Insight`), grouped so the summary table shows
the four side by side per operation (SC-002).

**Rationale**: `BenchmarkSwitcher` is the standard single-entry runner and gives free
filtering. Baseline+ratio is BenchmarkDotNet's built-in mechanism for the "who's fastest"
read (SC-004). `Dry` job runs each benchmark once — verifies the suite executes in CI in
seconds without being a perf gate (Constitution IV/VI: benchmark runs in CI), keeping the
pipeline fast.

**Alternatives considered**: `BenchmarkRunner.Run<T>()` per class — rejected, no built-in
CLI filtering/one entry point. A custom short job for CI instead of `Dry` — deferred; `Dry`
is the simplest "does it run" smoke.

## R7. Performance-budget regression gate (Principle V)

**Decision**: **Out of scope** for this feature. This feature establishes the measurement
suite + CI smoke. A regression gate that asserts per-release budgets (and fails merge on
overruns) is a documented follow-up, because budgets need a baseline run on a stable
reference machine first — which this suite produces.

**Rationale**: Principle V wants budgets *declared and enforced*; you can't declare credible
budgets before the suite that measures them exists. Sequencing the gate after first results
is the compliant, non-premature path. Recorded here so it isn't silently dropped.

## Resolved unknowns summary

| Unknown | Resolution |
|---------|-----------|
| Dapper / EF / Insight versions | R1 — pinned in CPM |
| Share one SQLite DB across libs | R2 — shared-cache in-memory + Dormant keep-alive |
| Insight + Microsoft.Data.Sqlite | R3 — inline-SQL APIs; provider shim as fallback (primary risk) |
| Async vs sync; EF tracking | R4 — async all; EF reads `AsNoTracking`, writes tracked |
| Write isolation per op | R5 — per-library scratch keys; delete uses IterationSetup |
| Runner + alloc reporting + CI | R6 — BenchmarkSwitcher + MemoryDiagnoser + Dry job |
| Perf-budget gate | R7 — out of scope, follow-up |
