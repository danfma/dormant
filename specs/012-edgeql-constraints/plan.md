# Implementation Plan: EdgeQL-Style Constraints

**Branch**: `012-edgeql-constraints` | **Date**: 2026-05-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-edgeql-constraints/spec.md`

## Summary

Replace DormantQL's ad-hoc trailing **type-modifier** rules (`primary`, `concurrency`, `db("…")`)
with a uniform, EdgeQL-inspired **constraint system**: named constraints declared in a `{ constraint
…; }` block on a member or at the entity level, drawn from a defined standard library with familiar
names (`exclusive`→`unique`, `expression`→`check`, `max_len_value`→`max_length`, …). Constraints may
span multiple members (`constraint unique on (a, b)`), carry arbitrary boolean expressions
(`constraint check (…)`), and pin the generated database constraint name via `as {name}`. The
feature also adds **custom scalar types** (`scalar Username extending str { … }`) and **entity
inheritance/composition** (`abstract entity` + `extending`). `primary` and `concurrency` are
re-expressed as constraints. The old modifier syntax is **removed (clean break, MAJOR)** with a
migration guide; the model mirrors Gel/EdgeDB as closely as reasonable.

## Technical Context

**Language/Version**: C# 14 / .NET 10. Roslyn **incremental source generator** (`netstandard2.0`),
runtime libs `net10.0`.

**Primary Dependencies**: Roslyn (`Microsoft.CodeAnalysis`), the existing hand-written DormantQL
lexer/parser, the neutral `SqlIr` + multi-dialect renderers (PostgreSQL + SQLite, spec 005),
`EntityBindingEmitter` (DDL), `SchemaInitializer` (EnsureCreated). Editor grammar tooling (Feature
011: Tree-sitter + TextMate + Zed).

**Storage**: N/A at the library layer; constraints are emitted into generated **DDL** and enforced
by the target database (PostgreSQL primary, SQLite secondary).

**Testing**: TUnit; cross-provider **conformance** suite (PostgreSQL via Testcontainers + SQLite
in-memory) for constraint enforcement; **Verify** source-generator snapshots + cacheability; diagnostic
unit tests; `tooling/grammar/validate-grammar.sh` for the 011 grammar.

**Target Platform**: .NET 10 consuming projects; AOT-clean.

**Project Type**: ORM source generator + DSL front-end + multi-dialect SQL emit (single solution,
`Dormant.slnx`), plus editor tooling under `tooling/`.

**Performance Goals**: All constraint SQL produced at **build time** (no runtime query/DDL
compilation, Principle V). No new hot-path allocations or reflection.

**Constraints**: AOT-clean, zero trimming/AOT warnings; generated SQL byte-stable per dialect;
constraint names deterministic. SQLite lacks native `REGEXP` → documented fallback (R-01).

**Scale/Scope**: One DSL surface change touching lexer→parser→model→validator→IR→DDL→dialect
renderers→bindings, plus scalar types, inheritance, all repo `.dqls` migration, the 011 grammar, and
a migration guide. **Breaking (MAJOR).**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Developer Experience First** — Uniform constraint syntax is more discoverable and composable
  than ad-hoc modifiers; mirrors a known model (EdgeQL). Every error path gets a source-located,
  actionable diagnostic (FR-009). ✅
- **II. Interface & Compatibility Stability** — This is a **deliberate breaking (MAJOR) change** to
  the DSL grammar surface: old modifier syntax removed (FR-012), `primary`/`concurrency` re-expressed
  (FR-013). Allowed under Principle II *with* a MAJOR bump + documented migration path. Tracked in
  Complexity Tracking (BC-1); the migration guide is a deliverable (P-F). The generated-code contract
  changes (DDL gains constraints); generated entity-type shapes are unchanged. ⚠️→ justified.
- **III. Statically-Known, Safe-by-Default Data Access** — Constraints are **schema-only metadata**
  driving DDL; they do not alter query result types or introduce runtime load-state. Flat entities
  unchanged. ✅
- **IV. First-Class Tooling** — The 011 grammar (Tree-sitter + TextMate + Zed) is updated in the same
  release for the new syntax (FR-011, 011 FR-005/SC-005); `validate-grammar.sh` + samples updated. ✅
- **V. Performance by Default** — Constraint DDL is generated at build time and enforced by the DB;
  no runtime reflection/compilation, no hot-path cost, AOT-clean. ✅
- **VI. Quality & Testing Discipline** — Cross-provider conformance proves enforcement on PG + SQLite;
  Verify snapshots cover generated DDL; each new diagnostic (ORM029+) gets a unit test; the DSL
  compatibility baseline + migration are updated in the same change. ✅

**Result**: PASS with one justified, intentional breaking change (BC-1). No unjustified violation.
A MAJOR version bump is required and recorded.

## Project Structure

### Documentation (this feature)

```text
specs/012-edgeql-constraints/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (SQLite regex, check-expr subset, inheritance model…)
├── data-model.md        # Phase 1 — schema model + IR node additions
├── quickstart.md        # Phase 1 — author-facing constraint/scalar/inheritance guide + migration
├── contracts/           # Phase 1
│   ├── constraint-dsl-contract.md   # the schema DSL surface (constraint/scalar/extending grammar)
│   └── constraint-ddl-contract.md   # neutral IR → per-dialect DDL mapping (PG + SQLite)
├── checklists/
│   └── requirements.md  # spec quality checklist (done)
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root) — touch map

```text
src/Dormant.SourceGeneration/
├── Parsing/
│   ├── Lexer.cs                 # no new tokens; keywords stay identifiers (parser-checked)
│   ├── SchemaParser.cs          # NEW: member block (constraint + annotation, function-call/named
│   │                            #      args, optional parens), entity-level constraints/annotations,
│   │                            #      `scalar … extending …`, `abstract`/`extending`;
│   │                            #      replace ParseModifiers() (incl. db("…") → column annotation)
│   ├── SchemaModel.cs           # NEW records: ConstraintModel(+ConstraintArg), AnnotationModel,
│   │                            #      ScalarTypeModel; extend EntityModel (Extends, IsAbstract,
│   │                            #      EntityConstraints, EntityAnnotations), PropertyModel
│   │                            #      (Constraints, Annotations); REMOVE NameOverride
│   └── SchemaValidator.cs       # NEW: unknown/typed-mismatch constraint, missing member,
│                                #      `as` collision, scalar base, inheritance conflict/cycle,
│                                #      unknown annotation + constraint/annotation on ref/collection
├── Diagnostics/
│   ├── DiagnosticDescriptors.cs # ORM029+ (constraint/scalar/inheritance diagnostics)
│   └── ../AnalyzerReleases.Unshipped.md
├── Ir/
│   ├── SqlIr.cs                 # NEW: ConstraintDef nodes (Unique/Check/Primary/…); extend
│   │                            #      CreateTableStatement with table-level constraints +
│   │                            #      named-constraint support on ColumnDef
│   └── Dialects/
│       ├── SqlDialectRendererBase.cs  # NEW: RenderConstraints(); extend RenderCreateTable()
│       ├── PostgreSqlRenderer.cs      # named CONSTRAINT, CHECK, UNIQUE, regex via `~`
│       └── SqliteRenderer.cs          # CHECK/UNIQUE (named inline); regex fallback (R-01)
├── Schema/
│   └── EntityBindingEmitter.cs  # carry constraints + inherited/scalar members → CreateTableStatement;
│                                #      abstract entities emit no table
└── Emit/EmitHelpers.cs          # TypeMap → scalar-aware resolution (custom scalar → base CLR)

tooling/grammar/ + tooling/{vscode,zed}-dormantql/   # 011 grammar: keywords, constraint block,
                                                     # scalar/extending/abstract; regenerate parser;
                                                     # update fixtures + validate-grammar.sh; bump Zed SHA

samples/**/*.dqls, tests/**/schema/*.dqls            # MIGRATE all schemas to new syntax (BC-1)
```

**Structure Decision**: Reuse the existing one-direction front-end → IR → dialect-renderer pipeline.
Constraints become **new neutral IR nodes** rendered per dialect (same pattern as 005/009), not
string concatenation. No new project; editor tooling stays under `tooling/`.

## Phasing (high-risk core first)

- **P-A — Front-end & model** (US1 base, FR-001/003/013): lexer keyword set; parser constraint
  blocks (member + entity) + `primary`/`concurrency` as constraints; `SchemaModel` additions;
  validator + ORM029+ diagnostics. Migrate all repo `.dqls`. *No behavior change to generated entity
  types.*
- **P-B — Constraint IR + DDL** (US1/US2/US3, FR-002/004/005/006/010): `ConstraintDef` IR;
  `RenderConstraints` in base + PG + SQLite; `as` naming + deterministic defaults; multi-field
  `unique on (…)`; `check (expr)` reusing the expression IR; PK/concurrency DDL. **Highest risk.**
- **P-C — Scalar types** (US4, FR-007): scalar registry; member typing resolves scalar→base CLR +
  inherited constraints; DDL applies them.
- **P-D — Inheritance/composition** (US5, FR-008): `abstract`/`extending`; flatten inherited
  members + constraints into concrete entities; abstract entities emit no table; conflict diagnostics.
- **P-E — Grammar 011**: update Tree-sitter + TextMate + Zed for new syntax; regenerate parser;
  fixtures + `validate-grammar.sh`; bump Zed grammar commit.
- **P-F — Migration, docs, conformance**: migration guide; cross-provider conformance (PG + SQLite)
  for each constraint kind; Verify DDL snapshots; AOT smoke; DSL compatibility baseline + MAJOR bump.

## Complexity Tracking

| ID | Item | Why Needed / Justification |
|----|------|----------------------------|
| BC-1 | Breaking (MAJOR) DSL change: remove old modifier syntax, re-express `primary`/`concurrency` | Intentional per spec clarification (clean break). Principle II permits an incompatible DSL change with a MAJOR bump + migration path. Mitigation: migration guide (P-F), all repo schemas migrated in-PR, grammar updated same cycle, diagnostics flag removed syntax (reuse the ORM020 "removed syntax" pattern). |
| RX-1 | SQLite has no native `REGEXP` | Documented fallback for the `regex` constraint on SQLite (research R-01): emit a `CHECK` only where a GLOB/LIKE approximation is faithful, otherwise skip DB-level enforcement with a recorded, logged limitation — never silently claim enforcement. |
