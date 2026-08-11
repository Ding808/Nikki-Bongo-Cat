using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;

namespace MyWpfApp;

public class setting_cat : Page, IComponentConnector
{
	public static int[] origincolor = new int[4];

	internal Button Button_standard_mode;

	internal Button Button_keyboard_mode;

	internal Button Button_gamepad_mode;

	internal Frame Setting_Cat_Frame;

	internal Line line;

	internal TextBox textbox_fps;

	internal Popup pop_colorpicker;

	internal ColorPicker colorPicker;

	internal Slider sliderR;

	internal Slider sliderG;

	internal Slider sliderB;

	internal Button movefocus;

	private bool _contentLoaded;

	public setting_cat()
	{
		InitializeComponent();
		base.DataContext = CommonData.config;
	}

	private void Button_standard_mode_Click(object sender, RoutedEventArgs e)
	{
		CommonData.config.mode = 1;
		CommonData.writecatcfg();
		CommonData.setting_page.startanime_switch_mode();
		(base.Resources["click_standard"] as Storyboard).Begin();
	}

	private void Button_keyboard_mode_Click(object sender, RoutedEventArgs e)
	{
		CommonData.config.mode = 2;
		CommonData.writecatcfg();
		CommonData.setting_page.startanime_switch_mode();
		(base.Resources["click_keyboard"] as Storyboard).Begin();
	}

	private void ToggleButton_Checked(object sender, RoutedEventArgs e)
	{
	}

	private void ToggleButton_Checked_1(object sender, RoutedEventArgs e)
	{
	}

	private void send_change_message(object sender, RoutedEventArgs e)
	{
		CommonData.writecatcfg();
	}

	private void open_config_json(object sender, RoutedEventArgs e)
	{
		Process.Start(AppDomain.CurrentDomain.BaseDirectory + "config.json");
	}

	private void open_resources_files(object sender, RoutedEventArgs e)
	{
		Process.Start(AppDomain.CurrentDomain.BaseDirectory);
	}

	private void Button_Click_background_color(object sender, RoutedEventArgs e)
	{
		if (!pop_colorpicker.IsOpen)
		{
			pop_colorpicker.IsOpen = true;
		}
		else
		{
			pop_colorpicker.IsOpen = false;
		}
	}

	private void button_bgcolor_white(object sender, RoutedEventArgs e)
	{
		CommonData.config.decoration_rgb[0] = 255;
		CommonData.config.decoration_rgb[1] = 255;
		CommonData.config.decoration_rgb[2] = 255;
		CommonData.config.decoration_rgb = CommonData.config.decoration_rgb;
	}

	private void button_bgcolor_blue(object sender, RoutedEventArgs e)
	{
		CommonData.config.decoration_rgb[0] = 0;
		CommonData.config.decoration_rgb[1] = 0;
		CommonData.config.decoration_rgb[2] = 255;
		CommonData.config.decoration_rgb = CommonData.config.decoration_rgb;
	}

	private void button_bgcolor_green(object sender, RoutedEventArgs e)
	{
		CommonData.config.decoration_rgb[0] = 0;
		CommonData.config.decoration_rgb[1] = 255;
		CommonData.config.decoration_rgb[2] = 0;
		CommonData.config.decoration_rgb = CommonData.config.decoration_rgb;
	}

	private void button_bgcolor_red(object sender, RoutedEventArgs e)
	{
		CommonData.config.decoration_rgb[0] = 255;
		CommonData.config.decoration_rgb[1] = 0;
		CommonData.config.decoration_rgb[2] = 0;
		CommonData.config.decoration_rgb = CommonData.config.decoration_rgb;
	}

	private void button_bgcolor_magenta(object sender, RoutedEventArgs e)
	{
		CommonData.config.decoration_rgb[0] = 255;
		CommonData.config.decoration_rgb[1] = 0;
		CommonData.config.decoration_rgb[2] = 255;
		CommonData.config.decoration_rgb = CommonData.config.decoration_rgb;
	}

	private void popupopen(object sender, EventArgs e)
	{
		origincolor[0] = CommonData.config.decoration_rgb[0];
		origincolor[1] = CommonData.config.decoration_rgb[1];
		origincolor[2] = CommonData.config.decoration_rgb[2];
	}

	private void popyoclose(object sender, EventArgs e)
	{
		CommonData.config.decoration_rgb[0] = origincolor[0];
		CommonData.config.decoration_rgb[1] = origincolor[1];
		CommonData.config.decoration_rgb[2] = origincolor[2];
		CommonData.config.decoration_rgb = CommonData.config.decoration_rgb;
	}

	private void button_saveBgColor(object sender, RoutedEventArgs e)
	{
		origincolor[0] = CommonData.config.decoration_rgb[0];
		origincolor[1] = CommonData.config.decoration_rgb[1];
		origincolor[2] = CommonData.config.decoration_rgb[2];
		CommonData.writecatcfg();
	}

	private void tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		string text = e.Text;
		e.Handled = !LeaveOnlyNumbers(text).Equals(e.Text);
	}

	private string LeaveOnlyNumbers(string inString)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < inString.Length; i++)
		{
			if (Regex.IsMatch(inString[i].ToString(), "^[0-9\\.]*$") && inString[i] != '.')
			{
				stringBuilder.Append(inString[i]);
			}
		}
		return stringBuilder.ToString();
	}

	private void TextBox_fps_Num_limite(object sender, TextChangedEventArgs e)
	{
		textbox_fps.Text = LeaveOnlyNumbers(textbox_fps.Text);
	}

	private void fps_return(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			movefocus.Focus();
		}
	}

	private void movefocus2visual(object sender, MouseButtonEventArgs e)
	{
		movefocus.Focus();
	}

	private void fps_null2zero(object sender, RoutedEventArgs e)
	{
		if (textbox_fps.Text == "")
		{
			textbox_fps.Text = "0";
		}
		CommonData.writecatcfg();
	}

	private void Button_gamepad_mode_Click(object sender, RoutedEventArgs e)
	{
		CommonData.config.mode = 3;
		CommonData.writecatcfg();
		CommonData.setting_page.startanime_switch_mode();
		(base.Resources["click_gamepad"] as Storyboard).Begin();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/setting_cat.xaml", UriKind.Relative);
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
			((Grid)target).MouseDown += movefocus2visual;
			break;
		case 2:
			Button_standard_mode = (Button)target;
			Button_standard_mode.Click += Button_standard_mode_Click;
			break;
		case 3:
			Button_keyboard_mode = (Button)target;
			Button_keyboard_mode.Click += Button_keyboard_mode_Click;
			break;
		case 4:
			Button_gamepad_mode = (Button)target;
			Button_gamepad_mode.Click += Button_gamepad_mode_Click;
			break;
		case 5:
			Setting_Cat_Frame = (Frame)target;
			break;
		case 6:
			line = (Line)target;
			break;
		case 7:
			((ToggleButton)target).Click += send_change_message;
			break;
		case 8:
			((ToggleButton)target).Click += send_change_message;
			break;
		case 9:
			textbox_fps = (TextBox)target;
			textbox_fps.LostFocus += fps_null2zero;
			textbox_fps.PreviewTextInput += tb_PreviewTextInput;
			textbox_fps.TextChanged += TextBox_fps_Num_limite;
			textbox_fps.PreviewKeyDown += fps_return;
			break;
		case 10:
			((Button)target).Click += Button_Click_background_color;
			break;
		case 11:
			((Button)target).Click += open_resources_files;
			break;
		case 12:
			((Button)target).Click += open_config_json;
			break;
		case 13:
			pop_colorpicker = (Popup)target;
			pop_colorpicker.Closed += popyoclose;
			pop_colorpicker.Opened += popupopen;
			break;
		case 14:
			colorPicker = (ColorPicker)target;
			break;
		case 15:
			sliderR = (Slider)target;
			break;
		case 16:
			sliderG = (Slider)target;
			break;
		case 17:
			sliderB = (Slider)target;
			break;
		case 18:
			((Button)target).Click += button_bgcolor_white;
			break;
		case 19:
			((Button)target).Click += button_bgcolor_blue;
			break;
		case 20:
			((Button)target).Click += button_bgcolor_green;
			break;
		case 21:
			((Button)target).Click += button_bgcolor_red;
			break;
		case 22:
			((Button)target).Click += button_bgcolor_magenta;
			break;
		case 23:
			((Button)target).Click += button_saveBgColor;
			break;
		case 24:
			movefocus = (Button)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
