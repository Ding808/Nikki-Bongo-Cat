using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp;

[ValueConversion(typeof(int), typeof(string))]
public class inputmode2textConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		if ((int)value == 1)
		{
			return "XInput";
		}
		return "DirectInput";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if ((string)value == "XInput")
		{
			return 1;
		}
		return 2;
	}
}
