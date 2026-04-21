namespace Michi.Exceptions;

/// <summary>
/// Thrown when a path string can't be normalized into a valid absolute <see cref="MPath" />.
/// <see cref="AttemptedPath" /> and <see cref="Reason" /> carry enough context to diagnose from logs.
/// </summary>
/// <remarks>
/// The <see cref="Exception.Message" /> wording is part of the public API and stable across minor
/// versions. Consumers may regex against it, so message changes are minor-version-breaking.
/// </remarks>
public sealed class InvalidPathException : MPathException {
    /// <param name="attemptedPath">The path string that was rejected, preserved as-is.</param>
    /// <param name="reason">Short human-readable reason, a sentence fragment.</param>
    public InvalidPathException(string attemptedPath, string reason)
            : base($"Invalid path '{attemptedPath}': {reason}.")
    {
        AttemptedPath = attemptedPath
                     ?? throw new ArgumentNullException(
                            nameof(attemptedPath),
                            "Attempted path must not be null. Pass the original (pre-normalization) input so the exception keeps the caller's context."
                        );

        Reason = reason
              ?? throw new ArgumentNullException(
                     nameof(reason),
                     "Reason must not be null. Pass a short sentence fragment describing why the path was rejected."
                 );
    }

    /// <param name="attemptedPath">The path string that was rejected.</param>
    /// <param name="reason">Short human-readable reason.</param>
    /// <param name="innerException">The exception that caused the rejection.</param>
    public InvalidPathException(string attemptedPath, string reason, Exception innerException)
            : base($"Invalid path '{attemptedPath}': {reason}.", innerException)
    {
        AttemptedPath = attemptedPath ?? throw new ArgumentNullException(nameof(attemptedPath));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>Original path string, preserved exactly as supplied.</summary>
    public string AttemptedPath { get; }

    /// <summary>Short human-readable reason for the rejection.</summary>
    public string Reason { get; }
}
