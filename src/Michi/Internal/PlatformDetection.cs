using System.Runtime.InteropServices;

namespace Michi.Internal;

/// <summary>
/// Host-OS detection and path-comparison primitives. Fixed at process startup.
/// </summary>
/// <remarks>
/// Single source of truth for path-comparison semantics. Equality and hashing elsewhere in Michi
/// must route through <see cref="PathComparison" /> or <see cref="PathComparer" />. Don't add a
/// second OS-detection site.
/// <para>
/// Detecting case sensitivity from path content doesn't work -- APFS on macOS defaults to
/// case-insensitive regardless of path shape.
/// </para>
/// </remarks>
internal static class HostOs {
    /// <summary>`true` on Windows (NTFS, case-insensitive).</summary>
    public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>`true` on macOS (APFS default, case-insensitive).</summary>
    public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>`true` on Linux (ext4/btrfs, case-sensitive).</summary>
    public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// <see cref="StringComparison" /> used by MPath equality, hashing, and ordering. Case-insensitive
    /// on Windows and macOS, case-sensitive on Linux. Always ordinal -- never culture-sensitive, so
    /// the Turkish-I trap can't bite.
    /// </summary>
    public static readonly StringComparison PathComparison =
            IsWindows || IsMacOS
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

    /// <summary>
    /// <see cref="StringComparer" /> aligned with <see cref="PathComparison" />. Used for allocation-free
    /// `GetHashCode(string)`.
    /// </summary>
    public static readonly StringComparer PathComparer = StringComparer.FromComparison(PathComparison);
}
