using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MyWpfApp;

[ValueConversion(typeof(int[]), typeof(SolidColorBrush))]
public class rgb2SolidColorBrushConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		int[] array = (int[])value;
		return new SolidColorBrush(Color.FromRgb((byte)array[0], (byte)array[1], (byte)array[2]));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		SolidColorBrush solidColorBrush = (SolidColorBrush)value;
		int[] array = new int[4];
		Color color = solidColorBrush.Color;
		array[0] = color.R;
		array[1] = color.G;
		array[2] = color.B;
		return array;
	}
}
