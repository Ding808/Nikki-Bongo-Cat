using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp;

public class UsingL2d2visibilityConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		if ((bool)value)
		{
			return 50;
		}
		return 0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (int)value switch
		{
			50 => true, 
			0 => null, 
			_ => null, 
		};
	}
}
