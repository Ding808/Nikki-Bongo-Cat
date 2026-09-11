using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PetStatsOverlay;

public sealed class PetWindowController : IDisposable
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
    private readonly Dictionary<IntPtr, nint> originalStyles = new();
    private readonly PetPixelSampler pixelSampler = new();
    private readonly MouseHookProc mouseHookCallback;
    private IntPtr mouseHook;
    private IntPtr cachedPetWindow;
    private uint cachedPetProcessId;
    private int heldButtons;
    private bool gestureStartedOnPet;
    private bool disposed;

    public PetWindowController()
    {
        mouseHookCallback = OnMouseInput;
    }

    public bool IsLocked { get; private set; }
    internal bool IsMouseHookInstalled => mouseHook != IntPtr.Zero;

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
        gestureStartedOnPet = false;
        ApplyLockState();
    }

    public void Unlock()
    {
        IsLocked = false;
        ApplyLockState();
    }

    public void ApplyLockState()
    {
        if (disposed)
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

        if (mouseHook == IntPtr.Zero)
        {
            // This callback runs before a mouse event is delivered. A timer alone
            // leaves a race when a user moves and clicks between two ticks.
            mouseHook = SetWindowsHookEx(14, mouseHookCallback, GetModuleHandle(null), 0);
        }

        // Also refresh while the pointer is stationary and an animated part of
        // the pet moves underneath it. An active gesture retains its origin.
        if (GetCursorPos(out var point))
        {
            UpdatePointerState(hwnd, point, 0);
        }
        if (IsLocked && (GetWindowLongPtr(hwnd, GwlExStyle) & 8) == 0)
        {
            SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }
    }

    private IntPtr OnMouseInput(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && !disposed && cachedPetWindow != IntPtr.Zero)
        {
            try
            {
                var input = Marshal.PtrToStructure<MouseHookData>(data);
                UpdatePointerState(cachedPetWindow, input.Point, unchecked((int)message));
            }
            catch
            {
                // Exceptions must not escape an unmanaged input callback.
                // Failure defaults to pass-through, never a blocked rectangle.
                SetPassThrough(cachedPetWindow, true);
            }
        }
        return CallNextHookEx(mouseHook, code, message, data);
    }

    internal void UpdatePointerState(IntPtr hwnd, Point point, int message)
    {
        if (message == 0)
        {
            // Recover from a release outside this desktop, or a missed hook
            // event, without leaving a rectangular gesture active indefinitely.
            if ((GetAsyncKeyState(1) & 0x8000) == 0) heldButtons &= ~1;
            if ((GetAsyncKeyState(2) & 0x8000) == 0) heldButtons &= ~2;
        }
        var downButton = message switch { 0x0201 => 1, 0x0204 => 2, _ => 0 };
        var upButton = message switch { 0x0202 => 1, 0x0205 => 2, _ => 0 };
        if (upButton != 0)
        {
            heldButtons &= ~upButton;
        }

        var visibleBody = !IsLocked && heldButtons == 0
            && pixelSampler.IsVisiblePixel(hwnd, point) && IsPetUnobscuredAt(hwnd, point);
        if (downButton != 0)
        {
            if (heldButtons == 0)
            {
                gestureStartedOnPet = visibleBody;
            }
            heldButtons |= downButton;
        }

        var interactive = !IsLocked && (heldButtons == 0 ? visibleBody : gestureStartedOnPet);
        SetPassThrough(hwnd, !interactive);

        // Mver polls global button state while it has focus, even when its HWND
        // is transparent. Move focus to the actual clicked window before the
        // click is delivered, so a blank-area click cannot initiate its drag.
        if (!interactive && downButton != 0 && GetForegroundWindow() == hwnd)
        {
            var target = GetAncestor(WindowFromPoint(point), 2);
            if (target != IntPtr.Zero && target != hwnd)
            {
                SetForegroundWindow(target);
            }
        }

        if (heldButtons == 0)
        {
            gestureStartedOnPet = false;
        }
    }

    private static bool IsPetUnobscuredAt(IntPtr hwnd, Point point)
    {
        var hit = GetAncestor(WindowFromPoint(point), 2);
        if (hit == IntPtr.Zero || hit == hwnd)
        {
            return true;
        }

        // WindowFromPoint skips the pet while it is passing clicks through.
        // Only a real window above it should prevent the pet being enabled.
        for (var above = GetWindow(hwnd, 3); above != IntPtr.Zero; above = GetWindow(above, 3))
        {
            if (above == hit)
            {
                return false;
            }
        }
        return true;
    }

    private void SetPassThrough(IntPtr hwnd, bool passThrough)
    {
        if (!originalStyles.TryGetValue(hwnd, out var original))
        {
            originalStyles[hwnd] = original = GetWindowLongPtr(hwnd, GwlExStyle);
        }
        var desired = passThrough ? original | WsExLayered | WsExTransparent | WsExNoActivate : original;
        if (IsLocked) desired |= 8; // WS_EX_TOPMOST
        if (GetWindowLongPtr(hwnd, GwlExStyle) != desired)
        {
            SetWindowLongPtr(hwnd, GwlExStyle, desired);
        }
    }

    public void Dispose()
    {
        disposed = true;
        if (mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(mouseHook);
            mouseHook = IntPtr.Zero;
        }
        if (cachedPetWindow != IntPtr.Zero && IsWindow(cachedPetWindow)
            && GetWindowThreadProcessId(cachedPetWindow, out var processId) != 0
            && processId == cachedPetProcessId
            && originalStyles.TryGetValue(cachedPetWindow, out var original))
        {
            SetWindowLongPtr(cachedPetWindow, GwlExStyle, original);
        }
        pixelSampler.Dispose();
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

        if (cachedPetWindow != IntPtr.Zero && IsWindow(cachedPetWindow)
            && GetWindowThreadProcessId(cachedPetWindow, out var previousProcessId) != 0
            && previousProcessId == cachedPetProcessId
            && originalStyles.TryGetValue(cachedPetWindow, out var previousStyle))
        {
            SetWindowLongPtr(cachedPetWindow, GwlExStyle, previousStyle);
        }
        cachedPetWindow = IntPtr.Zero;
        cachedPetProcessId = 0;
        heldButtons = 0;
        gestureStartedOnPet = false;
        originalStyles.Clear();
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
        // Only the bundled renderer has the alpha surface/drag semantics this
        // controller supports. Never change styles on BongoCatUI settings or
        // the separate Steam game just because their names share a prefix.
        return normalized.Equals("BongoCatMver", StringComparison.OrdinalIgnoreCase);
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
    private delegate IntPtr MouseHookProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int hook, MouseHookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

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
