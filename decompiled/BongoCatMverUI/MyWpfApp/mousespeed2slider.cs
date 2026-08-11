using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp;

public class mousespeed2slider : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		float num = (float)value;
		if ((double)num <= 0.1)
		{
			return 0;
		}
		return 100f * (10f * num - 1f) / (9f + 9f * num);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double num = (double)value;
		return 11.0 / (10.0 - 9.0 * num / 100.0) - 1.0;
	}
}
