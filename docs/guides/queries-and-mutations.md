# Queries And Mutations

DormantQL unit files use the `.dql` extension. They contain read `query` units and write `mutation` units.

The current grammar is a LINQ/SQL-style syntax with explicit aliases, alias-qualified members, brace-delimited blocks, and C#/TypeScript-style operators.

## Query

```dql
query users_by_email(email: string) {
  from User u
  where u.email == email
  select u
}
```

Query clause order is:

```text
from -> where? -> order by* -> select
```

Members must be alias-qualified, such as `u.email`. Leading-dot members like `.email` are not valid.

## Projection Query

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

`select u` returns full entities. `select { ... }` returns a distinct generated projection type with exactly the selected members.

## Mutation

```dql
mutation create_user(id: uuid, email: string, created_at: datetime, version: int) {
  insert User u {
    u.id = id
    u.email = email
    u.created_at = created_at
    u.version = version
  }
}
```

Mutations support `insert`, `update`, and `delete` command forms. Assignment uses single `=` inside insert/set bodies. Comparisons use `==`, `!=`, `<`, `<=`, `>`, and `>=`.

## Returning

`returning` controls mutation result shape:

```dql
mutation create_user_id(id: uuid, email: string, created_at: datetime, version: int) {
  insert User u {
    u.id = id
    u.email = email
    u.created_at = created_at
    u.version = version
  }
  returning u.id
}
```

Result shapes mirror query shapes:

- `returning u` returns the entity.
- `returning u.id` returns a scalar.
- `returning { u.id, u.email }` returns a projection.

## With Value Flow

The current source and sample include `with` value flow for binding a command result and reusing it in a later command:

```dql
mutation create_user_with_post(
  uid: uuid, email: string, created_at: datetime, version: int, pid: uuid, title: string) {
  with u = (insert User x {
    x.id = uid
    x.email = email
    x.created_at = created_at
    x.version = version
  })
  insert Post p {
    p.id = pid
    p.title = title
    p.author = u
  }
  returning p.id
}
```

This is the current form used in the sample. Older SpecKit notes may refer to "multi-command" wording; the current decided shape is `with` binding plus a terminal command/result.

## Operators

Implemented:

- `==`
- `!=`
- `<`, `<=`, `>`, `>=`
- `&&`
- `=` for assignment inside write bodies

Deferred:

- `||`
- `!`

The parser currently reports `||` and `!` as unsupported.

## Removed Forms

Do not use the old `002` forms:

```text
command Name(...) = ...;
query Name(...) = ...;
.email
:=
and / or / not
:: / ->
```

Use `query`, `mutation`, explicit aliases, alias-qualified members, symbolic operators, and brace-delimited blocks instead.
