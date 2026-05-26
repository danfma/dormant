# Quickstart: Comparative ORM Benchmarks

## Run the full comparison

```bash
dotnet run -c Release --project tests/Dormant.Benchmarks
```

Runs every operation group (read-by-key, filtered read, insert, update, delete) for all four
libraries and prints the BenchmarkDotNet summary with time/op, allocated/op, and ratio to the
Dormant baseline.

## Run a subset

```bash
# one operation
dotnet run -c Release --project tests/Dormant.Benchmarks -- --filter '*ReadByKey*'

# one library across all operations
dotnet run -c Release --project tests/Dormant.Benchmarks -- --filter '*EfCore*'

# list available benchmarks
dotnet run -c Release --project tests/Dormant.Benchmarks -- --list flat
```

## CI smoke (fast — verifies the suite executes, not a perf gate)

```bash
dotnet run -c Release --project tests/Dormant.Benchmarks -- --job dry --filter '*'
```

The `Dry` job runs each benchmark once. Wired into CI so the suite is exercised on every
change (Constitution IV/VI) without the multi-minute full run.

## Reading the results

- One table block per operation; rows are the four libraries.
- **Mean** = time per operation. **Allocated** = managed bytes per operation.
- **Ratio** compares each library to the Dormant baseline (1.00). <1.00 = faster than
  Dormant; >1.00 = slower.
- Lowest **Mean** = fastest for that operation; lowest **Allocated** = leanest. (SC-004)

## Measurement policy (so results are interpreted fairly)

- All operations are async.
- All four libraries hit one shared in-memory SQLite DB with identical schema + seed
  (Dormant owns the DDL).
- EF Core reads use `AsNoTracking()` (the other three don't track); EF writes use the normal
  tracked `SaveChanges` path.
- Setup/seeding/warmup are excluded from measured regions; write benchmarks use per-library
  scratch keys so writes never cross-contaminate.
- Results are **relative library overhead** on one machine — not absolute production
  throughput, and not generalizable to networked databases.

## Validate (maps to spec success criteria)

1. Full run completes and prints a summary unattended → **SC-001**.
2. Every operation block lists all four libraries with a time and an allocation figure →
   **SC-002**, **FR-004**.
3. Re-run ≥3× on the same machine; per-operation ranking is stable → **SC-003**, **FR-009**.
4. Fastest / leanest per operation is identifiable in under a minute from the ratio/columns →
   **SC-004**.
5. Operation set covers read-by-key + filtered read + insert + update + delete → **SC-005**,
   **FR-003**.
