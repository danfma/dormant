# Phase 0 Research: LINQ-Style DQL Grammar

All decisions below were either resolved interactively with the user (recorded in spec Clarifications) or
grounded in the existing `002` generator source. Each is stated as Decision / Rationale / Alternatives.

## 1. Lexer token set

**Decision**: Extend the existing hand-written `Lexer` to add `==` (EqualEqual), `!=` (BangEqual), `&&`
(AmpAmp), `||` (PipePipe), `!` (Bang). **Repurpose** `=` (existing `Equals`) as the **assignment** operator
(member writes in `insert`/`set`). **Retire** the `002`-era `:=` (`Assign`), `::` (`DoubleColon`), and `->`
(`Arrow`) tokens from the accepted grammar. Keep `< <= > >= . , ( ) { } :` and identifiers/numbers/strings/
`#`-comments/position tracking as-is.

**Rationale**: The lexer already emits most needed punctuation (`< > <= >= = . ( ) { }`); only the
two-character logical/comparison operators and `!` are missing. Repurposing `=`→assignment and adding `==`→
comparison matches C#/TypeScript exactly (FR-003). Retiring `:= :: ->` enforces the single-canonical-surface
rule (FR-015) at the token level — a removed operator never reaches the parser.

**Alternatives considered**: Keyword logical connectives (`and`/`or`/`not`, SQL/EdgeQL) — rejected: the user
asked for C#/TypeScript operators, and symbolic forms read consistently with the comparison operators. A
parser-generator/ANTLR grammar — rejected: the hand-written lexer is tiny, AOT/netstandard2.0-friendly, and
already integrated with located diagnostics.

## 2. Parser structure & clause order

**Decision**: Rewrite `QueryParser` and `CommandParser` as small **recursive-descent** parsers over brace-
delimited blocks. A unit is `('query'|'mutation') snake_name '(' params ')' '{' body '}'`. Enforce the
**canonical clause order**: queries `from E alias → where → order by → select`; mutations `insert E alias {…}`
| `update E alias → where → set {…}` | `delete E alias → where`, with an optional trailing `returning <expr>`
or trailing read. Statements are newline-separated; a stray `;` is tolerated and ignored; `#` starts a line
comment. Out-of-order clauses produce a located diagnostic rather than silent acceptance.

**Rationale**: Recursive descent keeps the parser explicit, debuggable, and diagnostic-friendly (precise
spans). A fixed clause order (rather than free-form) keeps the grammar learnable and the parser simple, and
matches the corrected base file. Newline-significant-with-optional-`;` matches the corrected `app.query`.

**Alternatives considered**: Free clause ordering — rejected: ambiguous, harder to diagnose, no user value.
Required `;` terminators — rejected: contradicts the corrected base file and the LINQ/EdgeQL feel.

## 3. Aliases & member references

**Decision**: Every subject MUST declare an explicit alias (`from User u`, `insert User u`, `update User u`,
`delete User u`). All member references are alias-qualified (`u.email`). The parser binds aliases per unit and
reports a located diagnostic for a missing, undeclared, or duplicate alias. The `002` leading-dot form
(`.email`) is not accepted.

**Rationale**: Explicit aliases (the user's decision) remove the leading-dot ambiguity, scale to future
multi-subject/`with` scenarios, and read like SQL/LINQ. Binding per-unit lets the emitter resolve each
`alias.field` to a concrete entity column.

**Alternatives considered**: Optional/implicit alias with leading-dot fallback — rejected: dual member-
reference syntaxes, contradicts FR-002 and the single-surface rule.

## 4. Operator → SQL mapping

**Decision**: Map DQL operators onto the existing SQL IR conditions: `==`→`=`, `!=`→`<>`, `<`/`<=`/`>`/`>=`
unchanged, `&&`→`AND`, `||`→`OR`, `!`→`NOT`, with C#/TypeScript precedence (`!` > comparison > `&&` > `||`),
parenthesizable. Assignment `=` maps to an `InsertColumn`/`SqlAssignment` value (reusing `002`'s IR nodes,
including the `::jsonb` write cast for `json`).

**Rationale**: The IR (`SqlCondition`, `SqlAssignment`, `UpdateStatement`, `InsertStatement`) already exists
from `002`; this feature only changes how the parsed model is produced, not the SQL it renders. Reusing the IR
keeps the renderer and provider untouched (Principle V/VI: no new hot-path surface).

**Alternatives considered**: Introducing a richer expression IR for boolean trees — deferred: `002`'s
condition model + `AND`/`OR`/`NOT` covers the v1 predicate surface; a general expression tree is only needed
for computed expressions (a later feature).

## 5. Result-type inference & `returning`

**Decision**: A unit's result is determined by its trailing statement (spec FR-008): `select alias` → full
entity; `select { … }` → distinct projection; `insert` (no `returning`) → the entity's primary-key value;
`update`/`delete` (no `returning`) → affected-row count; an explicit `returning <expr>` overrides the default
and **mirrors `select`** (`returning alias` entity, `returning { … }` projection, `returning alias.field`
scalar). Materialization reuses the binding's existing `Materialize`/projection machinery.

**Rationale**: Inference (no `returning` annotation in the common case) is the EdgeQL-parity default the user
asked for; `returning` reuses the *same* projection shaping as `select`, so there is one mental model and one
materializer code path. Because PKs are app-assigned (`002` FR-018), the default insert→id is a cheap
confirmation handle.

**Alternatives considered**: Always-explicit return annotations — rejected: verbose, contradicts the
inference intent. A mutation-only bespoke return syntax distinct from `select` — rejected: two shaping
grammars to learn and implement.

## 6. Multi-command mutations & `with`-bindings

**Decision**: A `mutation` block MAY contain a sequence of commands and an optional trailing read/`returning`;
the **trailing statement determines the result type** (spec FR-017). Values flow between commands via
**`with`-bindings** (`with x = <expr>`), compiled to ordinary C# locals threaded into subsequent command
parameters. Commands execute as a **statement sequence within the existing session transaction** (not a single
CTE round-trip).

**Rationale**: The session is already a transaction boundary (`002`), so a sequence is atomic without new
machinery. `with`-as-local is the simplest AOT-friendly value-flow mechanism (no runtime planning). This
delivers parent→child id flow (the common case) without the single-round-trip CTE complexity, which is
explicitly deferred.

**Alternatives considered**: Single-round-trip data-modifying CTE (`002` US2) — deferred (out of scope here);
it is an optimization of the same author-facing shape and can layer on later without grammar change. Implicit
auto-linking — rejected (carried from `002`'s decision: explicit only).

## 7. Identifier casing (unit names)

**Decision**: `query`/`mutation` names are authored in `snake_case` and generated as `PascalCase` C# methods
(`users_by_email` → `UsersByEmail`); entity names stay `PascalCase`; primitive/scalar type keywords are
lowercase (FR-016). Add a `snake_case → PascalCase` helper to `NamingConvention` (the inverse of the existing
`ToSnakeCase`); reuse the existing column-name resolution for members.

**Rationale**: Consistent with `002`'s snake_case-DQL → PascalCase-C# convention for members, now applied to
unit names. Keeps authored DQL uniform (everything snake) while generated C# stays idiomatic (PascalCase
methods, PascalCase entities).

**Alternatives considered**: Verbatim unit names (author PascalCase) — rejected by the user (names must be
snake_case in DQL).

## 8. Unit-file extension

**Decision (recommended, low-impact — confirm at implement time)**: Keep **`.dql`** as the single unit-file
extension holding both `query` and `mutation` blocks; `.dqls` remains the schema extension. The generator
already globs `.dql` for both queries and commands, so no glob change is needed. The drafted sample
`app.query` is standardized to `app.dql` during US5 migration.

**Rationale**: One canonical unit extension, zero generator-glob churn, and `002` tests already use `.dql`.
The `.query` name in the base file was illustrative of the new grammar, not a deliberate extension choice.

**Alternatives considered**: Adopt `.query` for units — rejected for v1: requires extending the glob and
creates a third extension alongside `.dqls`/`.dql` with no functional gain. (Trivially revisitable; this is
the one open low-stakes choice flagged for confirmation.)

## 9. Removed-syntax diagnostics

**Decision**: Authoring a removed `002` form (`command`, `Name(...) = …;`, leading-dot `.field`, `:=`, `and`/
`or`) produces a **located diagnostic** that names the removed construct and points to the new form, rather
than a generic parse error.

**Rationale**: DX (Principle I) and FR-009/FR-015: a developer migrating from `002` gets an actionable
message ("`command` is replaced by `mutation { … }`") instead of a confusing syntax error.

**Alternatives considered**: Generic "unexpected token" — rejected: poor migration DX.

## 10. What stays untouched (carried from `002`)

The SQL IR (`Ir/SqlIr.cs`) + renderer, immutable entity emission (`EntityEmitter`), `IEntityBinding` +
`EntityBindingEmitter` (Materialize/Schema/CreateTableSql/SelectByKey), the thin `Session`, the PostgreSQL
provider, configurable naming + overrides, schema-qualified DDL + `EnsureCreatedAsync`, the jsonb value type,
the `Ref*` read-side types, and Native AOT compatibility. This feature changes only how authored units are
lexed/parsed/named and how the parsed models map onto the (unchanged) IR.
