using System.ComponentModel;

namespace MyWpfApp;

public class updateStatus : INotifyPropertyChanged
{
	private bool _isloading;

	private bool _loading_result;

	public bool isloading
	{
		get
		{
			return _isloading;
		}
		set
		{
			_isloading = value;
			OnPropertyChanged("isloading");
		}
	}

	public bool loading_result
	{
		get
		{
			return _loading_result;
		}
		set
		{
			_loading_result = value;
			OnPropertyChanged("loading_result");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		if (this.PropertyChanged != null)
		{
			this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
