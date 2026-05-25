namespace Dormant.Abstractions.Querying;

/// <summary>
/// A build-time-produced query handle whose result type <typeparamref name="TResult"/> is fully known at
/// compile time (spec FR-006/FR-013). The Dormant source generator emits one per query, pairing the
/// prebuilt, parameter-bound <see cref="Statement"/> with the no-boxing <see cref="Materialize"/>
/// delegate. Only values/predicates vary at runtime; the result type never does.
/// </summary>
/// <typeparam name="TResult">The statically-known result type (a full entity or a projection).</typeparam>
public sealed class CompiledQuery<TResult>
{
    /// <summary>Creates a compiled query from its prebuilt statement and row materializer.</summary>
    /// <param name="statement">The prebuilt, parameter-bound SQL statement.</param>
    /// <param name="materialize">The generated, no-boxing row materializer.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public CompiledQuery(PreparedStatement statement, RowMaterializer<TResult> materialize)
    {
        Statement = statement ?? throw new ArgumentNullException(nameof(statement));
        Materialize = materialize ?? throw new ArgumentNullException(nameof(materialize));
    }

    /// <summary>The prebuilt, parameter-bound statement carrying the build-time SQL.</summary>
    public PreparedStatement Statement { get; }

    /// <summary>The generated row materializer producing <typeparamref name="TResult"/> without boxing.</summary>
    public RowMaterializer<TResult> Materialize { get; }
}
