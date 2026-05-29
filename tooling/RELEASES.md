# DormantQL Tooling Releases

## Unreleased

### Grammar — verified end to end with the tree-sitter CLI

- The Tree-sitter grammar (`grammar.js`) now **generates cleanly** with `tree-sitter generate`
  (previously it had an unresolved `field_declaration` / `relationship_declaration` conflict and
  was never run through the CLI). Schema members are unified into a single `member_declaration`.
- Added support the real DSL needs: `#` line comments, member access (`a.writer.name`),
  binary-operator precedence, `order by … asc/desc`, root-object shapes (`select a { … }`),
  nested shapes (`writer: { … }`), comma- or whitespace-separated shape fields, and the `into`
  clause (009 US3).
- `highlights.scm` now distinguishes types/entity names (`@type`), declared members (`@property`),
  and parameters (`@variable.parameter`) from generic identifiers — and is validated against the
  grammar. The Zed copy is kept in sync.
- `validate-grammar.sh` now (when the tree-sitter CLI is present) generates the parser and parses
  the edge-case fixtures, the real repository samples (incl. the 009 conformance catalog), a
  synthesized large file, and compiles the highlight query. Wired into CI as the `grammar` job.
- Added `tree-sitter.json` metadata and committed the generated parser artifacts so consumers can
  build without the CLI.

## 2026-05 (Initial Release)

**First public delivery of DormantQL editor support.**

### Highlights

- **VS Code**: Initial release of the DormantQL extension providing syntax highlighting and basic language configuration for `.dql` and `.dqls` files. Automatic activation, no manual setup required.
- **Zed**: Initial support via development extension using the shared Tree-sitter grammar.
- **Grammar**: Portable grammar strategy established (Tree-sitter as primary + TextMate as secondary) and published under `tooling/grammar/`.
- **Documentation**: Comprehensive [Editor Integration Guide](docs/editor-integration.md) with installation instructions and best-effort guidance for GitHub and other repository viewers.
- **Repository Support**: Clear `.gitattributes` workaround documented for reasonable highlighting on GitHub today.

### Scope of this release

This release focuses on **syntax highlighting only**. More advanced language features (diagnostics, completions, formatting, semantic tokens) are planned for future iterations.

### Artifacts

- VS Code extension package: `tooling/vscode-dormantql/dormantql-0.1.0.vsix`
- Shared grammar: `tooling/grammar/`
- Zed development extension: `tooling/zed-dormantql/`

### Next Steps (planned)

- Improve grammar coverage for newer DormantQL constructs.
- Publish VS Code extension to the Marketplace.
- Contribute Tree-sitter grammar to GitHub Linguist for first-class web highlighting.
- Explore richer support in other editors (JetBrains Rider, etc.).

---

*This is the first delivery of dedicated tooling for the DormantQL language.*