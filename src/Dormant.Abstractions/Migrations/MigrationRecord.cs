namespace Dormant.Abstractions.Migrations;

/// <summary>A migration's identity and applied/pending bookkeeping record (spec FR-020/FR-021).</summary>
/// <param name="Id">The ordered migration id (e.g. a timestamp).</param>
/// <param name="Name">The human-readable migration name.</param>
public sealed record MigrationRecord(string Id, string Name);
