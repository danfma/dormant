# VS Code Extension Contract – DormantQL

**Owner**: Dormant team
**Initial Version**: 0.1.0 (tied to first release of feature 011)

## Purpose

This document defines the expected behavior and contribution surface of the official DormantQL VS Code extension.

## Scope of the Initial Extension (per clarify decisions)

- **Must deliver**:
  - Proper extension package (`package.json`)
  - Automatic language registration for `.dql` and `.dqls` files
  - Syntax highlighting via the TextMate grammar
  - Basic `language-configuration.json` (brackets, comments, auto-closing pairs)
  - Clear installation and activation experience (no manual grammar configuration required)

- **Out of scope for v1**:
  - Language Server / semantic tokens
  - Diagnostics, formatting, go-to-definition, etc.
  - Snippets or rich editing features

## Activation

The extension **must** activate automatically when a user opens a file with extension `.dql` or `.dqls`.

Activation events (example):
```json
"activationEvents": [
  "onLanguage:dormantql"
]
```

## Contributed Languages & Grammars

The extension contributes one language:

- `id`: `dormantql`
- `extensions`: [".dql", ".dqls"]
- Grammar: references the TextMate grammar from the shared `tooling/grammar/` artifact (or vendored copy)

## Language Configuration

The extension provides `language-configuration.json` covering at minimum:

- Line and block comments
- Brackets (`{}`, `[]`, `()`)
- Auto-closing pairs
- Surrounding pairs

## Maintenance & Compatibility

- The extension version should be kept in sync with relevant Dormant / DormantQL releases when grammar changes occur.
- Grammar updates that affect highlighting must be released in the extension in a timely manner (ideally same cycle as the language change).
- The extension follows the same compatibility rules as other Dormant surfaces (see Principle II).

## Distribution

- Primary distribution: VS Code Marketplace (or Open VSX for non-Microsoft environments).
- Sideloading must also be supported and documented for development and air-gapped use.

## Future Evolution

Later versions may add:
- Semantic tokens (via a future DormantQL Language Server)
- Snippets
- Formatting
- Diagnostics

These additions are explicitly allowed as long as they remain backward-compatible for users who only want highlighting.