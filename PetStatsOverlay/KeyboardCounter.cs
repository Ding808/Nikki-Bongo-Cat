using System.Runtime.InteropServices;

namespace PetStatsOverlay;

public sealed class KeyboardCounter : IDisposable
{
    private const short KeyDownMask = unchecked((short)0x8000);

    private readonly System.Windows.Forms.Timer pollTimer;
    private readonly Keys[] trackedKeys;
    private readonly Keys[] mouseKeys = [Keys.LButton, Keys.RButton, Keys.MButton];
    private readonly Dictionary<Keys, bool> lastState;
    private readonly Dictionary<Keys, bool> lastMouseState;
    private bool lastLockState;
    private bool lastUnlockState;
    private readonly KeyboardHook settingsShortcutHook;
    private nint settingsShortcutHandle;
    private bool suppressSettingsKey;

    public event EventHandler? TextKeyPressed;
    public event EventHandler? MouseClicked;
    public event EventHandler? LockRequested;
    public event EventHandler? UnlockRequested;
    public event EventHandler? CustomizeRequested;

    public KeyboardCounter()
    {
        settingsShortcutHook = SettingsShortcut;
        trackedKeys = CreateTrackedKeys();
        lastState = trackedKeys.ToDictionary(key => key, _ => false);
        lastMouseState = mouseKeys.ToDictionary(key => key, _ => false);
        pollTimer = new System.Windows.Forms.Timer
        {
            // Bongo Cat polls key state during its render loop instead of using a
            // keyboard hook. GetAsyncKeyState keeps that non-blocking behavior while
            // still working when this overlay is not focused.
            Interval = 16
        };
        pollTimer.Tick += (_, _) => PollKeyboard();
    }

    public void Start()
    {
        if (settingsShortcutHandle == 0)
            settingsShortcutHandle = SetWindowsHookEx(13, settingsShortcutHook, GetModuleHandle(null), 0);
        pollTimer.Start();
    }

    public void Dispose()
    {
        pollTimer.Stop();
        pollTimer.Dispose();
        if (settingsShortcutHandle != 0) UnhookWindowsHookEx(settingsShortcutHandle);
        settingsShortcutHandle = 0;
    }

    private nint SettingsShortcut(int code, nint message, nint data)
    {
        // Route the pet's own focused settings shortcut to our localized editor.
        // Never consume Save As in another application.
        try
        {
            if (code >= 0 && Marshal.ReadInt32(data) == (int)Keys.S)
            {
                var down = message == 0x100 || message == 0x104;
                var up = message == 0x101 || message == 0x105;
                if (down && IsDown(Keys.ControlKey) && IsDown(Keys.ShiftKey) && !IsDown(Keys.Menu) && IsPetForeground())
                {
                    var notify = !suppressSettingsKey;
                    suppressSettingsKey = true;
                    if (notify) CustomizeRequested?.Invoke(this, EventArgs.Empty);
                    return 1;
                }
                if (suppressSettingsKey)
                {
                    if (up) suppressSettingsKey = false;
                    return 1;
                }
            }
        }
        catch
        {
            // Closing windows or a failing subscriber must not unwind a native hook.
        }
        return CallNextHookEx(settingsShortcutHandle, code, message, data);
    }

    private static bool IsPetForeground()
    {
        var window = GetForegroundWindow();
        if (window == 0) return false;
        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return false;
        using var process = System.Diagnostics.Process.GetProcessById((int)processId);
        return string.Equals(process.ProcessName, "BongoCatMver", StringComparison.OrdinalIgnoreCase);
    }

    private void PollKeyboard()
    {
        var lockState = IsDown(Keys.Subtract);
        if (lockState && !lastLockState)
        {
            LockRequested?.Invoke(this, EventArgs.Empty);
        }
        lastLockState = lockState;

        var unlockState = IsDown(Keys.Add);
        if (unlockState && !lastUnlockState)
        {
            UnlockRequested?.Invoke(this, EventArgs.Empty);
        }
        lastUnlockState = unlockState;

        foreach (var key in trackedKeys)
        {
            var isDown = IsDown(key);
            var wasDown = lastState[key];

            if (isDown && !wasDown)
            {
                TextKeyPressed?.Invoke(this, EventArgs.Empty);
            }

            lastState[key] = isDown;
        }

        foreach (var key in mouseKeys)
        {
            var isDown = IsDown(key);
            var wasDown = lastMouseState[key];
            if (isDown && !wasDown)
            {
                MouseClicked?.Invoke(this, EventArgs.Empty);
            }

            lastMouseState[key] = isDown;
        }
    }

    private static Keys[] CreateTrackedKeys()
    {
        List<Keys> keys = new();

        AddRange(keys, Keys.A, Keys.Z);
        AddRange(keys, Keys.D0, Keys.D9);
        AddRange(keys, Keys.NumPad0, Keys.NumPad9);

        keys.AddRange(
        [
            Keys.Space,
            Keys.Enter,
            Keys.Back,
            Keys.Tab,
            Keys.Oem1,
            Keys.Oem2,
            Keys.Oem3,
            Keys.Oem4,
            Keys.Oem5,
            Keys.Oem6,
            Keys.Oem7,
            Keys.Oem8,
            Keys.Oemcomma,
            Keys.OemPeriod,
            Keys.OemMinus,
            Keys.Oemplus,
            Keys.Decimal
        ]);

        return keys.Distinct().ToArray();
    }

    private static void AddRange(List<Keys> keys, Keys first, Keys last)
    {
        for (var key = first; key <= last; key++)
        {
            keys.Add(key);
        }
    }

    private static bool IsDown(Keys key)
    {
        return (GetAsyncKeyState((int)key) & KeyDownMask) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private delegate nint KeyboardHook(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, KeyboardHook callback, nint module, uint threadId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
