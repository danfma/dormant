namespace Dormant.Abstractions.Native;

/// <summary>The declared signature of a provider-native function/operator (spec FR-039).</summary>
/// <param name="ProviderScope">The provider scope the function is declared under (e.g. <c>postgres</c>).</param>
/// <param name="Name">The function/operator name.</param>
/// <param name="ParameterTypes">The declared parameter CLR type names, in order.</param>
/// <param name="ReturnType">The single, statically-known return CLR type name.</param>
public sealed record NativeFunctionSignature(
    string ProviderScope,
    string Name,
    IReadOnlyList<string> ParameterTypes,
    string ReturnType
);
