namespace Dormant.Abstractions.Mapping;

/// <summary>Resolves type bindings (scalar, collection, or native) for the active provider.</summary>
public interface ITypeBindingRegistry
{
    /// <summary>Resolves the binding for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The CLR type.</typeparam>
    /// <returns>The resolved binding.</returns>
    ITypeBinding<T> Resolve<T>();
}