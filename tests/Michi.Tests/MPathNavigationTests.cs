using Michi.Exceptions;
using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathNavigationTests {
    // Parent returns the containing directory.
    [Fact]
    public void Parent_ReturnsContainingDirectory()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/foo/bar/baz").Parent.ToUnixString().ShouldBe("/foo/bar");
        MPath.From("/foo").Parent.ToUnixString().ShouldBe("/");
    }

    // Parent on root throws NoParentException with verbose message.
    [Fact]
    public void Parent_OnRoot_ThrowsNoParentException()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var ex = Should.Throw<NoParentException>(() => { _ = MPath.From("/").Parent; });

        ex.Message.ShouldContain("no parent");
        ex.Message.ShouldContain("'/'");
    }

    // TryGetParent returns true with a result for non-root.
    [Fact]
    public void TryGetParent_OnNonRoot_ReturnsTrueWithResult()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/foo/bar").TryGetParent(out var parent).ShouldBeTrue();
        parent!.ToUnixString().ShouldBe("/foo");
    }

    // TryGetParent on root returns false with null.
    [Fact]
    public void TryGetParent_OnRoot_ReturnsFalseWithNull()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/").TryGetParent(out var parent).ShouldBeFalse();
        parent.ShouldBeNull();
    }

    // Up(n) walks n levels up.
    [Fact]
    public void Up_WalksNLevels()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c/d").Up(2).ToUnixString().ShouldBe("/a/b");
    }

    // Up(n < 0) throws ArgumentOutOfRangeException with verbose message.
    [Fact]
    public void Up_Negative_ThrowsArgumentOutOfRangeException_WithVerboseMessage()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\b" : "/a/b");
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => p.Up(-1));
        ex.ParamName.ShouldBe("levels");
        ex.Message.ShouldContain("non-negative");
        ex.Message.ShouldContain("Received -1");
    }

    // Up(n > Depth) throws NoParentException with both values in the message.
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

    // Chained .Parent.Parent at root throws.
    [Fact]
    public void Parent_Chained_AtRoot_ThrowsNoParentException()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        Should.Throw<NoParentException>(() => { _ = MPath.From("/foo").Parent.Parent; });
    }
}
