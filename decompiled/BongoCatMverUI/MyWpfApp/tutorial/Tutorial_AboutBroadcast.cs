using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MyWpfApp.tutorial;

public class Tutorial_AboutBroadcast : Page, IComponentConnector
{
	private bool _contentLoaded;

	public Tutorial_AboutBroadcast()
	{
		InitializeComponent();
	}

	private void Hyperlink_Click(object sender, RoutedEventArgs e)
	{
		Process process = new Process();
		process.StartInfo.FileName = "https://jingyan.baidu.com/article/22a299b562ce0cde18376a22.html";
		process.Start();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/tutorial/tutorial_aboutbroadcast.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		_contentLoaded = true;
	}
}
