using Michi.Internal;

namespace Michi;

/// <summary>
/// Explicit equality comparers for <see cref="MPath" /> that override the host-OS default.
/// Use when you need collection semantics that don't follow the current platform.
/// </summary>
/// <remarks>
/// <see cref="Ordinal" /> is always case-sensitive, <see cref="OrdinalIgnoreCase" /> always
/// case-insensitive. Neither consults the host OS.
/// <para>
/// Force case-insensitive on Linux:
/// <code>var seen = new HashSet&lt;MPath&gt;(MPathComparer.OrdinalIgnoreCase);</code>
/// </para>
/// <para>
/// Force case-sensitive on Windows or macOS (useful for NTFS directories with the per-directory
/// case-sensitivity flag set, or WSL interop):
/// <code>var index = new Dictionary&lt;MPath, int&gt;(MPathComparer.Ordinal);</code>
/// </para>
/// </remarks>
public static class MPathComparer {
    /// <summary>Case-sensitive ordinal comparer. Ignores host OS.</summary>
    public static IEqualityComparer<MPath> Ordinal { get; } = new Impl(StringComparison.Ordinal);

    /// <summary>Case-insensitive ordinal comparer. Ignores host OS.</summary>
    public static IEqualityComparer<MPath> OrdinalIgnoreCase { get; } = new Impl(StringComparison.OrdinalIgnoreCase);

    private sealed class Impl : IEqualityComparer<MPath> {
        private readonly StringComparer _comparer;

        public Impl(StringComparison comparison)
        {
            _comparer = StringComparer.FromComparison(comparison);
        }

        public bool Equals(MPath? x, MPath? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return _comparer.Equals(x.ToUnixString(), y.ToUnixString());
        }

        public int GetHashCode(MPath obj)
        {
            Guard.NotNull(
                obj,
                nameof(obj),
                "Cannot hash a null MPath via MPathComparer. Pass a non-null instance."
            );

            return _comparer.GetHashCode(obj.ToUnixString());
        }
    }
}
