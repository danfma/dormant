namespace Dormant.Abstractions.Querying;

/// <summary>
/// A build-time-produced write command handle (spec FR-002/FR-005/FR-012): the symmetric write counterpart
/// of <see cref="CompiledQuery{TResult}"/>. The Dormant source generator emits one per authored DQL
/// <c>insert</c>/<c>update</c>/<c>delete</c> command, pairing the prebuilt (possibly CTE) statement with the
/// no-boxing materializer for its result. Only parameter values vary at runtime; the SQL and result type are
/// fixed at build time.
/// </summary>
/// <typeparam name="TResult">The statically-known result type (an entity or a projection).</typeparam>
public sealed class CompiledCommand<TResult>
{
    /// <summary>Creates a compiled command from its prebuilt statement and result materializer.</summary>
    /// <param name="statement">The prebuilt, parameter-bound statement (with <c>RETURNING</c> / CTE as needed).</param>
    /// <param name="materialize">The generated, no-boxing result materializer.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public CompiledCommand(PreparedStatement statement, RowMaterializer<TResult> materialize)
    {
        Statement = statement ?? throw new ArgumentNullException(nameof(statement));
        Materialize = materialize ?? throw new ArgumentNullException(nameof(materialize));
    }

    /// <summary>The prebuilt, parameter-bound statement carrying the build-time SQL.</summary>
    public PreparedStatement Statement { get; }

    /// <summary>The generated row materializer producing <typeparamref name="TResult"/> without boxing.</summary>
    public RowMaterializer<TResult> Materialize { get; }
}
