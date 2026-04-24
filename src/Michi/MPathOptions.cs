namespace Michi;

/// <summary>
/// Immutable settings for <see cref="MPath" /> construction. Derive custom options from
/// <see cref="Default" /> via `with`:
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
    /// which stays consistent across framework-dependent, self-contained, single-file, `dotnet run`,
    /// and test-host deployments. <see cref="System.IO.Directory.GetCurrentDirectory" /> is not used as
    /// the default because it varies by launch context.
    /// </summary>
    public string BaseDirectory { get; init; } = AppContext.BaseDirectory;

    /// <summary>
    /// When `true`, a leading `~` or `~/` is expanded to the current user's profile
    /// directory. Default `false`.
    /// </summary>
    public bool ExpandTilde { get; init; }

    /// <summary>
    /// When `true`, env-var references are expanded during normalization: `%VAR%` on Windows,
    /// `$VAR` and `${VAR}` on Unix. Default `false`.
    /// </summary>
    public bool ExpandEnvironmentVariables { get; init; }

    /// <summary>
    /// Read-only singleton used when no explicit options are passed. Mirrors the
    /// `JsonSerializerOptions.Default` pattern: a fixed default instance that callers
    /// override per-call via record `with` rather than mutating shared state.
    /// </summary>
    public static MPathOptions Default { get; } = new();
}
