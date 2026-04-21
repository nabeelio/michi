using System.Runtime.InteropServices;
using Xunit;

namespace Michi.Tests.Internal;

/// <summary>
/// Platform-detection helpers for tests that are only meaningful on a specific OS
/// or filesystem case-sensitivity mode. Tests typically use the `IsX` boolean properties
/// with an early `return` (which registers as a silent pass) or the `SkipUnlessX` helpers
/// (which register as a skip in the MTP report via xUnit v3's native
/// <see cref="Assert.Skip(string)" />).
/// </summary>
/// <remarks>
/// xUnit v3 ships <see cref="Assert.Skip(string)" /> as a first-class API -- no
/// `SkippableFact` package is needed.
/// </remarks>
internal static class PlatformTestHelpers {
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Marks the current xUnit v3 test as skipped unless the host OS is Windows.
    /// </summary>
    public static void SkipUnlessWindows()
    {
        if (!IsWindows)
            Assert.Skip("Test is Windows-only.");
    }

    /// <summary>
    /// Marks the current xUnit v3 test as skipped unless the host OS is Unix (macOS or Linux).
    /// </summary>
    public static void SkipUnlessUnix()
    {
        if (IsWindows)
            Assert.Skip("Test is Unix-only (macOS or Linux).");
    }

    /// <summary>
    /// Marks the current test as skipped unless the host filesystem is case-insensitive by convention (Windows or macOS).
    /// </summary>
    public static void SkipUnlessCaseInsensitive()
    {
        if (IsLinux)
            Assert.Skip("Test requires case-insensitive FS (Windows or macOS).");
    }

    /// <summary>
    /// Marks the current test as skipped unless the host filesystem is case-sensitive (Linux).
    /// </summary>
    public static void SkipUnlessCaseSensitive()
    {
        if (!IsLinux)
            Assert.Skip("Test requires case-sensitive FS (Linux).");
    }
}
