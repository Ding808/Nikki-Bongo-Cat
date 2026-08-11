using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp;

[ValueConversion(typeof(bool), typeof(string))]
public class EmoticonKeepConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		if ((bool)value)
		{
			return "按键取消";
		}
		return "松手取消";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (string)value switch
		{
			"按键取消" => true, 
			"松手取消" => false, 
			_ => null, 
		};
	}
}
