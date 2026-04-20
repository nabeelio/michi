using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: OPT-01 — MPathOptions record with BaseDirectory/ExpandTilde/ExpandEnvironmentVariables
// requirement: OPT-02 — MPathOptions.Default read-only singleton
//
// D-44: Collection attribute on any test touching MPathOptions.Default.
// Even though Default is read-only, this enforces the hygiene pattern.
[Collection("MPathOptions.Default")]
public class MPathOptionsTests {
    // OPT-01 + D-08: MPathOptions is a record with three init-only properties
    [Fact]
    public void MPathOptions_IsRecord()
    {
        var type = typeof(MPathOptions);
        type.IsSealed.ShouldBeTrue();
        // Record types in C# have a compiler-generated `<Clone>$` method.
        type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
               .ShouldNotBeNull("MPathOptions must be a record (compiler generates <Clone>$)");
    }

    // OPT-01 + D-08: default BaseDirectory is AppContext.BaseDirectory
    [Fact]
    public void DefaultBaseDirectory_IsAppContextBaseDirectory()
    {
        var opts = new MPathOptions();
        opts.BaseDirectory.ShouldBe(AppContext.BaseDirectory);
    }

    // OPT-01 + D-08: ExpandTilde defaults false
    [Fact]
    public void ExpandTilde_DefaultsFalse()
    {
        new MPathOptions().ExpandTilde.ShouldBeFalse();
    }

    // OPT-01 + D-08: ExpandEnvironmentVariables defaults false
    [Fact]
    public void ExpandEnvironmentVariables_DefaultsFalse()
    {
        new MPathOptions().ExpandEnvironmentVariables.ShouldBeFalse();
    }

    // OPT-02 + D-09: Default property exists and is read-only (no setter)
    [Fact]
    public void Default_IsReadOnlyProperty()
    {
        var prop = typeof(MPathOptions).GetProperty(
            nameof(MPathOptions.Default),
            BindingFlags.Static | BindingFlags.Public
        );

        prop.ShouldNotBeNull();
        prop.CanRead.ShouldBeTrue();
        prop.CanWrite.ShouldBeFalse("MPathOptions.Default is { get; } only — D-09 read-only-singleton contract");
    }

    // OPT-02 + D-09: Default returns the same instance on repeat reads
    [Fact]
    public void Default_IsSingleton()
    {
        var a = MPathOptions.Default;
        var b = MPathOptions.Default;
        ReferenceEquals(a, b).ShouldBeTrue();
    }

    // OPT-02 + D-10 + record-with: `with` expression produces an override without mutating Default
    [Fact]
    public void RecordWith_CreatesOverride_WithoutMutatingDefault()
    {
        var before = MPathOptions.Default;
        var overridden = MPathOptions.Default with {
            ExpandTilde = true,,
        };

        overridden.ExpandTilde.ShouldBeTrue();
        MPathOptions.Default.ExpandTilde.ShouldBeFalse(); // Default is unchanged
        ReferenceEquals(before, MPathOptions.Default).ShouldBeTrue();
    }

    // D-10a: consumer can wrap their own static accessor
    [Fact]
    public void Consumer_CanBuildOwnOptionsViaRecordWith()
    {
        var myAppOptions = MPathOptions.Default with {
            BaseDirectory = "/opt/myapp",
            ExpandTilde = true,
            ExpandEnvironmentVariables = true,
        };

        myAppOptions.BaseDirectory.ShouldBe("/opt/myapp");
        myAppOptions.ExpandTilde.ShouldBeTrue();
        myAppOptions.ExpandEnvironmentVariables.ShouldBeTrue();
    }
}

// The collection fixture can be empty — the purpose is test-collection grouping per D-44.
// The "Collection" suffix is required by xUnit's [CollectionDefinition] convention (the attribute
// is typically applied to a type whose name ends in "Collection").
[CollectionDefinition("MPathOptions.Default")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification =
            "xUnit [CollectionDefinition] convention: the target type is named after the collection and conventionally ends in 'Collection'."
)]
public class MPathOptionsDefaultCollection { }
