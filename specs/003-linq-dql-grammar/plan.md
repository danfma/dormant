# Implementation Plan: LINQ-Style DQL Grammar

**Branch**: `003-linq-dql-grammar` | **Date**: 2026-05-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-linq-dql-grammar/spec.md`

## Summary

A **front-end (grammar) replacement** for DormantQL authored units. The prior `002` surface (`command Name(…)
= insert E { f := p };`, `query Name(…) = select E filter .x = p;`, leading-dot members, `:=` assignment,
single-`=` comparison, `and` keyword) is replaced by a **LINQ-/SQL-hybrid, brace-delimited grammar** with
**explicit aliases** and **C#/TypeScript operators**:

```
query users_by_email(email: string) {
  from User u
  where u.email == email
  select u
}

mutation create_user(id: uuid, email: string, created_at: datetime, version: int) {
  insert User u {
    u.id = id
    u.email = email
    u.created_at = created_at
    u.version = version
  }
}
```

The change is confined to the generator's **front end** (lexer tokens, parsers, parsed models, naming of unit
methods) plus the **emitters'** mapping from the new models onto the existing SQL IR. The **runtime is
untouched**: the SQL IR + renderer, immutable entity emission, `IEntityBinding`, the thin session, the
PostgreSQL provider, naming/overrides, schema-qualified DDL, jsonb, and Native AOT all carry over from `002`
unchanged. All `002` *semantics* are preserved (immutable results, command-driven writes, app-assigned PKs,
`<ref>_id` FK columns, count-based optimistic concurrency).

New capabilities introduced by the grammar (beyond a 1:1 surface swap): `query`/`mutation` identifiers
(GraphQL-style), result-type **inference** with optional **`returning`** shaping (mirrors `select`),
**multi-command** mutation blocks with **`with`-bindings** flowing values between commands, and
**snake_case unit names → PascalCase C# methods**.

Technical approach is fixed in [research.md](./research.md); design artifacts are [data-model.md](./data-model.md),
[contracts/](./contracts/), and [quickstart.md](./quickstart.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`); generator/analyzer target `netstandard2.0`.

**Primary Dependencies**: Roslyn incremental generators (hand-written lexer/recursive-descent parser, no
external parser lib); reused `002` IR/renderer/runtime; Npgsql slim; `System.Text.Json` sourcegen (jsonb).
Test/tooling: TUnit (Microsoft.Testing.Platform), Verify.TUnit + Verify.SourceGenerators, Testcontainers
(PostgreSQL), Microsoft.CodeAnalysis.PublicApiAnalyzers.

**Storage**: PostgreSQL (primary). No new storage capability; multi-command mutations execute as a statement
sequence within the existing session transaction (single-statement nested CTE remains out of scope).

**Testing**: TUnit unit + generator (Verify snapshots + cacheability); Testcontainers real PostgreSQL (never
mocks); AOT publish smoke (zero warnings). Grammar diagnostics asserted via generator tests.

**Target Platform**: cross-platform .NET 10, Native AOT + full trimming.

**Project Type**: managed multi-package library + source generator (front-end change within it).

**Performance Goals**: build-time SQL only (no runtime query compilation); deterministic + incrementally
cacheable generation for the new grammar (FR-013); no boxing/reflection/warm-up regressions vs `002`.

**Constraints**: single canonical grammar (removed `002` forms MUST NOT parse — FR-015); located diagnostics
for every grammar violation (FR-009); result types fixed at build time incl. `returning`/trailing reads;
logical connectives are symbolic (`&&`/`||`/`!`); newline-separated statements (no required `;`).

**Scale/Scope**: v1 = lexer operators + recursive-descent parser for `query`/`mutation` brace blocks (aliases,
clause order, `where`/`order by`/`select`/`set`/`returning`), result inference, multi-command + `with`,
snake→Pascal unit naming, and full migration of samples/tests off the `002` syntax. Out of scope: single-
statement nested CTE writes, dynamic/runtime DQL (macros), joins/advanced EdgeQL, richer return beyond
`returning`/trailing read.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.* Constitution v2.0.1.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | LINQ/SQL/C#-familiar grammar; explicit aliases remove leading-dot ambiguity; symbolic operators match host languages; `query`/`mutation` self-describe intent; located diagnostics; <15-min quickstart (SC-007). | PASS |
| II. Interface & Compatibility Stability | This is a **MAJOR DSL-surface change** (incompatible grammar replacement). Justified: **pre-1.0, no released consumers**; `002` is an unreleased fork. The generated-code/public-API surfaces stay materially the same (same methods, same result types); only the *authored DSL* changes. Recorded in Complexity Tracking. | PASS (justified) |
| III. Statically-Known, Safe-by-Default | Result types fixed at build time for `query`/`mutation` incl. `returning`/trailing read; projections distinct; accessing a non-selected member is a compile error; immutability preserved. **Strengthened** (inference never widens a type at runtime). | PASS |
| IV. First-Class Tooling | Generator + DSL diagnostics + Verify/PublicApi baselines + single CI entry + Testcontainers; grammar is the tooling contract and gets dedicated snapshot/cacheability tests. | PASS |
| V. Performance by Default | Build-time SQL only; deterministic + cacheable generation; no boxing/reflection/warm-up; multi-command = statement sequence (no new hot-path cost). | PASS |
| VI. Quality & Testing (NON-NEGOTIABLE) | TUnit generator (incl. removed-syntax-rejected diagnostics) + real-provider integration + AOT smoke, CI-gated; repro-test-before-fix; baselines updated in the same change. | PASS |

**Result: one justified deviation** (MAJOR DSL change, pre-1.0) — see Complexity Tracking. No other
violations.

## Project Structure

### Documentation (this feature)

```text
specs/003-linq-dql-grammar/
├── plan.md          # This file
├── research.md      # Phase 0 — grammar/lexer/parser/operators/return-inference/extension decisions
├── data-model.md    # Phase 1 — parsed-model (AST) shapes for query/mutation in the new grammar
├── quickstart.md    # Phase 1 — author a schema + query + mutation, round-trip in the new grammar
├── contracts/
│   ├── dql-grammar.md      # the authored grammar (EBNF-ish) + diagnostics
│   └── generated-code.md   # method shapes/result inference the generated code must honor
└── tasks.md         # /speckit-tasks output (NOT created here)
```

### Source Code (repository root) — files this feature touches

```text
src/Dormant.SourceGeneration/
├── Parsing/
│   ├── Lexer.cs            # ADD == != && || ! ; repurpose = as assignment; retire := :: -> (removed forms)
│   ├── QueryModel.cs       # ADD alias; alias-qualified columns; Returning shape (entity/projection/scalar)
│   ├── CommandModel.cs     # ADD alias; new operators; Returning; multi-command sequence; with-bindings
│   ├── QueryParser.cs      # REWRITE: brace `query { from..where..order by..select }`, clause order, aliases
│   ├── CommandParser.cs    # REWRITE: brace `mutation { insert|update|delete .. where .. set .. returning }`
│   └── (SchemaParser.cs)   # UNCHANGED (.dqls schema grammar is not part of this change)
├── Emit/NamingConvention.cs # ADD snake_case→PascalCase for unit method names (FR-016)
├── Query/QueryEmitter.cs    # map alias-qualified members + new operators onto existing IR
├── Command/CommandEmitter.cs# map new operators + returning + multi-command/with onto existing IR
├── Diagnostics/DiagnosticDescriptors.cs # ADD grammar diagnostics (alias, clause order, removed-syntax)
└── DormantGenerator.cs      # unit-file glob (.dql holds query+mutation); pipeline wiring unchanged shape

# UNCHANGED (runtime / back end): Ir/SqlIr.cs + renderer, Schema/EntityEmitter.cs,
# Schema/EntityBindingEmitter.cs, Dormant.Abstractions/*, Dormant.Core/* (Session), Dormant.Provider.PostgreSql/*

tests/
├── Dormant.SourceGeneration.Tests/  # rewrite query/command emit + cacheability for new grammar; add
│                                     #   removed-syntax-rejected + alias/clause-order diagnostic tests
└── Dormant.Provider.PostgreSql.Tests/ # migrate .dql units to query/mutation; add returning + multi-command
samples/Dormant.Sample.Quickstart/    # migrate app.query/app.dql units to the new grammar (already drafted)
tests/Dormant.Aot.SmokeTests/         # migrate smoke .dql; keep zero-warning publish
```

**Structure Decision**: Reuse the `002` multi-package layout wholesale. This feature is a **vertical change
through the generator's front end only**: lexer → parsers → parsed models → emitters → naming, plus a sweep of
authored `.dql` units in samples/tests. The SQL IR, renderer, entity/binding emission, session, and provider
are **not modified** — the emitters continue to target the same IR, only mapping from the new model shapes and
operator set. This keeps the blast radius in `Parsing/` + the two emitters + naming, with the rest of the
codebase shielded.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| MAJOR incompatible DSL change (Principle II) without a deprecation window | The user is deliberately reshaping the language pre-1.0 toward a LINQ/SQL/C# surface; the `002` grammar is an unreleased fork with no external consumers. A clean replacement avoids carrying a dead grammar. | Maintaining both grammars (additive/deprecated coexistence) doubles the parser surface and diagnostics, contradicts FR-015 (single canonical surface), and provides no value with zero released consumers. |

## Phase notes

- **Phase 0 (research)**: [research.md](./research.md) — lexer token additions/retirements; recursive-descent
  grammar + clause-order enforcement; operator → SQL mapping (`==`→`=`, `!=`→`<>`, `&&`→`AND`, `||`→`OR`,
  `!`→`NOT`); result-type inference + `returning`/trailing-read shaping; multi-command + `with` value flow;
  snake→Pascal unit naming; unit-file extension decision; what stays untouched.
- **Phase 1 (design)**: [data-model.md](./data-model.md) (parsed AST shapes for the new grammar),
  [contracts/](./contracts/) (authored grammar + diagnostics; generated-code/result-inference contract),
  [quickstart.md](./quickstart.md). `CLAUDE.md` plan pointer updated to this plan.
- **Implementation order (for /speckit-tasks)**: lexer operators (+retire removed) → QueryParser rewrite
  (read MVP, US1) → snake→Pascal naming → QueryEmitter map → CommandParser rewrite insert (US2) +
  result-id inference → CommandEmitter map → `returning` shaping → update/delete + where + count (US3) →
  projection select (US4) → multi-command + `with` → migrate samples/tests + removed-syntax diagnostics (US5)
  → cacheability/AOT/Verify baselines.
- **Out of scope / deferred**: single-statement nested-CTE writes (`002` US2 form), dynamic/runtime DQL
  (future "macros"), joins/advanced EdgeQL, richer mutation return beyond `returning`/trailing read.
