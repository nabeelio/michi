using Michi.Exceptions;
using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathConstructionTests {
    // From(string) -- absolute Unix path succeeds on Unix.
    [Fact]
    public void From_AbsoluteUnixPath_OnUnix_Succeeds()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/foo/bar");
        p.ToUnixString().ShouldBe("/foo/bar");
    }

    // From(string) -- absolute Windows path succeeds on Windows.
    [Fact]
    public void From_AbsoluteWindowsPath_OnWindows_Succeeds()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From(@"C:\foo\bar");
        p.ToUnixString().ShouldBe("C:/foo/bar");
        p.ToString().ShouldBe(@"C:\foo\bar");
    }

    [Theory]
    [InlineData(@"C:\temp\CON", "reserved device name 'CON'")]
    [InlineData(@"C:\temp\nul.txt", "reserved device name 'nul'")]
    [InlineData(@"C:\temp\Lpt1.log", "reserved device name 'Lpt1'")]
    [InlineData(@"C:\temp\NUL.tar.gz", "reserved device name 'NUL'")]
    [InlineData("C:\\temp\\COM\u00B9.txt", "reserved device name 'COM\u00B9'")]
    public void From_WindowsReservedDeviceName_Throws(string path, string messageFragment)
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var ex = Should.Throw<InvalidPathException>(() => MPath.From(path));
        ex.Message.ShouldContain(messageFragment);
    }

    [Theory]
    [InlineData(@"C:\temp\file.")]
    [InlineData(@"C:\temp\file ")]
    public void From_WindowsTrailingDotOrSpaceSegment_Throws(string path)
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var ex = Should.Throw<InvalidPathException>(() => MPath.From(path));
        ex.Message.ShouldContain("must not end with '.' or space");
    }

    [Theory]
    [InlineData(@"C:\CON\bad*name")]
    [InlineData(@"C:\bad.\good*name")]
    public void From_WindowsInvalidCharacter_WinsOverEarlierSegmentShapeError(string path)
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var ex = Should.Throw<InvalidPathException>(() => MPath.From(path));
        ex.Message.ShouldContain("invalid path character");
    }

    [Theory]
    [InlineData(@"C:\temp\COM10.txt")]
    [InlineData(@"C:\temp\conhost.txt")]
    public void From_WindowsReservedDeviceName_NearMisses_AreAllowed(string path)
    {
        PlatformTestHelpers.SkipUnlessWindows();

        Should.NotThrow(() => MPath.From(path));
    }

    // Null path throws ArgumentNullException with a verbose actionable message.
    [Fact]
    public void From_NullPath_ThrowsArgumentNullException_WithActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.From(null!));
        ex.ParamName.ShouldBe("path");
        ex.Message.ShouldContain("Use TryFrom to accept null input without exceptions");
    }

    // Relative path without an explicit base resolves against AppContext.BaseDirectory by default.
    [Fact]
    public void From_RelativePath_ResolvesAgainstDefaultBaseDirectory()
    {
        var result = MPath.From("foo/bar");
        result.ToUnixString().ShouldEndWith("/foo/bar");
    }

    // From(path, relativeTo) resolves against the explicit base.
    [Fact]
    public void From_RelativePath_WithRelativeTo_ResolvesAgainstExplicitBase()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("bar/baz", "/foo");
        p.ToUnixString().ShouldBe("/foo/bar/baz");
    }

    // relativeTo null throws ArgumentNullException naming the parameter. Use a named argument
    // to disambiguate from the 2-arg (path, options) overload -- calling
    // MPath.From("bar", null!) is ambiguous to overload resolution.
    [Fact]
    public void From_WithRelativeTo_NullRelativeTo_ThrowsArgumentNullException()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.From("bar", relativeTo: null!));
        ex.ParamName.ShouldBe("relativeTo");
        ex.Message.ShouldContain("absolute path string");
    }

    // TryFrom returns false for null without throwing.
    [Fact]
    public void TryFrom_NullInput_ReturnsFalse_WithNullResult()
    {
        var ok = MPath.TryFrom(null, out var result);
        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    // TryFrom returns false for empty string (catches InvalidPathException internally).
    [Fact]
    public void TryFrom_EmptyString_ReturnsFalse()
    {
        var ok = MPath.TryFrom("", out var result);
        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    // Custom options parameter fully replaces Default (no merge).
    [Fact]
    public void From_WithCustomOptions_DoesNotMergeWithDefault()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var opts = new MPathOptions {
            BaseDirectory = "/custom/base",
            ExpandTilde = true,
        };

        var p = MPath.From("relative", opts);
        p.ToUnixString().ShouldBe("/custom/base/relative");
    }

    // Custom options via record `with` expression.
    [Fact]
    public void From_WithOptionsUsingRecordWith_AppliesOverride()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var opts = MPathOptions.Default with {
            BaseDirectory = "/opt/myapp",
        };

        var p = MPath.From("data", opts);
        p.ToUnixString().ShouldBe("/opt/myapp/data");
    }
}
