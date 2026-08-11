using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWpfApp;

[ValueConversion(typeof(bool), typeof(string))]
public class UpdateboolConver : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		if ((bool)value)
		{
			return "当前已经是最新版本，不需要更新啦";
		}
		return "发现新版本，点击下载";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (string)value switch
		{
			"当前已经是最新版本，不需要更新啦" => true, 
			"发现新版本，点击下载" => false, 
			_ => null, 
		};
	}
}
