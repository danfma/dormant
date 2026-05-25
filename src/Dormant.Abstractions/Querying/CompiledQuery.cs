namespace Dormant.Abstractions.Querying;

/// <summary>
/// An opaque, build-time-produced query handle whose result type <typeparamref name="TResult"/> is
/// fully known at compile time (spec FR-006). Instances are emitted by the Dormant source generator;
/// the construction surface stabilizes with the query stories (US3/US4).
/// </summary>
/// <typeparam name="TResult">The statically-known result type (a full entity or a projection).</typeparam>
public sealed class CompiledQuery<TResult>
{
    // Intentionally minimal in Phase 2 (Foundational): the type and its consumption by ISession are
    // the contract; the payload (prepared statements + materializer + optional-parameter fragments)
    // is populated by generated code in US3/US4.
    internal CompiledQuery()
    {
    }
}
