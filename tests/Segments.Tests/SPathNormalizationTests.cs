using Segments.Exceptions;
using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public class SPathNormalizationTests {
    // Resolve `..` segments (past-root stops at root).
    [Theory]
    [InlineData("/foo/../bar", "/bar")]
    [InlineData("/foo/bar/../baz", "/foo/baz")]
    [InlineData("/foo/../../bar", "/bar")]
    public void Normalization_ResolvesDoubleDot_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Resolve `.` segments.
    [Theory]
    [InlineData("/foo/./bar", "/foo/bar")]
    [InlineData("/foo/./bar/./baz", "/foo/bar/baz")]
    public void Normalization_ResolvesSingleDot_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Collapse repeated separators.
    [Theory]
    [InlineData("/foo//bar", "/foo/bar")]
    [InlineData("/foo///bar////baz", "/foo/bar/baz")]
    public void Normalization_CollapsesRepeatedSeparators_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Backslash input is normalized to forward slash internally.
    [Theory]
    [InlineData("/foo\\bar", "/foo/bar")]
    [InlineData("/a\\b\\c", "/a/b/c")]
    public void Normalization_BackslashInput_NormalizesToForwardSlash_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From(input).ToUnixString().ShouldBe(expected);
    }

    [Fact]
    public void Normalization_BackslashTraversal_OnUnix_ResolvesBeforeReturning()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/safe\\..\\escape").ToUnixString().ShouldBe("/escape");
    }

    // Trailing slash is stripped unless the result IS the root.
    [Theory]
    [InlineData("/foo/bar/", "/foo/bar")]
    [InlineData("/foo/", "/foo")]
    public void Normalization_StripsTrailingSlash_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Root path preserves its trailing slash (it IS the root).
    [Fact]
    public void Normalization_RootPath_PreservesTrailingSlash_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/").ToUnixString().ShouldBe("/");
    }

    // Full pipeline: a multi-rule input canonicalizes in one pass.
    [Fact]
    public void Normalization_FullPipeline()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From("/foo/../bar//baz/");
        p.ToUnixString().ShouldBe("/bar/baz");
    }

    // Tilde expansion OFF by default -- the literal ~ survives into the canonical output.
    [Fact]
    public void Normalization_TildeExpansion_OffByDefault()
    {
        // With default options, ~/foo is a relative path containing a literal '~' -- resolved
        // against BaseDirectory without replacement.
        var p = SPath.From("~/foo");
        p.ToUnixString().ShouldContain("~");
    }

    // Tilde expansion ON via options -- ~ IS replaced with the user-profile path.
    [Fact]
    public void Normalization_TildeExpansion_OnViaOptions_ReplacesWithUserProfile()
    {
        var opts = SPathOptions.Default with {
            ExpandTilde = true,
        };

        var p = SPath.From("~/foo", opts);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/').TrimEnd('/');
        p.ToUnixString().ShouldBe(home + "/foo");
    }

    // Env-var expansion OFF by default -- literal "$VAR" preserved.
    [Fact]
    public void Normalization_EnvVarExpansion_OffByDefault_LiteralPreserved()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From("/data/$NONEXISTENT_VAR_XYZ/foo");
        p.ToUnixString().ShouldContain("$NONEXISTENT_VAR_XYZ");
    }

    // Normalization does NOT touch the filesystem -- construction is a pure string transform.
    [Fact]
    public void Normalization_DoesNotAccessFilesystem()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        Should.NotThrow(() => SPath.From("/this/path/definitely/does/not/exist/anywhere"));
    }

    // Invalid path characters on Windows throw InvalidPathException.
    [Fact]
    public void Normalization_InvalidPathCharacter_OnWindows_Throws()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var ex = Should.Throw<InvalidPathException>(() => SPath.From(@"C:\foo\b<ar"));
        ex.Message.ShouldContain("invalid path character");
    }

    // On Unix only NUL is invalid -- angle brackets and similar Windows-invalid chars pass.
    [Fact]
    public void Normalization_AngleBracketsAllowed_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        Should.NotThrow(() => SPath.From("/foo/b<ar"));
    }

    // macOS rejects ':' in segments because user-visible Apple tooling treats it as a separator.
    [Fact]
    public void Normalization_ColonInSegment_OnMacOS_Throws()
    {
        if (!PlatformTestHelpers.IsMacOS)
            return;

        var ex = Should.Throw<InvalidPathException>(() => SPath.From("/foo/ba:r"));
        ex.Message.ShouldContain("invalid path character");
    }

    // Linux: ':' is a perfectly legal filename character at the ext4/btrfs/xfs layer. POSIX
    // §3.170 forbids only NUL and '/'. Accepting ':' on Linux matches the real filesystem.
    [Fact]
    public void Normalization_ColonInSegment_OnLinux_Allowed()
    {
        if (!PlatformTestHelpers.IsLinux)
            return;

        Should.NotThrow(() => SPath.From("/foo/ba:r"));
    }

    // Unicode (emoji, CJK ideographs, accented Latin) is legal on every platform. NTFS stores
    // UTF-16 code units; APFS and ext4/btrfs are bag-of-bytes and store UTF-8 unchanged. Locks
    // in that Segments does NOT over-reject non-ASCII segments. The test runs on all three OSes
    // with platform-appropriate roots.
    [Theory]
    [InlineData("\ud83d\ude80rocket.txt")] // 🚀 (U+1F680, surrogate pair in UTF-16)
    [InlineData("\u65e5\u672c\u8a9e.txt")] // 日本語
    [InlineData("caf\u00e9.md")] // café (NFC)
    [InlineData("cafe\u0301.md")] // café (NFD)
    public void Normalization_UnicodeSegments_AllowedOnAllPlatforms(string segmentName)
    {
        var root = PlatformTestHelpers.IsWindows ? @"C:\foo\" : "/foo/";
        Should.NotThrow(() => SPath.From(root + segmentName));
    }

    // Relative path resolves against BaseDirectory.
    [Fact]
    public void Normalization_RelativePath_ResolvesAgainstBaseDirectory()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var opts = SPathOptions.Default with {
            BaseDirectory = "/opt/app",
        };

        var p = SPath.From("data/files", opts);
        p.ToUnixString().ShouldBe("/opt/app/data/files");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalization_EnvVarExpansion_RejectsEmptyExpandedPath(string value)
    {
        var variableName = "SEGMENTS_EMPTY_PATH_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variableName, value);

        try {
            var input = PlatformTestHelpers.IsWindows ? $"%{variableName}%" : "$" + variableName;
            var opts = SPathOptions.Default with {
                ExpandEnvironmentVariables = true,
            };

            var ex = Should.Throw<InvalidPathException>(() => SPath.From(input, opts));

            ex.Message.ShouldContain("Path is empty");
        }
        finally {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }
}
