namespace Dormant.Core.Diagnostics;

/// <summary>
/// Base type for runtime errors raised by Dormant. Messages are actionable: they state what failed,
/// why, and the next corrective step (spec FR-028).
/// </summary>
public class DormantException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public DormantException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The actionable error message.</param>
    public DormantException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The actionable error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public DormantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
