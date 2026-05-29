# DormantQL for Visual Studio Code

Syntax highlighting and basic language support for DormantQL (`.dql` and `.dqls` files).

## Features

- Syntax highlighting for DormantQL schema (`.dqls`) and query/mutation (`.dql`) files
- Bracket matching and auto-closing
- Comment toggling
- Automatic activation — no configuration required

## Installation

1. Install the **DormantQL** extension from the VS Code Marketplace.
2. Open any `.dql` or `.dqls` file — highlighting works immediately.

## Development

To work on the extension locally:

1. Clone the Dormant repository
2. Open the `tooling/vscode-dormantql/` folder in VS Code
3. Run the "Extension: Debug" command (F5) to launch an Extension Development Host
4. Open a `.dql` or `.dqls` file to test

## Grammar

The highlighting is powered by the shared DormantQL grammar located at `tooling/grammar/`.

For information about the grammar format and how to contribute new syntax support, see the main project documentation.

## Roadmap

Future versions may include:
- Semantic tokens (via Language Server)
- Snippets
- Formatting
- Diagnostics

## License

See the root Dormant repository license.