using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp;

[ValueConversion(typeof(bool), typeof(string))]
public class SoundKeepConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		if ((bool)value)
		{
			return "同时播放";
		}
		return "重新播放";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (string)value switch
		{
			"同时播放" => true, 
			"重新播放" => false, 
			_ => null, 
		};
	}
}
