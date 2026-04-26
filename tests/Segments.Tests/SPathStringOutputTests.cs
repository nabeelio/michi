using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public class SPathStringOutputTests {
    // ToString returns the OS-native separator form on every platform.
    [Fact]
    public void ToString_UsesOsNativeSeparators_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/b/c").ToString().ShouldBe("/a/b/c");
    }

    [Fact]
    public void ToString_UsesOsNativeSeparators_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        SPath.From(@"C:\a\b").ToString().ShouldBe(@"C:\a\b");
    }

    // Path property returns the same value as ToString -- it's the ergonomic alias for
    // LINQ / data-binding / string-typed API call sites.
    [Fact]
    public void Path_ReturnsSameAsToString_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From("/a/b/c");
        p.Path.ShouldBe(p.ToString());
        p.Path.ShouldBe("/a/b/c");
    }

    [Fact]
    public void Path_ReturnsSameAsToString_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From(@"C:\a\b");
        p.Path.ShouldBe(p.ToString());
        p.Path.ShouldBe(@"C:\a\b");
    }

    // Path and ToString return the same cached OS-native string.
    [Fact]
    public void Path_IsReferenceStable_AcrossCalls()
    {
        var p = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\b" : "/a/b");
        var first = p.Path;
        ReferenceEquals(first, p.Path).ShouldBeTrue();
        ReferenceEquals(first, p.ToString()).ShouldBeTrue();
    }

    [Fact]
    public void Value_ReturnsSameAsPathAndToString()
    {
        var p = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\b" : "/a/b");

        p.Value.ShouldBe(p.Path);
        p.Value.ShouldBe(p.ToString());
    }

    [Fact]
    public void ExplicitStringCast_ReturnsSameAsToString()
    {
        var p = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\b" : "/a/b");

        var value = (string) p;

        value.ShouldBe(p.ToString());
        ReferenceEquals(value, p.ToString()).ShouldBeTrue();
    }

    // ToUnixString always uses forward slashes.
    [Fact]
    public void ToUnixString_AlwaysForwardSlash()
    {
        if (PlatformTestHelpers.IsWindows) {
            SPath.From(@"C:\a\b").ToUnixString().ShouldBe("C:/a/b");
        } else {
            SPath.From("/a/b").ToUnixString().ShouldBe("/a/b");
        }
    }

    // ToWindowsString always uses backslashes.
    [Fact]
    public void ToWindowsString_AlwaysBackslash()
    {
        if (PlatformTestHelpers.IsWindows) {
            SPath.From(@"C:\a\b").ToWindowsString().ShouldBe(@"C:\a\b");
        } else {
            SPath.From("/a/b").ToWindowsString().ShouldBe(@"\a\b");
        }
    }
}
