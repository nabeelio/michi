namespace Michi.Internal;

/// <summary>
/// Lazy singletons backing <see cref="MPath.Home" /> and <see cref="MPath.Temp" />. Evaluated once
/// at first access and cached for the process lifetime.
/// </summary>
/// <remarks>
/// `MPath.CurrentDirectory` is NOT here -- it evaluates on every access because the process
/// CWD can change at runtime.
/// </remarks>
internal static class WellKnownPaths {
    /// <summary>Lazy <see cref="Environment.SpecialFolder.UserProfile" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> Home = new(
        () => MPath.From(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Path.GetTempPath" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> Temp = new(
        () => MPath.From(Path.GetTempPath()),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="AppContext.BaseDirectory" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> InstalledDirectory = new(
        () => MPath.From(AppContext.BaseDirectory),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Environment.SpecialFolder.LocalApplicationData" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> LocalApplicationData = new(
        () => MPath.From(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Environment.SpecialFolder.CommonApplicationData" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> CommonApplicationData = new(
        () => MPath.From(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Environment.SpecialFolder.ApplicationData" /> as an <see cref="MPath" />.</summary>
    public static readonly Lazy<MPath> ApplicationData = new(
        () => MPath.From(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );
}
