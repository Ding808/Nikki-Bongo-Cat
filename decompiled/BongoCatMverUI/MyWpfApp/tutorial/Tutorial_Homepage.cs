using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace MyWpfApp.tutorial;

public class Tutorial_Homepage : Page, IComponentConnector
{
	private bool _contentLoaded;

	public Tutorial_Homepage()
	{
		InitializeComponent();
	}

	private void Click_HowToUseThisUI(object sender, RoutedEventArgs e)
	{
		CommonData.tutorialframe.Source = new Uri("tutorial/Tutorial_HowToUseThisUI.xaml", UriKind.Relative);
	}

	private void Click_WhatYouMustKnow(object sender, RoutedEventArgs e)
	{
		CommonData.tutorialframe.Source = new Uri("tutorial/Tutorial_WhatYouMustKnow.xaml", UriKind.Relative);
	}

	private void Click_AboutBroadcast(object sender, RoutedEventArgs e)
	{
		CommonData.tutorialframe.Source = new Uri("tutorial/Tutorial_AboutBroadcast.xaml", UriKind.Relative);
	}

	private void Click_DIYCat(object sender, RoutedEventArgs e)
	{
		CommonData.tutorialframe.Source = new Uri("tutorial/Tutorial_DIYCat.xaml", UriKind.Relative);
	}

	private void Click_CatFreeze(object sender, RoutedEventArgs e)
	{
		CommonData.tutorialframe.Source = new Uri("tutorial/Question_CatFreeze.xaml", UriKind.Relative);
	}

	private void Click_Keymap(object sender, RoutedEventArgs e)
	{
		CommonData.tutorialframe.Source = new Uri("tutorial/Tutorial_Keymap.xaml", UriKind.Relative);
	}

	private void Click_Manu(object sender, RoutedEventArgs e)
	{
		CommonData.manuToggle_page.IsChecked = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/tutorial/tutorial_homepage.xaml", UriKind.Relative);
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
			((Hyperlink)target).Click += Click_HowToUseThisUI;
			break;
		case 2:
			((Hyperlink)target).Click += Click_WhatYouMustKnow;
			break;
		case 3:
			((Hyperlink)target).Click += Click_AboutBroadcast;
			break;
		case 4:
			((Hyperlink)target).Click += Click_DIYCat;
			break;
		case 5:
			((Hyperlink)target).Click += Click_Keymap;
			break;
		case 6:
			((Hyperlink)target).Click += Click_CatFreeze;
			break;
		case 7:
			((Hyperlink)target).Click += Click_Manu;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
