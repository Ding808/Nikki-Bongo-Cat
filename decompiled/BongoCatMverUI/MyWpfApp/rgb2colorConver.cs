using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MyWpfApp;

[ValueConversion(typeof(int[]), typeof(Color))]
public class rgb2colorConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		int[] array = (int[])value;
		return Color.FromRgb((byte)array[0], (byte)array[1], (byte)array[2]);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		Color color = (Color)value;
		return new int[4] { color.R, color.G, color.B, 0 };
	}
}
