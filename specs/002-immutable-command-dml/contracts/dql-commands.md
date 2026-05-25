# Contract: DQL command language (writes) + `with`

The DSL is a compatibility surface (Constitution II): command grammar/semantics MUST NOT change incompatibly
within a MAJOR version. This is a **v1 scope sketch** for the fork, fixing *what constructs exist*. Commands
live in `.dql` files alongside queries (a file may hold both). Authored in DQL, consumed by the generator as
`AdditionalFiles`.

## Commands (v1)

```
# insert with parameters → returns the inserted row (immutable)
command CreateUser(email: str) =
  insert User { email := email, created_at := datetime::now() };

# nested insert (related row inserted inline) — single round-trip
command CreatePostForNewAuthor(title: str, email: str) =
  insert Post {
    title := title,
    author := (insert User { email := email, created_at := datetime::now() })
  };

# parent + children via explicit `with` back-reference (no implicit auto-link, no `..id`)
command CreateAuthorWithPosts(email: str, t1: str, t2: str) =
  with u := (insert User { email := email, created_at := datetime::now() })
  insert Post { title := t1, author := u },
  insert Post { title := t2, author := u };

# update with optimistic concurrency (token match + bump); affects 0 rows on stale token
command RenamePost(id: uuid, title: str, version: int) =
  update Post filter .id = id and .version = version
  set { title := title, version := version + 1 };

# delete
command DeletePost(id: uuid) = delete Post filter .id = id;
```

- **Kinds**: `insert` / `update` / `delete`, each a named `command` with typed parameters (required +
  `optional`, as in queries).
- **Assignments**: `field := <expr>` where `<expr>` is a literal, parameter, `with`-reference, native call
  (`datetime::now()`), or a **nested write** (`(insert … )`).
- **`with` bindings**: `with name := <expr>` declares a reusable value/reference (including a nested write's
  result). An explicit `with` binding is the **only** way one write references another's generated value
  (e.g. a parent id). No implicit auto-link; no `..id` token.
- **Filter** (`update`/`delete`): the v1 predicate surface from `001` (`= < > <= >= like ilike`, conjunctions),
  including a concurrency-token match.
- **Result**: an `insert` returns the inserted row (immutable) by default; `update`/`delete` return an
  affected-row signal / optional `returning` projection. (Exact `returning` shape syntax is Tier-A scope.)
- **Native functions**: provider-scoped, build-time type-checked (e.g. `datetime::now()`).

## Execution semantics

- Each command compiles to **one** PostgreSQL statement. Nested/related writes use **data-modifying CTEs**
  (`WITH a AS (INSERT … RETURNING …), … <final>`) → single round-trip (SC-002).
- All SQL is produced at **build time**; only parameter values vary at runtime (no runtime compilation).
- `update`/`delete` matching zero rows (incl. stale concurrency token) is a defined, surfaced result — never
  a silent success.

## Out of scope (v1 of this fork)

Advanced EdgeQL: polymorphism/type-intersection, backlinks, link properties, broad set algebra, aggregates
beyond simple counts, `group … by`, free-object results, incremental link mutation (`+=`/`-=`) as a
standalone operation. Dynamic/runtime command generation is a future **macros** feature.

## Diagnostics (located)

Unknown entity/field/parameter; undefined or unreferenced `with` name; write-reference cycle; native
construct on an unsupported provider; result shape not closed at build time — all reported with a source
`Location` and an actionable message; no masking output.
