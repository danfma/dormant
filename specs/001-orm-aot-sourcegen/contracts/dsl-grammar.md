# Contract: DormantQL language (schema + query/DML)

The DSL is a compatibility surface (Constitution II): grammar/semantics MUST NOT change incompatibly
within a MAJOR version. This is a **v1 scope sketch**, not the final grammar; it fixes *what constructs
exist*, mapping to the spec's Tier A (v1) / Tier B (Phase 2) split. Authored in DormantQL files (`.dqls` schema, `.dql` query) consumed by the
generator as `AdditionalFiles`.

## Schema (v1)

```
module app;

entity User {
  id: uuid primary;
  email: str;
  created_at: datetime;
  profile: jsonb;                 # native type (FR-038), provider-scoped
  multi posts -> Post;            # multi link (one side of m:n, FR-037)
  version: int concurrency;       # optimistic concurrency token (FR-015)
}

entity Post {
  id: uuid primary;
  title: str;
  single author -> User;          # single link
}
```

- Value types (FR-036): `str, bool, int16/32/64, float32/64, decimal, bigint, uuid, datetime, duration,
  bytes, json` + collections `array<T>`, `tuple<...>`, named tuple. `map<K,V>` is **Phase 2**.
- Links: `single`/`multi`; many-to-many = `multi` per side; edge data = an explicit join entity (FR-037).
- Native: a property typed as a provider-native type (`jsonb`, `geometry`) is scoped by a provider
  directive (FR-042).

## Query / DML (v1, Tier A)

```
# shaped select returning a projection
query RecentPostsByAuthor(authorId: uuid, limit: optional int64) =
  select Post {
    title,
    author: { email }            # single-round-trip nested fetch (FR-010)
  }
  filter .author.id = authorId
  order by .created_at desc
  limit limit ?? 20;             # optional param + coalesce (FR-031)

# full-entity select
query UserById(id: uuid) = select User { ** disallowed; use * } filter .id = id;  # `**` excluded (FR-035)

# DML
mutation CreateUser(email: str) = insert User { email := email, created_at := now() };
mutation Rename(id: uuid, title: str) = update Post filter .id = id set { title := title };
mutation Delete(id: uuid) = delete Post filter .id = id;
```

- Predicates (FR-032): `= < > <= >= like ilike in exists ??`.
- Single-result narrowing (FR-033): `assertSingle(...)` / `assertExists(...)` (spirit).
- **Phase 2 (rejected in v1, FR-035)**: computed expressions, `[is T]` polymorphism, backlinks `.<link`,
  link props `@p`, set ops, aggregates beyond `count`, `for…union`, `unless conflict`, nested insert,
  `+=`/`-=`, `group by`, free objects, `map<K,V>`, deep-splat `**` (permanent).

## Native escape (v1, FR-039/FR-040/FR-042)

```
provider postgres {
  func st_dwithin(geometry, geometry, float64) -> bool;     # declared typed signature
  operator jsonb_contains "@>" (jsonb, jsonb) -> bool;
}

query NearbyShops(p: geometry) =
  select Shop { name }
  filter st_dwithin(.location, p, 1000.0);                  # type-checked native call

query Tagged(q: jsonb) =
  select Doc { id }
  filter native(postgres, returns: bool) { ".tags @> {0}" }(q);   # raw typed fragment, param-bound
```

- A native construct outside `provider <name> { … }` scope, or targeting an unsupported provider, is a
  located build diagnostic (FR-042). A raw fragment without a declared return type is a build error.

## Diagnostics (FR-004/FR-028)

Every schema/query/native error is reported with a source `Location` (file + line/col) and an actionable
message (what failed, why, next step). No output is emitted that masks an error.
