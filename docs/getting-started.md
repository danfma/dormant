# Getting Started

This guide shows the intended first-success path for a local checkout. Dormant is not documented here as a published NuGet package; use project references while working in this repository.

## Prerequisites

- .NET 10 SDK. `global.json` currently specifies SDK `10.0.201` with `latestFeature` roll-forward.
- Docker if you want to run the PostgreSQL provider tests.
- A PostgreSQL connection string if you want the quickstart sample to run a full database round-trip.

## Build The Repository

From the repository root:

```sh
./build.sh build
```

If the repository has not been restored yet and the build reports a missing `project.assets.json`, run:

```sh
./build.sh restore
./build.sh build
```

For the full local verification path:

```sh
./build.sh all
```

The full path runs provider tests that need Docker.

## Use The Existing Sample

The sample project lives at:

```text
samples/Dormant.Sample.Quickstart/
```

Its DormantQL files are:

```text
samples/Dormant.Sample.Quickstart/schema/app.dqls
samples/Dormant.Sample.Quickstart/schema/app.dql
```

Run the sample without a database to preview the generated surface:

```sh
dotnet run --project samples/Dormant.Sample.Quickstart
```

Set `DORMANT_SAMPLE_DB` to a PostgreSQL connection string to run schema apply, insert, load, and query operations:

```sh
DORMANT_SAMPLE_DB="Host=localhost;Database=dormant;Username=postgres;Password=postgres" dotnet run --project samples/Dormant.Sample.Quickstart
```

The exact connection string depends on your local PostgreSQL setup.

## Define A Schema

Schema modules use `.dqls` files:

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

Important first-pass rules:

- `module app;` maps the module to the database schema and generated namespace segment.
- Members use `name: TypeExpr`.
- Members are required by default.
- `?` marks an optional value.
- `author: User` is a single reference.
- `posts: Set<Post>` is a relationship collection.

More detail: [DormantQL schema guide](guides/dormantql-schema.md).

## Author A Mutation

Write units live in `.dql` files and use `mutation`:

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
```

This unit generates a C# method named `CreateUser`. Insert result inference and `returning` options are covered in [queries and mutations](guides/queries-and-mutations.md).

## Author A Query

Read units use `query`:

```dql
query users_by_email(email: string) {
  from User u
  where u.email == email
  select u
}
```

This unit generates a C# method named `UsersByEmail`. The selected shape is a full `User` entity.

Projection queries select exactly the requested fields:

```dql
query user_contacts(since: datetime) {
  from User u
  where u.created_at >= since
  order by u.email asc
  select {
    u.id
    u.email
  }
}
```

That produces a distinct projection result type instead of a partially-populated `User`.

## Call The Generated Surface

The sample demonstrates the generated entity and session extension methods:

```csharp
await using var factory = DormantPostgres.CreateSessionFactory(connectionString);

await using (var session = await factory.OpenSessionAsync())
{
    await session.CreateUser(id, "ada@example.com", DateTime.UtcNow, 1);
    await session.CommitAsync();
}

await using (var session = await factory.OpenSessionAsync())
{
    await foreach (var user in session.UsersByEmail("ada@example.com"))
    {
        Console.WriteLine(user.Email);
    }
}
```

Generated namespaces use `PascalCaseEachPart(RootNamespace + schema folders + module)`. In the sample, the generated namespace is `Dormant.Sample.Quickstart.Schema.App`.

More detail: [naming and generated code](guides/naming-and-generated-code.md).

## Validation Notes

This documentation feature validates examples against the current grammar and sample files. The full database round-trip depends on a live PostgreSQL database and is not required just to read or build the docs.

During the documentation implementation, `./build.sh restore` followed by `./build.sh build` completed successfully with 0 warnings and 0 errors.
