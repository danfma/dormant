using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Entities;

/// <summary>
/// The build-time-generated binding between an entity type and the database: no-reflection
/// materialization plus prebuilt SQL. One implementation is emitted per entity and registered into
/// <see cref="EntityBindings"/> at module initialization (research §10). This is how the
/// provider-agnostic session dispatches to generated code without reflection.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityBinding<TEntity>
    where TEntity : class
{
    /// <summary>Materializes an entity from the current reader row (value columns in fixed order).</summary>
    /// <param name="reader">The field reader positioned on a row.</param>
    /// <returns>The materialized entity.</returns>
    TEntity Materialize(IFieldReader reader);

    /// <summary>Builds the prebuilt INSERT statement for <paramref name="entity"/> (all mapped columns).</summary>
    /// <param name="entity">The entity to insert.</param>
    /// <returns>The parameterized statement.</returns>
    PreparedStatement Insert(TEntity entity);

    /// <summary>Builds the prebuilt SELECT-by-primary-key statement.</summary>
    /// <param name="key">The primary-key value.</param>
    /// <returns>The parameterized statement.</returns>
    PreparedStatement SelectByKey(object key);
}
