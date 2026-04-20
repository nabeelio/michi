using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: CORE-02 — Format() substitution + invariant culture + invalid-char rejection
public class MPathFormatTests {
    // CORE-02 + D-06: basic substitution via string.Format semantics
    [Fact]
    public void Format_BasicSubstitution_WorksLikeStringFormat()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.Format("/home/{0}/docs", "alice");
        p.ToUnixString().ShouldBe("/home/alice/docs");
    }

    // CORE-02 + D-06: double-brace escaping produces literal braces
    [Fact]
    public void Format_DoubleBraces_ProduceLiteralBraces()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.Format("/var/{{literal}}/{0}", "x");
        p.ToUnixString().ShouldBe("/var/{literal}/x");
    }

    // CORE-02 + D-06: null template throws ArgumentNullException with verbose message
    [Fact]
    public void Format_NullTemplate_ThrowsArgumentNullException_WithActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.Format(null!, "x"));
        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain("string.Format-style template");
    }

    // CORE-02 + D-06 + PITFALLS C-08: culture invariant — numeric formatting doesn't use current locale
    [Fact]
    public void Format_NumericArg_UsesInvariantCulture()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.Format("/data/{0}", 1234);
        // Invariant culture: no thousand separator, no decimal comma
        p.ToUnixString().ShouldBe("/data/1234");
    }

    // PITFALLS C-10: invalid path characters are rejected with a message naming the character + hex code
    [Fact]
    public void Format_NullCharInSubstitution_ThrowsInvalidPathException_NamingCharAndHex()
    {
        var bad = "evil\0name";
        var ex = Should.Throw<InvalidPathException>(() => MPath.Format("/home/{0}", bad));
        ex.Message.ShouldContain("invalid path character");
        ex.Message.ShouldContain("0x0000");
        ex.AttemptedPath.ShouldContain("evil");
    }

    // PITFALLS C-12: Format XML doc warns about traversal via user input — this test documents that the behavior
    // allows traversal (the safety warning is in docs, not in code). Consumer must use ResolveContained for safety.
    [Fact]
    public void Format_TraversalInUserInput_NormalizesButReachesParent_DocumentedAsRiskInXmlDoc()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var userInput = "../etc";
        var p = MPath.Format("/home/{0}/docs", userInput);
        // Normalization resolves `..` — the result escapes /home/.
        p.ToUnixString().ShouldBe("/etc/docs");
        // This test asserts the documented (unsafe-for-untrusted-input) behavior.
        // See MPath.Format XML doc's <remarks> block for the SECURITY warning.
    }
}
