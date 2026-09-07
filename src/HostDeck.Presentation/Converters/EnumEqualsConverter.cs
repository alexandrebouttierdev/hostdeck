using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HostDeck.Presentation.Converters;

/// <summary>
/// Compare la valeur liée à un enum fourni en <c>ConverterParameter</c>.
/// Sert au rail pour activer le bouton de la section courante.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static EnumEqualsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        return Equals(value, parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
