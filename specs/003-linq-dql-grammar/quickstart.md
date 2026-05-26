# Quickstart: LINQ-Style DQL Grammar

Author a schema, one `query`, and one `mutation` in the new grammar, then round-trip a write and read. Target:
under 15 minutes using only the DSL and documented APIs (SC-007). The `.dqls` schema and the runtime APIs are
unchanged from `002`; only the unit grammar is new.

## 1. Schema (`schema/app.dqls`) — unchanged grammar

```
module app;

entity User {
  id: uuid primary
  email: string
  name: string?
  created_at: datetime
  version: int
}
```

## 2. Units (`schema/app.dql`) — new LINQ-style grammar

```
module app;

# Write: insert a User. No `returning` → the method returns the inserted id.
mutation create_user(id: uuid, email: string, created_at: datetime, version: int) {
  insert User u {
    u.id = id
    u.email = email
    u.created_at = created_at
    u.version = version
  }
}

# Write with `returning` → the method returns the materialized User entity.
mutation create_user_returning(id: uuid, email: string, created_at: datetime, version: int) {
  insert User u {
    u.id = id
    u.email = email
    u.created_at = created_at
    u.version = version
  }
  returning u
}

# Write: update with a where filter → returns affected-row count (optimistic concurrency via version).
mutation set_user_name(id: uuid, name: string, expected_version: int, new_version: int) {
  update User u
  where u.id == id && u.version == expected_version
  set {
    u.name = name
    u.version = new_version
  }
}

# Read: full entity.
query users_by_email(email: string) {
  from User u
  where u.email == email
  select u
}

# Read: projection → distinct type exposing only id + email.
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

## 3. Use the generated methods (C#)

```csharp
await using var session = await factory.OpenSessionAsync();

// insert (no returning) → inserted id
var id = Guid.NewGuid();
Guid inserted = await session.CreateUser(id, "ada@example.com", DateTime.UtcNow, version: 0);
await session.CommitAsync();

// read full entity
await foreach (var user in session.UsersByEmail("ada@example.com"))
    Console.WriteLine(user.Email);

// projection → distinct type with only Id + Email
await foreach (var c in session.UserContacts(since: DateTime.UtcNow.AddDays(-1)))
    Console.WriteLine($"{c.Id} {c.Email}");

// update with optimistic concurrency → affected count (0 = stale)
int affected = await session.SetUserName(id, "Ada", expectedVersion: 0, newVersion: 1);
await session.CommitAsync();
// affected == 1 for the first writer; a stale expected_version yields 0.
```

## What changed from `002`

| `002` | `003` (new) |
|-------|-------------|
| `command create_user(...) = insert User { id := id, … };` | `mutation create_user(...) { insert User u { u.id = id … } }` |
| `query q(...) = select User filter .email = email;` | `query q(...) { from User u where u.email == email select u }` |
| `:=` assignment, single-`=` compare, `and`, leading-dot `.email` | `=` assignment, `==`/`!=`/`&&`/`\|\|`/`!`, alias `u.email` |
| command result = full entity | inferred: insert→id (or `returning u` for the entity) |

## Verify

- Build the sample (`dotnet build Dormant.slnx`): the new grammar generates; the removed `002` forms do not.
- Run the provider tests against Docker PostgreSQL: insert/read/update/concurrency behave as in `002`.
- AOT publish the smoke project: zero library-originated warnings.
