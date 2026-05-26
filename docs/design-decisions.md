# Design Decisions

This page summarizes the major decisions recorded across the SpecKit artifacts and the project constitution.

## AOT First

Dormant targets .NET 10 and Native AOT. The generator produces metadata, accessors, SQL, and materializers ahead of time so the core path does not depend on runtime code generation or runtime reflection.

Rationale: AOT applications need predictable startup, trimming compatibility, and no hidden warm-up cost.

## Build-Time SQL

DormantQL units are compiled into SQL during source generation. Runtime values are parameters; the generated method already knows the SQL shape and result shape.

Rationale: Build-time SQL keeps the query path deterministic and reviewable, and it supports the constitution's performance and compatibility principles.

## Statically-Known Results

Queries return either full entities or generated projection types. Dormant does not return partially-populated entities.

Rationale: If a field is absent from the query result, accessing it should fail at compile time rather than becoming a runtime surprise.

## Explicit Relationship Load State

Relationships use `Ref<T>` and collection variants such as `RefSet<T>` instead of pretending unloaded relationships are empty.

Rationale: The loaded/unloaded distinction is part of the type-level contract. It avoids accidental lazy loading and makes missing related data explicit.

## Immutable, Command-Driven Writes

The design moved away from implicit mutable change tracking toward authored `mutation` units. Writes are explicit, named, generated methods.

Rationale: Command-driven writes make data mutation visible in source and align writes with the same build-time model as queries.

## LINQ/SQL-Style DormantQL Grammar

Feature `003` replaces earlier command syntax with `query` and `mutation` blocks, explicit aliases, alias-qualified members, and symbolic operators.

Rationale: The grammar is more familiar to C# and SQL developers, and explicit aliases remove ambiguity from member references.

## Raw String SQL Literals

Generated SQL is emitted as C# raw string literals where static SQL is embedded in generated methods.

Rationale: Generated code is a surface developers may inspect. Raw string literals make quoted identifiers readable without changing SQL values.

## PostgreSQL First, Dialect Framework Next

PostgreSQL is the primary reference provider. The next provider direction is SQLite plus a generalized SQL dialect strategy. NMemory is deferred as future non-AOT work.

Rationale: PostgreSQL gives a strong real-provider reference. SQLite exercises provider abstraction without Docker and stays AOT-friendly. NMemory has a different execution model and should not leak non-AOT tradeoffs into the core.

## Constitution Principles

Dormant's constitution governs the design:

- Developer Experience First: public surfaces should be discoverable and time-to-first-success should be short.
- Interface & Compatibility Stability: public API, package compatibility, generated code, and DormantQL are all compatibility surfaces.
- Statically-Known, Safe-by-Default Data Access: result and relationship shapes must prevent partially-loaded-data bugs.
- First-Class Tooling: build, test, diagnostics, compatibility, migration, and documentation workflows are product work.
- Performance by Default: AOT compatibility, build-time SQL, no hot-path reflection/code generation, and measurable budgets matter.
- Quality & Testing Discipline: public behavior and compatibility surfaces need automated verification.

The documentation in this feature exists because public docs are part of those same principles, not a separate afterthought.
