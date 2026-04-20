using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: CORE-05 — ToString / ToUnixString / ToWindowsString / explicit string cast
public class MPathStringOutputTests {
    // CORE-05 + D-19: ToString returns OS-native separators on Unix
    [Fact]
    public void ToString_UsesOsNativeSeparators_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c").ToString().ShouldBe("/a/b/c");
    }

    // CORE-05 + D-19: ToString returns OS-native separators on Windows
    [Fact]
    public void ToString_UsesOsNativeSeparators_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        MPath.From(@"C:\a\b").ToString().ShouldBe(@"C:\a\b");
    }

    // CORE-05 + D-20: ToUnixString always uses forward slashes
    [Fact]
    public void ToUnixString_AlwaysForwardSlash()
    {
        if (PlatformTestHelpers.IsWindows) {
            MPath.From(@"C:\a\b").ToUnixString().ShouldBe("C:/a/b");
        } else {
            MPath.From("/a/b").ToUnixString().ShouldBe("/a/b");
        }
    }

    // CORE-05 + D-21: ToWindowsString always uses backslashes
    [Fact]
    public void ToWindowsString_AlwaysBackslash()
    {
        if (PlatformTestHelpers.IsWindows) {
            MPath.From(@"C:\a\b").ToWindowsString().ShouldBe(@"C:\a\b");
        } else {
            MPath.From("/a/b").ToWindowsString().ShouldBe(@"\a\b");
        }
    }

    // CORE-05 + D-22: explicit cast calls ToString
    [Fact]
    public void ExplicitCast_CallsToString()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/b");
        ((string) p).ShouldBe("/a/b");
    }

    // CORE-05 + D-22: explicit cast on null returns null (C# cast-null semantics)
    [Fact]
    public void ExplicitCast_OnNull_ReturnsNull()
    {
        MPath? p = null;
        ((string?) p!).ShouldBeNull();
    }
}
