# Feature Specification: SQLite & NMemory Provider Support

**Feature Branch**: `005-sqlite-nmemory-providers`

**Created**: 2026-05-26

**Status**: Draft

**Input**: User description: "Adicione suporte ao SQLite e ao NMemory para testar as capacidades do sistema.
Acho que o último não é AOT-friendly, mas podemos deixar claro num README e depois num pacote, quando
publicarmos pro NuGet, que esse suporte não é AOT-friendly, sendo escolha do usuário. O importante é termos o
nosso core AOT."

## Overview

Dormant is PostgreSQL-primary, but its provider boundary is designed so additional relational providers can be
added without breaking consumers (Constitution: Compatibility & Performance Standards). This feature
generalizes that boundary into a **dialect/execution-strategy abstraction** (NHibernate-style) and adds a
second real provider, primarily to **exercise the ORM across providers** — proving the abstraction holds
beyond PostgreSQL and giving a fast, Docker-free test target:

- **SQLite** *(in v1)* — a real, embedded relational engine. AOT-friendly (managed + native SQLite). Runs in
  file or in-memory mode; no Docker daemon needed. Becomes a shippable provider package.
- **NMemory** *(deferred — see Clarifications/Out of Scope)* — a pure-managed, non-SQL in-memory engine. Since
  it executes via expression trees (not SQL text) it would need a non-SQL execution strategy and is
  inherently **not AOT-friendly**; SQLite's in-memory mode already covers the AOT-friendly in-memory need, so
  NMemory is deferred to a future feature. v1 only ensures the boundary *admits* such a non-SQL strategy later.

The non-negotiable (the user's framing — "o importante é termos o nosso core AOT"): the **core, the dialect
framework, and the SQLite provider remain Native-AOT-clean**; any future non-AOT provider (NMemory) would be a
separate opt-in package whose non-AOT cost never leaks into the core or the AOT gate.

## Clarifications

### Session 2026-05-26

- Q: How is provider-specific build-time SQL produced (given the cross-provider testing goal + the
  build-time-SQL constitution rule)? → A: **Multi-variant, NHibernate-style dialects.** The generator emits a
  provider-neutral **structured SQL representation (IR)** and renders **one SQL variant per target dialect at
  build time**; at runtime the generated method **selects** the variant by the session's provider dialect (no
  runtime SQL compilation). Each provider owns a **Dialect** (render + execute). This lets the same compiled
  assembly run against multiple providers (FR-003).
- Q: How does NMemory execute, since it is not SQL-text-native, and is it in v1? → A: **NMemory is deferred to
  a future feature.** SQLite's in-memory mode already covers the AOT-friendly in-memory case; NMemory would
  need a heavy SQL→expression translator (the non-AOT cost). **v1 scope = SQLite + the dialect framework.**
  However, the **provider boundary is generalized into an execution-strategy abstraction**: a `Dialect`
  (render+execute SQL) is the strategy for SQL engines, and the boundary is shaped so a **non-SQL strategy**
  (e.g. NMemory consuming the IR to build expression trees) can plug in later **without core rework**. v1
  implements **only SQL dialects** (PostgreSQL + SQLite) (FR-004).
- Q: How is cross-provider parity tested? → A: A **parameterized cross-provider test suite** — the same
  authored units run against each provider (PostgreSQL via Docker, SQLite in-memory) from one source of truth,
  proving parity (FR-007).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run the ORM against SQLite without Docker (Priority: P1)

A developer (or CI) exercises authored DQL — schema apply, queries, mutations (insert/update/delete),
`returning`, and the `with`-block — against **SQLite** (file or in-memory), with no Docker daemon and no
PostgreSQL, getting the same behavior the PostgreSQL provider gives.

**Why this priority**: SQLite is the highest-value addition — a real second relational engine that validates
the provider boundary, runs everywhere (CI, laptops) without Docker, and is AOT-friendly so it stays inside
the core quality gate. It is the MVP of this feature.

**Independent Test**: Point a session at a SQLite database, `EnsureCreated`, run the existing authored
queries/mutations, and confirm reads/writes/concurrency behave identically to the PostgreSQL provider.

**Acceptance Scenarios**:

1. **Given** a schema + authored units, **When** they run against a SQLite database (file or `:memory:`),
   **Then** schema apply, inserts, updates, deletes, `returning`, and queries succeed with results equivalent
   to the PostgreSQL provider.
2. **Given** the SQLite provider, **When** a consuming app is published with Native AOT + full trimming,
   **Then** there are **zero** library-originated AOT/trim warnings (SQLite stays in the AOT gate).
3. **Given** a `with`-block mutation (parent → child FK flow), **When** it runs against SQLite, **Then** each
   binding executes as its own statement in the transaction and the child FK is persisted (parity with
   PostgreSQL).

### User Story 2 - A general provider execution-strategy abstraction (Priority: P2)

The provider boundary is generalized so a provider owns **how** it executes a unit, not just "run this SQL
string": for SQL engines the strategy is a **Dialect** (render + execute SQL), and the boundary is shaped so
a **non-SQL strategy** (consuming the structured representation directly — the future NMemory case) can plug
in later without changing the core. v1 implements only the SQL-dialect strategy (PostgreSQL + SQLite).

**Why this priority**: This is the design that lets new providers — including non-SQL ones like NMemory —
arrive later without core rework. Getting the boundary shape right now is cheap; reworking it later is not.

**Independent Test**: Inspect the provider boundary — the generator emits a provider-neutral structured
representation, and a SQL provider plugs in via a Dialect; confirm no SQL-text assumption is hard-wired into
the core (a non-SQL strategy could be added without touching it).

**Acceptance Scenarios**:

1. **Given** the provider boundary, **When** a new SQL provider (SQLite) is added, **Then** it is implemented
   as a Dialect over the shared structured representation, with **zero** core changes.
2. **Given** the same boundary, **When** a hypothetical non-SQL provider is considered, **Then** the contract
   admits a non-SQL execution strategy (consuming the IR) as a future extension point — no SQL-text assumption
   blocks it.

> **NMemory is deferred** to a future feature (see Out of Scope). SQLite's in-memory mode covers the
> AOT-friendly in-memory need; NMemory's non-SQL, non-AOT execution is the future non-SQL strategy that this
> abstraction makes possible.

### User Story 3 - Core AOT integrity is preserved (Priority: P1)

The introduction of the SQLite provider + the dialect framework never compromises the AOT-first core: the
core and SQLite publish AOT with zero library-originated warnings. (A future non-SQL provider like NMemory,
when added, would be a separate opt-in package documented as non-AOT — but that is out of v1 scope.)

**Why this priority**: "O importante é termos o nosso core AOT." This is the guardrail the whole feature is
constrained by — equal in priority to SQLite itself.

**Independent Test**: The AOT smoke publish (core + SQLite) passes with zero warnings.

**Acceptance Scenarios**:

1. **Given** the repository's AOT gate, **When** it runs, **Then** the core + SQLite + the dialect framework
   publish with **zero** library-originated AOT/trim warnings.
2. **Given** a consumer using the core + SQLite, **When** published AOT, **Then** zero library-originated
   warnings, and no first-call warm-up.

### Edge Cases

- Provider-specific SQL dialect differences (parameter placeholders, type names, `RETURNING` support, JSON
  storage, identifier quoting) must be handled so the **same authored DQL** produces correct SQL per provider.
- The provider boundary must not hard-wire a SQL-text assumption into the core, so a future non-SQL execution
  strategy (the deferred NMemory case) can be added without core rework.
- An authored capability not supported by a given provider (e.g. a JSON operation unavailable in SQLite) must
  surface a clear, provider-specific error rather than producing wrong results.
- In-memory lifetimes: a SQLite `:memory:` / NMemory store is per-connection/per-process; tests must get a
  clean store per case.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a **SQLite provider** implementing the same provider boundary as the
  PostgreSQL provider (connectivity, schema apply/`EnsureCreated`, command + query execution), so authored DQL
  runs against SQLite with no core changes.
- **FR-002**: The SQLite provider MUST support **file-based and in-memory** databases and MUST NOT require a
  Docker daemon or external server.
- **FR-003**: The generator MUST emit a provider-neutral **structured SQL representation** and render a
  **per-dialect SQL variant at build time** for each target dialect (NHibernate-style); at runtime the
  generated method **selects** the variant by the session's provider dialect (no runtime SQL compilation). The
  **authored DQL is unchanged** across providers. v1 dialects: **PostgreSQL + SQLite** (placeholders, type
  mapping, `RETURNING`, identifier quoting, JSON-as-TEXT, etc.).
- **FR-004**: The provider boundary MUST be an **execution-strategy abstraction**: a provider owns *how* it
  executes a unit. A **`Dialect`** (render + execute SQL) is the strategy for SQL engines; the boundary MUST
  NOT hard-wire a SQL-text assumption into the core, so a future **non-SQL strategy** (consuming the structured
  representation — the deferred NMemory case) can be added without core rework. v1 implements **only the
  SQL-dialect strategy** (PostgreSQL + SQLite).
- **FR-006**: The **core, the dialect framework, and the SQLite provider MUST remain Native-AOT +
  full-trimming clean** (zero library-originated warnings, no first-call warm-up).
- **FR-007**: Provider/connectivity and provider-specific behavior MUST be verified against the **real engine**
  for each provider — never mocks — via a **single parameterized cross-provider test suite** (the same
  authored units run against PostgreSQL (Docker) and SQLite (in-memory)), proving parity from one source.
- **FR-008**: Adding these providers MUST NOT change the public core API, the DSL, or the generated-code
  contract in a way that breaks existing PostgreSQL consumers (the provider boundary is the only extension
  point).
- **FR-009**: Where a provider cannot support an authored capability, the system MUST surface a **clear,
  located/provider-named diagnostic or runtime error**, not silent incorrect behavior.

### Key Entities *(include if feature involves data)*

- **Provider**: An adapter implementing Dormant's provider boundary (connection/session, schema apply,
  execution) for a specific engine. v1 new: **SQLite** (NMemory deferred).
- **Dialect**: A provider's SQL execution strategy — renders the shared structured SQL representation into the
  engine's SQL (placeholders, types, quoting, RETURNING, JSON) and executes it. One Dialect per SQL engine
  (PostgreSQL, SQLite); the future non-SQL strategy (NMemory) is a sibling of Dialect under the same boundary.
- **Structured SQL representation (IR)**: The provider-neutral, build-time model of each unit's SQL; the
  shared input that every dialect renders and that a future non-SQL strategy would consume.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The authored query/mutation/`with`-block suite passes against **SQLite** with results equivalent
  to PostgreSQL — **100%** of the covered behaviors match.
- **SC-002**: The core + SQLite publish with Native AOT + full trimming with **zero** library-originated
  warnings.
- **SC-003**: Adding the SQLite provider required **0** changes to the core to introduce its dialect — it
  plugs in purely through the provider/dialect boundary (verifiable by the changed-files set).
- **SC-004**: The provider/dialect boundary admits a **non-SQL execution strategy** as a future extension with
  no SQL-text assumption in the core (verifiable by inspecting the boundary contract).
- **SC-005**: Test runs against SQLite (in-memory) require **no Docker daemon** and complete a representative
  CRUD + query round-trip in a fraction of the PostgreSQL/Testcontainers time.
- **SC-006**: Adding a provider required **0** changes to the public core API / DSL / generated-code contract
  (verifiable: only the new adapter packages + the dialect layer changed).

## Assumptions

- The provider boundary established for PostgreSQL is the extension point; new providers are new adapter
  packages (e.g. `Dormant.Provider.Sqlite`, `Dormant.Provider.NMemory`) and do not modify the core.
- **SQL dialect = central technical work** (decided, Clarifications Q1): the generator emits a neutral
  structured representation and renders **per-dialect SQL at build time**; runtime selects by the session's
  dialect (no runtime SQL compilation) — satisfying the Constitution's build-time-SQL rule. Implies refactoring
  the currently PostgreSQL-specific renderer into a dialect abstraction with PostgreSQL + SQLite dialects.
- **SQLite is AOT-friendly** via the managed SQLite client + native SQLite; it stays inside the AOT gate.
- **NMemory is deferred** (decided, Clarifications Q2): it is not a SQL-text engine (expression-tree
  execution), so it needs a non-SQL execution strategy — the boundary is generalized to admit that later, but
  v1 implements only SQL dialects. SQLite's in-memory mode covers the AOT-friendly in-memory need now.
- Primary purpose is **testing the system's capabilities** across providers; SQLite may be published as a
  NuGet package later.
- C# 14 / .NET 10; PostgreSQL remains the primary reference provider.

## Out of Scope (v1 of this feature)

- **The NMemory provider entirely — deferred to a future feature** (a non-SQL execution strategy + opt-in,
  documented-non-AOT package). v1 only ensures the boundary *admits* it later.
- Production-grade feature parity for every PostgreSQL-specific capability on SQLite (e.g. advanced JSON,
  spatial); v1 targets the core authored-DQL surface (schema, CRUD, queries, `returning`, `with`-block).
- A general provider-plugin/SDK for third parties; v1 adds the first-party SQLite provider + the dialect
  framework.
- Changing the authored DQL or the core public API/generated-code contract.
