# DormantQL for Zed

Syntax highlighting support for DormantQL (`.dql` and `.dqls` files) in the Zed editor.

## Installation (Development)

1. Clone the Dormant repository.
2. In Zed, open the Command Palette (`Cmd+Shift+P` or `Ctrl+Shift+P`).
3. Run **Extensions: Install Dev Extension**.
4. Select the folder `tooling/zed-dormantql/`.

## Features

- Syntax highlighting powered by the shared Tree-sitter grammar.
- Bracket matching and auto-indentation (via queries).
- Automatic recognition of `.dql` and `.dqls` files.

## Grammar

This extension uses the Tree-sitter grammar located at `tooling/grammar/dormantql-tree-sitter/`.

For information about contributing to the grammar or adding support for new DormantQL syntax, see the main project documentation.

## Status

This is an early implementation. More advanced features (outline, runnables, etc.) can be added later via additional `.scm` query files.