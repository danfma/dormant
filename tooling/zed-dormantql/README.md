# DormantQL for Zed

Syntax highlighting support for DormantQL (`.dql` and `.dqls` files) in the Zed editor.

## How Zed loads the grammar (important)

Zed does **not** support local or `file://` grammar paths. It clones the grammar from a git
**`repository`** at a pinned **`commit`** and compiles `src/parser.c`. Because our grammar lives in
a subdirectory of this monorepo, `extension.toml` uses:

```toml
[grammars.dormantql]
repository = "https://github.com/danfma/dormant"
path = "tooling/grammar/dormantql-tree-sitter"
commit = "<full SHA pushed to GitHub>"
```

The pinned `commit` **must be pushed to GitHub** and must contain
`tooling/grammar/dormantql-tree-sitter/src/parser.c` (the committed, generated parser).

## Installing as a dev extension

1. Push your branch so the grammar commit is on GitHub, then set `commit` in `extension.toml`
   to that SHA:
   ```sh
   git push origin <branch>
   git rev-parse origin/<branch>        # copy this SHA into extension.toml [grammars.dormantql].commit
   ```
2. In Zed, open the Command Palette (`Cmd+Shift+P`).
3. Run **zed: install dev extension**.
4. Select the folder `tooling/zed-dormantql/`.

If install fails, the usual causes are: missing `schema_version`, a `commit` that is not on the
remote, or a `commit` whose `path` does not contain `src/parser.c`.

## Regenerating the parser

The grammar source is `tooling/grammar/dormantql-tree-sitter/grammar.js`. After editing it:

```sh
cd tooling/grammar/dormantql-tree-sitter
tree-sitter generate          # regenerates src/parser.c and friends (commit them)
bash ../validate-grammar.sh   # parses fixtures + real samples and validates highlights.scm
```

Commit and push the regenerated `src/parser.c`, then bump the `commit` SHA in `extension.toml`.

## Features

- Syntax highlighting via the shared Tree-sitter grammar (keywords, types/entity names, members,
  parameters, strings, numbers, comments).
- Bracket matching and `#` line comments.
- Automatic recognition of `.dql` and `.dqls` files.

## Status

Early implementation — syntax highlighting only. Outline/runnables and richer queries can be added
later via additional `.scm` files.
