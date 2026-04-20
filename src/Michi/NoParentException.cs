namespace Michi;

/// <summary>
/// Thrown by <see cref="MPath.Parent" /> and <see cref="MPath.Up(int)" /> when walking
/// past the root. The <see cref="Path" /> property carries the path that could not
/// produce a parent.
/// </summary>
public sealed class NoParentException : MPathException {
    /// <summary>
    /// Creates a new <see cref="NoParentException" /> with the default "no parent at root" message.
    /// </summary>
    /// <param name="path">
    /// The path that has no parent.
    /// </param>
    public NoParentException(MPath path)
            : base($"Path '{path}' has no parent (already at root).")
    {
        Path = path
            ?? throw new ArgumentNullException(
                   nameof(path),
                   "Path must not be null when constructing a NoParentException. Pass the MPath instance that triggered the failure."
               );
    }

    /// <summary>
    /// Creates a new <see cref="NoParentException" /> with a caller-specified message.
    /// </summary>
    /// <param name="path">
    /// The path that has no parent.
    /// </param>
    /// <param name="message">
    /// Custom message — used by <see cref="MPath.Up(int)" /> when the requested depth exceeds the actual depth so the message
    /// can include both values per D-35c.
    /// </param>
    public NoParentException(MPath path, string message) : base(message)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// The path that could not produce a parent.
    /// </summary>
    public MPath Path { get; }
}
