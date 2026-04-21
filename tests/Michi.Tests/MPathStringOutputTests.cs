using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathStringOutputTests {
    // ToString returns the OS-native separator form on every platform (CORE-05 + D-19).
    // Use ToUnixString when deterministic cross-platform output is required.
    [Fact]
    public void ToString_UsesOsNativeSeparators_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c").ToString().ShouldBe("/a/b/c");
    }

    [Fact]
    public void ToString_UsesOsNativeSeparators_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        MPath.From(@"C:\a\b").ToString().ShouldBe(@"C:\a\b");
    }

    // Path property returns the same value as ToString -- it's the ergonomic alias for
    // LINQ / data-binding / string-typed API call sites.
    [Fact]
    public void Path_ReturnsSameAsToString_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/b/c");
        p.Path.ShouldBe(p.ToString());
        p.Path.ShouldBe("/a/b/c");
    }

    [Fact]
    public void Path_ReturnsSameAsToString_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From(@"C:\a\b");
        p.Path.ShouldBe(p.ToString());
        p.Path.ShouldBe(@"C:\a\b");
    }

    // Path and ToString return the same string reference on repeat access -- the
    // OS-native form is precomputed at construction (D-03). Capturing to a local
    // first avoids ReSharper's EqualExpressionComparison warning on `p.Path == p.Path`.
    [Fact]
    public void Path_IsReferenceStable_AcrossCalls()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\b" : "/a/b");
        var first = p.Path;
        ReferenceEquals(first, p.Path).ShouldBeTrue();
        ReferenceEquals(first, p.ToString()).ShouldBeTrue();
    }

    // ToUnixString always uses forward slashes.
    [Fact]
    public void ToUnixString_AlwaysForwardSlash()
    {
        if (PlatformTestHelpers.IsWindows) {
            MPath.From(@"C:\a\b").ToUnixString().ShouldBe("C:/a/b");
        } else {
            MPath.From("/a/b").ToUnixString().ShouldBe("/a/b");
        }
    }

    // ToWindowsString always uses backslashes.
    [Fact]
    public void ToWindowsString_AlwaysBackslash()
    {
        if (PlatformTestHelpers.IsWindows) {
            MPath.From(@"C:\a\b").ToWindowsString().ShouldBe(@"C:\a\b");
        } else {
            MPath.From("/a/b").ToWindowsString().ShouldBe(@"\a\b");
        }
    }
}
