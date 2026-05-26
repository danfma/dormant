# Dormant

Dormant is an AOT-first ORM for .NET 10. You author schema and data-access units in DormantQL, and a Roslyn source generator turns those files into generated C# types, session extension methods, and build-time SQL.

The project is early, but the direction is deliberately sharp: predictable data access for Native AOT applications, with no runtime query compilation on the core path and no reflection-based hot path mapping.

## Why Dormant Exists

Most ORM trouble shows up as uncertainty: data that looks loaded but is not, queries that are assembled at runtime, and reflection-heavy behavior that fights trimming and Native AOT. Dormant pushes the shape of data access into the build.

Its main design bets are:

- Build-time SQL: DormantQL units are parsed and rendered by the generator before the application runs.
- Native AOT focus: generated metadata, accessors, and materializers avoid hot-path runtime code generation and reflection.
- Statically-known result shapes: a query returns a full entity or a distinct projection type, never a partially-populated entity.
- Explicit relationship load state: relationships use `Ref<T>`, `RefSet<T>`, `RefList<T>`, `RefBag<T>`, or `RefMap<TKey, TValue>` so unloaded data is not mistaken for empty data.
- Command-driven writes: writes are authored as named `mutation` units instead of relying on implicit mutable change tracking.
- PostgreSQL-first provider: PostgreSQL is the current reference provider; additional provider work is planned through the dialect boundary.

## Current Status

Dormant is pre-release project code. The current repository includes the core abstractions, source generator, PostgreSQL provider, spatial PostgreSQL companion package, sample project, and test projects.

Status highlights:

- Implemented: DormantQL schema files (`.dqls`), query/mutation unit files (`.dql`), generated entity/query/mutation surfaces, PostgreSQL provider, `Ref*` relationship types, raw string SQL literals in generated code.
- Planned: SQLite provider and a generalized SQL dialect framework.
- Deferred: NMemory provider support as a future, opt-in, non-AOT provider.

See [docs/status.md](docs/status.md) for the detailed capability matrix.

## A Small Taste

Schema lives in `.dqls` files:

```dql
module app;

entity User {
  id: uuid primary;
  email: str;
  created_at: datetime;
  bio: str?;
  posts: Set<Post>;
  version: int concurrency;
}

entity Post {
  id: uuid primary;
  title: str;
  author: User;
}
```

Queries and mutations live in `.dql` files:

```dql
module app;

mutation create_user(id: uuid, email: string, created_at: datetime, version: int) {
  insert User u {
    u.id = id
    u.email = email
    u.created_at = created_at
    u.version = version
  }
}

query users_by_email(email: string) {
  from User u
  where u.email == email
  select u
}
```

The generator maps `create_user` to a C# method named `CreateUser`, and `users_by_email` to `UsersByEmail`.

## Prerequisites

- .NET 10 SDK. The repository `global.json` currently pins SDK `10.0.201` with `latestFeature` roll-forward.
- Docker for the PostgreSQL provider integration tests.
- A local PostgreSQL connection string only when running the full quickstart sample round-trip.

Common local commands:

```sh
./build.sh build
./build.sh test
./build.sh all
```

`./build.sh all` runs provider tests that require Docker.

## Where To Go Next

- [Documentation index](docs/index.md)
- [Getting started](docs/getting-started.md)
- [Capability status](docs/status.md)
- [DormantQL schema guide](docs/guides/dormantql-schema.md)
- [Queries and mutations guide](docs/guides/queries-and-mutations.md)
- [Naming and generated code](docs/guides/naming-and-generated-code.md)
- [Architecture](docs/architecture.md)
- [Design decisions](docs/design-decisions.md)

The documentation is derived from the SpecKit artifacts in `specs/` and the project constitution. See [docs/speckit-sources.md](docs/speckit-sources.md) for the source map.
