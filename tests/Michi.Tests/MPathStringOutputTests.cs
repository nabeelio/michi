using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathStringOutputTests {
    // ToString returns the canonical forward-slash form on every platform (deterministic
    // for logging). Use ToNativeString when OS-native separators are required.
    [Fact]
    public void ToString_ReturnsCanonicalForwardSlash_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c").ToString().ShouldBe("/a/b/c");
    }

    [Fact]
    public void ToString_ReturnsCanonicalForwardSlash_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        MPath.From(@"C:\a\b").ToString().ShouldBe("C:/a/b");
    }

    [Fact]
    public void ToNativeString_ReturnsOsNativeSeparators_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c").ToNativeString().ShouldBe("/a/b/c");
    }

    [Fact]
    public void ToNativeString_ReturnsOsNativeSeparators_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        MPath.From(@"C:\a\b").ToNativeString().ShouldBe(@"C:\a\b");
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

    // Explicit cast returns the OS-native string form.
    [Fact]
    public void ExplicitCast_ReturnsNativeString()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/b");
        ((string?) p).ShouldBe("/a/b");
    }

    // Explicit cast on null returns null.
    [Fact]
    public void ExplicitCast_OnNull_ReturnsNull()
    {
        MPath? p = null;
        ((string?) p).ShouldBeNull();
    }
}
