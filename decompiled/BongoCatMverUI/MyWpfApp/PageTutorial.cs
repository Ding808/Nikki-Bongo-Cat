using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace MyWpfApp;

public class PageTutorial : Page, IComponentConnector
{
	internal Frame frame_tutorial;

	internal Grid grid_tutorialmanu;

	internal TreeView manu_tutorial;

	internal TreeViewItem treeView_FAQ;

	internal ToggleButton manuToggle_manu;

	internal ToggleButton manuToggle_page;

	private bool _contentLoaded;

	public PageTutorial()
	{
		InitializeComponent();
		CommonData.tutorialframe = frame_tutorial;
		CommonData.manuToggle_page = manuToggle_page;
		CommonData.manu_tutorial = manu_tutorial;
	}

	private void ListBoxItem_Selected(object sender, RoutedEventArgs e)
	{
	}

	private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
	{
	}

	private void CloseTutorialManu(object sender, RoutedEventArgs e)
	{
		(base.Resources["is_tutorial_manu_close_animation"] as Storyboard).Begin();
		if (manuToggle_manu.IsChecked == true)
		{
			manuToggle_manu.IsChecked = false;
		}
	}

	private void OpenTutorialManu(object sender, RoutedEventArgs e)
	{
		(base.Resources["is_tutorial_manu_open_animation"] as Storyboard).Begin();
	}

	private void Click_Tutorial_Homepage(object sender, RoutedEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_Homepage.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
	}

	private void Click_Tutorial_HowToUseThisUI(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_HowToUseThisUI.xaml", UriKind.Relative);
		((TreeViewItem)sender).IsSelected = true;
		manuToggle_page.IsChecked = false;
	}

	private void Click_Tutorial_WhatYouMustKnow(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_WhatYouMustKnow.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Tutorial_AboutBroadcast(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_AboutBroadcast.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Tutorial_DIYCat(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_DIYCat.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Tutorial_Keymap(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_Keymap.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Tutorial_ConfigComparisonTable(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_ConfigComparisonTable.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Tutorial_HowToAsk(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_HowToAsk.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Question_CatFreeze(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Question_CatFreeze.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Question_MissingPart(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Question_MissingPart.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Question_TopWindow(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Question_TopWindow.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Question_BlackBackground(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Question_BlackBackground.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_FAQ(object sender, MouseButtonEventArgs e)
	{
		TreeViewItem relativeTo = (TreeViewItem)sender;
		if (e.GetPosition(relativeTo).Y < 34.0)
		{
			if (!treeView_FAQ.IsExpanded)
			{
				treeView_FAQ.IsExpanded = true;
			}
			else
			{
				treeView_FAQ.IsExpanded = false;
			}
			e.Handled = true;
		}
		else
		{
			e.Handled = false;
		}
	}

	private void Click_Tutorial_Homepage(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_Homepage.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	private void Click_Tutorial_DIYCatPro(object sender, MouseButtonEventArgs e)
	{
		frame_tutorial.Source = new Uri("tutorial/Tutorial_DIYCatPro.xaml", UriKind.Relative);
		manuToggle_page.IsChecked = false;
		((TreeViewItem)sender).IsSelected = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/pagetutorial.xaml", UriKind.Relative);
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
			frame_tutorial = (Frame)target;
			break;
		case 2:
			grid_tutorialmanu = (Grid)target;
			break;
		case 3:
			manu_tutorial = (TreeView)target;
			break;
		case 4:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_Homepage;
			break;
		case 5:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_HowToUseThisUI;
			break;
		case 6:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_WhatYouMustKnow;
			break;
		case 7:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_AboutBroadcast;
			break;
		case 8:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_DIYCat;
			break;
		case 9:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_DIYCatPro;
			break;
		case 10:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_Keymap;
			break;
		case 11:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_ConfigComparisonTable;
			break;
		case 12:
			((TreeViewItem)target).PreviewMouseDown += Click_Tutorial_HowToAsk;
			break;
		case 13:
			treeView_FAQ = (TreeViewItem)target;
			treeView_FAQ.PreviewMouseDown += Click_FAQ;
			break;
		case 14:
			((TreeViewItem)target).PreviewMouseDown += Click_Question_CatFreeze;
			break;
		case 15:
			((TreeViewItem)target).PreviewMouseDown += Click_Question_MissingPart;
			break;
		case 16:
			((TreeViewItem)target).PreviewMouseDown += Click_Question_TopWindow;
			break;
		case 17:
			((TreeViewItem)target).PreviewMouseDown += Click_Question_BlackBackground;
			break;
		case 18:
			manuToggle_manu = (ToggleButton)target;
			break;
		case 19:
			manuToggle_page = (ToggleButton)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
