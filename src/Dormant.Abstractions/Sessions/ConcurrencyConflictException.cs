namespace Dormant.Abstractions.Sessions;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected at commit: another session changed the
/// same row since it was loaded (spec FR-015). The conflicting change is never silently overwritten.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public ConcurrencyConflictException()
        : base("An optimistic concurrency conflict was detected.")
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
