using System.Runtime.CompilerServices;

namespace Michi.Internal;

/// <summary>
/// Argument-guard helpers. Centralizes the
/// <c>
/// #if NET6_0_OR_GREATER
/// </c>
/// fallback for <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />
/// so call sites stay clean.
/// </summary>
internal static class Guard {
    /// <summary>
    /// Throws <see cref="ArgumentNullException" /> when <paramref name="value" /> is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotNull(object? value, string paramName, string? message = null)
    {
        if (value is null) {
            throw new ArgumentNullException(paramName, message);
        }
    }
}
