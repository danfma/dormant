# DormantQL Editor Integration Guide

This document explains how to get good syntax highlighting for DormantQL (`.dql` and `.dqls` files) in various editors and environments.

> **Current Status (2026)**: Best experience is available in VS Code. Zed support is available via dev extension. GitHub and other repository viewers have best-effort support.

## Visual Studio Code (Recommended)

Install the official **DormantQL** extension from the VS Code Marketplace.

The extension activates automatically for `.dql` and `.dqls` files and provides:
- Syntax highlighting
- Bracket matching
- Comment toggling
- Basic auto-closing pairs

No manual configuration is required.

## Zed

1. In Zed, open the Command Palette and run **Extensions: Install Dev Extension**.
2. Select the folder `tooling/zed-dormantql/` from the Dormant repository.

The extension provides syntax highlighting powered by the shared Tree-sitter grammar, along with bracket matching and basic structure support.

Note: For the best experience, the Tree-sitter grammar under `tooling/grammar/dormantql-tree-sitter/` must be present.

## Repository Viewers (GitHub, GitLab, etc.)

### Current Best-Effort Support

Because DormantQL is a custom language, full first-class syntax highlighting on public GitHub requires a contribution to [github-linguist/linguist](https://github.com/github-linguist/linguist). Until that happens, you can get reasonable results using language mapping.

Add this to your repository's `.gitattributes`:

```gitattributes
*.dql linguist-language=TypeScript
*.dqls linguist-language=TypeScript
```

**Tips for better results:**
- TypeScript usually gives decent keyword/string/comment coloring for DormantQL.
- You can try other languages (e.g., `Rust`, `Go`, or `JSON`) if they match your style better.
- This only affects language detection and highlighting — it does **not** give you DormantQL-specific tokens.

### Future First-Class Support

We maintain a proper Tree-sitter grammar (the recommended path for GitHub in 2026). Once we contribute it to Linguist, DormantQL files will get accurate, native highlighting on GitHub without any `.gitattributes` workaround.

See `tooling/grammar/` for the current grammar and contribution notes.

### GitLab / Other Platforms

Most platforms that support TextMate or Tree-sitter grammars can use the artifacts in `tooling/grammar/`. Check the platform’s documentation for custom language support.

## Other Editors

The grammar artifacts live in `tooling/grammar/`. They can be used in:
- Any editor that supports TextMate grammars
- Editors with good Tree-sitter support (by referencing the grammar repository)

Detailed instructions will be added as support expands.

## Contributing

See the main project documentation for how changes to the DormantQL language should be reflected in the grammar and extensions.

## Known Limitations & Future Work (as of first release)

- The grammars are functional for the core DormantQL constructs but are not yet exhaustive (some newer or edge-case syntax may have incomplete highlighting).
- VS Code extension is distributed as a local `.vsix` or development extension. A Marketplace release is planned.
- Zed support currently requires installing as a development extension.
- GitHub highlighting is best-effort via language mapping. First-class support requires contributing the Tree-sitter grammar to Linguist (tracked in `tooling/grammar/CONTRIBUTING.md`).

Feedback and contributions to improve the grammars and extensions are very welcome.