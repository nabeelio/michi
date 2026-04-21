using Michi.Exceptions;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// Trivial ctor + property + type-hierarchy tests are omitted -- they assert compiler-enforced
// invariants (subclass relationship, property assignment, inner-exception chaining) rather
// than library behavior. What remains pins user-facing exception messages.
public class MPathExceptionsTests {
    // From(null) ArgumentNullException has the full actionable message.
    [Fact]
    public void ArgumentNullException_FromNullPath_HasActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.From(null!));
        ex.Message.ShouldContain("Path cannot be null");
        ex.Message.ShouldContain("Use TryFrom");
    }

    // Format(null) ArgumentNullException has the full actionable message.
    [Fact]
    public void ArgumentNullException_FromNullFormatTemplate_HasActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.Format(null!, "x"));
        ex.Message.ShouldContain("Format template cannot be null");
        ex.Message.ShouldContain("string.Format-style template");
    }

    // Empty-path InvalidPathException names "empty" in the reason.
    [Fact]
    public void InvalidPathException_FromEmptyPath_NamesEmptyInReason()
    {
        var ex = Should.Throw<InvalidPathException>(() => MPath.From(""));
        ex.Message.ShouldContain("empty");
    }

    // Invalid-char InvalidPathException names the offending character and its hex code.
    [Fact]
    public void InvalidPathException_FromInvalidChar_NamesCharacterAndHex()
    {
        var ex = Should.Throw<InvalidPathException>(() => MPath.Format("/home/{0}", "a\0b"));
        ex.Message.ShouldContain("invalid path character");
        ex.Message.ShouldContain("0x0000");
    }
}
