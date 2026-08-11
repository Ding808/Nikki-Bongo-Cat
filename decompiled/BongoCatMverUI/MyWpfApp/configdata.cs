using System.Collections;
using System.ComponentModel;

namespace MyWpfApp;

public class configdata : INotifyPropertyChanged
{
	private static bool is_first_load = true;

	private static bool is_first_load2 = true;

	private static bool is_first_load3 = true;

	private int _mode;

	public bool livecache;

	public int decoration_width;

	public int decoration_height;

	private bool _decoration_leftHanded;

	private int[] _decoration_rgb;

	public int decoration_offsetXforMouse;

	public int decoration_offsetYforMouse;

	public double decoration_scalarForMouse;

	public int decoration_offsetXforPen;

	public int decoration_offsetYforPen;

	public double decoration_scalarForPen;

	private int _decoration_framerateLimit;

	private bool _decoration_topWindow;

	public int[] decoration_armLineColor;

	private bool _decoration_emoticonKeep;

	private bool _decoration_soundKeep;

	public int decoration_emoticonClear;

	public int decoration_correct;

	private double _decoration_l2dCorrect;

	private bool _decoration_l2dHorizontal;

	private bool _decoration_mouseForceMove;

	private bool _show_mouse_speed;

	private float _decoration_mouseSpeed;

	private bool _decoration_desktopPet;

	public bool workarea_workarea;

	public int[] workarea_topLeft;

	public int[] workarea_rightBottom;

	private bool _standard_mouse;

	public ArrayList standard_mouseLeft;

	public ArrayList standard_mouseRight;

	public ArrayList standard_mouseSide;

	public ArrayList standard_keyboard;

	public ArrayList standard_hand;

	public ArrayList standard_face;

	public ArrayList standard_sounds;

	public bool standard_faceOn;

	public bool standard_soundsOn;

	private bool _standard_l2d;

	public ArrayList keyboard_keyboard;

	public ArrayList keyboard_leftHand;

	public ArrayList keyboard_rightHand;

	public ArrayList keyboard_face;

	public ArrayList keyboard_sounds;

	public bool keyboard_faceOn;

	public bool keyboard_soundsOn;

	private bool _keyboard_l2d;

	private int _gamepad_input_mode;

	private int _gamepad_gamepad_ID;

	private bool _gamepad_l2d;

	public int mode
	{
		get
		{
			return _mode;
		}
		set
		{
			_mode = value;
			mode2 = _mode;
			OnPropertyChanged("mode");
		}
	}

	public int mode2
	{
		get
		{
			return _mode;
		}
		set
		{
			_mode = value;
			if (is_first_load)
			{
				OnPropertyChanged("mode2");
				is_first_load = false;
			}
		}
	}

	public bool decoration_leftHanded
	{
		get
		{
			return _decoration_leftHanded;
		}
		set
		{
			_decoration_leftHanded = value;
			OnPropertyChanged("decoration_leftHanded");
		}
	}

	public int[] decoration_rgb
	{
		get
		{
			return _decoration_rgb;
		}
		set
		{
			_decoration_rgb = value;
			OnPropertyChanged("decoration_rgb");
		}
	}

	public int decoration_framerateLimit
	{
		get
		{
			return _decoration_framerateLimit;
		}
		set
		{
			_decoration_framerateLimit = value;
			OnPropertyChanged("decoration_framerateLimit");
		}
	}

	public bool decoration_topWindow
	{
		get
		{
			return _decoration_topWindow;
		}
		set
		{
			_decoration_topWindow = value;
			OnPropertyChanged("decoration_topWindow");
		}
	}

	public bool decoration_emoticonKeep
	{
		get
		{
			return _decoration_emoticonKeep;
		}
		set
		{
			_decoration_emoticonKeep = value;
			OnPropertyChanged("decoration_emoticonKeep");
		}
	}

	public bool decoration_soundKeep
	{
		get
		{
			return _decoration_soundKeep;
		}
		set
		{
			_decoration_soundKeep = value;
			OnPropertyChanged("decoration_soundKeep");
		}
	}

	public double decoration_l2dCorrect
	{
		get
		{
			return _decoration_l2dCorrect;
		}
		set
		{
			_decoration_l2dCorrect = value;
			OnPropertyChanged("decoration_l2dCorrect");
		}
	}

	public bool decoration_l2dHorizontal
	{
		get
		{
			return _decoration_l2dHorizontal;
		}
		set
		{
			_decoration_l2dHorizontal = value;
			OnPropertyChanged("decoration_l2dHorizontal");
		}
	}

	public bool decoration_mouseForceMove
	{
		get
		{
			return _decoration_mouseForceMove;
		}
		set
		{
			_decoration_mouseForceMove = value;
			_show_mouse_speed = _decoration_mouseForceMove && _mode == 1;
			OnPropertyChanged("decoration_mouseForceMove");
		}
	}

	public bool show_mouse_speed
	{
		get
		{
			return _show_mouse_speed;
		}
		set
		{
			_show_mouse_speed = value;
			OnPropertyChanged("_show_mouse_speed");
		}
	}

	public float decoration_mouseSpeed
	{
		get
		{
			return _decoration_mouseSpeed;
		}
		set
		{
			_decoration_mouseSpeed = value;
			OnPropertyChanged("decoration_mouseSpeed");
		}
	}

	public bool decoration_desktopPet
	{
		get
		{
			return _decoration_desktopPet;
		}
		set
		{
			_decoration_desktopPet = value;
			OnPropertyChanged("decoration_desktopPet");
		}
	}

	public bool standard_mouse
	{
		get
		{
			return _standard_mouse;
		}
		set
		{
			_standard_mouse = value;
			OnPropertyChanged("standard_mouse");
		}
	}

	public bool standard_l2d
	{
		get
		{
			return _standard_l2d;
		}
		set
		{
			_standard_l2d = value;
			standard_l2d2 = _standard_l2d;
			OnPropertyChanged("standard_l2d");
		}
	}

	public bool standard_l2d2
	{
		get
		{
			return _standard_l2d;
		}
		set
		{
			_standard_l2d = value;
			if (is_first_load2)
			{
				OnPropertyChanged("standard_l2d2");
				is_first_load2 = false;
			}
		}
	}

	public bool keyboard_l2d
	{
		get
		{
			return _keyboard_l2d;
		}
		set
		{
			_keyboard_l2d = value;
			OnPropertyChanged("keyboard_l2d");
		}
	}

	public int gamepad_input_mode
	{
		get
		{
			return _gamepad_input_mode;
		}
		set
		{
			_gamepad_input_mode = value;
			OnPropertyChanged("gamepad_input_mode");
		}
	}

	public int gamepad_gamepad_ID
	{
		get
		{
			return _gamepad_gamepad_ID;
		}
		set
		{
			_gamepad_gamepad_ID = value;
			OnPropertyChanged("gamepad_gamepad_id");
		}
	}

	public bool gamepad_l2d
	{
		get
		{
			return _gamepad_l2d;
		}
		set
		{
			_gamepad_l2d = value;
			OnPropertyChanged("gamepad_l2d");
		}
	}

	public bool Standard_mouse
	{
		get
		{
			return standard_mouse;
		}
		set
		{
			standard_mouse = value;
			OnPropertyChanged("Standard_mouse");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public configdata()
	{
		decoration_rgb = new int[4];
		decoration_armLineColor = new int[4];
		workarea_topLeft = new int[2];
		workarea_rightBottom = new int[2];
		standard_mouseLeft = new ArrayList();
		standard_mouseRight = new ArrayList();
		standard_mouseSide = new ArrayList();
		standard_keyboard = new ArrayList();
		standard_hand = new ArrayList();
		standard_face = new ArrayList();
		standard_sounds = new ArrayList();
		keyboard_keyboard = new ArrayList();
		keyboard_leftHand = new ArrayList();
		keyboard_rightHand = new ArrayList();
		keyboard_face = new ArrayList();
		keyboard_sounds = new ArrayList();
	}

	protected void OnPropertyChanged(string propertyName)
	{
		if (this.PropertyChanged != null)
		{
			this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
