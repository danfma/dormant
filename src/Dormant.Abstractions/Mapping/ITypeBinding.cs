using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Mapping;

/// <summary>Reads/writes a column value of type <typeparamref name="T"/> without boxing (spec FR-019/FR-025).</summary>
/// <typeparam name="T">The CLR type bound to a column.</typeparam>
public interface ITypeBinding<T>
{
    /// <summary>Reads <typeparamref name="T"/> from the column at <paramref name="ordinal"/>.</summary>
    /// <param name="reader">The field reader.</param>
    /// <param name="ordinal">Zero-based column ordinal.</param>
    /// <returns>The value.</returns>
    T Read(IFieldReader reader, int ordinal);

    /// <summary>Writes <paramref name="value"/> as the positional parameter <paramref name="index"/>.</summary>
    /// <param name="writer">The parameter writer.</param>
    /// <param name="index">One-based positional parameter index.</param>
    /// <param name="value">The value to bind.</param>
    void Write(IParameterWriter writer, int index, T value);
}
