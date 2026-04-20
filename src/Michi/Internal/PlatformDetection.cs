using System.Runtime.InteropServices;

namespace Michi.Internal;

/// <summary>
/// Internal host-OS detection and path-comparison primitives. Fixed at process
/// startup — the underlying <see cref="RuntimeInformation.IsOSPlatform(OSPlatform)" />
/// check cannot change mid-flight.
/// </summary>
/// <remarks>
/// This is the single source of truth for path-comparison semantics. Equality and
/// hashing elsewhere in Michi MUST route through <see cref="PathComparison" /> or
/// <see cref="PathComparer" />; do NOT introduce a second OS-detection site. The
/// "detect case sensitivity from path content" approach (used by Nuke's
/// <c>
/// AbsolutePath
/// </c>
/// ) is the exact bug Michi fixes on macOS — see PITFALLS C-02.
/// </remarks>
internal static class HostOs {
    /// <summary>
    /// <c>
    /// true
    /// </c>
    /// on Windows (NTFS, case-insensitive).
    /// </summary>
    public static readonly bool IsWindows =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// <c>
    /// true
    /// </c>
    /// on macOS (APFS default, case-insensitive).
    /// </summary>
    public static readonly bool IsMacOS =
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// <c>
    /// true
    /// </c>
    /// on Linux (ext4/btrfs, case-sensitive).
    /// </summary>
    public static readonly bool IsLinux =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// The <see cref="StringComparison" /> value used by MPath equality, hashing,
    /// and ordering. <see cref="StringComparison.OrdinalIgnoreCase" /> on Windows
    /// and macOS (their filesystems default to case-insensitive); <see cref="StringComparison.Ordinal" />
    /// on Linux. Fixed per-process. Never culture-sensitive (see PITFALLS C-08
    /// for the Turkish-I trap).
    /// </summary>
    public static readonly StringComparison PathComparison =
            IsWindows || IsMacOS
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

    /// <summary>
    /// <see cref="StringComparer" /> aligned with <see cref="PathComparison" />. Used
    /// for allocation-free
    /// <c>
    /// GetHashCode(string)
    /// </c>
    /// calls (see PITFALLS M-19).
    /// </summary>
    public static readonly StringComparer PathComparer =
            StringComparer.FromComparison(PathComparison);
}
