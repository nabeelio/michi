namespace Segments.Exceptions;

/// <summary>
/// Base exception for Segments-specific failures. Catch this to handle any SPath-originated error.
/// </summary>
/// <remarks>
/// Every Segments-raised exception derives from <see cref="SPathException" />. Parameter-precondition
/// violations use the standard .NET argument-exception family (<see cref="ArgumentNullException" />,
/// <see cref="ArgumentOutOfRangeException" />).
/// </remarks>
public class SPathException : Exception {
    /// <summary>
    /// Constructs a new <see cref="SPathException" /> with the supplied message.
    /// </summary>
    /// <param name="message">Human-readable description of the failure.</param>
    public SPathException(string message) : base(message) { }

    /// <summary>
    /// Constructs a new <see cref="SPathException" /> with the supplied message
    /// and underlying cause.
    /// </summary>
    /// <param name="message">Human-readable description of the failure.</param>
    /// <param name="innerException">The underlying cause.</param>
    public SPathException(string message, Exception innerException) : base(message, innerException) { }
}
