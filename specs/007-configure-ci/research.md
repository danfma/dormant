# Research: CI Configuration for Dormant

## Decision: CI Provider & Environment
- **Decision**: GitHub Actions on `ubuntu-latest`.
- **Rationale**: standard for open-source projects, native integration with the repository, and supports service containers for PostgreSQL.
- **Alternatives considered**: Azure Pipelines (too complex for current needs).

## Decision: Linting Enforcement
- **Decision**: Use `dotnet format --verify-no-changes`.
- **Rationale**: This flag ensures that if `dotnet format` would have made changes, it returns a non-zero exit code, effectively failing the CI.
- **Implementation**: `dotnet format Dormant.slnx --verify-no-changes --severity warn`.

## Decision: Database Integration
- **Decision**: GitHub Actions Service Containers.
- **Rationale**: Native way to spin up PostgreSQL without manual Docker commands.
- **Implementation**:
  ```yaml
  services:
    postgres:
      image: postgres:latest
      env:
        POSTGRES_DB: dormant_test
        POSTGRES_PASSWORD: password
      ports:
        - 5432:5432
      options: >-
        --health-cmd pg_isready
        --health-interval 10s
        --health-timeout 5s
        --health-retries 5
  ```

## Decision: AOT Smoke Testing
- **Decision**: Publish and execute native binary.
- **Rationale**: Standard `dotnet test` doesn't verify AOT behavior. We must publish as AOT and run the resulting executable to catch trimming/AOT issues.
- **Implementation**:
  ```bash
  dotnet publish tests/Dormant.Aot.SmokeTests/Dormant.Aot.SmokeTests.csproj -r linux-x64 -c Release /p:PublishAot=true -o ./publish-aot
  ./publish-aot/Dormant.Aot.SmokeTests
  ```

## Decision: Test Execution & Reporting
- **Decision**: Use `dotnet test` with `GitHubActions` logger.
- **Rationale**: Provides rich annotations directly in the PR UI.
- **Implementation**: `dotnet test Dormant.slnx --logger "GitHubActions;summary.includePassedTests=true;summary.includeSkippedTests=true"`.
