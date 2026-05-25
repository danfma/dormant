# Quickstart: Immutable, Command-Driven ORM (schema → command → query)

Validates SC-008 — schema to a write-and-read round-trip using **only** the DSL and documented APIs. Writes
are authored **commands**; results are **immutable**; there is no add-track-save.

## 1. Install (≈1 min)

```bash
dotnet add package Dormant.Core
dotnet add package Dormant.Provider.PostgreSql
```

## 2. Describe the schema (≈3 min)

`schema/app.dqls` (`<AdditionalFiles Include="schema/*.dqls" />`):

```
module app;                 # → DB schema "app"

entity User {
  id: uuid primary;
  email: str;
  created_at: datetime;
  version: int concurrency;
}

entity Post {
  id: uuid primary;
  title: str;
  author: User;             # single reference (read side)
}
```

`dotnet build` generates **immutable** `User`/`Post` types (no setters) + bindings + schema-qualified DDL.

## 3. Author commands and queries (≈3 min)

`schema/app.dql` (`<AdditionalFiles Include="schema/*.dql" />`):

```
# write commands
command CreateUser(email: str) =
  insert User { email := email, created_at := datetime::now() };

command CreatePostForNewAuthor(title: str, email: str) =
  insert Post {
    title := title,
    author := (insert User { email := email, created_at := datetime::now() })  # nested, one round-trip
  };

command RenamePost(id: uuid, title: str, version: int) =
  update Post filter .id = id and .version = version
  set { title := title, version := version + 1 };               # optimistic concurrency

# read query
query UsersByEmail(email: str) = select User filter .email = email;
```

`dotnet build` generates `ISession` extension methods: `session.CreateUser(…)`,
`session.CreatePostForNewAuthor(…)`, `session.RenamePost(…)`, `session.UsersByEmail(…)`.

## 4. Apply the schema (≈1 min)

```csharp
await DormantPostgres.EnsureCreatedAsync(connectionString);   // CREATE SCHEMA "app" + tables (idempotent)
```

## 5. Write + read round-trip (≈4 min)

```csharp
await using var factory = DormantPostgres.CreateSessionFactory(connectionString);

await using (var session = await factory.OpenSessionAsync())
{
    // Write via authored commands (no Add/Save) — results are immutable
    var user = await session.CreateUser("ada@example.com");
    await session.CreatePostForNewAuthor("Hello", "grace@example.com");   // nested insert, 1 round-trip
    await session.CommitAsync();
}

await using (var session = await factory.OpenSessionAsync())
{
    await foreach (var u in session.UsersByEmail("ada@example.com"))
        Console.WriteLine(u.Email);   // immutable result; no setter, no "save"
}

// Optimistic concurrency: a stale `version` makes RenamePost affect 0 rows → surfaced conflict
```

## 6. Publish Native AOT (≈1 min) — zero library warnings (SC-006)

```bash
dotnet publish -c Release -r <rid> -p:PublishAot=true
# No Dormant-originated trimming/AOT warnings; no first-call warm-up.
```

## What you proved

- Schema → **immutable** generated entities; **commands** are the only write path (no add-track-save, no
  snapshot diff); a **nested write runs in one round-trip**; reads return build-time-known immutable results;
  optimistic concurrency is expressed in the command; AOT publish is clean. (SC-001..008.)
