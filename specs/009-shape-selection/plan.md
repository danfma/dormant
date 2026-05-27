# Implementation Plan: Shape Selection (EdgeQL-style) + Flat Immutable Entities

**Branch**: `009-shape-selection` | **Date**: 2026-05-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-shape-selection/spec.md`

## Summary

Turn the query `select` from a flat scalar projection into an **EdgeQL-style shape** blended with the
existing LINQ-flavored grammar, and flatten generated entities by removing the runtime relationship
wrappers (`Ref`/`RefSet`/…). A shaped read returns a nested immutable projection whose type *is* the
requested shape; nested to-one/to-many data is fetched in **one round-trip** by emitting
**SQL JSON aggregation** (correlated subqueries producing a single JSON column), rendered per dialect
(PostgreSQL `jsonb_build_object`/`json_agg`, SQLite `json_object`/`json_group_array`) through the
existing dialect framework. Generated entities become flat rows of their own columns plus the
foreign-key id scalar; relationships live only as **schema metadata** that drives joins, typed
navigation (`a.writer.name`), shapes, and EdgeQL-style backlinks. Nested results materialize through
**generator-emitted `Utf8JsonReader` parsers** (no runtime reflection, no per-type STJ source-gen
dependency). This is a MAJOR change (the generated-code contract breaks) and is large enough that the
implementation is phased; the to-many/JSON core is the high-risk centerpiece.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (runtime libs); the source generator targets `netstandard2.0`
(Roslyn incremental generator).

**Primary Dependencies**: Roslyn (`Microsoft.CodeAnalysis.CSharp` 4.14); `System.Text.Json` (in-box —
`Utf8JsonReader` for the emitted nested-row parsers); `Microsoft.Data.Sqlite.Core` + `e_sqlite3`
(SQLite JSON1 functions); `Npgsql` (PostgreSQL `jsonb`). Reuses the 005 dialect framework
(`SqlIr` + per-dialect renderers, `DialectId` switch).

**Storage**: PostgreSQL and SQLite (the two registered dialects). Shaped SQL is generated at build
time, one variant per dialect, selected by `session.Dialect`.

**Testing**: Verify snapshots (`Verify.TUnit` + `Verify.SourceGenerators`) for generated grammar/SQL,
extended for shapes; the parameterized conformance suite (PostgreSQL via Testcontainers + SQLite
in-memory) for real shaped-read results and cross-dialect parity.

**Target Platform**: Consuming apps on .NET 10 (incl. Native AOT). The generator runs in-build.

**Project Type**: Source generator + runtime libraries (`Abstractions`, `Core`, providers).

**Performance Goals**: A shaped read — including nested to-many — is one database round-trip (no N+1).
Materialization uses no runtime reflection and avoids boxing of scalar columns; nested JSON parsing is
`Utf8JsonReader`-based (low-alloc, no `JsonDocument`/reflection serializer).

**Constraints**: AOT-clean (zero trimming/AOT warnings); all core-path SQL produced at build time
(runtime *fragment selection* for optional filters stays allowed — no runtime SQL compilation); PG
output for existing flat queries stays byte-identical where unchanged.

**Scale/Scope**: Nesting depth is unbounded by design, cycle-guarded with a build-time diagnostic;
typical shapes are small/medium trees. This feature spans the whole generator pipeline (lexer →
parser → models → semantic resolution → SqlIr → renderers → emitters) plus runtime materialization.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **III. Statically-Known, Safe-by-Default Data Access** — *Advances, with an amendment.* Shapes make
  the result type fully build-time-known; reading a field outside the shape is a compile error; no
  lazy loading; no partial entities. The no-partial-data guarantee moves from the `Ref` load-state
  clause to **projection types**. **Action required:** the constitution's Principle III clause "Links
  MUST be modeled with explicit, type-checked loaded/unloaded states" is superseded and MUST be
  amended (run `/speckit-constitution`). Flagged as a governance dependency, not a silent change.
- **II. Interface & Compatibility Stability** — *Breaking → MAJOR.* Removing `Ref`/`RefSet`/… from
  generated entities and changing entity shape breaks the generated-code contract; ships as a MAJOR
  version with a documented migration (shape selection / FK scalar replace wrapper access). Compliant
  *because* it is taken as a MAJOR with migration notes (FR-017).
- **V. Performance by Default** — *Held.* Single round-trip (no N+1), build-time SQL, no runtime
  reflection, `Utf8JsonReader` parsing, no scalar boxing. The JSON-aggregation SQL is generated, not
  runtime-compiled. Must add/maintain perf coverage for shaped reads.
- **IV. First-Class Tooling / VI. Quality** — *Held.* New grammar + SQL get Verify snapshots; runtime
  behavior gets conformance tests on both dialects; CI stays green.

**Gate result: PASS** (with the Principle III amendment recorded as a required governance step). See
Complexity Tracking for the size/splittability note.

## Project Structure

### Documentation (this feature)

```text
specs/009-shape-selection/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (JSON-agg, Utf8JsonReader, IR extensions, etc.)
├── data-model.md        # Phase 1 — AST/model + SqlIr additions + entity flattening + result types
├── quickstart.md        # Phase 1 — authored examples + generated shapes + validation
├── contracts/
│   └── query-shape-grammar.md   # The shape grammar + generated API/result-type contract
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root) — areas touched

```text
src/Dormant.SourceGeneration/
├── Parsing/Lexer.cs                 # shape tokens: `{ } : ,` in select, `into`, read `with`
├── Parsing/UnitParser.cs            # parse shape select (root-shape + free-composition), read `with`, `into`, nested order by
├── Parsing/QueryModel.cs            # NET-NEW: shape AST (ShapeNode: scalar / to-one / to-many; QuerySource list; WithBinding; IntoTarget)
├── Parsing/SchemaModel.cs           # to-many collection members as metadata; backlink/inverse info
├── Schema/SchemaValidator.cs        # relationship resolution, navigation paths, cycle guard, into-structural-match
├── Ir/SqlIr.cs                      # NET-NEW nodes: Join, SubquerySelect, SelectItem (ColumnRef|FuncCall|JsonObject|JsonAgg|ScalarSubquery), CteBinding, qualified columns
├── Ir/Dialects/*.cs                 # render joins/subqueries/CTEs + JSON funcs per dialect
├── Query/QueryEmitter.cs            # build shaped SqlIr; emit nested projection record(s); emit Utf8JsonReader materializer; `with`/`into`
├── Schema/EntityEmitter.cs          # FLATTEN: drop Ref/RefSet members; emit FK id scalar property
└── Schema/EntityBindingEmitter.cs   # unchanged flat SelectByKey; FK column DDL stays

src/Dormant.Abstractions/
├── Entities/Ref.cs, RefSet.cs, RefList.cs, RefBag.cs, RefMap.cs   # REMOVE (wrappers deleted)
└── Querying/                        # IFieldReader stays; add a JSON-row reader helper if needed for the emitted parsers

src/Dormant.Core/, src/Dormant.Provider.{Sqlite,PostgreSql}/   # execute shaped statements (single text/JSON column path); verify JSON funcs available

samples/**, tests/Dormant.Providers.ConformanceTests/**, tests/Dormant.SourceGeneration.Tests/**   # migrate off Ref; add shape snapshots + conformance shape tests
```

**Structure Decision**: No new projects. The work extends the existing generator pipeline and the two
renderers, deletes the wrapper types from `Dormant.Abstractions.Entities`, and adds generator-emitted
nested materializers. The dialect framework (005) is the seam for the per-dialect JSON functions.

## Implementation Phasing (for /speckit-tasks)

Ordered so each phase is independently testable and the breaking change lands first:

- **P-A — Flatten entities (breaking, foundational)**: remove `Ref/RefSet/…` from `EntityEmitter`;
  emit FK id scalar; delete wrapper types; migrate samples/conformance/snapshots. After this, reads
  return flat rows + FK ids. (Satisfies SC-008, FR-015/016/017.)
- **P-B — Typed navigation in predicates/expressions** (`a.writer.name`): SqlIr `Join` + qualified
  columns + path resolution. Net-new IR. (FR-013.)
- **P-C — Root-object shape, to-one** (`select a { title, writer: { name } }`): SqlIr select-item
  expressions + `json_object` scalar subquery; emit nested record + `Utf8JsonReader` parser; single
  JSON column. MVP slice of US1.
- **P-D — to-many via JSON aggregation** (`tags: { label } [order by …]`): `json_agg`/
  `json_group_array` correlated subquery; nested list materialization; cycle guard. Completes US1;
  highest risk.
- **P-E — Free composition + read `with` CTE** (US2): multi-source FROM/CTE in IR; named-member
  shape; cascading `with`.
- **P-F — `into` user-owned record** (US3): structural match + diagnostics.
- **P-G — Polish**: cross-dialect conformance, perf coverage for shaped reads, docs, constitution
  amendment (Principle III).

## Complexity Tracking

> No constitution *violations*; this section records scope risk, not a principle breach.

| Concern | Why it's large | Mitigation / note |
|---------|----------------|-------------------|
| Net-new relational IR (joins, subqueries, CTEs, expression select-items, JSON funcs) | Current `SqlIr` is a flat single-table builder | Phases B→E add nodes incrementally; keep flat path byte-identical |
| Nested AOT materialization | Source generators don't see each other's output, so STJ source-gen can't serialize generated records | Generator emits its own `Utf8JsonReader` parsers (research R2) |
| Breaking change blast radius (Ref removal) | Touches every entity, sample, conformance test, snapshot | Land first (P-A) as its own MAJOR-tagged slice |
| **Feature size** | Spans the whole pipeline + runtime; effectively the query/projection engine | **Could be split** into "009a flatten + navigation" and "009b shape/JSON projection + with/into" if delivery risk warrants; recorded for the maintainer to decide at /speckit-tasks |
