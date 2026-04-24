using Michi.Extensions;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class ExistsPolicyTests {
    // `Fail` is the zero value and validates cleanly. It represents "abort on any collision".
    [Fact]
    public void Fail_is_zero_and_validates()
    {
        ((int) ExistsPolicy.Fail).ShouldBe(0);
        Should.NotThrow(() => ExistsPolicyValidator.Validate(ExistsPolicy.Fail, "policy"));
    }

    public static TheoryData<ExistsPolicy> NamedCombinations => [
        ExistsPolicy.Fail,
        ExistsPolicy.MergeAndSkip,
        ExistsPolicy.MergeAndOverwrite,
        ExistsPolicy.MergeAndOverwriteIfNewer,
    ];

    // Every named combination is a valid policy.
    [Theory]
    [MemberData(nameof(NamedCombinations))]
    public void Named_combinations_validate(ExistsPolicy policy)
    {
        Should.NotThrow(() => ExistsPolicyValidator.Validate(policy, nameof(policy)));
    }

    // Two file behaviors in the same policy is a user error -- the message must name both.
    [Fact]
    public void Conflicting_file_bits_throw()
    {
        var bad = ExistsPolicy.FileSkip | ExistsPolicy.FileOverwrite;

        var ex = Should.Throw<ArgumentException>(() => ExistsPolicyValidator.Validate(bad, "policy"));
        ex.ParamName.ShouldBe("policy");
        ex.Message.ShouldContain("FileSkip");
        ex.Message.ShouldContain("FileOverwrite");
    }

    // Two directory behaviors in the same policy is the same shape of error for directories.
    [Fact]
    public void Conflicting_directory_bits_throw()
    {
        var bad = ExistsPolicy.DirectoryMerge | ExistsPolicy.DirectoryReplace;

        var ex = Should.Throw<ArgumentException>(() => ExistsPolicyValidator.Validate(bad, "policy"));
        ex.ParamName.ShouldBe("policy");
        ex.Message.ShouldContain("DirectoryMerge");
        ex.Message.ShouldContain("DirectoryReplace");
    }

    // A policy combining a file conflict and a legal directory bit still throws on the file conflict.
    [Fact]
    public void Three_way_conflict_still_reports_file_conflict_first()
    {
        var bad = ExistsPolicy.FileSkip | ExistsPolicy.FileOverwrite | ExistsPolicy.DirectoryMerge;

        Should.Throw<ArgumentException>(() => ExistsPolicyValidator.Validate(bad, "policy"));
    }

    // Bits outside the defined range are rejected so consumers can't smuggle in future flags.
    [Fact]
    public void Unknown_flag_bit_throws()
    {
        var bad = (ExistsPolicy) 0x40000000;

        var ex = Should.Throw<ArgumentException>(() => ExistsPolicyValidator.Validate(bad, "policy"));
        ex.ParamName.ShouldBe("policy");
        ex.Message.ShouldContain("unknown");
    }

    // Named combinations must equal the exact bitwise OR they document -- Plan 04 relies on this.
    [Fact]
    public void MergeAndSkip_decomposes_correctly()
    {
        ExistsPolicy.MergeAndSkip.ShouldBe(ExistsPolicy.FileSkip | ExistsPolicy.DirectoryMerge);
    }

    [Fact]
    public void MergeAndOverwrite_decomposes_correctly()
    {
        ExistsPolicy.MergeAndOverwrite.ShouldBe(ExistsPolicy.FileOverwrite | ExistsPolicy.DirectoryMerge);
    }

    [Fact]
    public void MergeAndOverwriteIfNewer_decomposes_correctly()
    {
        ExistsPolicy.MergeAndOverwriteIfNewer.ShouldBe(ExistsPolicy.FileOverwriteIfNewer | ExistsPolicy.DirectoryMerge);
    }
}
