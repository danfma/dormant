using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Entities;

/// <summary>
/// The non-generic facet of an entity binding (002 fork): read + schema metadata only. Writes go through
/// authored DQL commands, so there is no change-tracking / snapshot / write member here. Dispatches to
/// generated code with no reflection.
/// </summary>
public interface IEntityBinding
{
    /// <summary>The database schema this entity's table lives in (the module's schema, FR-045).</summary>
    string Schema { get; }

    /// <summary>The prebuilt <c>CREATE TABLE IF NOT EXISTS</c> DDL for this entity (schema-qualified, FR-020/FR-045).</summary>
    string CreateTableSql { get; }
}

/// <summary>
/// The build-time-generated binding between an entity type and the database: no-reflection
/// materialization plus the prebuilt SELECT-by-key statement (reads). One implementation is emitted per
/// entity and registered into <see cref="EntityBindings"/> at module initialization (research §10).
/// Writes are authored DQL commands, not part of the binding (002 fork).
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityBinding<TEntity> : IEntityBinding
    where TEntity : class
{
    /// <summary>Materializes an entity from the current reader row (value columns in fixed order).</summary>
    /// <param name="reader">The field reader positioned on a row.</param>
    /// <returns>The materialized (immutable) entity.</returns>
    TEntity Materialize(IFieldReader reader);

    /// <summary>Builds the prebuilt SELECT-by-primary-key statement (read identity map).</summary>
    /// <param name="key">The primary-key value.</param>
    /// <returns>The parameterized statement.</returns>
    PreparedStatement SelectByKey(object key);
}
