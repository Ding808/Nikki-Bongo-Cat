using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace MyWpfApp.tutorial;

public class Tutorial_WhatYouMustKnow : Page, IComponentConnector
{
	private bool _contentLoaded;

	public Tutorial_WhatYouMustKnow()
	{
		InitializeComponent();
	}

	private void switch_to_aboutbroadcast(object sender, RoutedEventArgs e)
	{
		CommonData.tutorialframe.Source = new Uri("tutorial/Tutorial_AboutBroadcast.xaml", UriKind.Relative);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/tutorial/tutorial_whatyoumustknow.xaml", UriKind.Relative);
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
			((Hyperlink)target).Click += switch_to_aboutbroadcast;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
