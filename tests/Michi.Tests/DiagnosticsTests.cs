using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: DIAG-01 — debugger/JSON/TypeConverter attributes + sealed class
// requirement: CORE-01 — MPath is a sealed class
public class DiagnosticsTests {
    // DIAG-01 + D-02: MPath has [DebuggerDisplay]
    [Fact]
    public void MPath_HasDebuggerDisplayAttribute()
    {
        var attr = typeof(MPath).GetCustomAttribute<DebuggerDisplayAttribute>();
        attr.ShouldNotBeNull();
        attr.Value.ShouldContain("_path");
    }

    // DIAG-01 + D-02: MPath has [JsonConverter] pointing at MPathJsonConverter (even though converter body is stub until Phase 4)
    [Fact]
    public void MPath_HasJsonConverterAttribute()
    {
        var attr = typeof(MPath).GetCustomAttribute<JsonConverterAttribute>();
        attr.ShouldNotBeNull();
        attr.ConverterType.ShouldNotBeNull();
        attr.ConverterType!.Name.ShouldBe("MPathJsonConverter");
    }

    // DIAG-01 + D-02: MPath has [TypeConverter] pointing at MPathTypeConverter
    [Fact]
    public void MPath_HasTypeConverterAttribute()
    {
        var attr = typeof(MPath).GetCustomAttribute<TypeConverterAttribute>();
        attr.ShouldNotBeNull();
        attr.ConverterTypeName.ShouldContain("MPathTypeConverter");
    }

    // DIAG-01 + CORE-01: MPath is sealed
    [Fact]
    public void MPath_IsSealed()
    {
        typeof(MPath).IsSealed.ShouldBeTrue();
    }
}
