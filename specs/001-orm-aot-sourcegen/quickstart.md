# Quickstart: Dormant ORM (schema → CRUD → query in under 15 minutes)

Validates SC-008. Uses **only** the DSL and documented public APIs. Illustrative syntax/names.

## 1. Install (≈1 min)

```bash
dotnet add package Dormant.Core
dotnet add package Dormant.Provider.PostgreSql
dotnet tool install -g Dormant.Tool
```

## 2. Describe the schema in the DSL (≈3 min)

`schema/app.dqls`:

```
module app;                 # → DB schema "app"

entity User {
  id: uuid primary;
  email: str;               # required
  created_at: datetime;
  bio: str?;                # optional
  posts: multi Post;        # multi link
  version: int concurrency;
}

entity Post {
  id: uuid primary;
  title: str;
  author: User;             # required single link
}
```

Build — the generator emits partial `User`/`Post` types, snapshots, and materializers. Because the file
is `schema/app.dqls` in project `Dormant.Sample.Quickstart`, the generated namespace is
`Dormant.Sample.Quickstart.Schema.App` (root namespace + folders + module, PascalCased — FR-046):

```bash
dotnet build
```

Add custom behavior in a separate partial (survives regeneration, FR-003):

```csharp
namespace Dormant.Sample.Quickstart.Schema.App;
public partial class User { public bool IsRecent() => CreatedAt > DateTime.UtcNow.AddDays(-7); }
```

## 3. Author a query in the DSL (≈2 min)

`queries/users.dql`:

```
query RecentPostTitles(authorId: uuid, limit: optional int64) =
  select Post { title, author: { email } }   # nested fetch, one round-trip
  filter .author.id = authorId
  order by .created_at desc
  limit limit ?? 20;
```

`dotnet build` generates a typed method returning a projection with exactly `{ title, author: { email } }`.
Referencing `Post.body` here would **not compile** — it isn't on the projection (FR-008).

## 4. Create + apply a migration (≈3 min)

```bash
dotnet dormant migrations add Initial --project .
dotnet dormant migrations apply --connection "Host=localhost;Database=app;Username=...;Password=..."
dotnet dormant migrations status --connection "..."   # shows Initial = Applied
```

## 5. CRUD + query round-trip (≈4 min)

```csharp
await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
await using var session = await factory.OpenSessionAsync();

// Create
var user = await session.AddAsync(new User { email = "a@b.com", created_at = DateTime.UtcNow });
await session.CommitAsync();

// Read + update only the changed column
await using (var s2 = await factory.OpenSessionAsync())
{
    var u = await s2.QuerySingleOrDefaultAsync(Queries.UserById(user.id));
    u!.email = "new@b.com";
    await s2.CommitAsync();           // UPDATE writes only "email" (snapshot diff, FR-014)
}

// Query a projection, streaming
await foreach (var row in session.QueryAsync(Queries.RecentPostTitles(user.id, limit: 10)))
    Console.WriteLine($"{row.title} by {row.author.email}");

// Unfetched link is explicit (FR-009)
if (!user.posts.TryGetLoaded(out var posts))
    posts = (await session.LoadAsync(user.posts)).TryGetLoaded(out var loaded) ? loaded : [];
```

## 6. Publish Native AOT (≈1 min) — zero library warnings (SC-001/SC-006)

```bash
dotnet publish -c Release -p:PublishAot=true
# Build reports no Dormant-originated trimming/AOT warnings; first query needs no warm-up.
```

## What you proved

- Schema → generated entities (FR-001/FR-003); query → build-time-known projection (FR-006/FR-008);
  one-round-trip nested fetch (FR-010); change-tracked commit (FR-014); explicit link load (FR-009);
  migration round-trip via CLI only (SC-010); AOT publish clean (SC-001).
