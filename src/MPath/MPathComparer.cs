namespace Michi;

/// <summary>
/// Explicit equality comparers for <see cref="MPath" /> that override the host-OS
/// default. Use these when you need collection semantics that do NOT follow the
/// current platform.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="Ordinal" /> is always case-sensitive; <see cref="OrdinalIgnoreCase" />
///     is always case-insensitive. Neither consults
///     <c>
///     RuntimeInformation
///     </c>
///     .
///     </para>
///     <para>
///     Example (force case-insensitive even on Linux):
///     <code>
/// var seen = new HashSet&lt;MPath&gt;(MPathComparer.OrdinalIgnoreCase);
/// </code>
///     </para>
///     <para>
///     Example (force case-sensitive even on Windows/macOS — useful when
///     working inside an NTFS directory with the per-directory case-sensitivity flag
///     set, or in a WSL interop scenario — see PITFALLS C-07):
///     <code>
/// var index = new Dictionary&lt;MPath, int&gt;(MPathComparer.Ordinal);
/// </code>
///     </para>
/// </remarks>
public static class MPathComparer {
    /// <summary>
    /// Case-sensitive ordinal comparer — never folds case regardless of host OS.
    /// </summary>
    public static IEqualityComparer<MPath> Ordinal { get; } = new Impl(StringComparison.Ordinal);

    /// <summary>
    /// Case-insensitive ordinal comparer — always folds case regardless of host OS.
    /// </summary>
    public static IEqualityComparer<MPath> OrdinalIgnoreCase { get; } = new Impl(StringComparison.OrdinalIgnoreCase);

    private sealed class Impl : IEqualityComparer<MPath> {
        private readonly StringComparer _comparer;
        private readonly StringComparison _comparison;

        public Impl(StringComparison comparison)
        {
            _comparison = comparison;
            _comparer = StringComparer.FromComparison(comparison);
        }

        public bool Equals(MPath? x, MPath? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            // ToUnixString() returns the forward-slash canonical form.
            // Implemented in plan 01-04.
            return string.Equals(x.ToUnixString(), y.ToUnixString(), _comparison);
        }

        public int GetHashCode(MPath obj)
        {
            if (obj is null)
                throw new ArgumentNullException(
                    nameof(obj),
                    "Cannot compute a hash code for a null MPath via MPathComparer. Pass a non-null instance."
                );

            return _comparer.GetHashCode(obj.ToUnixString());
        }
    }
}
