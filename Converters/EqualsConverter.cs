using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KursMVVM.Converters;

/// <summary>
/// Конвертер сравнения значений. Возвращает true, если значение равно параметру.
/// Используется для переключения видимости графиков.
/// </summary>
public class EqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return value == parameter;

        // Сравнение чисел
        if (value is int intVal && int.TryParse(parameter.ToString(), out int intParam))
            return intVal == intParam;

        if (value is double dblVal && double.TryParse(parameter.ToString(), out double dblParam))
            return Math.Abs(dblVal - dblParam) < 0.0001;

        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
