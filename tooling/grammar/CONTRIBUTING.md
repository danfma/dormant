# Contributing to the DormantQL Grammar

This directory contains the canonical grammars for DormantQL.

## Structure

- `dormantql-tree-sitter/` — Primary grammar (Tree-sitter)
- `dormantql-textmate/` — Secondary grammar (TextMate) for VS Code baseline and GitHub fallback

## When to Update the Grammar

Update the grammar whenever new syntax is added to DormantQL (new keywords, constructs, expression forms, etc.).

The Tree-sitter grammar is considered the source of truth for new features.

## Process for Language Changes

1. Add support in the Tree-sitter grammar first (`grammar.js` + relevant `.scm` queries).
2. Port the equivalent support to the TextMate grammar.
3. Update tests / example files.
4. Update the VS Code and Zed extensions if needed.
5. Update documentation.

## Future GitHub Linguist Contribution

We plan to contribute the Tree-sitter grammar to [github-linguist/linguist](https://github.com/github-linguist/linguist).

Before opening a PR:

- Ensure the grammar passes `tree-sitter test`.
- Provide a good set of sample files in `samples/` or `test/`.
- Follow Linguist's current contribution guidelines (especially the Tree-sitter section).
- Update this file with the Linguist PR link once submitted.

Current status: Not yet contributed (as of 2026).

## Testing

A basic validation script exists at `validate-grammar.sh`.

For full Tree-sitter testing, run `tree-sitter generate && tree-sitter test` inside the `dormantql-tree-sitter/` directory.