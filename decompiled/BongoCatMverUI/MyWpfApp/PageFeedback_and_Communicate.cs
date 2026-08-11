using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MyWpfApp;

public class PageFeedback_and_Communicate : Page, IComponentConnector
{
	internal WebBrowser qqweb;

	private bool _contentLoaded;

	public PageFeedback_and_Communicate()
	{
		InitializeComponent();
	}

	private void Add_QQ_1(object sender, RoutedEventArgs e)
	{
		qqweb.Source = new Uri("https://space.bilibili.com/5808772");
		qqweb.Source = new Uri("tencent://groupwpa/?subcmd=all&param=7B2267726F757055696E223A3337343239353932322C2274696D655374616D70223A313538333634353633337D0A");
	}

	private void Add_QQ_2(object sender, RoutedEventArgs e)
	{
		qqweb.Source = new Uri("https://space.bilibili.com/5808772");
		qqweb.Source = new Uri("tencent://groupwpa/?subcmd=all&param=7B2267726F757055696E223A3638353632353234332C2274696D655374616D70223A313538333635343235317D0A");
	}

	private void Add_QQ_3(object sender, RoutedEventArgs e)
	{
		qqweb.Source = new Uri("https://space.bilibili.com/5808772");
		qqweb.Source = new Uri("tencent://groupwpa/?subcmd=all&param=7B2267726F757055696E223A313039323731313738382C2274696D655374616D70223A313539313535313632377D0A");
	}

	private void Add_QQ_4(object sender, RoutedEventArgs e)
	{
		qqweb.Source = new Uri("https://space.bilibili.com/5808772");
		qqweb.Source = new Uri("tencent://groupwpa/?subcmd=all&param=7B2267726F757055696E223A3632303936353533352C2274696D655374616D70223A313539313535313733397D0A");
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/pagefeedback_and_communicate.xaml", UriKind.Relative);
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
			((Button)target).Click += Add_QQ_1;
			break;
		case 2:
			((Button)target).Click += Add_QQ_2;
			break;
		case 3:
			((Button)target).Click += Add_QQ_3;
			break;
		case 4:
			((Button)target).Click += Add_QQ_4;
			break;
		case 5:
			qqweb = (WebBrowser)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
