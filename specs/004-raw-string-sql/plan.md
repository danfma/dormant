# Implementation Plan: Generated SQL as Raw String Literals

**Branch**: `004-raw-string-sql` | **Date**: 2026-05-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-raw-string-sql/spec.md`

## Summary

Emit the build-time SQL that the DormantQL generator embeds in generated method bodies as **C# multi-line raw
string literals** (`"""…"""`) instead of escaped regular strings. The SQL *value* is unchanged; only the C#
literal syntax changes, removing the `\"` escape noise around quoted identifiers so the generated code reads
as plain SQL. Localized to the two emitters' SQL-literal emission; no runtime, IR, grammar, or SQL-semantics
change.

## Technical Context

**Language/Version**: C# 14 / .NET 10; generator targets `netstandard2.0`. Generated code (consumer) compiles
under C# 14, so raw string literals (C# 11+) are always available.

**Primary Dependencies**: Roslyn incremental generator + existing emitters (reused from `003`). No new deps.

**Storage**: PostgreSQL (unchanged — same SQL bytes).

**Testing**: TUnit generator (Verify + emit asserts) + provider against real Docker PostgreSQL + AOT smoke.

**Target Platform**: cross-platform .NET 10, Native AOT + full trimming (unaffected — source-syntax change).

**Project Type**: managed multi-package library + source generator.

**Performance Goals**: deterministic + incrementally cacheable generation; zero runtime impact.

**Constraints**: emitted SQL text byte-identical (FR-002); raw-string fence valid for any SQL content
(FR-003); non-interpolated so `$n`/`{`/`}` are verbatim (FR-004); zero new AOT/trim warnings.

**Scale/Scope**: the static, build-time-rendered SQL statement literal in `QueryEmitter` and `CommandEmitter`.
String **parameter-value** literals stay regular escaped strings. The runtime StringBuilder (optional-filter)
path is opportunistic/out-of-scope for the core change.

## Constitution Check

*GATE.* Constitution v2.0.1.

| Principle | Gate | Status |
|-----------|------|--------|
| I. Developer Experience First | Generated code is a surface developers read; raw SQL literals remove escape noise → more readable/reviewable. | PASS (advances) |
| II. Interface & Compatibility Stability | Generated-code contract: the SQL *value* and method shapes are unchanged; only the literal syntax differs. Snapshot baselines updated in the same change. | PASS |
| III. Statically-Known, Safe-by-Default | Unaffected — no result-type or query-shape change. | PASS |
| IV. First-Class Tooling | Generator + Verify snapshots updated; CI-gated. | PASS |
| V. Performance by Default | Pure source-syntax change; no runtime/AOT effect; generation stays deterministic + cacheable. | PASS |
| VI. Quality & Testing (NON-NEGOTIABLE) | Emit/snapshot tests updated to the raw form; provider suite re-run to prove identical SQL/results. | PASS |

**Result: no violations.** Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/004-raw-string-sql/
├── plan.md · research.md · quickstart.md · data-model.md
├── contracts/generated-code.md
└── tasks.md   (/speckit-tasks)
```

### Source Code — files this feature touches

```text
src/Dormant.SourceGeneration/
├── Emit/EmitHelpers.cs        # ADD a shared raw-SQL-literal emitter (fence-aware, multi-line) + helper to
│                              #   compute the quote-fence length from the SQL content
├── Query/QueryEmitter.cs      # EmitStaticStatement: emit the SELECT SQL via the raw-SQL helper (was Quote(sql))
└── Command/CommandEmitter.cs  # EmitStatement: emit INSERT/UPDATE/DELETE SQL via the raw-SQL helper
                               #   (KEEP Quote(value.Text) for string PARAMETER VALUES — those stay regular)

tests/Dormant.SourceGeneration.Tests/   # update emit asserts + Verify snapshots from \"-escaped → raw form
# UNCHANGED: Ir/SqlIr.cs + SqlRenderer (SQL text), runtime, provider, schema/entity emit
```

**Structure Decision**: A single shared helper in `EmitHelpers` produces the raw-string literal for a SQL
statement; both emitters call it where they currently call `Quote(sql)`. Everything else — the SQL string
itself (from `SqlRenderer`), the bind callbacks, parameter-value literals, the dynamic StringBuilder path —
is untouched.

## Complexity Tracking

> No violations — section intentionally empty.

## Phase notes

- **Phase 0 (research)**: [research.md](./research.md) — why multi-line raw (single-line raw cannot end with
  `"`, and our SQL ends with `"id"`); fence-length computation; indentation/dedent interplay with
  `SourceWriter`; non-interpolated literal preserves `$n`/braces; scope (static SQL vs parameter-value
  literals vs dynamic builder).
- **Phase 1 (design)**: [contracts/generated-code.md](./contracts/generated-code.md) (before/after emitted
  shape), [data-model.md](./data-model.md) (the generated-SQL-literal element), [quickstart.md](./quickstart.md).
  `CLAUDE.md` plan pointer updated.
- **Implementation order (for /speckit-tasks)**: add the raw-SQL helper (+ fence calc) → switch QueryEmitter
  static SQL → switch CommandEmitter SQL → update emit-test asserts + Verify snapshots → re-run provider
  (real Docker) to prove identical SQL/results → AOT smoke 0-warning.
