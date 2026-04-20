using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: NAV-01 — Parent / TryGetParent / Up navigation
public class MPathNavigationTests {
    // NAV-01: Parent returns the containing directory
    [Fact]
    public void Parent_ReturnsContainingDirectory()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/foo/bar/baz").Parent.ToUnixString().ShouldBe("/foo/bar");
        MPath.From("/foo").Parent.ToUnixString().ShouldBe("/");
    }

    // NAV-01 + D-25: Parent on root throws NoParentException with verbose message
    [Fact]
    public void Parent_OnRoot_ThrowsNoParentException()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var ex = Should.Throw<NoParentException>(() => {
                var _ = MPath.From("/").Parent;
            }
        );

        ex.Message.ShouldContain("no parent");
        ex.Message.ShouldContain("'/'");
    }

    // NAV-01 + D-26: TryGetParent returns true with result for non-root
    [Fact]
    public void TryGetParent_OnNonRoot_ReturnsTrueWithResult()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/foo/bar").TryGetParent(out var parent).ShouldBeTrue();
        parent!.ToUnixString().ShouldBe("/foo");
    }

    // NAV-01 + D-26: TryGetParent on root returns false with null
    [Fact]
    public void TryGetParent_OnRoot_ReturnsFalseWithNull()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/").TryGetParent(out var parent).ShouldBeFalse();
        parent.ShouldBeNull();
    }

    // NAV-01 + D-27: Up(0) returns self
    [Fact]
    public void Up_Zero_ReturnsSelf()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/b/c");
        p.Up(0).ShouldBe(p);
    }

    // NAV-01 + D-27: Up(n) walks n levels up
    [Fact]
    public void Up_WalksNLevels()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c/d").Up(2).ToUnixString().ShouldBe("/a/b");
    }

    // NAV-01 + D-27: Up(n < 0) throws ArgumentOutOfRangeException with verbose message
    [Fact]
    public void Up_Negative_ThrowsArgumentOutOfRangeException_WithVerboseMessage()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\b" : "/a/b");
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => p.Up(-1));
        ex.ParamName.ShouldBe("levels");
        ex.Message.ShouldContain("non-negative");
        ex.Message.ShouldContain("Received -1");
    }

    // NAV-01 + D-27 + D-35c: Up(n > Depth) throws NoParentException with message containing both n and Depth
    [Fact]
    public void Up_ExceedsDepth_ThrowsNoParentException_WithBothValuesInMessage()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/foo/bar"); // Depth = 2
        var ex = Should.Throw<NoParentException>(() => p.Up(5));
        ex.Message.ShouldContain("Cannot go up 5 levels");
        ex.Message.ShouldContain("depth=2");
    }

    // NAV-01 + phase success criterion 5: Parent.Parent at root throws
    [Fact]
    public void Parent_Chained_AtRoot_ThrowsNoParentException_Phase1Criterion5()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        // /foo -> / (root) -> throws
        Should.Throw<NoParentException>(() => {
                var _ = MPath.From("/foo").Parent.Parent;
            }
        );
    }
}
