namespace Dormant.Abstractions.Querying;

/// <summary>
/// No-boxing column reader passed to generated materializers. Implementations route the generic
/// <see cref="GetValue{T}"/> to the provider's typed read path (spec FR-019).
/// </summary>
public interface IFieldReader
{
    /// <summary>Returns whether the column at <paramref name="ordinal"/> is database NULL.</summary>
    /// <param name="ordinal">Zero-based column ordinal.</param>
    /// <returns><see langword="true"/> if the column is NULL.</returns>
    bool IsNull(int ordinal);

    /// <summary>Reads the column at <paramref name="ordinal"/> as <typeparamref name="T"/> without boxing.</summary>
    /// <typeparam name="T">The CLR type to read.</typeparam>
    /// <param name="ordinal">Zero-based column ordinal.</param>
    /// <returns>The typed value.</returns>
    T GetValue<T>(int ordinal);
}