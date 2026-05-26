# Contract: Benchmark Operation Parity

This is the parity contract for the suite. Every library MUST implement each of the five
operations with **semantically identical** behavior against the shared `Product` table, so
the only thing the measurement reflects is the library's own overhead. A benchmark method
that does more or less work than its peers for the same operation is a defect.

## Common rules (all operations, all libraries)

- Target the one shared in-memory SQLite DB (`Data Source=bench;Mode=Memory;Cache=Shared`).
- Be `async`; return `Task`/`ValueTask` consumed by BenchmarkDotNet.
- Connection/seed/warmup happen in `GlobalSetup` (or `IterationSetup` for delete) and are
  **excluded** from the measured region (FR-006).
- Touch only the agreed columns; no extra round-trips, no logging, no validation a peer
  doesn't also do.
- The Dormant method in each group is `[Benchmark(Baseline = true)]`.

## OP-1 Read-by-key

- **Input**: a `Guid` id known to exist in the seed set.
- **Behavior**: fetch the single `Product` with that PK; return the materialized object.
- **Must**: select all five columns; no tracking (EF uses `FindAsync`/`AsNoTracking`).
- **Result equivalence**: a fully-populated `Product` (or its Dormant entity) for that id.

## OP-2 Filtered multi-row read

- **Input**: a `category` value present in the seed set.
- **Behavior**: fetch all `Product` rows where `category == input`; fully enumerate/
  materialize the result into a realized collection (no lazy/deferred leftovers).
- **Must**: select all five columns; EF uses `AsNoTracking`; Dormant fully drains the
  `IAsyncEnumerable`.
- **Result equivalence**: the same row count and same rows for a given category across all
  four libraries.

## OP-3 Insert

- **Input**: a new `Product` with a freshly generated `Guid` PK.
- **Behavior**: insert exactly one row, committed/persisted so a subsequent read would see
  it.
- **Must**: single INSERT of all five columns; no read-back beyond what the library's
  idiomatic insert naturally returns.
- **Isolation**: fresh PK per invocation; no cleanup (table grows equally for all libraries).

## OP-4 Update

- **Input**: an existing per-library scratch `id` + a new `quantity`.
- **Behavior**: set `quantity` on that one row; persist.
- **Must**: affect exactly one row by PK; EF does its idiomatic load-track-save, others do a
  direct UPDATE.
- **Isolation**: each library owns a distinct scratch id; updates are idempotent.

## OP-5 Delete

- **Input**: a per-library scratch `id` that exists (inserted in `IterationSetup`).
- **Behavior**: delete that one row; persist.
- **Must**: delete exactly one row by PK.
- **Isolation**: the target row is (re)created in `IterationSetup` (excluded from
  measurement); per-library scratch keys.

## Reporting contract (suite output)

- Every operation group reports all four libraries with **time/op** and **allocated/op**
  (`MemoryDiagnoser`). — SC-002, FR-004
- A ratio-to-baseline column makes relative standing readable at a glance. — SC-004
- The measurement policy (async, EF `AsNoTracking` on reads, isolation strategy) is stated
  in the suite's README/notes. — FR-010
