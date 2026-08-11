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

namespace MyWpfApp;

public class setting_cat_standard_mode : Page, IComponentConnector
{
	internal Grid grid_using_pen;

	internal CheckBox is_pen_check;

	internal Grid grid_lefthand;

	internal CheckBox is_leftHand_check;

	internal Grid grid_force_move_mouse;

	internal CheckBox is_mouseForceMove_check;

	internal Grid grid_force_move_mouse_speed;

	internal Slider sliderB;

	internal TextBox textbox_mouse_correct;

	internal Grid grid_gamepad_input;

	internal Grid grid_gamepad_id;

	internal TextBlock is_emoticonKeep_check;

	internal Grid gird_using_live2d;

	internal CheckBox is_Using_live2d_check;

	internal Button button;

	internal Grid gird_l2d_horizontal_flip;

	internal CheckBox is_live2d_horizontal_flip_check;

	internal Grid gird_using_live2d_keyboard;

	internal CheckBox is_Using_live2d_check_keyboard;

	internal Button button_keyboard_l2d;

	internal Grid gird_using_live2d_gamepad;

	internal CheckBox is_Using_live2d_check_gamepad;

	internal Button button_gamepad_l2d;

	internal Button movefocus;

	private bool _contentLoaded;

	public setting_cat_standard_mode()
	{
		InitializeComponent();
		CommonData.setting_page = this;
		base.DataContext = CommonData.config;
		switch (CommonData.config.mode)
		{
		case 1:
			gird_using_live2d_keyboard.Height = 0.0;
			gird_using_live2d_gamepad.Height = 0.0;
			if (!CommonData.config.standard_l2d)
			{
				gird_l2d_horizontal_flip.Height = 0.0;
			}
			break;
		case 2:
			gird_using_live2d.Height = 0.0;
			gird_using_live2d_gamepad.Height = 0.0;
			gird_l2d_horizontal_flip.Height = 0.0;
			break;
		case 3:
			gird_using_live2d.Height = 0.0;
			gird_using_live2d_keyboard.Height = 0.0;
			gird_l2d_horizontal_flip.Height = 0.0;
			break;
		}
	}

	public void startanime_switch_mode()
	{
		Storyboard storyboard;
		switch (CommonData.config.mode)
		{
		case 1:
			storyboard = base.Resources["switch_to_standard"] as Storyboard;
			if (CommonData.config.standard_l2d)
			{
				(base.Resources["is_Using_live2d_on_animation"] as Storyboard).Begin();
			}
			if (CommonData.config.decoration_mouseForceMove)
			{
				(base.Resources["is_force_move_on_animation"] as Storyboard).Begin();
			}
			break;
		case 2:
			storyboard = base.Resources["switch_to_keyboard"] as Storyboard;
			break;
		case 3:
			storyboard = base.Resources["switch_to_gamepad"] as Storyboard;
			break;
		default:
			storyboard = base.Resources["switch_to_standard"] as Storyboard;
			break;
		}
		storyboard.Begin();
	}

	private void Button_is_pen_Click(object sender, RoutedEventArgs e)
	{
		if (!CommonData.config.standard_mouse)
		{
			CommonData.config.standard_mouse = true;
		}
		else
		{
			CommonData.config.standard_mouse = false;
		}
		CommonData.writecatcfg();
		is_pen_check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_pen_check));
	}

	private void button_is_Using_live2d_Click(object sender, RoutedEventArgs e)
	{
		if (is_Using_live2d_check.IsChecked == false)
		{
			gird_l2d_horizontal_flip.Visibility = Visibility.Visible;
			(base.Resources["is_Using_live2d_on_animation"] as Storyboard).Begin();
			CommonData.config.standard_l2d = true;
		}
		else
		{
			(base.Resources["is_Using_live2d_off_animation"] as Storyboard).Begin();
			CommonData.config.standard_l2d = false;
		}
		CommonData.writecatcfg();
		is_Using_live2d_check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_Using_live2d_check));
	}

	private void TextBox_TextChanged_Num_limite(object sender, TextChangedEventArgs e)
	{
		((TextBox)sender).Text = LeaveOnlyNumbers(((TextBox)sender).Text, 4);
	}

	private void tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "." && ((TextBox)sender).Text.Contains("."))
		{
			e.Handled = true;
			return;
		}
		string text = e.Text;
		e.Handled = !LeaveOnlyNumbers(text).Equals(e.Text);
	}

	private string LeaveOnlyNumbers(string inString)
	{
		bool flag = false;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < inString.Length; i++)
		{
			if (!Regex.IsMatch(inString[i].ToString(), "^[0-9\\.]*$"))
			{
				continue;
			}
			if (inString[i] == '.')
			{
				if (!flag)
				{
					flag = true;
					stringBuilder.Append(inString[i]);
				}
			}
			else
			{
				stringBuilder.Append(inString[i]);
			}
		}
		return stringBuilder.ToString();
	}

	private string LeaveOnlyNumbers(string inString, int length)
	{
		bool flag = false;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < inString.Length && i < length; i++)
		{
			if (!Regex.IsMatch(inString[i].ToString(), "^[0-9\\.]*$"))
			{
				continue;
			}
			if (inString[i] == '.')
			{
				if (!flag)
				{
					flag = true;
					stringBuilder.Append(inString[i]);
				}
			}
			else
			{
				stringBuilder.Append(inString[i]);
			}
		}
		return stringBuilder.ToString();
	}

	private void Button_is_lefthand_Click(object sender, RoutedEventArgs e)
	{
		if (!CommonData.config.decoration_leftHanded)
		{
			CommonData.config.decoration_leftHanded = true;
		}
		else
		{
			CommonData.config.decoration_leftHanded = false;
		}
		CommonData.writecatcfg();
		is_leftHand_check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_pen_check));
	}

	private void button_is_mouse_force_move_Click(object sender, RoutedEventArgs e)
	{
		if (!CommonData.config.decoration_mouseForceMove)
		{
			CommonData.config.decoration_mouseForceMove = true;
			(base.Resources["is_force_move_on_animation"] as Storyboard).Begin();
		}
		else
		{
			CommonData.config.decoration_mouseForceMove = false;
			(base.Resources["is_force_move_off_animation"] as Storyboard).Begin();
		}
		CommonData.writecatcfg();
		is_mouseForceMove_check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_pen_check));
	}

	private void button_way_to_cancel_emoticon_Click(object sender, RoutedEventArgs e)
	{
		if (!CommonData.config.decoration_emoticonKeep)
		{
			CommonData.config.decoration_emoticonKeep = true;
		}
		else
		{
			CommonData.config.decoration_emoticonKeep = false;
		}
		CommonData.writecatcfg();
	}

	private void button_is_live2d_horizontal_flip(object sender, RoutedEventArgs e)
	{
		if (!CommonData.config.decoration_l2dHorizontal)
		{
			CommonData.config.decoration_l2dHorizontal = true;
		}
		else
		{
			CommonData.config.decoration_l2dHorizontal = false;
		}
		CommonData.writecatcfg();
		is_live2d_horizontal_flip_check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_pen_check));
	}

	private void button_way_to_play_sound_Click(object sender, RoutedEventArgs e)
	{
		if (!CommonData.config.decoration_soundKeep)
		{
			CommonData.config.decoration_soundKeep = true;
		}
		else
		{
			CommonData.config.decoration_soundKeep = false;
		}
		CommonData.writecatcfg();
	}

	private void scale_null2init(object sender, RoutedEventArgs e)
	{
		if (textbox_mouse_correct.Text == "")
		{
			textbox_mouse_correct.Text = "1.0";
		}
		CommonData.writecatcfg();
	}

	private void live2dcorrect_return(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			movefocus.Focus();
		}
	}

	private void button_gamepad_input_mode_Click(object sender, RoutedEventArgs e)
	{
		if (CommonData.config.gamepad_input_mode == 1)
		{
			CommonData.config.gamepad_input_mode = 2;
		}
		else
		{
			CommonData.config.gamepad_input_mode = 1;
		}
		CommonData.writecatcfg();
	}

	private void button_gamepad_id_Click(object sender, RoutedEventArgs e)
	{
		if (CommonData.config.gamepad_gamepad_ID > 3)
		{
			CommonData.config.gamepad_gamepad_ID = 1;
		}
		else
		{
			CommonData.config.gamepad_gamepad_ID++;
		}
		CommonData.writecatcfg();
	}

	private void button_is_Using_live2d_Click_keyboard(object sender, RoutedEventArgs e)
	{
		if (is_Using_live2d_check_keyboard.IsChecked == false)
		{
			CommonData.config.keyboard_l2d = true;
		}
		else
		{
			CommonData.config.keyboard_l2d = false;
		}
		CommonData.writecatcfg();
		is_Using_live2d_check_keyboard.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_Using_live2d_check_keyboard));
	}

	private void button_is_Using_live2d_Click_gamepad(object sender, RoutedEventArgs e)
	{
		if (is_Using_live2d_check_gamepad.IsChecked == false)
		{
			CommonData.config.gamepad_l2d = true;
		}
		else
		{
			CommonData.config.gamepad_l2d = false;
		}
		CommonData.writecatcfg();
		is_Using_live2d_check_gamepad.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_Using_live2d_check_gamepad));
	}

	private void sliderB_MouseUp(object sender, MouseButtonEventArgs e)
	{
		CommonData.writecatcfg();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/setting_cat_standard_mode.xaml", UriKind.Relative);
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
			grid_using_pen = (Grid)target;
			break;
		case 2:
			is_pen_check = (CheckBox)target;
			break;
		case 3:
			((Button)target).Click += Button_is_pen_Click;
			break;
		case 4:
			grid_lefthand = (Grid)target;
			break;
		case 5:
			is_leftHand_check = (CheckBox)target;
			break;
		case 6:
			((Button)target).Click += Button_is_lefthand_Click;
			break;
		case 7:
			grid_force_move_mouse = (Grid)target;
			break;
		case 8:
			is_mouseForceMove_check = (CheckBox)target;
			break;
		case 9:
			((Button)target).Click += button_is_mouse_force_move_Click;
			break;
		case 10:
			grid_force_move_mouse_speed = (Grid)target;
			break;
		case 11:
			sliderB = (Slider)target;
			sliderB.PreviewMouseUp += sliderB_MouseUp;
			break;
		case 12:
			textbox_mouse_correct = (TextBox)target;
			textbox_mouse_correct.LostFocus += scale_null2init;
			textbox_mouse_correct.TextChanged += TextBox_TextChanged_Num_limite;
			textbox_mouse_correct.PreviewTextInput += tb_PreviewTextInput;
			textbox_mouse_correct.PreviewKeyDown += live2dcorrect_return;
			break;
		case 13:
			grid_gamepad_input = (Grid)target;
			break;
		case 14:
			((Button)target).Click += button_gamepad_input_mode_Click;
			break;
		case 15:
			grid_gamepad_id = (Grid)target;
			break;
		case 16:
			((Button)target).Click += button_gamepad_id_Click;
			break;
		case 17:
			is_emoticonKeep_check = (TextBlock)target;
			break;
		case 18:
			((Button)target).Click += button_way_to_cancel_emoticon_Click;
			break;
		case 19:
			((Button)target).Click += button_way_to_play_sound_Click;
			break;
		case 20:
			gird_using_live2d = (Grid)target;
			break;
		case 21:
			is_Using_live2d_check = (CheckBox)target;
			break;
		case 22:
			button = (Button)target;
			button.Click += button_is_Using_live2d_Click;
			break;
		case 23:
			gird_l2d_horizontal_flip = (Grid)target;
			break;
		case 24:
			is_live2d_horizontal_flip_check = (CheckBox)target;
			break;
		case 25:
			((Button)target).Click += button_is_live2d_horizontal_flip;
			break;
		case 26:
			gird_using_live2d_keyboard = (Grid)target;
			break;
		case 27:
			is_Using_live2d_check_keyboard = (CheckBox)target;
			break;
		case 28:
			button_keyboard_l2d = (Button)target;
			button_keyboard_l2d.Click += button_is_Using_live2d_Click_keyboard;
			break;
		case 29:
			gird_using_live2d_gamepad = (Grid)target;
			break;
		case 30:
			is_Using_live2d_check_gamepad = (CheckBox)target;
			break;
		case 31:
			button_gamepad_l2d = (Button)target;
			button_gamepad_l2d.Click += button_is_Using_live2d_Click_gamepad;
			break;
		case 32:
			movefocus = (Button)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
