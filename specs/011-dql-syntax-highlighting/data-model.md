# Data Model: DQL Syntax Highlighting

**Feature**: 011-dql-syntax-highlighting

## Overview

This feature does not introduce runtime data entities. Instead, it defines a small set of **tooling components** that together deliver syntax highlighting for DormantQL.

The model below describes the main conceptual components and their relationships.

## Core Components

### 1. Portable Grammar (Canonical Source of Truth)

- **Description**: The single authoritative definition of DormantQL tokenization and highlighting rules.
- **Primary Format**: Tree-sitter grammar (grammar.js + generated parser + queries).
- **Secondary Artifact**: TextMate grammar (`.tmLanguage.json`) derived or maintained from the primary grammar.
- **Location**: `tooling/grammar/`
- **Consumers**:
  - VS Code extension (via TextMate artifact)
  - Zed extension (via Tree-sitter + queries)
  - Repository hosting platforms (best-effort via GitHub Linguist or `.gitattributes`)
  - Future editors (Rider, etc.)

**Key Properties**:
- Must be forward-compatible with new DormantQL syntax.
- Must be maintainable by the Dormant team without external approval for day-to-day changes.

### 2. VS Code Extension

- **Description**: The official first-class editor integration for VS Code.
- **Type**: Proper VS Code extension package (not a raw grammar file).
- **Responsibilities**:
  - Register the DormantQL language (`.dql`, `.dqls`).
  - Contribute the TextMate grammar for syntax highlighting.
  - Provide `language-configuration.json` (brackets, comments, auto-closing, etc.).
  - Automatic activation when opening DormantQL files.
- **Location**: `tooling/vscode-dormantql/`

### 3. Zed Extension (Follow-up after VS Code)

- **Description**: Syntax highlighting and basic editor support for the Zed editor.
- **Type**: Zed language extension.
- **Responsibilities**:
  - Reference the shared Tree-sitter grammar.
  - Provide query files (`highlights.scm`, `indents.scm`, `config.toml`, etc.).
- **Location**: `tooling/zed-dormantql/` (planned)

### 4. Grammar Contract (for External Consumers)

- **Description**: Documentation + packaging rules that allow third parties (GitHub, other editors, tools) to consume the DormantQL grammar.
- **Forms**:
  - The grammar files themselves (in `tooling/grammar/`).
  - `editor-integration.md` documentation explaining how to use the grammar in different environments.
  - Recommended `.gitattributes` patterns for repository owners.

## Relationships

- **Portable Grammar** → consumed by → VS Code Extension, Zed Extension, and external platforms.
- **VS Code Extension** → depends on → TextMate artifact of the Portable Grammar.
- **Zed Extension** → depends on → Tree-sitter grammar + queries of the Portable Grammar.
- All editor integrations ultimately serve the same goal: improving the authoring and review experience of DormantQL files (see User Stories in spec).

## Evolution

- When new DormantQL syntax is introduced (future features), the Portable Grammar is updated first.
- Downstream consumers (extensions) are updated in the same release cycle where possible (per SC-005).
- Major changes to the grammar format or contribution model are treated as interface changes (see Principle II – Interface & Compatibility Stability).

## Out of Scope for Data Model

- Language Server / semantic tokens (future work)
- Full IntelliSense, diagnostics, or refactoring support
- Build-time or runtime Dormant artifacts (entities, queries, etc.)