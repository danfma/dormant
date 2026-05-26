# Feature Specification: Comparative ORM Benchmarks

**Feature Branch**: `008-orm-benchmarks`

**Created**: 2026-05-26

**Status**: Draft

**Input**: User description: "Adicione benchmarks usando o Benchmark.NET, comparando o Dormant contra Dapper, EF e Insight.Database. Use o SQLite para isso."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compare Dormant against peer data-access libraries (Priority: P1)

A Dormant maintainer runs the benchmark suite to see how Dormant's read and write
operations perform relative to widely used data-access libraries (Dapper, EF, and
Insight.Database) on the same representative workload, backed by the same embedded
database. The maintainer gets a results summary showing, per operation, the time and
memory cost for each library side by side.

**Why this priority**: This is the core value of the feature. Without a fair,
side-by-side comparison the suite delivers nothing. A single run that produces the
comparison table is the MVP.

**Independent Test**: Run the suite with one command against a populated embedded
database and confirm it emits a summary covering all four libraries across the defined
operation set, with time and allocation figures per pair.

**Acceptance Scenarios**:

1. **Given** an embedded database seeded with identical schema and data for every
   library, **When** the suite runs, **Then** it reports per-operation time and memory
   allocation for Dormant, Dapper, EF, and Insight.Database.
2. **Given** a completed run, **When** the maintainer reads the summary, **Then** each
   benchmarked operation lists results for all four libraries so relative standing is
   clear.
3. **Given** the same machine and inputs, **When** the suite is re-run, **Then** the
   relative ranking between libraries per operation is stable across runs.

---

### User Story 2 - Cover representative data-access operations (Priority: P2)

The suite exercises a defined set of operations that reflect real ORM usage so the
comparison is meaningful rather than a single micro-case: fetching one row by key,
fetching many rows with a filter, inserting, updating, and deleting a row.

**Why this priority**: A comparison limited to a single operation would mislead. The
operation set is needed for the comparison to be trustworthy, but it builds on the P1
harness.

**Independent Test**: Inspect the results and confirm each operation in the defined set
is present for every library and each is exercised through that library's idiomatic
API.

**Acceptance Scenarios**:

1. **Given** the defined operation set, **When** the suite runs, **Then** every operation
   is measured for every library.
2. **Given** a library that offers an idiomatic API for an operation, **When** that
   operation runs, **Then** the library is exercised through its intended/idiomatic usage
   rather than a forced lowest-common-denominator path.

---

### User Story 3 - Extend the suite with new operations (Priority: P3)

A contributor adds a new operation or scenario to the comparison without restructuring
the harness, and the new operation is automatically measured for every library.

**Why this priority**: Keeps the suite useful over time, but the feature delivers value
before this is in place.

**Independent Test**: Add one new operation, run the suite, and confirm it appears for
all libraries in the summary with no changes to unrelated benchmarks.

**Acceptance Scenarios**:

1. **Given** a new operation added to the suite, **When** the suite runs, **Then** the
   operation is reported for all four libraries.

---

### Edge Cases

- A library lacks an idiomatic API for one of the operations — the operation is recorded
  for that library using the closest supported approach, and the deviation is noted.
- First-call/warmup skew (JIT, connection priming) distorts a measurement — measured
  regions exclude setup so per-operation cost is what is reported.
- Embedded-database state from one library's writes leaks into another's measurement —
  each library runs against isolated data so writes do not cross-contaminate.
- Async vs synchronous API differences between libraries — the suite uses a consistent
  policy (async where the library supports it) and documents it so comparisons stay fair.
- The embedded database's behavior differs from a networked database — results are framed
  as relative library overhead, not absolute production throughput.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The suite MUST measure and compare Dormant against Dapper, EF, and
  Insight.Database for each benchmarked operation.
- **FR-002**: All four libraries MUST execute against an embedded SQLite database using an
  identical schema and identical seed data within a single run, so results are
  apples-to-apples.
- **FR-003**: The suite MUST cover a defined set of representative data-access operations:
  single-row read by key, multi-row filtered read, insert, update, and delete.
- **FR-004**: For each library/operation pair the suite MUST report execution time and
  memory allocated per operation.
- **FR-005**: Each library MUST be exercised through its idiomatic API for each operation
  so the comparison reflects realistic usage.
- **FR-006**: Measured regions MUST exclude one-time setup (connection establishment,
  schema creation, data seeding, warmup) so reported figures reflect per-operation cost.
- **FR-007**: Each library's measurements MUST be isolated so write operations from one
  library do not affect another library's data or results.
- **FR-008**: The suite MUST be runnable with a single command and MUST produce a
  human-readable summary comparing all libraries across all operations.
- **FR-009**: Results MUST be reproducible on the same machine: the relative ranking
  between libraries per operation MUST be stable across repeated runs.
- **FR-010**: The suite MUST document its measurement policy (async vs sync usage,
  isolation strategy, what is and isn't measured) so readers can interpret results
  correctly.
- **FR-011**: Adding a new operation to the comparison MUST measure it for every library
  without restructuring existing benchmarks.

### Key Entities *(include if feature involves data)*

- **Benchmark Entity**: A small, representative record type used by all four libraries
  (an identity key plus a few typical scalar fields). Kept minimal so the cost measured is
  library overhead, not domain complexity.
- **Operation**: A single measured data-access action (read-by-key, filtered read, insert,
  update, delete) applied uniformly across libraries.
- **Library Adapter**: The per-library code path that performs a given operation through
  that library's idiomatic API.
- **Result Record**: The per-(library, operation) measurement — time and allocation —
  emitted into the comparison summary.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A single command runs the full comparison end to end and produces a results
  summary without manual intervention.
- **SC-002**: Every benchmarked operation in the summary includes all four libraries, each
  with both a time figure and an allocation figure.
- **SC-003**: Re-running the suite on the same machine keeps the relative per-operation
  ranking between libraries unchanged across at least three runs.
- **SC-004**: A reader can identify which library is fastest and which allocates least for
  any given operation from the summary in under one minute.
- **SC-005**: The defined operation set covers at least one read-by-key, one filtered
  multi-row read, and all three of insert, update, and delete.

## Assumptions

- The suite measures relative library overhead on a single developer/CI machine; it is not
  a statement of absolute production throughput or an SLA.
- SQLite is used as an embedded, dependency-free, deterministic backing store; results are
  not assumed to generalize to networked databases.
- Asynchronous APIs are used where a library supports them; otherwise the synchronous path
  is used, and the policy is documented.
- A small representative entity is sufficient — no full domain model is needed to compare
  per-operation library overhead.
- The existing `Dormant.Benchmarks` project is reused and extended rather than creating a
  new project; it is changed to target the embedded SQLite path.
- The benchmark project is not AOT-compiled, since the comparison libraries rely on runtime
  reflection.
- Standard warmup/iteration discipline (isolated process, warmup, repeated iterations) is
  applied uniformly to every library.
