using System.Diagnostics.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// Reflection probes for "is record" / "Default is read-only" / default-value checks are
// omitted -- those are compiler-enforced. What remains pins behavior: Default is truly
// immutable under `with` expressions, and relative paths resolve against whatever
// BaseDirectory the options carry.
[Collection("MPathOptions.Default")]
public class MPathOptionsTests {
    // Default BaseDirectory is AppContext.BaseDirectory.
    [Fact]
    public void DefaultBaseDirectory_IsAppContextBaseDirectory() =>
            MPathOptions.Default.BaseDirectory.ShouldBe(AppContext.BaseDirectory);

    // `with` expression produces an override without mutating Default.
    [Fact]
    public void RecordWith_CreatesOverride_WithoutMutatingDefault()
    {
        var before = MPathOptions.Default;
        var overridden = MPathOptions.Default with {
            ExpandTilde = true,
        };

        overridden.ExpandTilde.ShouldBeTrue();
        MPathOptions.Default.ExpandTilde.ShouldBeFalse();
        ReferenceEquals(before, MPathOptions.Default).ShouldBeTrue();
    }
}

// xUnit test-collection grouping for any test touching MPathOptions.Default. Even though
// Default is read-only, the pattern is in place for any future test that needs it.
[CollectionDefinition("MPathOptions.Default")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit [CollectionDefinition] convention requires the 'Collection' suffix."
)]
public class MPathOptionsDefaultCollection;
