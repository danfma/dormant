# Contract: Generated Code (raw-string SQL literal)

The generated method shapes, runtime types, parameter binding, and SQL **value** are unchanged from `003`.
Only the lexical form of the embedded SQL statement literal changes.

## Before (regular escaped string)

```csharp
var statement = new global::Dormant.Abstractions.Querying.PreparedStatement(
    "INSERT INTO \"catalog\".\"widget\" (\"id\", \"name\", \"quantity\") VALUES ($1, $2, $3) RETURNING \"id\", \"name\", \"quantity\"",
    writer =>
    {
        writer.Write(1, id);
        writer.Write(2, name);
        writer.Write(3, quantity);
    });
```

## After (multi-line raw string literal)

```csharp
var statement = new global::Dormant.Abstractions.Querying.PreparedStatement(
    """
    INSERT INTO "catalog"."widget" ("id", "name", "quantity") VALUES ($1, $2, $3) RETURNING "id", "name", "quantity"
    """,
    writer =>
    {
        writer.Write(1, id);
        writer.Write(2, name);
        writer.Write(3, quantity);
    });
```

## Rules the emitted literal MUST honor

| Rule | Requirement |
|------|-------------|
| Value identity | The string value is byte-identical to the previous escaped form (FR-002). |
| Form | Multi-line raw string: `"""` line, content line(s), `"""` line — all at equal indentation. |
| Fence | Opening/closing fence length = `max(3, longestRunOfDoubleQuotesInSql + 1)` (FR-003). |
| Non-interpolated | Plain `"""` (never `$"""`); `$1`,`$2`,… and any `{`/`}` are verbatim (FR-004). |
| Scope | Only the `PreparedStatement` SQL argument. Parameter-value string literals (`writer.Write(n, "…")`) stay regular escaped strings. |
| Dynamic path | The runtime StringBuilder optional-filter path keeps its current behavior (value unchanged). |

## Verification hooks

- Generator emit tests assert the raw form (no `\"` in the SQL literal) and the unchanged SQL value.
- Provider tests (real Docker PostgreSQL) pass unchanged → proves identical executed SQL/results (SC-002).
- AOT smoke publishes with zero warnings (SC-003).
