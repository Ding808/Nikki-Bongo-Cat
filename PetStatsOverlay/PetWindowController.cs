using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PetStatsOverlay;

public sealed class PetWindowController
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private readonly Dictionary<IntPtr, nint> originalStyles = new();
    private IntPtr cachedPetWindow;
    private uint cachedPetProcessId;

    public bool IsLocked { get; private set; }

    public Rectangle? GetPetRect()
    {
        var hwnd = FindPetWindow();
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public void Lock()
    {
        IsLocked = true;
        ApplyLockState();
    }

    public void Unlock()
    {
        IsLocked = false;
        var hwnd = FindPetWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (originalStyles.TryGetValue(hwnd, out var style))
        {
            SetWindowLongPtr(hwnd, GwlExStyle, style);
        }

        SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    public void ApplyLockState()
    {
        if (!IsLocked)
        {
            return;
        }

        var hwnd = FindPetWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (!originalStyles.ContainsKey(hwnd))
        {
            originalStyles[hwnd] = GetWindowLongPtr(hwnd, GwlExStyle);
        }

        var style = GetWindowLongPtr(hwnd, GwlExStyle);
        SetWindowLongPtr(hwnd, GwlExStyle, style | WsExLayered | WsExTransparent | WsExNoActivate);
        SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private IntPtr FindPetWindow()
    {
        if (cachedPetWindow != IntPtr.Zero
            && IsWindow(cachedPetWindow)
            && IsWindowVisible(cachedPetWindow)
            && GetWindowThreadProcessId(cachedPetWindow, out var cachedProcessId) != 0
            && cachedProcessId == cachedPetProcessId
            && GetWindowRect(cachedPetWindow, out var cachedRect)
            && cachedRect.Right > cachedRect.Left
            && cachedRect.Bottom > cachedRect.Top)
        {
            return cachedPetWindow;
        }

        cachedPetWindow = IntPtr.Zero;
        cachedPetProcessId = 0;
        var candidates = new List<(IntPtr Hwnd, uint ProcessId, Rectangle Rect, string ProcessName, string Title)>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
            {
                return true;
            }

            string processName;
            try
            {
                processName = Process.GetProcessById((int)processId).ProcessName;
            }
            catch
            {
                return true;
            }

            var title = GetWindowTitle(hwnd);
            // Window titles are not identity. For example, an Explorer window open
            // at this repository is titled "Nikki-Bongo-Cat" and used to be cached
            // before the real pet finished starting. That full-screen rectangle
            // pinned the companion entry to the top of the monitor forever.
            if (!LooksLikeBongoCatProcess(processName))
            {
                return true;
            }

            if (!GetWindowRect(hwnd, out var rect))
            {
                return true;
            }

            var rectangle = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (rectangle.Width > 20 && rectangle.Height > 20)
            {
                candidates.Add((hwnd, processId, rectangle, processName, title));
            }

            return true;
        }, IntPtr.Zero);

        var selected = candidates
            .OrderByDescending(candidate => candidate.ProcessName.Contains("Mver", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(candidate => candidate.Title.Contains("Bongo Cat", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(candidate => candidate.Rect.Width * candidate.Rect.Height)
            .FirstOrDefault();
        cachedPetWindow = selected.Hwnd;
        cachedPetProcessId = selected.ProcessId;

        return cachedPetWindow;
    }

    private static bool LooksLikeBongoCatProcess(string value)
    {
        var normalized = new string(value
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray());
        return normalized.StartsWith("BongoCat", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return "";
        }

        var buffer = new char[length + 1];
        var copied = GetWindowText(hwnd, buffer, buffer.Length);
        return copied <= 0 ? "" : new string(buffer, 0, copied);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
