using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: CORE-02 — construction API (From / From+relativeTo / TryFrom / Format)
public class MPathConstructionTests {
    // CORE-02: From(string) — absolute path succeeds on Unix
    [Fact]
    public void From_AbsoluteUnixPath_OnUnix_Succeeds()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/foo/bar");
        p.ToUnixString().ShouldBe("/foo/bar");
    }

    // CORE-02: From(string) — absolute Windows path succeeds on Windows
    [Fact]
    public void From_AbsoluteWindowsPath_OnWindows_Succeeds()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From(@"C:\foo\bar");
        p.ToUnixString().ShouldBe("C:/foo/bar");
        p.ToString().ShouldBe(@"C:\foo\bar");
    }

    // CORE-02 + D-35d: null path throws ArgumentNullException with verbose message
    [Fact]
    public void From_NullPath_ThrowsArgumentNullException_WithActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.From(null!));
        ex.ParamName.ShouldBe("path");
        ex.Message.ShouldContain("Use TryFrom to accept null input without exceptions");
    }

    // CORE-02 + D-35b: relative path without base resolves via AppContext.BaseDirectory by default
    [Fact]
    public void From_RelativePath_ResolvesAgainstDefaultBaseDirectory()
    {
        var result = MPath.From("foo/bar");
        result.ToUnixString().ShouldEndWith("/foo/bar");
    }

    // CORE-02 + D-05 + D-35b: empty path throws InvalidPathException with "empty" reason
    [Fact]
    public void From_EmptyString_ThrowsInvalidPathException_WithEmptyReason()
    {
        var ex = Should.Throw<InvalidPathException>(() => MPath.From(""));
        ex.AttemptedPath.ShouldBe("");
        ex.Message.ShouldContain("empty");
    }

    // CORE-02 + D-04: From(path, relativeTo) resolves against the explicit base
    [Fact]
    public void From_RelativePath_WithRelativeTo_ResolvesAgainstExplicitBase()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("bar/baz", "/foo");
        p.ToUnixString().ShouldBe("/foo/bar/baz");
    }

    // CORE-02 + D-35d: relativeTo null throws ArgumentNullException naming the parameter.
    // Use a named argument to disambiguate from the 2-arg (path, options) overload —
    // calling MPath.From("bar", null!) is ambiguous to overload resolution (null binds
    // to both `string relativeTo` and `MPathOptions? options`).
    [Fact]
    public void From_WithRelativeTo_NullRelativeTo_ThrowsArgumentNullException()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.From("bar", relativeTo: null!));
        ex.ParamName.ShouldBe("relativeTo");
        ex.Message.ShouldContain("absolute path string");
    }

    // CORE-02 + D-05: TryFrom returns false for null without throwing
    [Fact]
    public void TryFrom_NullInput_ReturnsFalse_WithNullResult()
    {
        var ok = MPath.TryFrom(null, out var result);
        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    // CORE-02 + D-05: TryFrom returns false for empty string (catches InvalidPathException internally)
    [Fact]
    public void TryFrom_EmptyString_ReturnsFalse()
    {
        var ok = MPath.TryFrom("", out var result);
        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    // CORE-02: TryFrom returns true + result for a valid path
    [Fact]
    public void TryFrom_ValidPath_ReturnsTrue_WithResult()
    {
        var input = PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo";
        var ok = MPath.TryFrom(input, out var result);
        ok.ShouldBeTrue();
        result.ShouldNotBeNull();
    }

    // CORE-02 + D-10: options parameter fully replaces Default (no merge)
    [Fact]
    public void From_WithCustomOptions_DoesNotMergeWithDefault()
    {
        if (PlatformTestHelpers.IsWindows)
            return; // test below uses Unix-style absolute paths

        var opts = new MPathOptions {
            BaseDirectory = "/custom/base",
            ExpandTilde = true,,
        };

        var p = MPath.From("relative", opts);
        p.ToUnixString().ShouldBe("/custom/base/relative");
    }

    // CORE-02 + D-10 + record-with: custom options via record `with` expression
    [Fact]
    public void From_WithOptionsUsingRecordWith_AppliesOverride()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var opts = MPathOptions.Default with {
            BaseDirectory = "/opt/myapp",,
        };

        var p = MPath.From("data", opts);
        p.ToUnixString().ShouldBe("/opt/myapp/data");
    }
}
