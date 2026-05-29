# Grammar Contract – DormantQL Syntax Highlighting

**Owner**: Dormant team
**Versioning**: Follows the DormantQL language versioning (tied to Dormant releases)

## Purpose

This document defines the contract for the portable DormantQL grammar so that it can be reliably consumed by:

- The official VS Code and Zed extensions
- Repository hosting platforms (GitHub, etc.)
- Future editors and tools
- Third-party projects that want DormantQL syntax highlighting

## Core Principles

1. **Single Source of Truth**
   - The grammar under `tooling/grammar/` is the canonical definition.
   - All editor integrations and external consumers should reference this location (or a published artifact derived from it).

2. **Dual Format (2026–2027 Strategy)**
   - **Primary**: Tree-sitter grammar (required for high-quality Zed support and modern GitHub highlighting).
   - **Secondary**: TextMate grammar (`.tmLanguage.json`) for VS Code baseline compatibility and GitHub fallback.

3. **Stability**
   - Changes that affect existing highlighting behavior for valid DormantQL code are considered breaking for the grammar contract.
   - Such changes require a MINOR or MAJOR bump in the associated Dormant release and must be documented.

## Deliverables

| Artifact | Format | Consumers | Stability Level |
|----------|--------|-----------|-----------------|
| Tree-sitter grammar | `grammar.js` + generated parser + `.scm` queries | Zed, GitHub (preferred), future editors | High |
| TextMate grammar | `dormantql.tmLanguage.json` | VS Code extension, GitHub fallback | Medium-High |
| Documentation | `editor-integration.md` | All consumers | High |

## Usage by External Parties

Repository owners can get reasonable highlighting today by using:

```gitattributes
*.dql linguist-language=TypeScript
*.dqls linguist-language=TypeScript
```

(or the closest existing language).

For first-class custom highlighting, they should wait for an official contribution of the Tree-sitter grammar to `github-linguist/linguist` (planned follow-up work).

## Evolution Rules

- New DormantQL syntax (new keywords, constructs, etc.) **must** receive appropriate highlighting in the same release where the language feature ships.
- Removal or semantic change of existing tokenization rules requires deprecation + migration guidance.
- The team commits to keeping the two grammar formats reasonably in sync.

## Non-Goals

- This contract does **not** cover semantic tokens, diagnostics, or language server features.
- It does **not** guarantee identical visual appearance across all editors (theme differences are expected).