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

public class setting_cat_keyboard_mode : Page, IComponentConnector
{
	internal CheckBox is_Using_live2d_check;

	internal Button button;

	internal Grid gird_l2d_horizontal_flip;

	internal Grid gird_l2d_correct;

	internal TextBox textbox_l2d_correct;

	private bool _contentLoaded;

	public setting_cat_keyboard_mode()
	{
		InitializeComponent();
	}

	private void button_is_Using_live2d_Click(object sender, RoutedEventArgs e)
	{
		if (is_Using_live2d_check.IsChecked == false)
		{
			gird_l2d_horizontal_flip.Visibility = Visibility.Visible;
			gird_l2d_correct.Visibility = Visibility.Visible;
			(base.Resources["is_Using_live2d_on_animation"] as Storyboard).Begin();
			is_Using_live2d_check.IsChecked = true;
		}
		else
		{
			(base.Resources["is_Using_live2d_off_animation"] as Storyboard).Begin();
			is_Using_live2d_check.IsChecked = false;
		}
		is_Using_live2d_check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, is_Using_live2d_check));
	}

	private void TextBox_TextChanged_Num_limite(object sender, TextChangedEventArgs e)
	{
		textbox_l2d_correct.Text = LeaveOnlyNumbers(textbox_l2d_correct.Text);
	}

	private void tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		if (e.Text == "." && textbox_l2d_correct.Text.Contains("."))
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

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/BongoCatMverUI;component/setting_cat_keyboard_mode.xaml", UriKind.Relative);
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
			is_Using_live2d_check = (CheckBox)target;
			break;
		case 2:
			button = (Button)target;
			button.Click += button_is_Using_live2d_Click;
			break;
		case 3:
			gird_l2d_horizontal_flip = (Grid)target;
			break;
		case 4:
			gird_l2d_correct = (Grid)target;
			break;
		case 5:
			textbox_l2d_correct = (TextBox)target;
			textbox_l2d_correct.TextChanged += TextBox_TextChanged_Num_limite;
			textbox_l2d_correct.PreviewTextInput += tb_PreviewTextInput;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
