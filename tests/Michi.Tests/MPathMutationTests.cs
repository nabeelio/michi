using Michi.Exceptions;
using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathMutationTests {
    // WithName replaces the final segment.
    [Fact]
    public void WithName_ReplacesFinalSegment()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c.txt").WithName("d.md").ToUnixString().ShouldBe("/a/b/d.md");
    }

    // WithName containing / throws with verbose message.
    [Fact]
    public void WithName_WithForwardSlash_Throws()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo\bar" : "/foo/bar");
        var ex = Should.Throw<InvalidPathException>(() => p.WithName("sub/name"));
        ex.Message.ShouldContain("contains a directory separator");
        ex.Message.ShouldContain("Use the / operator or Join()");
    }

    // WithName null throws ArgumentNullException with verbose message.
    [Fact]
    public void WithName_Null_ThrowsArgumentNullException()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<ArgumentNullException>(() => p.WithName(null!));
        ex.ParamName.ShouldBe("name");
        ex.Message.ShouldContain("non-empty segment string");
    }

    // WithExtension accepts leading-dot or no-dot form.
    [Fact]
    public void WithExtension_AcceptsBothDotAndNoDot()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo").WithExtension("txt").ToUnixString().ShouldBe("/a/foo.txt");
        MPath.From("/a/foo").WithExtension(".txt").ToUnixString().ShouldBe("/a/foo.txt");
    }

    // WithExtension replaces an existing extension.
    [Fact]
    public void WithExtension_ReplacesExistingExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo.txt").WithExtension("md").ToUnixString().ShouldBe("/a/foo.md");
    }

    // WithExtension(null) is equivalent to WithoutExtension.
    [Fact]
    public void WithExtension_Null_EquivalentToWithoutExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/foo.txt");
        p.WithExtension(null).ShouldBe(p.WithoutExtension());
        p.WithExtension(null).ToUnixString().ShouldBe("/a/foo");
    }

    // WithExtension(".") throws -- a bare dot is not a valid extension.
    [Fact]
    public void WithExtension_BareDot_Throws()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<InvalidPathException>(() => p.WithExtension("."));
        ex.AttemptedPath.ShouldBe(".");
        ex.Message.ShouldContain("Extension '.' is not valid");
        ex.Message.ShouldContain("Use WithoutExtension() to remove");
    }

    // WithoutExtension strips the trailing extension.
    [Fact]
    public void WithoutExtension_StripsExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo.txt").WithoutExtension().ToUnixString().ShouldBe("/a/foo");
    }

    // WithoutExtension on an extension-less path is idempotent.
    [Fact]
    public void WithoutExtension_NoExtension_IsIdempotent()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/foo");
        p.WithoutExtension().ShouldBe(p);
    }
}
