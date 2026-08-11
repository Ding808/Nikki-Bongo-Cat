using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MyWpfApp;

public static class CommonData
{
	public static IntPtr HWND_catUIThread;

	public static ulong ID_catUIThread;

	public static IntPtr HWND_UIwindow;

	public static configdata config = new configdata();

	public static version version = new version();

	public static bool configflag = true;

	public static bool configChangeflag = true;

	public static Frame tutorialframe;

	public static ToggleButton manuToggle_page;

	public static TreeView manu_tutorial;

	public static setting_cat_standard_mode setting_page;

	public const int USER = 1024;

	public const int UIWM_CLOSE = 1124;

	public const int UIWM_WRITECONFIG = 1026;

	public const int UIWM_WRITECONFIG_AND_RELOAD_FILE = 1027;

	[DllImport("user32.dll")]
	public static extern void PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);

	[DllImport("user32", SetLastError = true)]
	public static extern bool PostThreadMesssage(ulong threadid, uint msg, int wParam, int lParam);

	public static void sendCloseMessage()
	{
		PostMessage(HWND_UIwindow, 1124, 0, 0);
	}

	public static bool readcatcfg()
	{
		configflag = false;
		return true;
	}

	public static bool writecatcfg()
	{
		PostMessage(HWND_UIwindow, 1026, 0, 0);
		return true;
	}

	public static bool writecatcfg_and_reloadfile()
	{
		PostMessage(HWND_UIwindow, 1027, 0, 0);
		return true;
	}
}
