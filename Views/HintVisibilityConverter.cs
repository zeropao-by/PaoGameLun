using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameLauncher.Views;

/// <summary>
/// 当 TextBox 有文字时隐藏提示，反之显示
/// </summary>
public class HintVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
