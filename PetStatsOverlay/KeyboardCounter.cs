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

    public event EventHandler? TextKeyPressed;
    public event EventHandler? MouseClicked;
    public event EventHandler? LockRequested;
    public event EventHandler? UnlockRequested;

    public KeyboardCounter()
    {
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
        pollTimer.Start();
    }

    public void Dispose()
    {
        pollTimer.Stop();
        pollTimer.Dispose();
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
}
