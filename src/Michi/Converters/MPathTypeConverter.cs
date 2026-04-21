using System.ComponentModel;
using System.Globalization;
using Michi.Exceptions;

// netstandard2.1's TypeConverter base class declares parameters as non-nullable that net8+ declares
// nullable. Bridging both signatures from a single source file triggers false-positive nullability
// warnings -- the runtime contract matches on every framework.
// ReSharper disable AssignNullToNotNullAttribute

namespace Michi.Converters;

/// <summary>
/// <see cref="TypeConverter" /> for <see cref="MPath" />. Supports round-tripping via the
/// canonical string form -- used by
/// <c>
/// IConfiguration
/// </c>
/// binding, ASP.NET model binding,
/// WPF property grids, and any framework that resolves converters via <see cref="TypeDescriptor" />.
/// </summary>
internal sealed class MPathTypeConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
            destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string s) {
            return base.ConvertFrom(context, culture, value);
        }

        try {
            return MPath.From(s);
        } catch (InvalidPathException ex) {
            throw new FormatException($"Cannot convert '{s}' to MPath: {ex.Message}", ex);
        }
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType
    )
    {
        if (destinationType == typeof(string)) {
            return value?.ToString();
        }

        // netstandard2.1's TypeConverter base signature requires non-null value/destinationType;
        // newer TFMs accept nullables. The null-forgiving operators match the runtime contract --
        // we'd already have returned above if value were null and destinationType were string.
        return base.ConvertTo(context, culture, value!, destinationType);
    }
}
