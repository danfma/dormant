# Architecture

Dormant is organized as a multi-package .NET library with a Roslyn source generator and provider adapters. The architecture favors small capability boundaries and one-directional dependencies toward stable abstractions.

## Component Map

```text
src/
├── Dormant.Abstractions/        stable kernel contracts and low-level types
├── Dormant.Core/                runtime engine, sessions, diagnostics, schema apply
├── Dormant.SourceGeneration/    DormantQL parser, model validation, code/SQL emit
├── Dormant.Provider.PostgreSql/ PostgreSQL provider adapter
├── Dormant.Spatial.PostgreSql/  PostGIS/spatial companion package
└── Dormant.Tool/                command-line tool surface

samples/
└── Dormant.Sample.Quickstart/   generated-surface sample

tests/
├── Dormant.Core.Tests/
├── Dormant.SourceGeneration.Tests/
├── Dormant.Provider.PostgreSql.Tests/
├── Dormant.Spatial.PostgreSql.Tests/
├── Dormant.Aot.SmokeTests/
└── Dormant.Benchmarks/
```

## Dependency Direction

The intended dependency direction is inward:

```text
providers/tools/samples -> core/source generation -> abstractions
```

`Dormant.Abstractions` contains stable contracts and dependency-light types. Providers adapt concrete engines to those contracts. Generated code binds consumer projects to the public/generated-code contract, so generated shapes are treated as compatibility surface.

## Abstraction Groups

The stable kernel is grouped by capability rather than generic "ports" terminology:

- `Entities`: `Ref<T>`, `RefSet<T>`, `RefList<T>`, `RefBag<T>`, `RefMap<TKey, TValue>`, entity binding support.
- `Sessions`: session and session factory abstractions.
- `Querying`: prepared statements, compiled queries/commands, row materializers, field readers, parameter writers.
- `Providers`: data source, database session, SQL dialect/provider contracts.
- `Mapping`: type binding registries.
- `Migrations`: migration storage contracts.
- `Native`: provider-native functions and signatures.

## Generation Pipeline

DormantQL files are added to the project as source-generator inputs:

```text
.dqls schema files
  -> schema parser/model
  -> validation
  -> generated entities and entity bindings

.dql query/mutation files
  -> unit parser/model
  -> SQL representation/rendering
  -> generated session extension methods
  -> generated materializers and parameter binders
```

SQL is produced at build time for the core query path. Runtime values are bound as parameters; the query/result shape is not built dynamically at runtime.

## Provider Boundary

PostgreSQL is the current reference provider. Feature `005` specifies the direction for additional providers:

- SQL engines should plug in through a dialect/render/execute strategy.
- SQLite is planned as the next relational provider and Docker-free test target.
- A future non-SQL provider such as NMemory would need a separate non-SQL execution strategy and is deferred.

The goal is that authored DormantQL remains stable while provider-specific SQL details stay behind provider/dialect boundaries.

## Safety Model

Dormant avoids partially-loaded entities:

- A query returns a full entity or a distinct projection type.
- Projection types contain exactly the selected fields.
- Relationship references carry explicit loaded/unloaded state.
- There is no implicit lazy loading hidden behind property access.

Those choices follow the constitution's safe-by-default data access principle.
