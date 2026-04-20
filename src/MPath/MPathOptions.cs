namespace Michi;

/// <summary>
/// Immutable settings that govern <see cref="MPath" /> construction. Use the record's
/// <c>
/// with
/// </c>
/// expression to derive a customized instance from <see cref="Default" />:
/// <code>
/// var opts = MPathOptions.Default with { ExpandTilde = true };
/// var p = MPath.From("~/config", opts);
/// </code>
/// </summary>
/// <remarks>
///     <para>
///     Options affect construction and normalization only. They do not retroactively
///     change the equality, hashing, or ordering of already-constructed <see cref="MPath" />
///     instances.
///     </para>
///     <para>
///     The <see cref="Default" /> property is
///     <b>
///     read-only
///     </b>
///     (no setter) and holds a
///     singleton <see cref="MPathOptions" /> with all properties at their default values.
///     Consumers who need process-wide custom defaults should wrap their own static
///     accessor:
///     <code>
/// public static class AppPaths
/// {
///     public static MPathOptions Options { get; } = MPathOptions.Default with
///     {
///         BaseDirectory = "/opt/myapp",
///         ExpandTilde = true,
///     };
/// }
/// // usage: MPath.From("~/config", AppPaths.Options);
/// </code>
///     Keeping the global-state decision on the consumer side (not library side) avoids
///     the test-isolation pitfalls that a mutable static
///     <c>
///     Default
///     </c>
///     would introduce
///     under parallel xUnit collections.
///     </para>
/// </remarks>
public sealed record MPathOptions {
    /// <summary>
    /// Base directory used by <see cref="MPath.From(string, MPathOptions?)" /> to
    /// resolve relative inputs. Defaults to <see cref="AppContext.BaseDirectory" />,
    /// which is consistent across framework-dependent, self-contained, single-file,
    /// <c>
    /// dotnet run
    /// </c>
    /// , and test-host deployment modes. (<see cref="System.IO.Directory.GetCurrentDirectory" />
    /// is deliberately NOT used as the default because it varies by launch context.)
    /// </summary>
    public string BaseDirectory { get; init; } = AppContext.BaseDirectory;

    /// <summary>
    /// When
    /// <c>
    /// true
    /// </c>
    /// , leading
    /// <c>
    /// ~
    /// </c>
    /// or
    /// <c>
    /// ~/
    /// </c>
    /// in a path is expanded to the
    /// current user's profile folder (<see cref="Environment.GetFolderPath(Environment.SpecialFolder)" />
    /// with <see cref="Environment.SpecialFolder.UserProfile" />). Default
    /// <c>
    /// false
    /// </c>
    /// .
    /// </summary>
    public bool ExpandTilde { get; init; }

    /// <summary>
    /// When
    /// <c>
    /// true
    /// </c>
    /// , environment-variable references embedded in a path
    /// (
    /// <c>
    /// %VAR%
    /// </c>
    /// on Windows,
    /// <c>
    /// $VAR
    /// </c>
    /// and
    /// <c>
    /// ${VAR}
    /// </c>
    /// on Unix) are expanded
    /// during normalization. Default
    /// <c>
    /// false
    /// </c>
    /// .
    /// </summary>
    public bool ExpandEnvironmentVariables { get; init; }

    /// <summary>
    /// Read-only singleton used when no explicit <see cref="MPathOptions" /> is passed
    /// to <see cref="MPath.From(string, MPathOptions?)" /> et al. Holds an instance
    /// with all properties at their default values (<see cref="AppContext.BaseDirectory" />,
    /// <c>
    /// ExpandTilde = false
    /// </c>
    /// ,
    /// <c>
    /// ExpandEnvironmentVariables = false
    /// </c>
    /// ).
    /// </summary>
    /// <remarks>
    /// Matches the <see cref="System.Text.Json.JsonSerializerOptions.Default" /> pattern:
    /// a read-only singleton plus per-call override ergonomics via the record's
    /// <c>
    /// with
    /// </c>
    /// expression.
    /// </remarks>
    public static MPathOptions Default { get; } = new();
}
