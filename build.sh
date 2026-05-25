#!/usr/bin/env bash
# Single documented entry point for the Dormant build (Constitution IV: First-Class Tooling).
# Usage: ./build.sh [restore|build|test|all]   (default: all)
set -euo pipefail
cd "$(dirname "$0")"

target="${1:-all}"
config="${CONFIGURATION:-Release}"

# TUnit runs on Microsoft.Testing.Platform; on the .NET 10 SDK the legacy `dotnet test` VSTest path is
# unsupported, so each test project is executed directly as its MTP host.
TEST_PROJECTS=(
  tests/Dormant.Core.Tests
  tests/Dormant.SourceGeneration.Tests
  tests/Dormant.Provider.PostgreSql.Tests
  tests/Dormant.Spatial.PostgreSql.Tests
)

run_tests() {
  for proj in "${TEST_PROJECTS[@]}"; do
    echo ">> $proj"
    dotnet run --no-build -c "$config" --project "$proj"
  done
}

case "$target" in
  restore) dotnet restore ;;
  build)   dotnet build --no-restore -c "$config" ;;
  test)    run_tests ;;
  all)
    dotnet restore
    dotnet build --no-restore -c "$config"
    run_tests
    ;;
  *) echo "unknown target: $target (use restore|build|test|all)" >&2; exit 2 ;;
esac
