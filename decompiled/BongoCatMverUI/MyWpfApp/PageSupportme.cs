using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shapes;

namespace MyWpfApp;

public class PageSupportme : Page, IComponentConnector
{
	private bool _contentLoaded;

	public PageSupportme()
	{
		InitializeComponent();
	}

	private void click_bilibili(object sender, MouseButtonEventArgs e)
	{
		Process process = new Process();
		process.StartInfo.FileName = "https://space.bilibili.com/5808772";
		process.Start();
	}

	private void click_aifadian(object sender, MouseButtonEventArgs e)
	{
		Process process = new Process();
		process.StartInfo.FileName = "https://afdian.net/@MMmmmoko";
		process.Start();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/pagesupportme.xaml", UriKind.Relative);
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
			((Image)target).MouseDown += click_bilibili;
			break;
		case 2:
			((Rectangle)target).MouseDown += click_aifadian;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
