using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MyWpfApp;

public class PageHome : Page, IDisposable, IComponentConnector
{
	public bool is_window_MouseLeftButtonDown;

	public bool is_window_closed;

	private static bool firstload = true;

	private bool disposed;

	internal Frame Show_Page;

	internal Button button_tutorial;

	internal TextBlock text_tutorial;

	internal Button button_feedback_and_communicate;

	internal Button button_download_updates;

	internal Button button_donate;

	internal Button botton_cat_set;

	internal Button btn_window_close;

	internal SolidColorBrush color_close_btn;

	internal ScaleTransform close_btn_scale;

	internal Rectangle rect_move_window;

	internal SolidColorBrush windowmove_btn_color;

	private bool _contentLoaded;

	protected virtual void Dispose(bool disposing)
	{
		if (!disposed)
		{
			disposed = true;
			if (disposing)
			{
				GC.SuppressFinalize(this);
			}
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
	}

	~PageHome()
	{
		Dispose(disposing: false);
	}

	public PageHome()
	{
		is_window_MouseLeftButtonDown = false;
		is_window_closed = false;
		InitializeComponent();
	}

	private void button_donate_Click(object sender, RoutedEventArgs e)
	{
		Show_Page.Source = new Uri("PageSupportme.xaml", UriKind.Relative);
	}

	private void button_window_set_Click(object sender, RoutedEventArgs e)
	{
		Show_Page.Source = new Uri("MyNewPage.xaml", UriKind.Relative);
	}

	private void button_feedback_and_communicate_Click(object sender, RoutedEventArgs e)
	{
		Show_Page.Source = new Uri("PageFeedback_and_Communicate.xaml", UriKind.Relative);
	}

	[DllImport("user32.dll")]
	public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	public static extern bool ReleaseCapture();

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		ReleaseCapture();
		SendMessage(CommonData.HWND_UIwindow, 274, 61458, 5);
	}

	private void Move_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		is_window_MouseLeftButtonDown = false;
	}

	private void button_tutorial_Click(object sender, RoutedEventArgs e)
	{
		if (firstload)
		{
			Show_Page.Source = new Uri("PageTutorial.xaml", UriKind.Relative);
			firstload = false;
		}
		else
		{
			Show_Page.Source = new Uri("PageTutorial.xaml", UriKind.Relative);
			CommonData.tutorialframe.Source = new Uri("tutorial/Tutorial_Homepage.xaml", UriKind.Relative);
		}
	}

	private void Page_Loaded(object sender, RoutedEventArgs e)
	{
		CommonData.readcatcfg();
	}

	private void btn_window_close_Click(object sender, RoutedEventArgs e)
	{
		CommonData.sendCloseMessage();
	}

	private void button_download_updates_Click(object sender, RoutedEventArgs e)
	{
		Show_Page.Source = new Uri("PageUpdate.xaml", UriKind.Relative);
	}

	private void botton_cat_set_Click(object sender, RoutedEventArgs e)
	{
		Show_Page.Source = new Uri("setting_cat.xaml", UriKind.Relative);
	}

	private void botton_btn_MouseEnter(object sender, MouseEventArgs e)
	{
		Panel.SetZIndex((Button)sender, 1);
	}

	private void botton_btn_MouseLeave(object sender, MouseEventArgs e)
	{
		Panel.SetZIndex((Button)sender, 0);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/pagehome.xaml", UriKind.Relative);
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
			((PageHome)target).Loaded += Page_Loaded;
			break;
		case 2:
			Show_Page = (Frame)target;
			break;
		case 3:
			button_tutorial = (Button)target;
			button_tutorial.Click += button_tutorial_Click;
			button_tutorial.MouseEnter += botton_btn_MouseEnter;
			button_tutorial.MouseLeave += botton_btn_MouseLeave;
			break;
		case 4:
			text_tutorial = (TextBlock)target;
			break;
		case 5:
			button_feedback_and_communicate = (Button)target;
			button_feedback_and_communicate.Click += button_feedback_and_communicate_Click;
			button_feedback_and_communicate.MouseEnter += botton_btn_MouseEnter;
			button_feedback_and_communicate.MouseLeave += botton_btn_MouseLeave;
			break;
		case 6:
			button_download_updates = (Button)target;
			button_download_updates.Click += button_download_updates_Click;
			button_download_updates.MouseEnter += botton_btn_MouseEnter;
			button_download_updates.MouseLeave += botton_btn_MouseLeave;
			break;
		case 7:
			button_donate = (Button)target;
			button_donate.Click += button_donate_Click;
			button_donate.MouseEnter += botton_btn_MouseEnter;
			button_donate.MouseLeave += botton_btn_MouseLeave;
			break;
		case 8:
			botton_cat_set = (Button)target;
			botton_cat_set.Click += botton_cat_set_Click;
			botton_cat_set.MouseEnter += botton_btn_MouseEnter;
			botton_cat_set.MouseLeave += botton_btn_MouseLeave;
			break;
		case 9:
			btn_window_close = (Button)target;
			btn_window_close.Click += btn_window_close_Click;
			break;
		case 10:
			color_close_btn = (SolidColorBrush)target;
			break;
		case 11:
			close_btn_scale = (ScaleTransform)target;
			break;
		case 12:
			rect_move_window = (Rectangle)target;
			rect_move_window.MouseLeftButtonDown += Move_MouseLeftButtonDown;
			break;
		case 13:
			windowmove_btn_color = (SolidColorBrush)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
