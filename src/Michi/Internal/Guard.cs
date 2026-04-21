using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Michi.Exceptions;

namespace Michi.Internal;

/// <summary>
/// Argument-guard helpers used across the library. Keeps call sites terse by letting the
/// compiler capture the caller's argument expression as `paramName` automatically.
/// </summary>
internal static class Guard {
    /// <summary>
    /// Throws <see cref="ArgumentNullException" /> when <paramref name="value" /> is null.
    /// <paramref name="paramName" /> is captured automatically from the caller's argument
    /// expression -- callers do not pass it explicitly.
    /// </summary>
    /// <param name="value">The argument to null-check.</param>
    /// <param name="message">Optional custom exception message.</param>
    /// <param name="paramName">
    /// Compiler-filled from the caller's argument expression. Do not pass explicitly.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="value" /> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotNull(
        [NotNull] object? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))]
        string? paramName = null
    )
    {
        if (value is null) {
            throw new ArgumentNullException(paramName, message);
        }
    }

    /// <summary>
    /// Scans every segment of <paramref name="normalized" /> (a forward-slash-normalized absolute
    /// path) for platform-invalid characters and throws <see cref="InvalidPathException" /> on the
    /// first offender. The root prefix is skipped -- drive-letter colons and UNC backslashes are
    /// legitimate at the root but not within segments.
    /// </summary>
    /// <param name="normalized">The forward-slash normalized absolute path.</param>
    /// <param name="root">The root prefix in forward-slash form (e.g. "/", "C:/", "//server/share").</param>
    /// <param name="attempted">The original caller-supplied path, for diagnostic reporting.</param>
    /// <exception cref="InvalidPathException">
    /// A segment contains a character rejected by <see cref="HostOs.InvalidSegmentChars" /> on the
    /// current platform.
    /// </exception>
    internal static void InvalidSegmentChar(string normalized, string root, string attempted)
    {
        var tail = normalized.Length > root.Length ? normalized[root.Length..] : string.Empty;
        if (tail.Length == 0) {
            return;
        }

        var invalid = HostOs.InvalidSegmentChars;
        var badIndex = tail.IndexOfAny(invalid);
        if (badIndex < 0) {
            return;
        }

        var ch = tail[badIndex];

        throw new InvalidPathException(
            attempted,
            $"Contains invalid path character '{(char.IsControl(ch) ? ' ' : ch)}' (0x{(int) ch:X4}). "
          + "Platform-specific invalid-char set is documented on HostOs.InvalidSegmentChars"
        );
    }
}
