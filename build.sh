#!/usr/bin/env bash
# Single documented entry point for the Dormant build (Constitution IV: First-Class Tooling).
# Usage: ./build.sh [restore|build|test|all]   (default: all)
set -euo pipefail
cd "$(dirname "$0")"

target="${1:-all}"
config="${CONFIGURATION:-Release}"

case "$target" in
  restore) dotnet restore ;;
  build)   dotnet build --no-restore -c "$config" ;;
  test)    dotnet test -c "$config" ;;
  all)
    dotnet restore
    dotnet build --no-restore -c "$config"
    dotnet test --no-build -c "$config"
    ;;
  *) echo "unknown target: $target (use restore|build|test|all)" >&2; exit 2 ;;
esac
