namespace Michi;

/// <summary>
/// Immutable settings for <see cref="MPath" /> construction. Derive custom options from
/// <see cref="Default" /> via <c>with</c>:
/// <code>
/// var opts = MPathOptions.Default with { ExpandTilde = true };
/// var p = MPath.From("~/config", opts);
/// </code>
/// </summary>
/// <remarks>
/// Options only affect construction and normalization. They do not retroactively change the
/// equality, hashing, or ordering of paths that are already constructed.
/// <para>
/// <see cref="Default" /> is read-only. If you need process-wide custom defaults, wrap your own
/// static accessor:
/// <code>
/// public static class AppPaths
/// {
///     public static MPathOptions Options { get; } = MPathOptions.Default with
///     {
///         BaseDirectory = "/opt/myapp",
///         ExpandTilde = true,
///     };
/// }
/// </code>
/// Keeping global state on the consumer side avoids test-isolation problems under parallel test runners.
/// </para>
/// </remarks>
public sealed record MPathOptions {
    /// <summary>
    /// Base directory used to resolve relative inputs. Defaults to <see cref="AppContext.BaseDirectory" />,
    /// which stays consistent across framework-dependent, self-contained, single-file, <c>dotnet run</c>,
    /// and test-host deployments. <see cref="System.IO.Directory.GetCurrentDirectory" /> is not used as
    /// the default because it varies by launch context.
    /// </summary>
    public string BaseDirectory { get; init; } = AppContext.BaseDirectory;

    /// <summary>
    /// When <c>true</c>, a leading <c>~</c> or <c>~/</c> is expanded to the current user's profile
    /// directory. Default <c>false</c>.
    /// </summary>
    public bool ExpandTilde { get; init; }

    /// <summary>
    /// When <c>true</c>, env-var references are expanded during normalization: <c>%VAR%</c> on Windows,
    /// <c>$VAR</c> and <c>${VAR}</c> on Unix. Default <c>false</c>.
    /// </summary>
    public bool ExpandEnvironmentVariables { get; init; }

    /// <summary>
    /// Read-only singleton used when no explicit options are passed. Mirrors the
    /// <see cref="System.Text.Json.JsonSerializerOptions.Default" /> pattern.
    /// </summary>
    public static MPathOptions Default { get; } = new();
}
