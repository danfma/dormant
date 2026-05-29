# Specification Quality Checklist: DQL Syntax Highlighting

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-05-27
**Feature**: [specs/011-dql-syntax-highlighting/spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- First validation pass completed. Issues found and fixed in iteration 1:
  - Reduced specific editor names in User Story 2 and Assumptions to stay more agnostic.
  - Strengthened FR-006 to be more testable.
  - Rewrote SC-001, SC-002, SC-003, and SC-004 to be more concrete and measurable without relying on vague "increases measurably" language.
- All checklist items now pass.
- No [NEEDS CLARIFICATION] markers required.
- Ready for `/speckit-plan` or `/speckit-clarify`.

## Cross-Editor Parity Rubric (FR-006 / SC-004)

Makes "comparable quality" testable. Open the same DormantQL sample files
(`samples/Dormant.Sample.Quickstart/schema/app.dqls` + `.dql`) in each editor and mark each
semantic category Pass/Fail per editor. v1 target: **Strong** categories must Pass in both;
**Basic** categories may be weaker but must still be visibly distinct.

| Semantic category (FR-003) | v1 target | VS Code (TextMate) | Zed (Tree-sitter) |
|----------------------------|-----------|--------------------|--------------------|
| Keywords (`entity`/`query`/`mutation`/`from`/`where`/`select`/`insert`/`update`/`delete`/`with`/`returning`/`set`) | Strong | [ ] Pass | [ ] Pass |
| Types & entity names (`uuid`/`string`/`int`/… + entity refs) | Strong | [ ] Pass | [ ] Pass |
| Comments | Strong | [ ] Pass | [ ] Pass |
| String literals | Strong | [ ] Pass | [ ] Pass |
| Numeric literals | Strong | [ ] Pass | [ ] Pass |
| Parameters (signature + usage) | Basic | [ ] Pass | [ ] Pass |
| Aliases (e.g. `u` in `from User u`) | Basic | [ ] Pass | [ ] Pass |
| Punctuation / operators | Basic | [ ] Pass | [ ] Pass |

**Parity verdict**: PASS when every *Strong* row is Pass in **both** editors and no *Basic* row
renders as undifferentiated plain text. Exact colors differ by theme — judge distinguishability,
not hue.

## Clarification Updates (Session 2026-05-27)

- Q1 answered (A): Repository/web syntax highlighting (GitHub etc.) remains in scope alongside local editor support.
- Q2 answered (A): Grammar must be designed for extensibility from day one because JetBrains Rider and other editors are planned for the future.
- Q3 answered (A): Initial implementation focus will be on VS Code first (to validate grammar and ecosystem), even though the user personally prefers starting with Zed. Zed comes immediately after.
- Q4 answered (B): A proper (minimal but complete) VS Code extension must be delivered, including package.json and automatic activation for .dql/.dqls files (not just a raw grammar file).
- Q5 answered (B): The grammar will be maintained in our own repository (portable/TextMate format) so GitHub and other platforms can consume it, instead of relying on contributing directly to external projects like Linguist.
- Spec updated in: Clarifications, Assumptions, FR-004.
