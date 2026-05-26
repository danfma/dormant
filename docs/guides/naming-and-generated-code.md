# Naming And Generated Code

DormantQL is authored in a database-friendly style, while generated C# uses .NET naming conventions.

## Unit Names To Methods

DormantQL unit names are snake_case:

```dql
query users_by_email(email: string) {
  from User u
  where u.email == email
  select u
}
```

The generated method is PascalCase:

```csharp
session.UsersByEmail("ada@example.com")
```

Likewise, `create_user` becomes `CreateUser`.

## Entities And Members

Entities are authored as PascalCase:

```dql
entity User {
  created_at: datetime;
}
```

Members are authored as snake_case and generated as PascalCase C# members:

```csharp
user.CreatedAt
```

## Namespaces

Generated namespaces are derived from:

```text
PascalCaseEachPart(RootNamespace + schema folders + module)
```

The quickstart sample uses:

```csharp
using Dormant.Sample.Quickstart.Schema.App;
```

## Query Results

`select u` returns full immutable entities:

```dql
query users_by_email(email: string) {
  from User u
  where u.email == email
  select u
}
```

`select { ... }` returns a distinct projection type with exactly the selected members:

```dql
query user_contacts(since: datetime) {
  from User u
  where u.created_at >= since
  select {
    u.id
    u.email
  }
}
```

That projection is not a partially-loaded `User`.

## Mutation Results

Mutation result inference depends on the command and optional `returning` shape:

- Insert without explicit `returning`: generated result follows the current command emitter's default insert shape.
- Update/delete without explicit `returning`: affected-row count.
- `returning alias`: full entity shape.
- `returning alias.member`: scalar shape.
- `returning { ... }`: projection shape.

Use `returning` when you want the generated method signature to be obvious to readers.

## Generated SQL Readability

Generated methods carry build-time SQL in C# raw string literals. This keeps quoted identifiers readable in generated code:

```csharp
var statement = new global::Dormant.Abstractions.Querying.PreparedStatement(
    """
    SELECT "id", "email" FROM "app"."user" WHERE "email" = $1
    """,
    writer => writer.Write(1, email));
```

The raw string syntax changes the generated C# literal form, not the SQL value.
