# Quickstart: Generated SQL as Raw String Literals

No authoring change — this feature only affects how generated code *reads*. Author DormantQL exactly as in
`003`; after building, the generated `*.g.cs` shows SQL as readable raw string literals.

## Author (unchanged — 003 grammar)

```
# schema/app.dql
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

## Inspect the generated code

**Before (003):**

```csharp
new PreparedStatement(
    "SELECT \"id\", \"email\", \"created_at\", \"version\" FROM \"app\".\"user\" WHERE \"email\" = $1",
    writer => { writer.Write(1, email); });
```

**After (004):**

```csharp
new PreparedStatement(
    """
    SELECT "id", "email", "created_at", "version" FROM "app"."user" WHERE "email" = $1
    """,
    writer => { writer.Write(1, email); });
```

The SQL is identical; only the C# literal form changed — quoted identifiers read verbatim, no `\"`.

## Verify

- `dotnet build Dormant.slnx` → 0 warnings; open a generated `*.g.cs` and confirm SQL statements are
  `"""…"""` raw literals with no `\"` escapes.
- Provider tests against Docker PostgreSQL pass **unchanged** (same SQL executed → same results).
- AOT smoke publishes with zero warnings.
