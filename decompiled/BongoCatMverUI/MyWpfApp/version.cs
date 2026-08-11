using System.ComponentModel;

namespace MyWpfApp;

public class version : INotifyPropertyChanged
{
	public static string version1 = "0";

	public static string version2 = "1";

	public static string version3 = "6";

	public static string version4 = "0";

	private static bool _is_lastest;

	public bool is_lastest
	{
		get
		{
			return _is_lastest;
		}
		set
		{
			_is_lastest = value;
			OnPropertyChanged("is_lastest");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public static bool is_lastestversion(string v1, string v2, string v3, string v4)
	{
		if (v1.Equals(version1) && v2.Equals(version2) && v3.Equals(version3) && v4.Equals(version4))
		{
			return true;
		}
		return false;
	}

	protected void OnPropertyChanged(string propertyName)
	{
		if (this.PropertyChanged != null)
		{
			this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
