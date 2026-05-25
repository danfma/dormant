using System.Collections.Generic;

namespace Dormant.Abstractions.Entities;

/// <summary>
/// Registry of generated <see cref="IEntityBinding{TEntity}"/> instances. Each consuming assembly's
/// generated code registers its bindings here from a <c>[ModuleInitializer]</c> (runs before
/// <c>Main</c>), so the session resolves them without reflection (research §10).
/// </summary>
public static class EntityBindings
{
    private static readonly Dictionary<System.Type, object> Bindings = [];

    /// <summary>Registers the binding for <typeparamref name="TEntity"/> (called from generated code).</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="binding">The generated binding.</param>
    public static void Register<TEntity>(IEntityBinding<TEntity> binding)
        where TEntity : class
        => Bindings[typeof(TEntity)] = binding;

    /// <summary>Resolves the binding for <typeparamref name="TEntity"/>.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The registered binding.</returns>
    /// <exception cref="InvalidOperationException">No binding is registered for the type.</exception>
    public static IEntityBinding<TEntity> Get<TEntity>()
        where TEntity : class
        => Bindings.TryGetValue(typeof(TEntity), out var binding)
            ? (IEntityBinding<TEntity>)binding
            : throw new InvalidOperationException(
                $"No Dormant entity binding registered for '{typeof(TEntity)}'. Ensure its schema is compiled by the generator.");
}
