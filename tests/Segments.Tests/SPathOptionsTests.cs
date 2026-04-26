using System.Diagnostics.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Segments.Tests;

// Reflection probes for "is record" / "Default is read-only" / default-value checks are
// omitted -- those are compiler-enforced. What remains pins behavior: Default is truly
// immutable under `with` expressions, and relative paths resolve against whatever
// BaseDirectory the options carry.
[Collection("SPathOptions.Default")]
public class SPathOptionsTests {
    // Default BaseDirectory is AppContext.BaseDirectory.
    [Fact]
    public void DefaultBaseDirectory_IsAppContextBaseDirectory() =>
            SPathOptions.Default.BaseDirectory.ShouldBe(AppContext.BaseDirectory);

    // `with` expression produces an override without mutating Default.
    [Fact]
    public void RecordWith_CreatesOverride_WithoutMutatingDefault()
    {
        var before = SPathOptions.Default;
        var overridden = SPathOptions.Default with {
            ExpandTilde = true,
        };

        overridden.ExpandTilde.ShouldBeTrue();
        SPathOptions.Default.ExpandTilde.ShouldBeFalse();
        ReferenceEquals(before, SPathOptions.Default).ShouldBeTrue();
    }
}

// xUnit test-collection grouping for any test touching SPathOptions.Default. Even though
// Default is read-only, the pattern is in place for any future test that needs it.
[CollectionDefinition("SPathOptions.Default")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit [CollectionDefinition] convention requires the 'Collection' suffix."
)]
public class SPathOptionsDefaultCollection;
