# Tasks: DQL Syntax Highlighting

**Feature**: 011-dql-syntax-highlighting
**Branch**: 011-dql-syntax-highlighting
**Input**: plan.md, spec.md, research.md, data-model.md, contracts/

**Prerequisites**: All design documents listed above

**Tests**: Not explicitly requested in the specification. Grammar/extension validation tasks are
included where natural for correctness (Tree-sitter tests + `validate-grammar.sh` + cross-editor
review checklist), per Constitution Principle VI.

**Organization**: Tasks are grouped by user story to enable independent implementation and delivery.

## Format Reminder

- `- [ ] T001 [P?] [USx?] Description with exact file path`
- `[P]` = can run in parallel (different files, no dependency on incomplete tasks)
- `[US1]`, `[US2]`, `[US3]` = maps to User Story priority from spec.md

---

## Phase 1: Setup (Shared Tooling Infrastructure)

**Purpose**: Initialize the `tooling/` tree (outside the .NET solution) for grammar + extension work.

- [X] T001 Create top-level `tooling/` directory tree per plan.md (grammar/, vscode-dormantql/, zed-dormantql/, docs/)
- [X] T002 Create `tooling/grammar/` with subfolders `dormantql-tree-sitter/` and `dormantql-textmate/` for the portable grammar
- [X] T003 Create `tooling/vscode-dormantql/` skeleton for the VS Code extension
- [X] T004 [P] Initialize `tooling/vscode-dormantql/package.json` skeleton (name, publisher, engines)
- [X] T005 [P] Create placeholder `tooling/docs/editor-integration.md`
- [X] T006 [P] Add `tooling/README.md` and `tooling/RELEASES.md` overview/release-notes files

**Checkpoint**: Tooling tree exists and is ready for grammar and extension development.

---

## Phase 2: Foundational – Portable Grammar + Contracts

**Purpose**: Establish the single source of truth grammar (Tree-sitter primary + TextMate secondary)
and supporting contracts. **Blocks all user stories.**

**⚠️ CRITICAL**: No user story implementation begins until this phase is substantially complete.

- [X] T007 [P] Build the Tree-sitter grammar in `tooling/grammar/dormantql-tree-sitter/grammar.js` (nodes for entities, fields, relationship types, query/mutation units, keywords, types, params, aliases, strings, numbers, comments, punctuation)
- [X] T008 [P] Author the TextMate grammar `tooling/grammar/dormantql-textmate/dormantql.tmLanguage.json` covering the same constructs (backtracking-safe regexes), aligned with research.md R-01 parity decisions
- [X] T009 Author `tooling/grammar/dormantql-tree-sitter/src/highlights.scm` with capture names for the FR-003 semantic categories (keywords/types/comments strong; params/aliases basic)
- [X] T010 Create shared `tooling/grammar/language-configuration.json` (line/block comments, brackets `{}[]()`, auto-closing + surrounding pairs)
- [X] T011 Finalize `specs/011-dql-syntax-highlighting/contracts/grammar-contract.md` (single-source-of-truth, dual-format, stability/evolution rules)
- [X] T012 Finalize `specs/011-dql-syntax-highlighting/contracts/vscode-extension-contract.md`
- [X] T013 Write `tooling/docs/editor-integration.md` with consumption instructions for the grammar across editors/platforms
- [X] T014 Create `tooling/grammar/validate-grammar.sh` to test the grammar against real `.dql`/`.dqls` example files in the repo

**Checkpoint**: A working portable grammar exists in `tooling/grammar/` and loads in at least one consumer. Contracts defined.

---

## Phase 3: User Story 1 – Readable DormantQL Files in Primary Editor (Priority: P1) 🎯 MVP

**Goal**: Ship a proper VS Code extension giving good highlighting for `.dql`/`.dqls`, auto-activating.

**Independent Test**: Install the VS Code extension (source or `.vsix`), open existing `.dql`/`.dqls`
files from the repo, confirm keywords, entity definitions, query/mutation blocks, strings, and
comments are visually distinct and useful.

### Implementation for User Story 1

- [X] T015 [US1] Complete `tooling/vscode-dormantql/package.json` (`contributes.languages` id `dormantql` + extensions `.dql`/`.dqls`, `contributes.grammars`, activation, metadata) per vscode-extension-contract.md
- [X] T016 [US1] Vendor the TextMate grammar into `tooling/vscode-dormantql/syntaxes/dormantql.tmLanguage.json` and reference it in package.json
- [X] T017 [US1] Add `tooling/vscode-dormantql/language-configuration.json` (comments, brackets, auto-closing pairs)
- [X] T018 [US1] Implement minimal `tooling/vscode-dormantql/src/extension.ts` that activates on DormantQL files
- [X] T019 [US1] Write `tooling/vscode-dormantql/README.md` with install + usage instructions
- [X] T020 [US1] Validate the extension in an Extension Development Host (F5) against real DormantQL files
- [X] T021 [US1] Package the extension to `tooling/vscode-dormantql/dormantql-0.1.0.vsix` (vsce) and verify sideloading
- [X] T022 [US1] Update root `README.md` to point to the VS Code extension as the primary way to get DormantQL highlighting

**Checkpoint**: US1 complete. A developer installs the extension and immediately gets good highlighting for both `.dql` and `.dqls`.

---

## Phase 4: User Story 2 – Consistent Experience Across Primary Editors (Priority: P2)

**Goal**: Add Zed support using the shared Tree-sitter grammar, comparable in quality to VS Code.

**Independent Test**: Install the Zed extension (dev or published), open the same files as US1, verify
highlighting quality is comparable (keywords/entities/strings/comments clearly distinguished) even if
theme colors differ.

### Implementation for User Story 2

- [X] T023 [US2] Create `tooling/zed-dormantql/` structure
- [X] T024 [US2] Author `tooling/zed-dormantql/extension.toml` referencing the shared Tree-sitter grammar (repo + commit) per research.md R-02
- [X] T025 [US2] Add Zed language config (`config.toml`) registering `dormantql`, extensions, and comments
- [X] T026 [US2] Provide Zed `highlights.scm` (and `indents.scm` if valuable) tuned for DormantQL, kept in sync with `tooling/grammar/`
- [X] T027 [US2] Write `tooling/zed-dormantql/README.md`
- [X] T028 [US2] Document installing the Zed extension as a dev extension in `tooling/docs/editor-integration.md`
- [X] T029 [US2] Validate highlighting in Zed against the same files used for VS Code testing
- [X] T030 [US2] Update `tooling/docs/editor-integration.md` with Zed instructions and known differences vs VS Code

**Checkpoint**: US1 + US2 functional. Switching between VS Code and Zed gives useful, consistent highlighting.

---

## Phase 5: User Story 3 – Improved Contribution and Code Review Experience (Priority: P3)

**Goal**: Best-effort highlighting when viewing DormantQL files on GitHub and other repo platforms.

**Independent Test**: View a DormantQL file/diff on GitHub after applying the recommended
`.gitattributes` mapping; confirm reasonable coloring (even via a generic language mapping).

### Implementation for User Story 3

- [X] T031 [US3] Finalize recommended `.gitattributes` patterns (`*.dql`/`*.dqls` → `linguist-language=TypeScript`) and document in `tooling/docs/editor-integration.md`
- [X] T032 [US3] Update `tooling/grammar/` README / editor-integration docs with current GitHub (and GitLab) best practice
- [X] T033 [US3] Prepare Tree-sitter grammar metadata/structure for a future `github-linguist/linguist` contribution PR
- [X] T034 [US3] Add documentation examples showing repo owners how to get the best possible highlighting today
- [X] T035 [US3] Add a "DormantQL on GitHub" section to root `README.md` or `tooling/docs/`

**Checkpoint**: US3 complete. Repo owners have clear, documented instructions for usable GitHub highlighting while a first-class Linguist contribution is pending.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final quality, documentation, and long-term maintainability benefiting all user stories.

- [X] T036 [P] Review and polish all docs (`tooling/docs/editor-integration.md`, extension READMEs, `quickstart.md`)
- [X] T037 Verify `tooling/grammar/` is the single source of truth and both extensions reference it without drift
- [X] T038 [P] Ensure `tooling/grammar/validate-grammar.sh` runs as a smoke test against real DormantQL files
- [X] T039 Update `CLAUDE.md` with the tooling structure and how to work on syntax highlighting
- [X] T040 Add `tooling/grammar/CONTRIBUTING.md` explaining how future grammar changes and editor support are handled
- [X] T041 Review the full feature against spec.md + research.md decisions; close any remaining gaps
- [X] T042 Prepare release notes / changelog entry in `tooling/RELEASES.md` for the first delivery

**Checkpoint**: Feature complete across US1–US3 plus polish. GitHub support intentionally "best effort" this release — stated clearly in docs.

---

## Phase 7: Deferred / Follow-up (tracked, NON-BLOCKING for v1)

**Purpose**: Items surfaced by `/speckit-analyze` and recorded in plan.md (Complexity Tracking CI-1
+ Web-scope G1). None block the v1 release; tracked here so they are not silently dropped
(Constitution Principle IV/VI).

- [X] T043 [P] [CI-1] Wire `tooling/grammar/validate-grammar.sh` into `.github/workflows/ci.yml` as a dedicated lightweight `grammar` job (per plan.md Complexity Tracking CI-1)
- [X] T044 [P] [G1] Added root `.gitattributes` mapping `*.dql`/`*.dqls` → `linguist-language=TypeScript` (best-effort GitHub viewing; documented as copyable for repo owners)
- [X] T045 [P] [A1] Added a cross-editor parity rubric to `specs/011-dql-syntax-highlighting/checklists/requirements.md` (FR-003 categories × pass/fail per VS Code and Zed)
- [X] T046 [P] [D1] Fixed distribution wording in `specs/011-dql-syntax-highlighting/quickstart.md`: `.vsix` sideload as current path, Marketplace "when published"
- [X] T047 [P] [U1] Added edge-case fixtures (`tooling/grammar/fixtures/edge-cases.{dql,dqls}`: escaped strings, dense operators) + large-file parse check in `validate-grammar.sh`

**Checkpoint**: Follow-up items resolved; CI verifies the grammar, parity is rubric-checked, docs accurate.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Can start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1. **Blocks all user stories.**
- **Phase 3 (US1 – VS Code)**: Depends on Phase 2.
- **Phase 4 (US2 – Zed)**: Depends on Phase 2; benefits from Phase 3 for consistency validation.
- **Phase 5 (US3 – Web)**: Depends on Phase 2; can run in parallel with Phase 4 once Phase 3 is stable.
- **Phase 6 (Polish)**: Starts after the desired user stories are complete.

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories. Primary MVP.
- **US2 (P2)**: Benefits from US1 for consistency validation; otherwise independent after foundational grammar.
- **US3 (P3)**: Largely independent after foundational grammar + docs. Lowest risk.

### Parallel Opportunities

- Most Phase 1 setup tasks (T004, T005, T006) run in parallel.
- Grammar development splits: Tree-sitter (T007/T009) vs TextMate (T008) on different files.
- Documentation writing parallels implementation in later phases.
- US3 (Phase 5) can begin as soon as grammar + basic docs exist — does not require the VS Code UI.

---

## Implementation Strategy & MVP Recommendation

**Recommended MVP**: **User Story 1 only** (VS Code extension with good highlighting) — largest
immediate value for the majority of Dormant users with the smallest surface area.

**Incremental Delivery Path**:
1. Phase 1 + Phase 2 → Foundation
2. Phase 3 → MVP (VS Code)
3. Phase 4 → Add Zed (US2)
4. Phase 5 → Improve web experience (US3)
5. Phase 6 → Polish and long-term maintainability

**Risk Mitigation**: The dual grammar (Tree-sitter + TextMate) is the largest long-term maintenance
cost. Keep the canonical grammar in `tooling/grammar/` and treat drift between the two formats as a
defect (T037).

---

## Status

Core feature tasks T001–T042 are complete (`[X]`); the implementation is present under `tooling/`
(Tree-sitter grammar, TextMate grammar, VS Code extension + `.vsix`, Zed extension, docs). GitHub
support ships intentionally as "best effort" via `.gitattributes` mapping; a first-class Linguist
contribution remains planned follow-up work.

Phase 7 (T043–T047) — follow-up items from `/speckit-analyze` (CI wiring, repo `.gitattributes`,
parity rubric, doc wording, edge-case fixtures) — are now **complete** (`[X]`). Grammar validation
runs in CI (`grammar` job); `validate-grammar.sh` passes locally (tree-sitter CLI optional, falls
back to structural checks).

**Grammar hardening (during T047 verification with the tree-sitter CLI)**: running the validator
with `tree-sitter generate` exposed that the Tree-sitter grammar (T007/T009) had never actually
been generated and contained an unresolved parse conflict. The grammar was fixed and extended to
parse all real repository `.dql`/`.dqls` files (including the 009 conformance catalog: member
access, `#` comments, `order by asc/desc`, root-object + nested shapes, `into`). `highlights.scm`
was upgraded to distinguish types/members/parameters and synced to the Zed copy; generated parser
artifacts + `tree-sitter.json` are committed. See `tooling/RELEASES.md` (Unreleased).
