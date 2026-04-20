using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: NORM-01 — six-rule normalization pipeline
public class MPathNormalizationTests {
    // NORM-01 rule 1: resolve `..` segments
    [Theory]
    [InlineData("/foo/../bar", "/bar")]
    [InlineData("/foo/bar/../baz", "/foo/baz")]
    [InlineData("/foo/../../bar", "/bar")] // `..` past root stops at root
    public void Normalization_ResolvesDoubleDot_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // NORM-01 rule 1: resolve `.` segments
    [Theory]
    [InlineData("/foo/./bar", "/foo/bar")]
    [InlineData("/foo/./bar/./baz", "/foo/bar/baz")]
    public void Normalization_ResolvesSingleDot_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // NORM-01 rule 2: collapse repeated separators
    [Theory]
    [InlineData("/foo//bar", "/foo/bar")]
    [InlineData("/foo///bar////baz", "/foo/bar/baz")]
    public void Normalization_CollapsesRepeatedSeparators_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // NORM-01 rule 3: backslash input is normalized to forward slash internally
    [Theory]
    [InlineData("/foo\\bar", "/foo/bar")]
    [InlineData("/a\\b\\c", "/a/b/c")]
    public void Normalization_BackslashInput_NormalizesToForwardSlash_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // NORM-01 rule 4: trailing slash is stripped (unless root)
    [Theory]
    [InlineData("/foo/bar/", "/foo/bar")]
    [InlineData("/foo/", "/foo")]
    public void Normalization_StripsTrailingSlash_OnUnix(string input, string expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(input).ToUnixString().ShouldBe(expected);
    }

    // NORM-01 rule 4 (edge): root path preserves its trailing slash
    [Fact]
    public void Normalization_RootPath_PreservesTrailingSlash_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/").ToUnixString().ShouldBe("/");
    }

    // Full pipeline per phase success criterion 1
    [Fact]
    public void Normalization_FullPipeline_MatchesPhase1Criterion1()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/foo/../bar//baz/");
        p.ToUnixString().ShouldBe("/bar/baz");
    }

    // NORM-01 rule 5: tilde expansion OFF by default — tilde remains literal (not expanded).
    [Fact]
    public void Normalization_TildeExpansion_OffByDefault_ExpandsNotPerformed()
    {
        // With default options, ~/foo is treated as a relative path containing a
        // literal '~' — it is resolved against BaseDirectory. The important property
        // is that the tilde is NOT REPLACED by the user-profile directory during
        // normalization. We prove that by confirming the literal '~' survives into
        // the canonical output. (Checking ShouldNotStartWith(UserProfile) is too
        // strict because BaseDirectory is itself usually under the user profile.)
        var p = MPath.From("~/foo"); // default options, ExpandTilde = false
        p.ToUnixString().ShouldContain("~");
    }

    // NORM-01 rule 5: tilde expansion ON via options — tilde IS expanded
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

    // NORM-01 rule 6: env var expansion OFF by default — literal "$VAR" preserved
    [Fact]
    public void Normalization_EnvVarExpansion_OffByDefault_LiteralPreserved()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/data/$NONEXISTENT_VAR_XYZ/foo");
        p.ToUnixString().ShouldContain("$NONEXISTENT_VAR_XYZ");
    }

    // PITFALLS C-04: normalization does NOT touch the filesystem
    [Fact]
    public void Normalization_DoesNotAccessFilesystem()
    {
        // Construct an MPath for a non-existent path — must not throw, must not hang on slow FS.
        if (PlatformTestHelpers.IsWindows)
            return;

        Should.NotThrow(() => MPath.From("/this/path/definitely/does/not/exist/anywhere"));
    }

    // PITFALLS C-10: invalid path characters on Windows throw InvalidPathException
    [Fact]
    public void Normalization_InvalidPathCharacter_OnWindows_Throws()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var ex = Should.Throw<InvalidPathException>(() => MPath.From(@"C:\foo\b<ar"));
        ex.Message.ShouldContain("invalid path character");
    }

    // PITFALLS C-10: on Unix, only NUL is invalid; other chars that are invalid on Windows are fine
    [Fact]
    public void Normalization_AngleBracketsAllowed_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        Should.NotThrow(() => MPath.From("/foo/b<ar"));
    }

    // D-45: relative path resolves against BaseDirectory (full pipeline smoke)
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
