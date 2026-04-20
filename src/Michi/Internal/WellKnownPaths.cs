namespace Michi.Internal;

/// <summary>
/// Lazy singletons backing <see cref="MPath.Home" /> and <see cref="MPath.Temp" />.
/// Both are evaluated exactly once at first access, then cached for the process lifetime.
/// </summary>
/// <remarks>
///     <para>
///     <c>
///     MPath.CurrentDirectory
///     </c>
///     is deliberately NOT here — it must evaluate on every access
///     because the process CWD can change at runtime.
///     </para>
/// </remarks>
internal static class WellKnownPaths {
    /// <summary>
    /// Lazy-initialized <see cref="Environment.SpecialFolder.UserProfile" /> as an <see cref="MPath" />.
    /// </summary>
    public static readonly Lazy<MPath> Home = new(
        () => MPath.From(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        true
    );

    /// <summary>
    /// Lazy-initialized <see cref="Path.GetTempPath" /> as an <see cref="MPath" />.
    /// </summary>
    public static readonly Lazy<MPath> Temp = new(
        () => MPath.From(Path.GetTempPath()),
        true
    );
}
