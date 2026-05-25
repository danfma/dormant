namespace Dormant.Abstractions.Querying;

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
