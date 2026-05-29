# Implementation Plan: DQL Syntax Highlighting

**Branch**: `011-dql-syntax-highlighting` | **Date**: 2026-05-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/011-dql-syntax-highlighting/spec.md`

## Summary

Make the DormantQL DSL a first-class citizen in editors by shipping syntax highlighting
for `.dql` (operations) and `.dqls` (schema) files. The approach centers on a **portable,
team-owned grammar** maintained in `tooling/grammar/`: a **Tree-sitter** grammar as the
canonical source of truth (consumed by Zed and future Tree-sitter hosts) plus a **TextMate**
grammar (`.tmLanguage.json`) as the secondary artifact consumed by the **VS Code extension**.
Repository/web viewing (GitHub etc.) is best-effort via a `.gitattributes` generic language
mapping now, with a planned Linguist contribution later.

Delivery order per clarifications: build the shared grammar foundation first, then advance the
VS Code and Zed extensions in a coordinated/parallel fashion. v1 must give **strong, comparable**
distinction for keywords, types/entity names, and comments in both VS Code and Zed; parameters
and aliases may start with basic treatment and improve in follow-ups.

## Technical Context

**Language/Version**: Tree-sitter grammar in JavaScript (`grammar.js`) + generated C parser;
TextMate grammar in JSON; VS Code extension in TypeScript (Node 18+); Zed extension config in TOML;
Tree-sitter queries in Scheme (`.scm`).

**Primary Dependencies**: `tree-sitter` CLI (grammar codegen/test), VS Code Extension API
(`vscode` engine + `vsce` for packaging), Zed extension manifest format, GitHub Linguist (future).

**Storage**: N/A — no runtime data. Artifacts are files under `tooling/`.

**Testing**: Tree-sitter corpus/highlight tests + `tooling/grammar/validate-grammar.sh`; real
DormantQL sample files from the repo as fixtures; manual cross-editor review checklist
(`checklists/requirements.md`). No .NET test project is involved.

**Target Platform**: VS Code (initial), Zed (parallel follow-up), repository web viewers
(best-effort), JetBrains Rider and other editors (future, enabled by the portable grammar).

**Project Type**: Editor tooling / language-support extensions (outside the .NET solution).

**Performance Goals**: Highlighting must stay responsive on large files (hundreds of entities,
deeply nested queries). Tree-sitter incremental parsing covers this on its hosts; TextMate rules
must avoid catastrophic-backtracking regexes.

**Constraints**: Grammar must be designed for extensibility from day one (new keywords/constructs
add without rewrites). Quality parity between TextMate and Tree-sitter for the priority categories.
The grammar is owned/maintained in our repo, not delegated to an external canonical project.

**Scale/Scope**: One DSL, two file extensions, one grammar in two formats, two editor extensions
(VS Code + Zed), one best-effort web mapping, plus contributor/integration docs.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Developer Experience First** — Direct embodiment: highlighting lowers cognitive load,
  speeds onboarding, and makes structure scannable. Quickstart gives a zero-config install path. ✅
- **II. Interface & Compatibility Stability** — The grammar itself becomes a maintained artifact;
  it must track DSL evolution (FR-005/SC-005) and is treated as an interface surface (data-model
  "Evolution"). No change to the DSL grammar/semantics, public API, or generated-code contract —
  this feature only *reads* the existing DormantQL syntax. ✅
- **III. Statically-Known, Safe-by-Default Data Access** — Not applicable; no query/result types. ✅
- **IV. First-Class Tooling** — Direct embodiment: this *is* the tooling principle realized for the
  DSL's editor experience. The grammar validation script (`validate-grammar.sh`) now runs in CI via
  a dedicated `grammar` job in `.github/workflows/ci.yml` (CI-1 resolved). ✅
- **V. Performance by Default** — No .NET hot path touched; AOT/trimming N/A. Editor-side perf is
  handled by Tree-sitter incremental parsing and backtracking-safe TextMate patterns (Constraints). ✅
- **VI. Quality & Testing Discipline** — Grammar verified via Tree-sitter tests + validation script
  against real sample files; cross-editor parity tracked by a review rubric
  (`checklists/requirements.md`). The validation script runs in CI (CI-1 resolved). ✅

**Result**: PASS. No principle is violated; the prior CI deferral (CI-1) is now resolved — grammar
validation runs in CI as its own job. This feature serves Principle IV (First-Class Tooling) and
Principle I (DX) and touches no compatibility surface that would force a version bump.

## Project Structure

### Documentation (this feature)

```text
specs/011-dql-syntax-highlighting/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (component model)
├── quickstart.md        # Phase 1 output (install/usage)
├── contracts/           # Phase 1 output
│   ├── grammar-contract.md
│   └── vscode-extension-contract.md
├── checklists/
│   └── requirements.md  # cross-editor quality / acceptance checklist
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
tooling/
├── README.md                          # tooling overview
├── RELEASES.md                        # grammar/extension release notes
├── docs/
│   └── editor-integration.md          # how external editors/platforms consume the grammar
├── grammar/                           # PORTABLE GRAMMAR — canonical source of truth
│   ├── CONTRIBUTING.md                # contribution/maintenance model (R-05)
│   ├── validate-grammar.sh            # validation entry point (R-03)
│   ├── language-configuration.json    # brackets/comments/auto-close (shared base)
│   ├── dormantql-tree-sitter/
│   │   ├── grammar.js                 # primary grammar definition
│   │   └── src/highlights.scm         # Tree-sitter highlight captures
│   └── dormantql-textmate/
│       └── dormantql.tmLanguage.json  # secondary artifact (VS Code/web)
├── vscode-dormantql/                  # VS Code extension (initial focus)
│   ├── package.json                   # language registration + activation for .dql/.dqls
│   ├── language-configuration.json
│   ├── syntaxes/dormantql.tmLanguage.json
│   ├── src/extension.ts
│   ├── dormantql-0.1.0.vsix           # packaged extension (sideload)
│   └── README.md
└── zed-dormantql/                     # Zed extension (parallel follow-up)
    ├── extension.toml
    ├── languages/dormantql/
    │   ├── config.toml
    │   └── highlights.scm
    └── README.md
```

**Structure Decision**: All artifacts live under a top-level `tooling/` tree, **outside** the
.NET solution (`Dormant.slnx`), because they are editor extensions and grammars, not library code.
The portable grammar in `tooling/grammar/` is the single source of truth; `tooling/vscode-dormantql/`
and `tooling/zed-dormantql/` are consumers. The VS Code extension carries its own copy of the
TextMate grammar under `syntaxes/` (kept in sync with `tooling/grammar/dormantql-textmate/`); the
Zed extension carries its own `highlights.scm` (kept in sync with the Tree-sitter source).

### Web / repository highlighting scope (G1)

SC-003 and US3 are delivered as **best effort**: the canonical mechanism is a documented
`.gitattributes` mapping (`*.dql`/`*.dqls` → `linguist-language=TypeScript`) that **repo owners
apply to their own repositories** (quickstart.md, editor-integration.md). Adding such a mapping to
Dormant's *own* repo root is optional and deferred (it forces all `.dql`/`.dqls` to render as
TypeScript on GitHub, which is acceptable but a project-level choice, not a feature requirement).
First-class GitHub highlighting via a Linguist Tree-sitter contribution remains planned follow-up.

## Complexity Tracking

> No outstanding Constitution Check violations. CI-1 (below) is resolved.

| ID | Item | Resolution |
|----|------|------------|
| CI-1 | `validate-grammar.sh` not wired into CI | **Resolved** — added a dedicated lightweight `grammar` job to `.github/workflows/ci.yml` that runs the script (jq + optional tree-sitter CLI). Satisfies Principle IV/VI without bloating the .NET build job. |
