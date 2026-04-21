namespace Michi.Internal;

/// <summary>
/// Lazy singletons backing <see cref="MPath.Home" /> and <see cref="MPath.Temp" />. Evaluated once
/// at first access and cached for the process lifetime.
/// </summary>
/// <remarks>
/// <c>MPath.CurrentDirectory</c> is NOT here -- it evaluates on every access because the process
/// CWD can change at runtime.
/// </remarks>
internal static class WellKnownPaths {
    /// <summary>Lazy <see cref="Environment.SpecialFolder.UserProfile" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> Home = new(
        () => MPath.From(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        true
    );

    /// <summary>Lazy <see cref="Path.GetTempPath" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> Temp = new(() => MPath.From(Path.GetTempPath()), true);
}
