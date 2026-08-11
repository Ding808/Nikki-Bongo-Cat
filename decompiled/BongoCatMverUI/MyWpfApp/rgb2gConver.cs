using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp;

[ValueConversion(typeof(int[]), typeof(int))]
public class rgb2gConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		return ((int[])value)[1];
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double num = (double)value;
		CommonData.config.decoration_rgb[1] = (int)num;
		return CommonData.config.decoration_rgb;
	}
}
