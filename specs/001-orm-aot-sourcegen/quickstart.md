# Quickstart: Dormant ORM (schema → CRUD → query)

Validates SC-008 — schema to a CRUD-and-query round-trip using **only** the DSL and documented public
APIs. Sections marked **🔜 planned** describe v1 features not yet in the current slice; everything else
is implemented and exercised by `samples/Dormant.Sample.Quickstart` + the integration tests.

## 1. Install (≈1 min)

```bash
dotnet add package Dormant.Core
dotnet add package Dormant.Provider.PostgreSql
# 🔜 planned: dotnet tool install -g Dormant.Tool   (migrations CLI)
```

## 2. Describe the schema in the DSL (≈3 min)

`schema/app.dqls` (add `<AdditionalFiles Include="schema/*.dqls" />` to the project):

```
module app;                 # → DB schema "app" (snake_case names by default)

entity User {
  id: uuid primary;
  email: str;               # required
  created_at: datetime;
  bio: str?;                # optional
  posts: Set<Post>;         # collection reference → RefSet<Post>
  version: int concurrency;
}

entity Post {
  id: uuid primary;
  title: str;
  author: User;             # required single reference
}
```

`dotnet build` — the generator emits partial `User`/`Post` types (PascalCase members: `Id`, `Email`,
`CreatedAt`…), entity bindings, snapshots, and schema-qualified DDL. The generated namespace is
`Dormant.Sample.Quickstart.Schema.App` (root namespace + folders + module, PascalCased — FR-046).

Add custom behavior in a separate partial (survives regeneration, FR-003):

```csharp
namespace Dormant.Sample.Quickstart.Schema.App;
public partial class User { public bool IsRecent() => CreatedAt > DateTime.UtcNow.AddDays(-7); }
```

## 3. Author queries in the DSL (≈2 min)

`schema/app.dql` (add `<AdditionalFiles Include="schema/*.dql" />`):

```
query UsersByEmail(email: str) = select User filter .email = email;

# flat projection → a distinct record with exactly { id, email }
query UserContacts(since: datetime) =
  select User { id, email } filter .created_at >= since order by .email asc;

# optional parameters: a filter is included only when its argument is supplied (result type fixed)
query SearchUsers(email: optional str) = select User filter .email = email;
```

`dotnet build` generates one `ISession` extension method per query (inside a C# 14 extension block on
`AppQueries`). Referencing a non-projected field on `UserContacts`' result would **not compile** (FR-008).

> 🔜 **planned**: single-round-trip nested fetch (`select Post { title, author: { email } }`),
> `limit … ?? 20` coalesce, and path-navigation filters (`.author.id`).

## 4. Apply the schema (≈3 min)

Current: apply the generated schema-qualified DDL (CREATE SCHEMA + CREATE TABLE, idempotent):

```csharp
await DormantPostgres.EnsureCreatedAsync(connectionString);
```

> 🔜 **planned**: versioned migrations via CLI — `dotnet dormant migrations add/apply/rollback/status`.

## 5. CRUD + query round-trip (≈4 min)

```csharp
await using var factory = DormantPostgres.CreateSessionFactory(connectionString);

var id = Guid.NewGuid();
await using (var session = await factory.OpenSessionAsync())
{
    await session.AddAsync(new User { Id = id, Email = "a@b.com", CreatedAt = DateTime.UtcNow, Version = 1 });
    await session.CommitAsync();
}

// Read by key + update only the changed column (snapshot diff, FR-014)
await using (var session = await factory.OpenSessionAsync())
{
    var u = await session.GetAsync<User>(id);
    u!.Email = "new@b.com";
    await session.CommitAsync();   // UPDATE writes only "email"; "version" bumped (optimistic concurrency, FR-015)
}

// Query via the generated ISession extension methods, streaming
await using (var session = await factory.OpenSessionAsync())
{
    await foreach (var user in session.UsersByEmail("new@b.com"))
        Console.WriteLine(user.Email);

    await foreach (var contact in session.UserContacts(DateTime.UtcNow.AddDays(-1)))
        Console.WriteLine($"{contact.Id}: {contact.Email}");   // distinct projection record
}

// Unfetched collection is explicit (FR-009) — never silently lazy-loaded
var loaded = (await factory.OpenSessionAsync()) is var s && (await s.GetAsync<User>(id))!.Posts.IsLoaded;
```

## 6. Publish Native AOT (≈1 min) — zero library warnings (SC-001/SC-006)

```bash
dotnet publish -c Release -r <rid> -p:PublishAot=true
# No Dormant-originated trimming/AOT warnings; the native binary's first query needs no warm-up.
```

## What you proved

- Schema → generated entities (FR-001/FR-003); query → build-time-known entity/projection
  (FR-006/FR-008); change-tracked commit with optimistic concurrency (FR-014/FR-015); optional
  parameters with a fixed result type (FR-012/FR-031); snake_case schema-qualified DDL applied
  (FR-045); AOT publish clean (SC-001/SC-006).
- 🔜 planned: single-round-trip nested fetch (FR-010), explicit on-demand link load, versioned
  migration CLI (SC-010).
