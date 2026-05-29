#!/usr/bin/env bash
# Grammar validation for DormantQL (Feature 011).
# Runs from tooling/grammar/. Verifies the portable grammar artifacts and, when the
# tree-sitter CLI is available, parses real + edge-case fixtures (escaped strings, a very
# large file) to catch regressions. Without the CLI it falls back to structural checks so
# the script is still useful in minimal environments (and in CI as a smoke gate).
set -euo pipefail

cd "$(dirname "$0")"
GRAMMAR_DIR="$PWD"
REPO_ROOT="$(cd ../.. && pwd)"

fail() { echo "✗ $1"; exit 1; }

echo "Validating DormantQL grammars..."

# 1. TextMate grammar must be valid JSON.
if command -v jq >/dev/null 2>&1; then
  jq empty dormantql-textmate/dormantql.tmLanguage.json 2>/dev/null \
    && echo "✓ TextMate grammar is valid JSON" \
    || fail "TextMate grammar is invalid JSON"
else
  echo "• jq not found — skipping TextMate JSON check"
fi

# 2. Tree-sitter grammar.js must look like a grammar definition.
if [ -f dormantql-tree-sitter/grammar.js ]; then
  grep -q "module.exports = grammar" dormantql-tree-sitter/grammar.js \
    && echo "✓ Tree-sitter grammar.js looks like a valid grammar definition" \
    || fail "Tree-sitter grammar.js does not look like a valid grammar"
else
  fail "Tree-sitter grammar.js not found"
fi

# 3. Edge-case fixtures must exist (spec.md Edge Cases: escaped/raw strings, special chars).
for fx in fixtures/edge-cases.dqls fixtures/edge-cases.dql; do
  [ -f "$fx" ] && echo "✓ fixture present: $fx" || fail "missing fixture: $fx"
done

# 4. If the tree-sitter CLI is available, generate the parser and parse fixtures + a very
#    large generated file (large-file edge case) with NO parse errors.
if command -v tree-sitter >/dev/null 2>&1; then
  echo "• tree-sitter CLI found — generating parser and parsing fixtures + real samples"
  ( cd dormantql-tree-sitter && tree-sitter generate >/dev/null )

  parse() {
    # tree-sitter parse exits non-zero and prints (ERROR ...) nodes on failure.
    ( cd dormantql-tree-sitter && tree-sitter parse "$1" >/dev/null 2>&1 ) \
      && echo "  ✓ parsed: ${1#"$REPO_ROOT"/}" \
      || fail "parse errors in $1"
  }

  # Edge-case fixtures (escaped strings, dense operators).
  parse "$GRAMMAR_DIR/fixtures/edge-cases.dqls"
  parse "$GRAMMAR_DIR/fixtures/edge-cases.dql"

  # Real repository samples — regression guard so the grammar keeps pace with the DSL
  # (FR-005 / SC-005). The conformance catalog exercises 009 shapes, into, and navigation.
  parse "$REPO_ROOT/samples/Dormant.Sample.Quickstart/schema/app.dqls"
  parse "$REPO_ROOT/samples/Dormant.Sample.Quickstart/schema/app.dql"
  parse "$REPO_ROOT/tests/Dormant.Providers.ConformanceTests/schema/catalog.dqls"
  parse "$REPO_ROOT/tests/Dormant.Providers.ConformanceTests/schema/catalog.dql"

  # Large-file edge case: synthesize ~2000 entities and confirm it parses.
  big="$(mktemp -t dormantql-big-XXXX).dqls"
  {
    echo "module big;"
    for i in $(seq 1 2000); do
      printf 'entity E%d { id: uuid primary; name: str; n: int; }\n' "$i"
    done
  } > "$big"
  ( cd dormantql-tree-sitter && tree-sitter parse "$big" >/dev/null 2>&1 ) \
    && echo "  ✓ parsed large generated file (2000 entities) without errors" \
    || { rm -f "$big"; fail "parse errors in large generated file"; }
  rm -f "$big"

  # Highlight query must stay valid against the grammar.
  ( cd dormantql-tree-sitter && tree-sitter query src/highlights.scm \
      "$GRAMMAR_DIR/fixtures/edge-cases.dqls" >/dev/null 2>&1 ) \
    && echo "  ✓ highlights.scm query is valid against the grammar" \
    || fail "highlights.scm query failed to compile against the grammar"
else
  echo "• tree-sitter CLI not found — skipping parser-level fixture checks"
  echo "  (install with: npm i -g tree-sitter-cli, then re-run for full validation)"
fi

echo "DormantQL grammar validation passed."
