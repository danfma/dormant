# CI Quickstart

## Local Replication

To run the CI checks locally before pushing:

### 1. Build and Lint
```bash
# Build the solution
dotnet build Dormant.slnx -c Release

# Check formatting (will fail if changes are needed)
dotnet format Dormant.slnx --verify-no-changes --severity warn
```

### 2. Run Tests
Ensure you have a PostgreSQL instance running if you want to run integration tests.
```bash
# Run all tests in the solution
dotnet test Dormant.slnx
```

### 3. Run AOT Smoke Tests
```bash
# Publish as AOT (requires native tools installed locally)
dotnet publish tests/Dormant.Aot.SmokeTests/Dormant.Aot.SmokeTests.csproj -r osx-arm64 -c Release /p:PublishAot=true -o ./publish-aot

# Run the native binary
./publish-aot/Dormant.Aot.SmokeTests
```
*Note: Replace `osx-arm64` with your local Runtime Identifier (e.g., `linux-x64`, `win-x64`).*

## Troubleshooting

- **Linting Failure**: Run `dotnet format Dormant.slnx` to automatically fix style issues.
- **AOT Failure**: Check for `IL2026` or `IL3050` warnings in the build logs.
