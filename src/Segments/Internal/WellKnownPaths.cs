namespace Segments.Internal;

/// <summary>
/// Lazy singletons backing <see cref="SPath.Home" /> and <see cref="SPath.Temp" />. Evaluated once
/// at first access and cached for the process lifetime.
/// </summary>
/// <remarks>
/// `SPath.CurrentDirectory` is NOT here -- it evaluates on every access because the process
/// CWD can change at runtime.
/// </remarks>
internal static class WellKnownPaths {
    /// <summary>Lazy <see cref="Environment.SpecialFolder.UserProfile" /> as an <see cref="SPath" />.</summary>
    public static readonly Lazy<SPath> Home = new(
        () => SPath.From(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Path.GetTempPath" /> as an <see cref="SPath" />.</summary>
    public static readonly Lazy<SPath> Temp = new(
        () => SPath.From(Path.GetTempPath()),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="AppContext.BaseDirectory" /> as an <see cref="SPath" />.</summary>
    public static readonly Lazy<SPath> InstalledDirectory = new(
        () => SPath.From(AppContext.BaseDirectory),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Environment.SpecialFolder.LocalApplicationData" /> as an <see cref="SPath" />.</summary>
    public static readonly Lazy<SPath> LocalApplicationData = new(
        () => SPath.From(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Environment.SpecialFolder.CommonApplicationData" /> as an <see cref="SPath" />.</summary>
    public static readonly Lazy<SPath> CommonApplicationData = new(
        () => SPath.From(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Lazy <see cref="Environment.SpecialFolder.ApplicationData" /> as an <see cref="SPath" />.</summary>
    public static readonly Lazy<SPath> ApplicationData = new(
        () => SPath.From(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
        LazyThreadSafetyMode.ExecutionAndPublication
    );
}
