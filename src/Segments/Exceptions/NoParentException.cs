namespace Segments.Exceptions;

/// <summary>
/// Thrown by <see cref="SPath.Parent" /> and <see cref="SPath.Up(int)" /> when walking past the root.
/// <see cref="Path" /> carries the path that couldn't produce a parent.
/// </summary>
public sealed class NoParentException : SPathException {
    /// <summary>Creates a <see cref="NoParentException" /> with a default "no parent at root" message.</summary>
    /// <param name="path">The path that has no parent.</param>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    public NoParentException(SPath path)
            : base($"Path '{path}' has no parent (already at root).")
    {
        Path = path
            ?? throw new ArgumentNullException(
                   nameof(path),
                   "Path must not be null. Pass the SPath instance that triggered the failure."
               );
    }

    /// <summary>Creates a <see cref="NoParentException" /> with a caller-specified message.</summary>
    /// <param name="path">The path that has no parent.</param>
    /// <param name="message">
    /// Custom message. Used by <see cref="SPath.Up(int)" /> to include both the requested ascent
    /// count and the actual depth.
    /// </param>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    public NoParentException(SPath path, string message) : base(message)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>The path that couldn't produce a parent.</summary>
    public SPath Path { get; }
}
