namespace Michi.Exceptions;

/// <summary>
/// Base exception for Michi-specific failures. Catch this to handle any MPath-originated error.
/// </summary>
/// <remarks>
/// Every Michi-raised exception derives from <see cref="MPathException" />. Parameter-precondition
/// violations use the standard .NET argument-exception family (<see cref="ArgumentNullException" />,
/// <see cref="ArgumentOutOfRangeException" />).
/// </remarks>
public class MPathException : Exception {
    /// <param name="message">Human-readable description of the failure.</param>
    public MPathException(string message) : base(message) { }

    /// <param name="message">Human-readable description of the failure.</param>
    /// <param name="innerException">The underlying cause.</param>
    public MPathException(string message, Exception innerException) : base(message, innerException) { }
}
