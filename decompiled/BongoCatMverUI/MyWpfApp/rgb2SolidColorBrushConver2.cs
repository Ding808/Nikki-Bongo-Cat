using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MyWpfApp;

[ValueConversion(typeof(int[]), typeof(SolidColorBrush))]
public class rgb2SolidColorBrushConver2 : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		int[] array = (int[])value;
		if (((double)array[0] * 0.3 + (double)array[1] * 0.59 + (double)array[2] * 0.11) / 255.0 > 0.5)
		{
			return new SolidColorBrush(Color.FromRgb(81, 45, 168));
		}
		return new SolidColorBrush(Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		SolidColorBrush solidColorBrush = (SolidColorBrush)value;
		int[] array = new int[4];
		Color color = solidColorBrush.Color;
		array[0] = 255 - color.R;
		array[1] = 255 - color.G;
		array[2] = 255 - color.B;
		return array;
	}
}
