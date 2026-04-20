using System.ComponentModel;
using System.Globalization;

namespace Michi;

/// <summary>
/// Phase 1 stub. Full <see cref="MPath" /> <see cref="TypeConverter" /> support
/// (IConfiguration binding, ASP.NET model binding, WPF property grid) lands in Phase 4.
/// </summary>
internal sealed class MPathTypeConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        throw new NotImplementedException("MPathTypeConverter ships in Phase 4.");
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        throw new NotImplementedException("MPathTypeConverter ships in Phase 4.");
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        throw new NotImplementedException("MPathTypeConverter ships in Phase 4.");
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType
    )
    {
        throw new NotImplementedException("MPathTypeConverter ships in Phase 4.");
    }
}
