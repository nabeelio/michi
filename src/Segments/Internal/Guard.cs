using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Segments.Internal;

/// <summary>
/// Argument-guard helpers used across the library. Keeps call sites terse by letting the
/// compiler capture the caller's argument expression as `paramName` automatically.
/// </summary>
internal static class Guard {
    /// <summary>
    /// Throws <see cref="ArgumentNullException" /> when `value` is null.
    /// `paramName` is captured automatically from the caller's argument
    /// expression -- callers do not pass it explicitly.
    /// </summary>
    /// <param name="value">The argument to null-check.</param>
    /// <param name="message">Optional custom exception message.</param>
    /// <param name="paramName">
    /// Compiler-filled from the caller's argument expression. Do not pass explicitly.
    /// </param>
    /// <exception cref="ArgumentNullException">`value` is null.</exception>
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
}
