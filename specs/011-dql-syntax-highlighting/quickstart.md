# Quickstart: DormantQL Editor Integration

This guide helps you get syntax highlighting for DormantQL files (`.dql` and `.dqls`) as quickly as possible.

## 1. Visual Studio Code (Initial Focus)

**Current install path — sideload the packaged extension** (the extension is not yet published to the Marketplace):

1. Download / locate `tooling/vscode-dormantql/dormantql-0.1.0.vsix` from the repository.
2. In VS Code run **Extensions: Install from VSIX…** (Command Palette) and select that file — or from a terminal: `code --install-extension tooling/vscode-dormantql/dormantql-0.1.0.vsix`.
3. Open any `.dql` or `.dqls` file. The extension activates automatically and you immediately see highlighting for keywords, entities, queries, mutations, strings, comments, etc.

**No manual configuration is required.**

> **When published**: once the extension ships to the VS Code Marketplace (and Open VSX), you will be able to search for "DormantQL" and install it directly. Until then, use the VSIX sideload above.

If you want to hack on the extension during development:
- Clone the repository
- Open the `tooling/vscode-dormantql/` folder in VS Code
- Press `F5` to launch an Extension Development Host

## 2. Zed (Coming Shortly After VS Code)

1. Once the Zed extension is published, install it from the Zed extensions marketplace.
2. Open a DormantQL file — highlighting, bracket matching, and basic structure should work out of the box.

Until the official extension is available, you can install it as a development extension by pointing Zed at the `tooling/zed-dormantql/` directory.

## 3. Repository / GitHub Viewing (Best Effort)

For reasonable highlighting when browsing DormantQL files on GitHub:

Add the following to your repository's `.gitattributes` file:

```gitattributes
*.dql linguist-language=TypeScript
*.dqls linguist-language=TypeScript
```

This is a temporary workaround. True first-class DormantQL highlighting on GitHub will come after we contribute the Tree-sitter grammar to Linguist (planned follow-up work).

## 4. Other Editors

See `docs/editor-integration.md` (or the equivalent document in `tooling/docs/`) for instructions on using the portable grammar in other editors (Vim, Neovim with Tree-sitter, JetBrains Rider in the future, etc.).

## Next Steps

- For authors: Enjoy the improved readability while writing schemas and queries.
- For contributors/maintainers: See the full plan (`plan.md`) and research (`research.md`) for how the grammar and extensions are maintained.