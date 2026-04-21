using Michi.Exceptions;
using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathNormalizationTests {
    // Resolve `..` segments (past-root stops at root).
    [Theory]
    [InlineData("/foo/../bar", "/bar")]
    [InlineData("/foo/bar/../baz", "/foo/baz")]
    [InlineData("/foo/../../bar", "/bar")]
    public void Normalization_ResolvesDoubleDot_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Resolve `.` segments.
    [Theory]
    [InlineData("/foo/./bar", "/foo/bar")]
    [InlineData("/foo/./bar/./baz", "/foo/bar/baz")]
    public void Normalization_ResolvesSingleDot_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Collapse repeated separators.
    [Theory]
    [InlineData("/foo//bar", "/foo/bar")]
    [InlineData("/foo///bar////baz", "/foo/bar/baz")]
    public void Normalization_CollapsesRepeatedSeparators_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Backslash input is normalized to forward slash internally.
    [Theory]
    [InlineData("/foo\\bar", "/foo/bar")]
    [InlineData("/a\\b\\c", "/a/b/c")]
    public void Normalization_BackslashInput_NormalizesToForwardSlash_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Trailing slash is stripped unless the result IS the root.
    [Theory]
    [InlineData("/foo/bar/", "/foo/bar")]
    [InlineData("/foo/", "/foo")]
    public void Normalization_StripsTrailingSlash_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // Root path preserves its trailing slash (it IS the root).
    [Fact]
    public void Normalization_RootPath_PreservesTrailingSlash_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/").ToUnixString().ShouldBe("/");
    }

    // Full pipeline: a multi-rule input canonicalizes in one pass.
    [Fact]
    public void Normalization_FullPipeline()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/foo/../bar//baz/");
        p.ToUnixString().ShouldBe("/bar/baz");
    }

    // Tilde expansion OFF by default -- the literal ~ survives into the canonical output.
    [Fact]
    public void Normalization_TildeExpansion_OffByDefault()
    {
        // With default options, ~/foo is a relative path containing a literal '~' -- resolved
        // against BaseDirectory without replacement.
        var p = MPath.From("~/foo");
        p.ToUnixString().ShouldContain("~");
    }

    // Tilde expansion ON via options -- ~ IS replaced with the user-profile path.
    [Fact]
    public void Normalization_TildeExpansion_OnViaOptions_ReplacesWithUserProfile()
    {
        var opts = MPathOptions.Default with {
            ExpandTilde = true,
        };

        var p = MPath.From("~/foo", opts);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/').TrimEnd('/');
        p.ToUnixString().ShouldBe(home + "/foo");
    }

    // Env-var expansion OFF by default -- literal "$VAR" preserved.
    [Fact]
    public void Normalization_EnvVarExpansion_OffByDefault_LiteralPreserved()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/data/$NONEXISTENT_VAR_XYZ/foo");
        p.ToUnixString().ShouldContain("$NONEXISTENT_VAR_XYZ");
    }

    // Normalization does NOT touch the filesystem -- construction is a pure string transform.
    [Fact]
    public void Normalization_DoesNotAccessFilesystem()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        Should.NotThrow(() => MPath.From("/this/path/definitely/does/not/exist/anywhere"));
    }

    // Invalid path characters on Windows throw InvalidPathException.
    [Fact]
    public void Normalization_InvalidPathCharacter_OnWindows_Throws()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var ex = Should.Throw<InvalidPathException>(() => MPath.From(@"C:\foo\b<ar"));
        ex.Message.ShouldContain("invalid path character");
    }

    // On Unix only NUL is invalid -- angle brackets and similar Windows-invalid chars pass.
    [Fact]
    public void Normalization_AngleBracketsAllowed_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        Should.NotThrow(() => MPath.From("/foo/b<ar"));
    }

    // macOS rejects ':' in segments because Finder/Carbon legacy translates ':' to '/' in
    // user-visible names, so round-tripping through Finder / AppleScript / NSURL would lie.
    // See PITFALLS C-10 research: POSIX at the kernel level allows ':', but the Mac ecosystem
    // does not.
    [Fact]
    public void Normalization_ColonInSegment_OnMacOS_Throws()
    {
        if (!PlatformTestHelpers.IsMacOS)
            return;

        var ex = Should.Throw<InvalidPathException>(() => MPath.From("/foo/ba:r"));
        ex.Message.ShouldContain("invalid path character");
    }

    // Linux: ':' is a perfectly legal filename character at the ext4/btrfs/xfs layer. POSIX
    // §3.170 forbids only NUL and '/'. Accepting ':' on Linux matches the real filesystem.
    [Fact]
    public void Normalization_ColonInSegment_OnLinux_Allowed()
    {
        if (!PlatformTestHelpers.IsLinux)
            return;

        Should.NotThrow(() => MPath.From("/foo/ba:r"));
    }

    // Unicode (emoji, CJK ideographs, accented Latin) is legal on every platform. NTFS stores
    // UTF-16 code units; APFS and ext4/btrfs are bag-of-bytes and store UTF-8 unchanged. Locks
    // in that Michi does NOT over-reject non-ASCII segments. The test runs on all three OSes
    // with platform-appropriate roots.
    [Theory]
    [InlineData("\ud83d\ude80rocket.txt")] // 🚀 (U+1F680, surrogate pair in UTF-16)
    [InlineData("\u65e5\u672c\u8a9e.txt")] // 日本語
    [InlineData("caf\u00e9.md")] // café (NFC)
    [InlineData("cafe\u0301.md")] // café (NFD)
    public void Normalization_UnicodeSegments_AllowedOnAllPlatforms(string segmentName)
    {
        var root = PlatformTestHelpers.IsWindows ? @"C:\foo\" : "/foo/";
        Should.NotThrow(() => MPath.From(root + segmentName));
    }

    // Relative path resolves against BaseDirectory.
    [Fact]
    public void Normalization_RelativePath_ResolvesAgainstBaseDirectory()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var opts = MPathOptions.Default with {
            BaseDirectory = "/opt/app",
        };

        var p = MPath.From("data/files", opts);
        p.ToUnixString().ShouldBe("/opt/app/data/files");
    }
}
