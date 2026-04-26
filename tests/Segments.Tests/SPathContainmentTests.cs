using System.Diagnostics.Contracts;
using Segments.Exceptions;
using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public sealed class SPathContainmentTests {
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

    // Reusable ZIP-slip corpus. Backslash traversal is included because Segments treats
    // `\\` as a separator on every host before containment checks run.
    public static IEnumerable<object[]> LexicalEscapeAttempts =>
            new[] {
                new object[] { "../etc/passwd" },
                new object[] { "../../etc/passwd" },
                new object[] { "subdir/../../escape" },
                new object[] { "sub\\..\\..\\escape" },
                new object[] { "./../../escape" },
                new object[] { "a/b/../../../escape" },
                new object[] { "../../../../../../../tmp/x" },
                new object[] { "../sibling" },
            };

    // Segments that should remain within the base path.
    public static IEnumerable<object[]> ContainedSuccesses =>
            new[] {
                new object[] {
                    "file.txt",
                    "file.txt",
                },
                new object[] {
                    "sub/nested.txt",
                    "sub/nested.txt",
                },
                new object[] {
                    "deep/./././nested",
                    "deep/nested",
                },
                new object[] {
                    "a/../b",
                    "b",
                },
                new object[] {
                    "/file.txt",
                    "file.txt",
                },
            };

    private static SPath BaseUploads() => SPath.From(BaseUploadsPath);

    private static SPath BaseWww() => SPath.From(BaseWwwPath);

    [Theory]
    [MemberData(nameof(LexicalEscapeAttempts))]
    public void ResolveContained_rejects_lexical_escape(string attack)
    {
        var basePath = BaseUploads();

        var ex = Should.Throw<InvalidPathException>(() => basePath.ResolveContained(attack));

        // Pin the current wording around normalization above the base path.
        ex.Message.ShouldContain("normalizes above");
        // AttemptedPath should preserve the caller input.
        ex.AttemptedPath.ShouldBe(attack);
    }

    [Theory]
    [MemberData(nameof(LexicalEscapeAttempts))]
    public void TryResolveContained_returns_false_on_escape(string attack)
    {
        var basePath = BaseUploads();

        var ok = basePath.TryResolveContained(attack, out var result);

        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(ContainedSuccesses))]
    public void ResolveContained_accepts_valid_segments(string segment, string relativeUnix)
    {
        var basePath = BaseUploads();

        var result = basePath.ResolveContained(segment);

        result.ToUnixString().ShouldBe(BaseUploadsUnix + "/" + relativeUnix);
    }

    [Fact]
    public void ResolveContained_rejects_null_segment()
    {
        var basePath = BaseUploads();

        var ex = Should.Throw<ArgumentNullException>(() => basePath.ResolveContained(null!));

        ex.ParamName.ShouldBe("segment");
    }

    [Fact]
    public void TryResolveContained_throws_on_null_segment()
    {
        // Null stays loud even for the Try* variant.
        var basePath = BaseUploads();

        var ex = Should.Throw<ArgumentNullException>(() => basePath.TryResolveContained(null!, out _));

        ex.ParamName.ShouldBe("segment");
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("  ")]
    public void ResolveContained_rejects_empty_or_pure_dots(string segment)
    {
        var basePath = BaseUploads();

        var ex = Should.Throw<InvalidPathException>(() => basePath.ResolveContained(segment));

        // Pin the current wording for no-op targets.
        ex.Message.ShouldContain("empty or resolves to no change");
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("  ")]
    public void TryResolveContained_returns_false_on_empty_or_pure_dots(string segment)
    {
        // ResolveContained throws for these; TryResolveContained returns false.
        var basePath = BaseUploads();

        var ok = basePath.TryResolveContained(segment, out var result);

        ok.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveContained_rejects_sibling_prefix_false_positive()
    {
        // A naive prefix check would wrongly treat /var/www-evil as contained by /var/www.
        var basePath = BaseWww();

        Should.Throw<InvalidPathException>(() => basePath.ResolveContained("../www-evil/file.txt"));
    }

    [Fact]
    public void ResolveContained_is_marked_Pure()
    {
        var method = typeof(SPath).GetMethod(
            nameof(SPath.ResolveContained),
            [typeof(string)]
        );

        method.ShouldNotBeNull();
        method.GetCustomAttributes(typeof(PureAttribute), false)
               .Length.ShouldBe(1);
    }

    [Fact]
    public void TryResolveContained_is_NOT_marked_Pure()
    {
        // `out` parameters are observable side effects, so [Pure] does not apply here.
        var method = typeof(SPath).GetMethod(nameof(SPath.TryResolveContained));

        method.ShouldNotBeNull();
        method.GetCustomAttributes(typeof(PureAttribute), false)
               .Length.ShouldBe(0);
    }

    [Fact]
    public void ResolveContained_leading_slash_stripped()
    {
        // Leading separators are stripped so absolute-looking input still resolves under the base.
        var basePath = BaseUploads();

        var result = basePath.ResolveContained("/file.txt");

        result.ToUnixString().ShouldBe(BaseUploadsUnix + "/file.txt");
    }

    [Fact]
    public void ResolveContained_all_leading_separators_are_stripped()
    {
        var basePath = BaseUploads();

        var result = basePath.ResolveContained("//nested/file.txt");

        result.ToUnixString().ShouldBe(BaseUploadsUnix + "/nested/file.txt");
    }

    [Fact]
    public void ResolveContained_rejects_drive_letter_escape_on_windows()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        // On Windows, C:/evil stays drive-anchored and fails containment.
        var basePath = SPath.From(@"C:\app");

        Should.Throw<InvalidPathException>(() => basePath.ResolveContained("C:/evil"));
    }

    [Fact]
    public void ResolveContained_treats_UNC_like_segment_as_relative()
    {
        var basePath = BaseUploads();

        var result = basePath.ResolveContained("//server/share/sibling");

        result.ToUnixString().ShouldBe(BaseUploadsUnix + "/server/share/sibling");
    }

    [Fact]
    public void ResolveContained_accepts_segment_that_normalizes_to_base()
    {
        // "a/.." normalizes back to the base and is allowed. Raw ".", "..", and ""
        // are rejected earlier because they do not identify a target.
        var basePath = BaseWww();

        var result = basePath.ResolveContained("a/..");

        result.ShouldBe(basePath);
    }

    [Fact]
    public void TryResolveContained_canonical_zip_extraction_workflow()
    {
        // Typical ZIP extraction flow: valid entries succeed, escapes are skipped.
        var uploads = BaseUploads();

        uploads.TryResolveContained("safe/file.bin", out var safe).ShouldBeTrue();
        safe.ShouldNotBeNull();
        safe.ToUnixString().ShouldBe(BaseUploadsUnix + "/safe/file.bin");

        uploads.TryResolveContained("../../../../etc/passwd", out var escaped).ShouldBeFalse();
        escaped.ShouldBeNull();
    }

    [Fact]
    public void ResolveContained_accepts_descendant_under_unix_root()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var root = SPath.From("/");

        var result = root.ResolveContained("etc/passwd");

        result.ToUnixString().ShouldBe("/etc/passwd");
    }

    [Fact]
    public void ResolveContained_accepts_descendant_under_windows_drive_root()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var root = SPath.From(@"C:\");

        var result = root.ResolveContained("Windows/System32");

        result.ToUnixString().ShouldBe("C:/Windows/System32");
    }
}
