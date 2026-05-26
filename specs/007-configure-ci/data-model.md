# Pipeline Data Model: CI

## Pipeline
- **Triggers**: push to `main`, pull_request to `main`.
- **Environment Variables**:
  - `DOTNET_NOLOGO`: true
  - `DOTNET_CLI_TELEMETRY_OPTOUT`: true

## Jobs

### Build & Lint
- **Inputs**: Source code, `.editorconfig`.
- **Outputs**: Compiled assemblies (cached for test jobs).
- **Steps**:
  - Checkout
  - Setup .NET 10
  - Restore
  - Build (Release)
  - Lint (`dotnet format --verify-no-changes`)

### Test
- **Dependencies**: Build & Lint.
- **Services**: PostgreSQL (Docker).
- **Inputs**: Compiled assemblies, `Dormant.slnx`.
- **Outputs**: Test results (trx), Coverage (optional).
- **Steps**:
  - Run `dotnet test Dormant.slnx`.

### AOT Smoke Test
- **Dependencies**: Build & Lint.
- **Inputs**: `Dormant.Aot.SmokeTests.csproj`.
- **Outputs**: Executable binary results.
- **Steps**:
  - `dotnet publish` (AOT).
  - Run executable.
