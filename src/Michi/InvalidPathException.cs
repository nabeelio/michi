namespace Michi;

/// <summary>
/// Thrown when a path string cannot be normalized into a valid absolute
/// <see cref="MPath" />. The <see cref="AttemptedPath" /> and <see cref="Reason" />
/// properties preserve enough context to diagnose the failure from logs alone.
/// </summary>
/// <remarks>
/// Per D-35a..D-35f (CONTEXT.md): the exception <see cref="Exception.Message" />
/// is a user-facing API subject to SemVer scrutiny. Post-1.0 breaking changes
/// to message wording that consumers might regex against are to be avoided.
/// </remarks>
public sealed class InvalidPathException : MPathException {
    /// <summary>
    /// Creates a new <see cref="InvalidPathException" />.
    /// </summary>
    /// <param name="attemptedPath">
    /// The path string that was rejected. Preserved as-is — no normalization is applied.
    /// </param>
    /// <param name="reason">
    /// Short human-readable reason (a sentence fragment suitable for inclusion in the composed message).
    /// </param>
    public InvalidPathException(string attemptedPath, string reason)
            : base($"Invalid path '{attemptedPath}': {reason}.")
    {
        AttemptedPath = attemptedPath
                     ?? throw new ArgumentNullException(
                            nameof(attemptedPath),
                            "Attempted path must not be null when constructing an InvalidPathException. Pass the original (pre-normalization) input string so the exception preserves the caller's context."
                        );

        Reason = reason
              ?? throw new ArgumentNullException(
                     nameof(reason),
                     "Reason must not be null when constructing an InvalidPathException. Pass a short human-readable sentence fragment describing why the path was rejected."
                 );
    }

    /// <summary>
    /// Creates a new <see cref="InvalidPathException" /> that wraps another exception.
    /// </summary>
    /// <param name="attemptedPath">
    /// The path string that was rejected.
    /// </param>
    /// <param name="reason">
    /// Short human-readable reason.
    /// </param>
    /// <param name="innerException">
    /// The exception that caused the rejection.
    /// </param>
    public InvalidPathException(string attemptedPath, string reason, Exception innerException)
            : base($"Invalid path '{attemptedPath}': {reason}.", innerException)
    {
        AttemptedPath = attemptedPath ?? throw new ArgumentNullException(nameof(attemptedPath));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>
    /// The original path string that was rejected, preserved exactly as supplied.
    /// </summary>
    public string AttemptedPath { get; }

    /// <summary>
    /// A short human-readable reason describing why the path was rejected.
    /// </summary>
    public string Reason { get; }
}
