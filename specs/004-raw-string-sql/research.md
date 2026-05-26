# Phase 0 Research: Generated SQL as Raw String Literals

Grounded in the existing emitters (`Query/QueryEmitter.cs`, `Command/CommandEmitter.cs`, `Emit/EmitHelpers.cs`).
Decision / Rationale / Alternatives.

## 1. Literal form: multi-line raw string

**Decision**: Emit the SQL statement as a **multi-line** raw string literal:

```csharp
var statement = new …PreparedStatement(
    """
    INSERT INTO "catalog"."widget" ("id", "name", "quantity") VALUES ($1, $2, $3) RETURNING "id", "name", "quantity"
    """,
    writer => { … });
```

**Rationale**: A **single-line** raw string literal (`"""…"""`) is invalid when its content **ends (or starts)
with a `"`** — and our SQL routinely ends with a quoted identifier (`… RETURNING "id"`). The multi-line form
(content on its own line(s) between the opening `"""<newline>` and a `<newline>"""` line) has no such
restriction and dedents to exactly the content. So multi-line is the robust, always-valid choice.

**Alternatives considered**: Single-line raw (`"""sql"""`) — rejected (fails for SQL ending in `"`). Keeping
escaped regular strings — rejected (the feature's whole point). `@"…"` verbatim strings — rejected (still
require doubling every `"` → `""`, only marginally better than `\"`).

## 2. Quote-fence length

**Decision**: Use a fence of **three** double-quotes by default, but compute it as `max(3, longestRunOfQuotes
+ 1)` from the SQL content (FR-003). DormantQL SQL quotes single identifiers (`"x"`), so the longest run is 1
→ fence 3 in practice; the computation guarantees validity for any content.

**Rationale**: The closing/opening fence must be longer than any `"`-run inside the content. Computing it is
cheap, deterministic, and future-proofs against SQL that ever contains `""`/`"""`.

**Alternatives**: Hard-code `"""` — rejected (not robust per FR-003, though sufficient today).

## 3. Indentation / dedent with `SourceWriter`

**Decision**: Emit the opening `"""`, the SQL content line(s), and the closing `"""` **all at the same
`SourceWriter` indent level**. C# strips the closing-delimiter's leading whitespace from every content line,
so equal indentation yields exactly the SQL text with no leading spaces. The opening newline and the final
newline before the closing fence are not part of the value, so a single-line SQL becomes exactly that string
(no surrounding newlines) — satisfying FR-002 (byte-identical value).

**Rationale**: `SourceWriter.Line` already prefixes `_indent*4` spaces; aligning the three lines makes the
dedent a no-op on the SQL content. The SQL from `SqlRenderer` is single-line, so one content line.

**Alternatives**: Manual whitespace management outside `SourceWriter` — rejected (fragile, inconsistent
indentation risks changing the value).

## 4. Non-interpolated literal (preserve `$n` and braces)

**Decision**: Use a plain raw string (`"""`), never an interpolated raw string (`$"""`). The SQL contains
PostgreSQL positional placeholders `$1`, `$2`, … and may contain `{`/`}`; a plain raw string treats all of
these verbatim (FR-004).

**Rationale**: Interpolation would assign meaning to `{`/`}` and require `{{`/`}}` escaping — reintroducing
escape noise and risking value changes. We never interpolate into the SQL (parameters are bound via the
writer callback), so plain raw is correct.

## 5. Scope: which literals change

**Decision**: Convert **only the SQL statement literal** (the `PreparedStatement` SQL argument) in
`EmitStaticStatement` (QueryEmitter) and `EmitStatement` (CommandEmitter). **Keep regular escaped strings**
for: string **parameter-value** literals (`CommandEmitter.ValueToken` → `writer.Write(n, "literal")`), and
the runtime **StringBuilder** dynamic-SQL path (QueryEmitter `EmitDynamicStatement`), whose fragments are
assembled at runtime — its behavior must be preserved (FR-006). The dynamic seed may be converted
opportunistically only if byte-equivalence is guaranteed.

**Rationale**: The static SQL literal is where the escape noise lives and where the readability win is. Value
literals are arbitrary data (not SQL) and are fine as regular strings. The dynamic path mixes C# expressions
with SQL fragments; raw-ifying it risks value/behavior drift for little gain.

**Alternatives**: Convert every string literal — rejected (over-reach; risks changing parameter values and
dynamic-SQL behavior).

## 6. Helper placement

**Decision**: Add a shared `EmitHelpers`-level helper (e.g. `RawSqlLiteral(SourceWriter, sql)` or a string-
returning `RawStringLiteralLines(sql)`) used by both emitters, so the raw-emission logic (fence + multi-line
layout) lives in one place. `Quote(string)` stays for value/identifier literals that remain regular.

**Rationale**: Single source of truth for the raw form; both emitters already share `EmitHelpers` (`Naming`,
`SourceWriter`, `TypeMap`).

## 7. Verification

**Decision**: Update generator emit-test asserts (which currently match the `\"`-escaped SQL, e.g.
`"INSERT INTO \\\"catalog\\\"…"`) to match the raw form, refresh any Verify snapshots, and **re-run the
provider suite against real Docker PostgreSQL** to prove the executed SQL and results are unchanged (SC-002),
plus the AOT smoke 0-warning publish (SC-003).

**Rationale**: The change is "invisible" at runtime by design; the provider suite passing unchanged is the
proof. Generator asserts/snapshots are the build-time contract check.
