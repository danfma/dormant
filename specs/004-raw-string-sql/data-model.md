# Phase 1 Data Model: Generated SQL as Raw String Literals

This feature has **no domain data model** — it changes the C# syntax used to embed SQL in generated code. The
only relevant element is the generated literal itself.

## Generated SQL literal

- **What**: The C# string literal in a generated method body that carries one statement's build-time SQL (the
  SQL argument of `PreparedStatement`).
- **Value** (unchanged): the exact SQL text rendered by `SqlRenderer` — e.g.
  `INSERT INTO "catalog"."widget" ("id") VALUES ($1) RETURNING "id"`.
- **Syntax before**: a regular string literal with `\"`-escaped identifiers and `\\` escapes.
- **Syntax after**: a multi-line raw string literal (`"""` … `"""`) with verbatim `"` identifiers, a
  computed quote-fence (`max(3, longestQuoteRun+1)`), non-interpolated, dedented to the exact value.
- **Invariant**: `valueAfter == valueBefore` (byte-for-byte). Only the lexical representation differs.

## Out of model (unchanged)

The SQL IR (`SqlIr`), `SqlRenderer`, parameter binding, the `PreparedStatement`/`CompiledCommand`/
`CompiledQuery` runtime types, parameter-value string literals, and the dynamic StringBuilder path.
