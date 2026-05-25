namespace Dormant.Abstractions.Ports;

/// <summary>Catalog of provider-native functions/operators available for type-checked invocation.</summary>
public interface INativeFunctionCatalog
{
    /// <summary>Attempts to resolve a native function signature.</summary>
    /// <param name="providerScope">The provider scope.</param>
    /// <param name="name">The function/operator name.</param>
    /// <param name="signature">The signature when found.</param>
    /// <returns><see langword="true"/> if a matching signature exists.</returns>
    bool TryGet(string providerScope, string name, out NativeFunctionSignature? signature);
}