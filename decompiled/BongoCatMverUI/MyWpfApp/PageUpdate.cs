using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml;

namespace MyWpfApp;

public class PageUpdate : Page, IComponentConnector
{
	private string verson1;

	private string verson2;

	private string verson3;

	private string verson4;

	private string downloadlink;

	private Grid serverpage;

	private string pagecontent;

	private static updateStatus updatestatus = new updateStatus();

	internal Grid GridForLoadingSuccess;

	internal Grid serverframe;

	internal Grid GridForLoadingFailure;

	internal ProgressBar IndeterminateToDeterminateCircularProgress;

	private bool _contentLoaded;

	private void refresh(object sender, RoutedEventArgs e)
	{
	}

	private void click_download(object sender, RoutedEventArgs e)
	{
		Process process = new Process();
		process.StartInfo.FileName = downloadlink;
		process.Start();
	}

	public PageUpdate()
	{
		InitializeComponent();
		base.DataContext = updatestatus;
		GridForLoadingSuccess.DataContext = CommonData.version;
		GridForLoadingSuccess.Visibility = Visibility.Collapsed;
		GridForLoadingFailure.Visibility = Visibility.Collapsed;
		updatestatus.isloading = true;
		new Thread(ReadVerson).Start();
	}

	public void ReadVerson()
	{
		try
		{
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://150.158.110.86/bongocatpage/lastestversion.txt");
			httpWebRequest.Timeout = 5000;
			if (((HttpWebResponse)httpWebRequest.GetResponse()).StatusCode != HttpStatusCode.RequestTimeout)
			{
				using (StreamReader streamReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream(), Encoding.UTF8))
				{
					verson1 = streamReader.ReadLine();
					verson2 = streamReader.ReadLine();
					verson3 = streamReader.ReadLine();
					verson4 = streamReader.ReadLine();
					downloadlink = streamReader.ReadLine();
				}
				CommonData.version.is_lastest = version.is_lastestversion(verson1, verson2, verson3, verson4);
				HttpWebRequest httpWebRequest2 = (HttpWebRequest)WebRequest.Create("http://150.158.110.86/bongocatpage/pageupdate.xaml");
				httpWebRequest2.Timeout = 5000;
				if (((HttpWebResponse)httpWebRequest2.GetResponse()).StatusCode != HttpStatusCode.RequestTimeout)
				{
					using (StreamReader streamReader2 = new StreamReader(httpWebRequest2.GetResponse().GetResponseStream(), Encoding.Unicode))
					{
						StringBuilder stringBuilder = new StringBuilder();
						stringBuilder.Append(streamReader2.ReadToEnd());
						pagecontent = stringBuilder.ToString();
					}
					base.Dispatcher.BeginInvoke((Action)delegate
					{
						updatestatus.loading_result = true;
						updatestatus.isloading = false;
						GridForLoadingSuccess.Visibility = Visibility.Visible;
						XmlReader reader = XmlReader.Create(new StringReader(pagecontent));
						serverpage = (Grid)XamlReader.Load(reader);
						serverframe.Children.Add(serverpage);
					});
				}
				else
				{
					base.Dispatcher.BeginInvoke((Action)delegate
					{
						updatestatus.isloading = false;
						updatestatus.loading_result = false;
					});
				}
			}
			else
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					updatestatus.isloading = false;
					updatestatus.loading_result = false;
				});
			}
		}
		catch (WebException)
		{
			verson1 = version.version1;
			verson2 = version.version2;
			verson3 = version.version3;
			verson4 = version.version4;
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				updatestatus.isloading = false;
				updatestatus.loading_result = false;
				GridForLoadingFailure.Visibility = Visibility.Visible;
			});
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/pageupdate.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			GridForLoadingSuccess = (Grid)target;
			break;
		case 2:
			((Hyperlink)target).Click += click_download;
			break;
		case 3:
			serverframe = (Grid)target;
			break;
		case 4:
			GridForLoadingFailure = (Grid)target;
			break;
		case 5:
			IndeterminateToDeterminateCircularProgress = (ProgressBar)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
