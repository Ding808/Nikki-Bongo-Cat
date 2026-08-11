using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace MyWpfApp.tutorial;

public class Tutorial_HowToAsk : Page, IComponentConnector
{
	private bool _contentLoaded;

	public Tutorial_HowToAsk()
	{
		InitializeComponent();
	}

	private void Hyperlink_Click(object sender, RoutedEventArgs e)
	{
		Process process = new Process();
		process.StartInfo.FileName = "https://github.com/dogfight360/Stop-Ask-Questions-The-Stupid-Ways/blob/master/README.md";
		process.Start();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/tutorial/tutorial_howtoask.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 1)
		{
			((Hyperlink)target).Click += Hyperlink_Click;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
