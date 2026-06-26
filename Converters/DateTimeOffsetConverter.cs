using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KursMVVM.Converters;

public class DateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
            return dto.DateTime;
        if (value is DateTime dt)
            return dt;
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
            return new DateTimeOffset(dt);
        if (value is DateTimeOffset dto)
            return dto;
        return value;
    }
}