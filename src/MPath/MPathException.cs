namespace Michi;

/// <summary>
/// Base exception for all Michi-specific failure modes. Catch this type to handle
/// any MPath-originated error uniformly.
/// </summary>
/// <remarks>
/// Every exception raised directly by Michi derives from <see cref="MPathException" />
/// (the argument-exception family — <see cref="ArgumentNullException" />,
/// <see cref="ArgumentOutOfRangeException" /> — is used for precondition violations
/// on parameters per .NET convention).
/// </remarks>
public class MPathException : Exception {
    /// <summary>
    /// Creates a new <see cref="MPathException" />.
    /// </summary>
    /// <param name="message">
    /// Human-readable message describing the failure.
    /// </param>
    public MPathException(string message) : base(message) { }

    /// <summary>
    /// Creates a new <see cref="MPathException" /> that wraps another exception.
    /// </summary>
    /// <param name="message">
    /// Human-readable message describing the failure.
    /// </param>
    /// <param name="innerException">
    /// The exception that caused the current one.
    /// </param>
    public MPathException(string message, Exception innerException) : base(message, innerException) { }
}
