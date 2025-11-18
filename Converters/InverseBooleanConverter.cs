using System;
using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace ExportExt3;

/// <summary>
/// Simple converter used to invert boolean bindings without creating extra code-behind.
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolean
            ? !boolean
            : Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolean
            ? !boolean
            : Binding.DoNothing;
    }
}
