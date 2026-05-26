# Feature Specification: Generated SQL as Raw String Literals

**Feature Branch**: `004-raw-string-sql`

**Created**: 2026-05-26

**Status**: Draft

**Input**: User description: "Vamos escrever as SQL geradas usando raw strings para facilitar a leitura e
evitar todos aqueles escapes de quotes."

## Overview

The DormantQL source generator embeds the build-time SQL it produces as C# string literals inside the
generated method bodies. Today that SQL is emitted as a **regular quoted string**, so every quoted SQL
identifier (`"schema"."table"`, `"column"`) becomes an escaped `\"` in the generated source — e.g.
`"INSERT INTO \"catalog\".\"widget\" (\"id\", \"name\") VALUES ($1, $2) RETURNING \"id\""`. This is correct
but hard to read and review.

This feature changes the generator to emit that SQL using **C# raw string literals** (`"""…"""`), so the
quoted identifiers appear verbatim and the escape noise disappears — e.g.
`"""INSERT INTO "catalog"."widget" ("id", "name") VALUES ($1, $2) RETURNING "id\""""` reads as plain SQL. The
**SQL text itself is unchanged**; only the C# literal syntax used to carry it changes. This improves the
readability and reviewability of the generated code — a first-class compatibility surface (Constitution I/II).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read generated SQL without escape noise (Priority: P1)

A developer inspecting the generated `*.g.cs` (to debug, review a diff, or understand the emitted SQL) sees
the SQL as readable, verbatim text with real double-quotes around identifiers, not `\"`-escaped strings.

**Why this priority**: The generated code is a contract surface developers read; escape noise is the main
friction when reviewing it. This is the entire point of the feature.

**Independent Test**: Author a schema + a query and a mutation, build, open the generated source, and confirm
the SQL literals are raw string literals (`"""…"""`) containing un-escaped `"` identifiers, while the
program still compiles and the SQL executes identically.

**Acceptance Scenarios**:

1. **Given** a query that renders to `SELECT "a", "b" FROM "schema"."table" WHERE "a" = $1`, **When** the
   project builds, **Then** the generated method carries that SQL as a raw string literal with verbatim
   double-quotes and no `\"` escapes.
2. **Given** an insert/update/delete mutation, **When** the project builds, **Then** its INSERT/UPDATE/DELETE
   SQL is likewise emitted as a raw string literal.
3. **Given** the same schema before and after this change, **When** both versions run against the database,
   **Then** the executed SQL text and results are byte-for-byte identical (only the C# literal form changed).

### Edge Cases

- SQL whose text contains a run of double-quotes that would collide with the raw-string delimiter → the
  generator MUST choose a delimiter (quote-fence) long enough to remain valid (raw strings allow `""""`+).
- SQL containing braces `{`/`}` → the literal MUST NOT be an interpolated raw string (`$"""…"""`); a plain
  raw string treats braces verbatim.
- Multi-line vs single-line SQL → either is acceptable as long as the emitted literal compiles and preserves
  the exact SQL text (no introduced/stripped whitespace that changes the SQL).
- Dynamically-assembled SQL (optional-filter fragments built at runtime via a builder) → fragments that are
  authored as C# string pieces SHOULD also drop escape noise where a raw literal is equivalent; runtime
  concatenation behavior MUST be unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The generator MUST emit build-time SQL embedded in generated code as **C# raw string literals**
  (`"""…"""`), not escaped regular string literals, for the static (build-time-rendered) SQL of queries and
  mutations.
- **FR-002**: The **SQL text MUST be unchanged** by this feature — identical bytes sent to the provider before
  and after; only the C# literal syntax differs.
- **FR-003**: The generator MUST choose a raw-string delimiter (quote-fence length) that is always valid for
  the SQL content, including SQL that itself contains double-quote runs.
- **FR-004**: Emitted raw string literals MUST NOT be interpolated (`$"""`) so that `{`/`}` and `$n`
  positional placeholders in SQL are preserved verbatim.
- **FR-005**: Generation MUST remain deterministic and incrementally cacheable, and the library MUST remain
  Native AOT + full-trimming compatible with zero new warnings (raw strings are a pure source-syntax change
  with no runtime effect).
- **FR-006**: Generator snapshot/emit tests and any assertions that match the previous escaped form MUST be
  updated to the raw-string form in the same change (Constitution VI), and provider behavior MUST continue to
  pass against a real provider.

### Key Entities *(include if feature involves data)*

- **Generated SQL literal**: The C# literal in a generated method body that carries the build-time SQL string;
  this feature changes its syntax (regular → raw) but not its value.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of static build-time SQL literals in generated code are raw string literals; **0** `\"`
  escapes remain in the emitted SQL of queries and mutations.
- **SC-002**: The executed SQL is unchanged — **0** differences in SQL text or query/mutation results versus
  before the change (verified by the existing provider test suite passing unchanged).
- **SC-003**: The full test suite (generator + provider against real Docker) and the AOT smoke publish pass
  with **zero** new warnings after the change.
- **SC-004**: A developer reviewing a generated SQL statement can read the quoted identifiers verbatim without
  mentally un-escaping `\"` (readability check).

## Assumptions

- Consuming projects compile with C# 11+ (raw string literals); Dormant targets C# 14 / .NET 10, so this is
  always satisfied.
- This feature builds on `003-linq-dql-grammar`'s generator/emitters; it is a generated-code-quality change
  only — no DSL grammar, runtime, IR, or SQL-semantics change.
- The primary target is the static, build-time-rendered SQL (the main readability win). The runtime
  StringBuilder path for optional-filter fragments is improved opportunistically where a raw literal is
  equivalent, but its assembly behavior is preserved.
- The internal escaping helpers in the emitters are an implementation detail; their replacement is in scope.

## Out of Scope

- Any change to the SQL dialect, IR, grammar, runtime, or execution semantics.
- Reformatting or pretty-printing the SQL itself (e.g. multi-line indentation) beyond what raw literals
  naturally allow — the SQL text stays identical.
- Changing how parameters are bound or how positional placeholders (`$n`) are generated.
