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

/// <summary>Materializes one result row from an <see cref="IFieldReader"/>; emitted by the generator.</summary>
/// <typeparam name="TRow">The row/result type.</typeparam>
/// <param name="reader">The current-row reader.</param>
/// <returns>The materialized row.</returns>
public delegate TRow RowMaterializer<out TRow>(IFieldReader reader);

/// <summary>
/// A prebuilt, parameterized SQL statement produced at build time (spec FR-013). Carries the SQL text
/// and an optional no-boxing parameter-binding callback.
/// </summary>
public sealed class PreparedStatement
{
    /// <summary>Creates a prepared statement.</summary>
    /// <param name="sql">The prebuilt SQL using positional placeholders (<c>$1</c>, <c>$2</c>, …).</param>
    /// <param name="bindParameters">Optional callback that binds parameter values without boxing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sql"/> is <see langword="null"/>.</exception>
    public PreparedStatement(string sql, Action<IParameterWriter>? bindParameters = null)
    {
        Sql = sql ?? throw new ArgumentNullException(nameof(sql));
        BindParameters = bindParameters;
    }

    /// <summary>The prebuilt SQL text.</summary>
    public string Sql { get; }

    /// <summary>Optional no-boxing parameter binder, or <see langword="null"/> when there are no parameters.</summary>
    public Action<IParameterWriter>? BindParameters { get; }
}
