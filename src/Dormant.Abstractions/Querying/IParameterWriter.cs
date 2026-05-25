namespace Dormant.Abstractions.Querying;

/// <summary>
/// No-boxing parameter binder used by generated, prebuilt statements. Implementations route the
/// generic <see cref="Write{T}"/> to the provider's typed parameter path (spec FR-019/FR-041).
/// </summary>
public interface IParameterWriter
{
    /// <summary>Binds <paramref name="value"/> as the positional parameter <paramref name="index"/>.</summary>
    /// <typeparam name="T">The CLR type of the value.</typeparam>
    /// <param name="index">One-based positional parameter index (<c>$1</c>, <c>$2</c>, …).</param>
    /// <param name="value">The value to bind.</param>
    void Write<T>(int index, T value);
}