using System.Diagnostics.Contracts;
using Michi.Exceptions;
using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public sealed class MPathContainmentTests {
    // Platform-appropriate base path. On Windows, /var/www/uploads resolves against the
    // current drive, so we use an explicit C: base for determinism. On Unix we use the
    // literal path. ToUnixString() comparisons then fold out the remaining separator
    // divergence.
    private static readonly string BaseUploadsPath =
            PlatformTestHelpers.IsWindows ? @"C:\var\www\uploads" : "/var/www/uploads";

    private static readonly string BaseUploadsUnix =
            PlatformTestHelpers.IsWindows ? "C:/var/www/uploads" : "/var/www/uploads";

    private static readonly string BaseWwwPath =
            PlatformTestHelpers.IsWindows ? @"C:\var\www" : "/var/www";

    private static MPath BaseUploads() => MPath.From(BaseUploadsPath);

    private static MPath BaseWww() => MPath.From(BaseWwwPath);

    // D-47: reusable ZIP-slip attack corpus. Future phases (HIER-01 when it lands) can
    // reference this via [MemberData(nameof(LexicalEscapeAttempts))] for regression
    // coverage without re-listing the vectors.
    //
    // The backslash-traversal variants live in WindowsOnlyEscapeAttempts because on Linux
    // `\\` is a valid filename character, not a separator -- so the segment normalizes
    // under the base rather than escaping. Testing platform-divergent inputs under a
    // platform-independent theory would produce divergent assertions for the same input.
    public static IEnumerable<object[]> LexicalEscapeAttempts =>
            new[] {
                new object[] { "../etc/passwd" },
                new object[] { "../../etc/passwd" },
                new object[] { "subdir/../../escape" },
                new object[] { "./../../escape" },
                new object[] { "a/b/../../../escape" },
                new object[] { "../../../../../../../tmp/x" },
                new object[] { "../sibling" },
            };

    // D-48: positive-case corpus. Segments that MUST be accepted so we confirm no
    // over-rejection. `expectedUnix` is appended to BaseUploadsUnix at runtime to
    // keep the expected shape platform-appropriate.
    public static IEnumerable<object[]> ContainedSuccesses =>
            new[] {
                new object[] { "file.txt", "file.txt" },
                new object[] { "sub/nested.txt", "sub/nested.txt" },
                new object[] { "deep/./././nested", "deep/nested" },
                new object[] { "a/../b", "b" },
                new object[] { "/file.txt", "file.txt" }, // D-42: leading-slash stripped
            };

    // D-46 / P2-02: Windows-only attack vectors. Backslash-traversal variants only make
    // sense where `\\` is a path separator (NTFS). On Unix they're normal filename chars.
    public static IEnumerable<object[]> WindowsOnlyEscapeAttempts =>
            new[] {
                new object[] { "..\\..\\..\\Windows" },
                new object[] { "..\\etc\\passwd" },
                new object[] { "sub\\..\\..\\escape" },
            };

    // ===== Test 1: lexical escape rejection (throwing variant) =====
    [Theory]
    [MemberData(nameof(LexicalEscapeAttempts))]
    public void ResolveContained_rejects_lexical_escape(string attack)
    {
        var basePath = BaseUploads();

        var ex = Should.Throw<InvalidPathException>(() => basePath.ResolveContained(attack));

        // D-44 message wording pin: the "normalizes above" substring is SemVer-governed.
        ex.Message.ShouldContain("normalizes above");
        // Caller context preserved: AttemptedPath is the ORIGINAL segment, not the
        // stripped or normalized form.
        ex.AttemptedPath.ShouldBe(attack);
    }

    // ===== Test 2: lexical escape rejection (Try variant returns false) =====
    [Theory]
    [MemberData(nameof(LexicalEscapeAttempts))]
    public void TryResolveContained_returns_false_on_escape(string attack)
    {
        var basePath = BaseUploads();

        var ok = basePath.TryResolveContained(attack, out var result);

        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    // ===== Test 3: positive containment =====
    [Theory]
    [MemberData(nameof(ContainedSuccesses))]
    public void ResolveContained_accepts_valid_segments(string segment, string relativeUnix)
    {
        var basePath = BaseUploads();

        var result = basePath.ResolveContained(segment);

        result.ToUnixString().ShouldBe(BaseUploadsUnix + "/" + relativeUnix);
    }

    // ===== Test 4: null segment (throwing variant) =====
    [Fact]
    public void ResolveContained_rejects_null_segment()
    {
        var basePath = BaseUploads();

        var ex = Should.Throw<ArgumentNullException>(() => basePath.ResolveContained(null!));

        ex.ParamName.ShouldBe("segment");
    }

    // ===== Test 5: null segment (Try variant also throws, D-40) =====
    [Fact]
    public void TryResolveContained_throws_on_null_segment()
    {
        // D-40: null stays loud even through Try*. Mirrors Phase 01 TryFrom's null contract.
        var basePath = BaseUploads();

        var ex = Should.Throw<ArgumentNullException>(
            () => basePath.TryResolveContained(null!, out _)
        );

        ex.ParamName.ShouldBe("segment");
    }

    // ===== Test 6: empty / pure-dots rejection (throwing variant) =====
    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("  ")]
    public void ResolveContained_rejects_empty_or_pure_dots(string segment)
    {
        var basePath = BaseUploads();

        var ex = Should.Throw<InvalidPathException>(() => basePath.ResolveContained(segment));

        // D-44 message wording pin.
        ex.Message.ShouldContain("empty or resolves to no change");
    }

    // ===== Test 7: empty / pure-dots Try variant returns false (D-41) =====
    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("  ")]
    public void TryResolveContained_returns_false_on_empty_or_pure_dots(string segment)
    {
        // D-41: these throw from ResolveContained but Try* returns false cleanly.
        var basePath = BaseUploads();

        var ok = basePath.TryResolveContained(segment, out var result);

        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    // ===== Test 8: sibling-prefix false-positive regression (D-37, T-02-03) =====
    [Fact]
    public void ResolveContained_rejects_sibling_prefix_false_positive()
    {
        // D-37 regression: /var/www-evil MUST NOT be contained in /var/www. If IsContained
        // used naive StartsWith, this would incorrectly succeed -- the CWE-22 sibling-prefix
        // class of bug Nuke's AbsolutePath shipped before the boundary-guard fix.
        var basePath = BaseWww();

        Should.Throw<InvalidPathException>(
            () => basePath.ResolveContained("../www-evil/file.txt")
        );
    }

    // ===== Test 9a: [Pure] attribute present on ResolveContained (D-56) =====
    [Fact]
    public void ResolveContained_is_marked_Pure()
    {
        var method = typeof(MPath).GetMethod(
            nameof(MPath.ResolveContained),
            [typeof(string)]
        );

        method.ShouldNotBeNull();
        method.GetCustomAttributes(typeof(PureAttribute), inherit: false)
              .Length.ShouldBe(1);
    }

    // ===== Test 9b: [Pure] NOT applied to TryResolveContained (D-56) =====
    [Fact]
    public void TryResolveContained_is_NOT_marked_Pure()
    {
        // D-56: an `out` parameter mutates the caller's stack -- that's a side effect, so
        // [Pure] doesn't apply. Pin the negative so it can't be retrofitted incorrectly.
        var method = typeof(MPath).GetMethod(nameof(MPath.TryResolveContained));

        method.ShouldNotBeNull();
        method.GetCustomAttributes(typeof(PureAttribute), inherit: false)
              .Length.ShouldBe(0);
    }

    // ===== Test 10: leading-separator strip regression (D-42) =====
    [Fact]
    public void ResolveContained_leading_slash_stripped()
    {
        // D-42 regression: "/file.txt" is stripped to "file.txt" and joined under the base,
        // matching the `/` operator's RHS-always-relative rule. This differs from
        // Path.Combine which would treat "/file.txt" as absolute and discard the left side.
        var basePath = BaseUploads();

        var result = basePath.ResolveContained("/file.txt");

        result.ToUnixString().ShouldBe(BaseUploadsUnix + "/file.txt");
    }

    // ===== Test 11: Windows drive-letter escape on Windows (T-02-06, P2-02) =====
    [Fact]
    public void ResolveContained_rejects_drive_letter_escape_on_windows()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        // On Windows, "C:/evil" is a drive-anchored absolute path, not a filename segment.
        // After D-42 strips nothing (no leading separator), Path.GetFullPath treats it as
        // absolute and returns "C:/evil" -- which fails the containment check against
        // "C:/app" and throws.
        var basePath = MPath.From(@"C:\app");

        Should.Throw<InvalidPathException>(() => basePath.ResolveContained("C:/evil"));
    }

    // ===== Test 12: Windows backslash traversal (D-46, P2-02) =====
    [Theory]
    [MemberData(nameof(WindowsOnlyEscapeAttempts))]
    public void ResolveContained_rejects_backslash_traversal_on_windows(string attack)
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var basePath = MPath.From(@"C:\var\www\uploads");

        Should.Throw<InvalidPathException>(() => basePath.ResolveContained(attack));
    }

    // ===== Test 13: UNC segment against local base on Windows (P2-03) =====
    [Fact]
    public void ResolveContained_handles_UNC_segment_against_local_base_on_windows()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        // D-42 strips ONE leading separator from "//server/share/sibling" producing
        // "/server/share/sibling". On Windows, Path.GetFullPath resolves this against the
        // current drive root -- producing e.g. "C:/server/share/sibling" -- which is NOT
        // under "C:/app", so containment rejects. P2-03 pinned.
        var basePath = MPath.From(@"C:\app");

        Should.Throw<InvalidPathException>(
            () => basePath.ResolveContained("//server/share/sibling")
        );
    }

    // ===== Test 14: exact-equality edge case (D-37 exact-match branch) =====
    [Fact]
    public void ResolveContained_accepts_segment_that_normalizes_to_base()
    {
        // "a/.." cancels out -- the normalized result equals the base exactly. D-37's
        // IsContained exact-equality branch allows this. Contrast with D-41 which rejects
        // the raw inputs ".", "..", "" (those never reach the boundary check because
        // TryResolveContainedCore's pure-dots guard rejects them first).
        //
        // Deliberate asymmetry: a literal "." segment means "I computed nothing meaningful"
        // (programmer bug); a segment that happens to cancel out means "I computed
        // something that resolves to self" (legitimate use).
        var basePath = BaseWww();

        var result = basePath.ResolveContained("a/..");

        result.ShouldBe(basePath);
    }

    // ===== Test 15: canonical ZIP-extraction use case (caller workflow) =====
    [Fact]
    public void TryResolveContained_canonical_zip_extraction_workflow()
    {
        // The motivating use case: extracting archive entries under a trusted base.
        // Legitimate entry succeeds; escaping entry returns false -- consumer skips.
        var uploads = BaseUploads();

        uploads.TryResolveContained("safe/file.bin", out var safe).ShouldBeTrue();
        safe.ShouldNotBeNull();
        safe.ToUnixString().ShouldBe(BaseUploadsUnix + "/safe/file.bin");

        uploads.TryResolveContained("../../../../etc/passwd", out var escaped).ShouldBeFalse();
        escaped.ShouldBeNull();
    }
}
