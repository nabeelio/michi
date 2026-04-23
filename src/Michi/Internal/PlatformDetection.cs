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
    public static readonly StringComparer PathComparer =
            StringComparer.FromComparison(PathComparison);

    /// <summary>
    /// Characters that are invalid inside a path segment on Windows (NTFS / FAT32 / exFAT). Per
    /// https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file
    /// https://stackoverflow.com/a/31976060
    /// Separators (`/` and `\`) are deliberately excluded -- segment validation runs
    /// AFTER splitting on separators, so a separator cannot appear within a segment by construction.
    /// </summary>
    internal static readonly char[] InvalidWindowsSegmentChars = [
        // Control chars 0x00-0x1F.
        '\x00',
        '\x01',
        '\x02',
        '\x03',
        '\x04',
        '\x05',
        '\x06',
        '\x07',
        '\x08',
        '\x09',
        '\x0A',
        '\x0B',
        '\x0C',
        '\x0D',
        '\x0E',
        '\x0F',
        '\x10',
        '\x11',
        '\x12',
        '\x13',
        '\x14',
        '\x15',
        '\x16',
        '\x17',
        '\x18',
        '\x19',
        '\x1A',
        '\x1B',
        '\x1C',
        '\x1D',
        '\x1E',
        '\x1F',

        // Filename-reserved chars from the MS "Naming a file" page.
        '<',
        '>',
        ':',
        '"',
        '|',
        '?',
        '*',
    ];

    /// <summary>
    /// Characters that are invalid inside a path segment on macOS
    /// https://ss64.com/mac/syntax-filenames.html
    /// </summary>
    internal static readonly char[] InvalidMacOsSegmentChars = [
        '\0',
        ':',
    ];

    /// <summary>
    /// Characters that are invalid inside a path segment on Linux. POSIX §3.170 forbids only
    /// `\0` (and `/`, handled as the separator). ext4, btrfs, xfs all inherit this minimal rule.
    /// Characters like `&lt;`, `&gt;`, `:`, `*`, `?` are perfectly legal filename characters :o
    /// https://stackoverflow.com/a/31976060
    /// </summary>
    internal static readonly char[] InvalidLinuxSegmentChars = ['\0'];

    /// <summary>
    /// Characters that are invalid inside a path segment on the current platform. Resolves to one
    /// of <see cref="InvalidWindowsSegmentChars" />, <see cref="InvalidMacOsSegmentChars" />, or
    /// <see cref="InvalidLinuxSegmentChars" /> based on <see cref="IsWindows" /> /
    /// <see cref="IsMacOS" /> / <see cref="IsLinux" />.
    /// </summary>
    /// <remarks>
    /// Hand-authored, for better or worse -- cited -- not derived from
    /// <see cref="Path.GetInvalidFileNameChars" /> which Microsoft's docs explicitly disclaim as
    /// non-authoritative
    /// </remarks>
    public static readonly char[] InvalidSegmentChars =
            IsWindows ? InvalidWindowsSegmentChars
            : IsMacOS ? InvalidMacOsSegmentChars
            : InvalidLinuxSegmentChars;
}
