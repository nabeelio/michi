using Segments.Exceptions;
using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public class SPathMutationTests {
    // WithName replaces the final segment.
    [Fact]
    public void WithName_ReplacesFinalSegment()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/b/c.txt").WithName("d.md").ToUnixString().ShouldBe("/a/b/d.md");
    }

    // WithName containing / throws with verbose message.
    [Fact]
    public void WithName_WithForwardSlash_Throws()
    {
        var p = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo\bar" : "/foo/bar");
        var ex = Should.Throw<InvalidPathException>(() => p.WithName("sub/name"));
        ex.Message.ShouldContain("contains a directory separator");
        ex.Message.ShouldContain("Use the / operator or Join()");
    }

    // WithName null throws ArgumentNullException with verbose message.
    [Fact]
    public void WithName_Null_ThrowsArgumentNullException()
    {
        var p = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<ArgumentNullException>(() => p.WithName(null!));
        ex.ParamName.ShouldBe("name");
        ex.Message.ShouldContain("non-empty segment string");
    }

    [Fact]
    public void WithName_Empty_PreservesOriginalFailureWording()
    {
        var path = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\file.txt" : "/a/file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithName(string.Empty));

        ex.AttemptedPath.ShouldBe(string.Empty);
        ex.Reason.ShouldBe("Name is empty. Pass a non-empty segment string");
        ex.Message.ShouldBe("Invalid path '': Name is empty. Pass a non-empty segment string.");
    }

    [Fact]
    public void WithName_Dot_Throws()
    {
        var path = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\file.txt" : "/a/file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithName("."));

        ex.Message.ShouldContain("single path segment");
    }

    [Fact]
    public void WithName_DoubleDot_Throws()
    {
        var path = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\a\file.txt" : "/a/file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithName(".."));

        ex.Message.ShouldContain("single path segment");
    }

    [Fact]
    public void WithName_ReservedDeviceNameOnWindows_Throws()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var path = SPath.From(@"C:\a\file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithName("NUL"));

        ex.Message.ShouldContain("reserved device name");
    }

    [Fact]
    public void WithName_TrailingDotOnWindows_Throws()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var path = SPath.From(@"C:\a\file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithName("name."));

        ex.Message.ShouldContain("must not end with '.' or space");
    }

    [Fact]
    public void WithName_TrailingSpaceOnWindows_Throws()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var path = SPath.From(@"C:\a\file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithName("name "));

        ex.Message.ShouldContain("must not end with '.' or space");
    }

    // WithExtension accepts leading-dot or no-dot form.
    [Fact]
    public void WithExtension_AcceptsBothDotAndNoDot()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/foo").WithExtension("txt").ToUnixString().ShouldBe("/a/foo.txt");
        SPath.From("/a/foo").WithExtension(".txt").ToUnixString().ShouldBe("/a/foo.txt");
    }

    // WithExtension replaces an existing extension.
    [Fact]
    public void WithExtension_ReplacesExistingExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/foo.txt").WithExtension("md").ToUnixString().ShouldBe("/a/foo.md");
    }

    // WithExtension(null) is equivalent to WithoutExtension.
    [Fact]
    public void WithExtension_Null_EquivalentToWithoutExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From("/a/foo.txt");
        p.WithExtension(null).ShouldBe(p.WithoutExtension());
        p.WithExtension(null).ToUnixString().ShouldBe("/a/foo");
    }

    // WithExtension(".") throws -- a bare dot is not a valid extension.
    [Fact]
    public void WithExtension_BareDot_Throws()
    {
        var p = SPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<InvalidPathException>(() => p.WithExtension("."));
        ex.AttemptedPath.ShouldBe(".");
        ex.Message.ShouldContain("Extension '.' is not valid");
        ex.Message.ShouldContain("Use WithoutExtension() to remove");
    }

    [Fact]
    public void WithExtension_InvalidCharacterOnWindows_Throws()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var path = SPath.From(@"C:\a\file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithExtension("bad*name"));

        ex.Message.ShouldContain("invalid path character");
    }

    [Fact]
    public void WithExtension_TrailingDotOnWindows_Throws()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var path = SPath.From(@"C:\a\file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithExtension("bad."));

        ex.Message.ShouldContain("must not end with '.' or space");
    }

    [Fact]
    public void WithExtension_TrailingSpaceOnWindows_Throws()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var path = SPath.From(@"C:\a\file.txt");
        var ex = Should.Throw<InvalidPathException>(() => path.WithExtension("bad "));

        ex.Message.ShouldContain("must not end with '.' or space");
    }

    [Fact]
    public void InvalidSingleSegmentRule_IsSharedAcrossJoinWithNameAndWithExtension_OnMacOS()
    {
        if (!PlatformTestHelpers.IsMacOS) {
            Assert.Skip("Test is macOS-only.");
        }

        var directory = SPath.From("/a");
        var file = SPath.From("/a/file.txt");

        var join = Should.Throw<InvalidPathException>(() => _ = directory / "bad:name");
        var rename = Should.Throw<InvalidPathException>(() => file.WithName("bad:name"));
        var extension = Should.Throw<InvalidPathException>(() => file.WithExtension("bad:name"));

        rename.Reason.ShouldBe(join.Reason);
        extension.Reason.ShouldBe(join.Reason);
    }

    // WithoutExtension strips the trailing extension.
    [Fact]
    public void WithoutExtension_StripsExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/foo.txt").WithoutExtension().ToUnixString().ShouldBe("/a/foo");
    }

    // WithoutExtension on an extension-less path is idempotent.
    [Fact]
    public void WithoutExtension_NoExtension_IsIdempotent()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From("/a/foo");
        p.WithoutExtension().ShouldBe(p);
    }
}
