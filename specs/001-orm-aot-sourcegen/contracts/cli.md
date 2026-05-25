# Contract: CLI tooling (`dotnet dormant`)

`Dormant.Tool` ships as a `dotnet tool`, itself AOT-publishable (FR-023). Command schema = compatibility
surface for the developer workflow (FR-020, SC-010). Comparable in scope to mainstream .NET ORM tooling
(not command-compatible).

## Commands

| Command | Purpose | Key options | Spec |
|---------|---------|-------------|------|
| `dotnet dormant migrations add <name>` | Create a migration from the schema diff vs the last applied/known state | `--project`, `--output` | FR-020/FR-021 |
| `dotnet dormant migrations apply` | Apply pending migrations | `--connection`, `--to <id>` | FR-020 |
| `dotnet dormant migrations rollback` | Revert the last (or `--to <id>`) migration | `--connection`, `--to <id>` | FR-020 |
| `dotnet dormant migrations status` | List applied + pending | `--connection` | FR-020 |
| `dotnet dormant schema validate` | Validate the DSL, print located diagnostics | `--project` | FR-004/SC-009 |

## Behavior contracts

- **Incremental diff**: `migrations add` captures only the delta vs the previous schema snapshot (FR-021).
- **Destructive ops**: a migration that drops a column/table (data loss) is **flagged** and not applied
  silently; `apply` requires an explicit confirm/flag to proceed (FR-022).
- **Status**: exit code + machine-readable (`--json`) output reporting applied/pending sets (FR-020).
- **Diagnostics**: validation failures are source-located and actionable (FR-028); non-zero exit on error.
- **No hand-edited SQL** is required anywhere in the edit→migrate→apply→rollback loop (SC-010).
