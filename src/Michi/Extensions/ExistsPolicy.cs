namespace Michi.Extensions;

/// <summary>
/// Conflict policy for filesystem move/copy operations. Combines at most one file behavior
/// with at most one directory behavior. Omitting a category's bit selects fail-on-collision
/// for that category.
/// </summary>
/// <remarks>
/// The named combinations (<see cref="Fail" />, <see cref="MergeAndSkip" />,
/// <see cref="MergeAndOverwrite" />, <see cref="MergeAndOverwriteIfNewer" />) are the
/// recommended surface at call sites -- they document the full behavior pair in one token.
/// Raw bit combinations are supported for uncommon cases, but combining two bits in the same
/// category (e.g. `FileSkip | FileOverwrite`) is rejected by
/// <see cref="ExistsPolicyValidator.Validate(ExistsPolicy, string)" />.
/// </remarks>
[Flags]
public enum ExistsPolicy {
    /// <summary>
    /// Fail on any collision (file or directory). Equivalent to passing no flags.
    /// </summary>
    Fail = 0,

    /// <summary>
    /// On file collision, leave the existing destination file untouched and skip the source.
    /// </summary>
    FileSkip = 1 << 0,

    /// <summary>
    /// On file collision, overwrite the destination with the source unconditionally.
    /// </summary>
    FileOverwrite = 1 << 1,

    /// <summary>
    /// On file collision, overwrite the destination only when the source's last-write time
    /// is newer than the destination's.
    /// </summary>
    FileOverwriteIfNewer = 1 << 2,

    /// <summary>
    /// On directory collision, merge the source tree into the existing destination directory.
    /// File-level conflicts inside are resolved by the file behavior bit.
    /// </summary>
    DirectoryMerge = 1 << 8,

    /// <summary>
    /// On directory collision, delete the existing destination directory and replace it
    /// with the source.
    /// </summary>
    DirectoryReplace = 1 << 9,

    /// <summary>
    /// Merge directories; skip files that already exist at the destination.
    /// </summary>
    MergeAndSkip = FileSkip | DirectoryMerge,

    /// <summary>
    /// Merge directories; overwrite files that already exist at the destination.
    /// </summary>
    MergeAndOverwrite = FileOverwrite | DirectoryMerge,

    /// <summary>
    /// Merge directories; overwrite files only when the source is newer than the destination.
    /// </summary>
    MergeAndOverwriteIfNewer = FileOverwriteIfNewer | DirectoryMerge,
}

/// <summary>
/// Validates <see cref="ExistsPolicy" /> values before filesystem operations act on them.
/// Rejects combinations that would be ambiguous at runtime (two file behaviors or two
/// directory behaviors in the same policy) and unknown flag bits.
/// </summary>
internal static class ExistsPolicyValidator {
    private const ExistsPolicy FileBits =
            ExistsPolicy.FileSkip | ExistsPolicy.FileOverwrite | ExistsPolicy.FileOverwriteIfNewer;

    private const ExistsPolicy DirBits =
            ExistsPolicy.DirectoryMerge | ExistsPolicy.DirectoryReplace;

    private const ExistsPolicy AllKnownBits = FileBits | DirBits;

    /// <summary>
    /// Throws <see cref="ArgumentException" /> when `policy` contains unknown bits,
    /// more than one file behavior, or more than one directory behavior. Returns normally
    /// otherwise (including for <see cref="ExistsPolicy.Fail" />).
    /// </summary>
    /// <param name="policy">The policy to validate.</param>
    /// <param name="paramName">
    /// The parameter name to attach to the thrown exception. Callers pass `nameof(...)` of
    /// the public API argument they're forwarding.
    /// </param>
    /// <exception cref="ArgumentException">
    /// `policy` contains an unknown bit, combines multiple file behaviors,
    /// or combines multiple directory behaviors.
    /// </exception>
    public static void Validate(ExistsPolicy policy, string paramName)
    {
        // Reject unknown bits first so the other two checks only reason about defined flags.
        var unknown = policy & ~AllKnownBits;
        if (unknown != 0) {
            throw new ArgumentException(
                $"ExistsPolicy '{policy}' contains unknown flag value 0x{(int) unknown:X}. "
              + $"Valid flags are: {string.Join(", ", GetEnumNames())}.",
                paramName
            );
        }

        var fileBits = policy & FileBits;
        if (PopCount((int) fileBits) > 1) {
            throw new ArgumentException(
                $"ExistsPolicy '{policy}' combines multiple file behaviors ({fileBits}). "
              + "Choose exactly one of FileSkip, FileOverwrite, or FileOverwriteIfNewer -- or omit all three for fail-on-file.",
                paramName
            );
        }

        var dirBits = policy & DirBits;
        if (PopCount((int) dirBits) > 1) {
            throw new ArgumentException(
                $"ExistsPolicy '{policy}' combines multiple directory behaviors ({dirBits}). "
              + "Choose exactly one of DirectoryMerge or DirectoryReplace -- or omit both for fail-on-directory.",
                paramName
            );
        }
    }

    // Plain popcount, suitable for the small bit widths this validator sees. Avoids taking
    // a dependency on System.Numerics.BitOperations, which is public on net6+ but internal
    // on netstandard2.1.
    private static int PopCount(int value)
    {
        var count = 0;
        while (value != 0) {
            count++;
            value &= value - 1;
        }

        return count;
    }

    // The generic overload (`Enum.GetNames<TEnum>()`) exists on net5.0+, but the non-generic
    // form is the only option on netstandard2.1. Select per-TFM so the analyzers on net8/net10
    // get the CA2263-preferred form while netstandard2.1 still compiles.
    private static string[] GetEnumNames()
    {
        #if NET8_0_OR_GREATER
        return Enum.GetNames<ExistsPolicy>();
        #else
        return Enum.GetNames(typeof(ExistsPolicy));
        #endif
    }
}
