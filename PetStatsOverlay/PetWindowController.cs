using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PetStatsOverlay;

public sealed class PetWindowController : IDisposable
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x20, WsExLayered = 0x80000, WsExNoActivate = 0x08000000;
    private const uint RefreshMessage = 0x8000 + 171;
    private const int CaptureIntervalMilliseconds = 33;
    private const int ButtonHandshakeMilliseconds = 50;
    private static readonly nuint ReplayTagMask = unchecked((nuint)0xFFFFFFFF00000000UL);
    private static readonly nuint ReplayProtocolMask = unchecked((nuint)0xFFFF000000000000UL);
    private static readonly nuint ReplayProtocol = unchecked((nuint)0x4E4B000000000000UL);
    private readonly nuint replayTagPrefix = ReplayProtocol | unchecked((nuint)((ulong)Random.Shared.Next(1, 65536) << 32));
    private readonly ConcurrentDictionary<nuint, MouseReplay> replayInputs = new();
    private int replayNumber;
    private readonly PetPixelSampler pixelSampler = new();
    private readonly MouseHookProc mouseHookCallback;
    private readonly Func<IntPtr>? windowFinder;
    private readonly Func<IntPtr, PetAlphaFrame?>? captureOverride;
    private readonly bool runWorkers;
    private readonly AutoResetEvent samplingWake = new(false);
    private NativeInputThread? inputThread;
    private NativeInputThread? windowThread;
    private Thread? samplingThread;
    private IntPtr mouseHook, cachedPetWindow;
    private uint cachedPetProcessId;
    private PetTarget? target, activeTarget;
    private PetAlphaFrame? alphaFrame;
    private PetTarget? controlledTarget;
    private nint? appliedStyle;
    private bool controlledLocked;
    private ControlRequest? lastControlRequest;
    private long controlSequence;
    private readonly PetGestureState gesture = new();
    private int locked, disposed;
    private bool previouslyLocked;
    private long lastButtonEvent, releaseAfter;
    private long captureCount;
    private long styleUpdateCount;
    private MouseReplay? leftReplay, rightReplay;
    private MouseReplay? injectedLeft, injectedRight;
    private int pendingReleaseRetry;

    public PetWindowController() : this(null, true, null) { }

    internal PetWindowController(Func<IntPtr>? windowFinder, bool runWorkers = true,
        Func<IntPtr, PetAlphaFrame?>? captureOverride = null)
    {
        this.windowFinder = windowFinder;
        this.runWorkers = runWorkers;
        this.captureOverride = captureOverride;
        mouseHookCallback = OnMouseInput;
    }

    public bool IsLocked => Volatile.Read(ref locked) != 0;
    internal bool IsMouseHookInstalled => Volatile.Read(ref mouseHook) != IntPtr.Zero;
    internal int InputManagedThreadId => inputThread?.ManagedThreadId ?? 0;
    internal long FrameCaptureCount => Interlocked.Read(ref captureCount);
    internal long StyleUpdateCount => Interlocked.Read(ref styleUpdateCount);
    internal PetAlphaFrame? CachedFrame => Volatile.Read(ref alphaFrame);
    internal bool WaitForInputThread(int milliseconds) => inputThread?.WaitUntilStarted(milliseconds) == true;

    public Rectangle? GetPetRect()
    {
        var hwnd = windowFinder?.Invoke() ?? FindPetWindow();
        return hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect)
            ? Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom) : null;
    }

    public void Lock()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        Volatile.Write(ref locked, 1);
        ApplyLockState();
        inputThread?.Post(RefreshMessage);
        samplingWake.Set();
    }

    public void Unlock()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        Volatile.Write(ref locked, 0);
        ApplyLockState();
        inputThread?.Post(RefreshMessage);
        samplingWake.Set();
    }

    // Called by the UI follow timer. It performs no image capture or input-hook
    // work; neither a busy dashboard nor an expensive repaint can delay input.
    public void ApplyLockState()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        var hwnd = windowFinder?.Invoke() ?? FindPetWindow();
        var current = Volatile.Read(ref target);
        if (hwnd == IntPtr.Zero)
        {
            if (current is not null)
            {
                Volatile.Write(ref target, null);
                Volatile.Write(ref alphaFrame, null);
                inputThread?.Post(RefreshMessage);
            }
            return;
        }
        GetWindowThreadProcessId(hwnd, out var processId);
        if (current?.Window != hwnd || current.ProcessId != processId)
        {
            var previous = Volatile.Read(ref activeTarget);
            var original = previous?.Window == hwnd && previous.ProcessId == processId
                ? previous.OriginalStyle : GetWindowLongPtr(hwnd, GwlExStyle);
            Volatile.Write(ref target, new PetTarget(hwnd, processId, original));
            Volatile.Write(ref alphaFrame, null);
            samplingWake.Set();
            inputThread?.Post(RefreshMessage);
        }
        if (!runWorkers)
        {
            CaptureFrame();
            ApplyPendingState();
            return;
        }
        if (inputThread is null)
        {
            windowThread = new NativeInputThread("Nikki window control", () => { }, () =>
            {
                ReleaseInjectedButtons();
                RestoreControlledTarget();
            });
            windowThread.Start();
            inputThread = new NativeInputThread("Nikki mouse input", () =>
            {
                mouseHook = SetWindowsHookEx(14, mouseHookCallback, GetModuleHandle(null), 0);
                ApplyPendingState();
            }, StopInput, (message, _, _) =>
            {
                if (message == RefreshMessage) ApplyPendingState();
            });
            inputThread.Start();
            samplingThread = new Thread(SampleFrames)
            {
                IsBackground = true, Name = "Nikki alpha capture", Priority = ThreadPriority.BelowNormal
            };
            samplingThread.Start();
        }
    }

    private void SampleFrames()
    {
        try
        {
            while (Volatile.Read(ref disposed) == 0)
            {
                try { CaptureFrame(); }
                catch { Volatile.Write(ref alphaFrame, null); }
                inputThread?.Post(RefreshMessage);
                samplingWake.WaitOne(CaptureIntervalMilliseconds);
            }
        }
        finally
        {
            pixelSampler.Dispose();
            samplingWake.Dispose();
        }
    }

    private void CaptureFrame()
    {
        var current = Volatile.Read(ref target);
        if (IsLocked || current is null)
        {
            Volatile.Write(ref alphaFrame, null);
            return;
        }
        var frame = captureOverride?.Invoke(current.Window) ?? (captureOverride is null ? pixelSampler.Capture(current.Window) : null);
        if (frame is not null) Interlocked.Increment(ref captureCount);
        if (ReferenceEquals(current, Volatile.Read(ref target))) Volatile.Write(ref alphaFrame, frame);
    }

    private IntPtr OnMouseInput(int code, IntPtr message, IntPtr data)
    {
        // The overwhelmingly common path is intentionally just the next hook.
        // In particular, 1,000/8,000-Hz mouse movement never samples a pixel,
        // enumerates windows, changes a style, or waits for the UI/capture worker.
        if (code < 0 || !IsButtonMessage(unchecked((int)message)))
            return CallNextHookEx(mouseHook, code, message, data);
        try
        {
            var input = Marshal.PtrToStructure<MouseHookData>(data);
            if ((input.ExtraInfo & ReplayProtocolMask) == ReplayProtocol)
            {
                // Another running overlay may have prepared this press. Never
                // recursively replay its events; only validate our own tokens.
                if ((input.ExtraInfo & ReplayTagMask) != replayTagPrefix) return CallNextHookEx(mouseHook, code, message, data);
                if (message == 0x201 || message == 0x204)
                {
                    if (!replayInputs.TryGetValue(input.ExtraInfo, out var injected)
                        || IsControlExpired(injected.Request) || !ReferenceEquals(injected.Target, Volatile.Read(ref target)))
                        return new IntPtr(1);
                }
                else replayInputs.TryRemove(input.ExtraInfo, out _);
                return CallNextHookEx(mouseHook, code, message, data);
            }
            if (Volatile.Read(ref disposed) != 0) return CallNextHookEx(mouseHook, code, message, data);
            var current = Volatile.Read(ref target);
            var mouseMessage = unchecked((int)message);
            if (mouseMessage is 0x202 or 0x205)
            {
                var replay = mouseMessage == 0x202 ? leftReplay : rightReplay;
                if (mouseMessage == 0x202) leftReplay = null; else rightReplay = null;
                if (current is not null) UpdatePointerState(current.Window, input.Point, mouseMessage);
                if (replay is not null && windowThread?.Post(() => ReleaseReplay(replay)) == true) return new IntPtr(1);
            }
            else if (current is not null && UpdatePointerState(current.Window, input.Point, mouseMessage))
            {
                // Windows chose the original target before this low-level
                // hook, while the idle pet was transparent. Replay the matched
                // press after preparing its style so Windows hit-tests afresh.
                // Its physical release uses the same queue, preserving order
                // even when a very fast click finishes during the handshake.
                var replay = new MouseReplay(current, lastControlRequest!, mouseMessage == 0x201,
                    replayTagPrefix | unchecked((uint)Interlocked.Increment(ref replayNumber)));
                if (mouseMessage == 0x201) leftReplay = replay; else rightReplay = replay;
                if (windowThread?.Post(() => ReplayBodyPress(replay)) == true) return new IntPtr(1);
                if (mouseMessage == 0x201) leftReplay = null; else rightReplay = null;
            }
        }
        catch
        {
            gesture.Cancel();
            inputThread?.Post(RefreshMessage);
        }
        return CallNextHookEx(mouseHook, code, message, data);
    }

    internal static bool IsButtonMessage(int message) => message is 0x201 or 0x202 or 0x204 or 0x205;

    internal bool UpdatePointerState(IntPtr hwnd, Point point, int message)
    {
        if (!IsButtonMessage(message)) return false;
        EnsureActiveTarget();
        if (activeTarget?.Window != hwnd) return false;
        var down = message is 0x201 or 0x204;
        var frame = Volatile.Read(ref alphaFrame);
        var onBody = down && !IsLocked && frame?.Window == hwnd
            && Stopwatch.GetElapsedTime(frame.CapturedAt) < TimeSpan.FromMilliseconds(500)
            && frame.IsVisiblePixel(point);
        var interactive = gesture.Process(message, onBody, IsLocked);
        lastButtonEvent = Stopwatch.GetTimestamp();
        if (down)
        {
            releaseAfter = 0;
            var focus = !interactive && GetForegroundWindow() == hwnd;
            if (!SetPassThrough(!interactive, wait: true, point, interactive ? frame : null, focus))
            {
                gesture.Cancel();
                // A hung renderer must not hold the global input hook. The late
                // worker result is invalidated, then restored to pass-through.
                SetPassThrough(true);
            }
            else return interactive;
        }
        else if (!gesture.HasHeldButtons)
        {
            // Let SFML receive the button-up and release its mouse capture.
            // A subsequent button-down always rechecks the alpha before routing.
            releaseAfter = lastButtonEvent + Stopwatch.Frequency / 20;
        }
        return false;
    }

    private void ApplyPendingState()
    {
        EnsureActiveTarget();
        if (activeTarget is null) return;
        if (IsLocked)
        {
            gesture.Cancel();
            if (!previouslyLocked && GetCursorPos(out var point))
                SetPassThrough(true, point: point, redirectFocus: true);
            else SetPassThrough(true);
        }
        else
        {
            // Recover a release missed across a desktop switch. This polling is
            // on the input thread's timer messages, never on mouse-move events.
            if (gesture.HasHeldButtons && Stopwatch.GetElapsedTime(lastButtonEvent).TotalMilliseconds > 100)
                gesture.ReconcileButtons((GetAsyncKeyState(1) & 0x8000) != 0, (GetAsyncKeyState(2) & 0x8000) != 0);
            if (!gesture.HasHeldButtons && Stopwatch.GetTimestamp() >= releaseAfter) SetPassThrough(true);
        }
        previouslyLocked = IsLocked;
    }

    private void EnsureActiveTarget()
    {
        var current = Volatile.Read(ref target);
        if (ReferenceEquals(activeTarget, current)) return;
        activeTarget = current;
        lastControlRequest = null;
        gesture.Reset();
        releaseAfter = 0;
        previouslyLocked = false;
        if (current is null) SetPassThrough(true);
    }

    private bool SetPassThrough(bool passThrough, bool wait = false, Point? point = null,
        PetAlphaFrame? requiredFrame = null, bool redirectFocus = false)
    {
        var previous = lastControlRequest;
        if (requiredFrame is null && !redirectFocus && previous is not null && !previous.IsCancelled
            && ReferenceEquals(previous.Target, activeTarget) && previous.PassThrough == passThrough && previous.Locked == IsLocked
            && (!passThrough || Volatile.Read(ref pendingReleaseRetry) == 0 || !previous.Completion.Task.IsCompleted)
            && (!previous.Completion.Task.IsCompleted || previous.Completion.Task.Result))
            return !wait || WaitForControl(previous);
        var request = new ControlRequest(activeTarget, passThrough, IsLocked, point, requiredFrame,
            redirectFocus, Interlocked.Increment(ref controlSequence));
        lastControlRequest = request;
        if (!runWorkers) ExecuteControl(request);
        else if (windowThread?.Post(() => ExecuteControl(request)) != true) request.Completion.TrySetResult(false);
        return !wait || WaitForControl(request);
    }

    private static bool WaitForControl(ControlRequest request)
    {
        // Bound every button handshake. No move event reaches this method.
        // SFML services window messages on its frame loop. A sub-frame timeout
        // rejects ordinary clicks on a healthy 30/60 fps renderer.
        if (request.Completion.Task.Wait(ButtonHandshakeMilliseconds)) return request.Completion.Task.Result;
        request.Cancel();
        return false;
    }

    private bool IsControlExpired(ControlRequest request) => request.IsCancelled
        || request.Sequence != Interlocked.Read(ref controlSequence) || request.Locked != IsLocked
        || Volatile.Read(ref disposed) != 0;

    private void ExecuteControl(ControlRequest request)
    {
        var success = false;
        try
        {
            if (IsControlExpired(request)) return;
            if (!ReferenceEquals(controlledTarget, request.Target))
            {
                RestoreControlledTarget();
                controlledTarget = request.Target;
                appliedStyle = controlledTarget?.OriginalStyle;
                controlledLocked = false;
            }
            if (controlledTarget is null) { success = true; return; }
            if (request.RequiredFrame is not null && request.Point is { } point
                && (!IsFrameAligned(controlledTarget.Window, request.RequiredFrame)
                    || !request.RequiredFrame.IsVisiblePixel(point) || !IsPetUnobscuredAt(controlledTarget.Window, point)))
            {
                ApplyNativeStyle(true, request.Locked);
                return;
            }
            if (IsControlExpired(request)) return;
            ApplyNativeStyle(request.PassThrough, request.Locked);
            if (IsControlExpired(request))
            {
                // SetWindowLongPtr can finish after its bounded waiter timed
                // out. Undo that late activation before considering focus.
                // If the renderer also stalls its cleanup style message, the
                // native change can remain visible until that message returns;
                // the independent global input pump remains responsive.
                ApplyNativeStyle(true, IsLocked);
                return;
            }
            if (request.RedirectFocus && request.Point is { } clicked)
            {
                // Focus is never changed by a request that completed late.
                GiveFocusToClickedWindow(controlledTarget.Window, clicked, request);
            }
            success = !IsControlExpired(request);
        }
        catch { }
        finally { request.Completion.TrySetResult(success); }
    }

    private void ApplyNativeStyle(bool passThrough, bool lockedState)
    {
        if (controlledTarget is null) return;
        if (lockedState) ReleaseInjectedButtons();
        else if (passThrough)
        {
            if (injectedLeft is { ReleaseRequested: true } left) ReleaseReplay(left);
            if (injectedRight is { ReleaseRequested: true } right) ReleaseReplay(right);
        }
        var desired = passThrough ? controlledTarget.OriginalStyle | WsExLayered | WsExTransparent | WsExNoActivate : controlledTarget.OriginalStyle;
        if (lockedState) desired |= 8;
        if (appliedStyle != desired)
        {
            Marshal.SetLastPInvokeError(0);
            var old = SetWindowLongPtr(controlledTarget.Window, GwlExStyle, desired);
            if (old == 0 && Marshal.GetLastPInvokeError() != 0) throw new InvalidOperationException("Cannot update pet window style.");
            Interlocked.Increment(ref styleUpdateCount);
            appliedStyle = desired;
        }
        if (controlledLocked != lockedState)
        {
            var topMost = lockedState || (controlledTarget.OriginalStyle & 8) != 0;
            SetWindowPos(controlledTarget.Window, new IntPtr(topMost ? -1 : -2), 0, 0, 0, 0, 0x4013);
            controlledLocked = lockedState;
        }
    }

    private static bool IsFrameAligned(IntPtr hwnd, PetAlphaFrame frame)
    {
        var origin = Point.Empty;
        return GetClientRect(hwnd, out var bounds) && ClientToScreen(hwnd, ref origin)
            && origin == frame.Bounds.Location && bounds.Right == frame.Bounds.Width && bounds.Bottom == frame.Bounds.Height;
    }

    private void GiveFocusToClickedWindow(IntPtr hwnd, Point point, ControlRequest request)
    {
        // Mver polls global buttons while focused, even on transparent pixels.
        // This runs only for button presses/lock transitions, never mouse moves.
        if (GetForegroundWindow() != hwnd) return;
        var clicked = GetAncestor(WindowFromPoint(point), 2);
        if (clicked != IntPtr.Zero && clicked != hwnd && !IsControlExpired(request)) SetForegroundWindow(clicked);
    }

    private static bool IsPetUnobscuredAt(IntPtr hwnd, Point point)
    {
        var hit = GetAncestor(WindowFromPoint(point), 2);
        if (hit == IntPtr.Zero || hit == hwnd) return true;
        for (var above = GetWindow(hwnd, 3); above != IntPtr.Zero; above = GetWindow(above, 3))
            if (above == hit) return false;
        return true;
    }

    private void RestoreControlledTarget()
    {
        ReleaseInjectedButtons();
        var previous = controlledTarget;
        if (previous is not null && IsWindow(previous.Window)
            && GetWindowThreadProcessId(previous.Window, out var processId) != 0 && processId == previous.ProcessId)
        {
            SetWindowLongPtr(previous.Window, GwlExStyle, previous.OriginalStyle);
            SetWindowPos(previous.Window, new IntPtr((previous.OriginalStyle & 8) != 0 ? -1 : -2), 0, 0, 0, 0, 0x4013);
        }
        controlledTarget = null;
    }

    private void ReplayBodyPress(MouseReplay replay)
    {
        // Do not move the user's pointer or send a delayed press to a different
        // application if the target, lock state, or pointer changed meanwhile.
        if (IsControlExpired(replay.Request) || !ReferenceEquals(controlledTarget, replay.Target)
            || !GetCursorPos(out var point) || GetAncestor(WindowFromPoint(point), 2) != replay.Target.Window)
            return;
        var frame = Volatile.Read(ref alphaFrame);
        if (frame?.Window != replay.Target.Window || Stopwatch.GetElapsedTime(frame.CapturedAt).TotalMilliseconds > 500
            || !IsFrameAligned(replay.Target.Window, frame) || !frame.IsVisiblePixel(point)) return;
        replayInputs[replay.Tag] = replay;
        if (SendReplayButton(replay.Left ? 0x2u : 0x8u, replay.Tag))
        {
            replay.Injected = true;
            if (replay.Left) injectedLeft = replay; else injectedRight = replay;
        }
        else replayInputs.TryRemove(replay.Tag, out _);
    }

    private void ReleaseReplay(MouseReplay replay)
    {
        replay.ReleaseRequested = true;
        if (!replay.Injected) return;
        if (!SendReplayButton(replay.Left ? 0x4u : 0x10u, replay.Tag))
        {
            Volatile.Write(ref pendingReleaseRetry, 1);
            return;
        }
        replay.Injected = false;
        if (replay.Left) injectedLeft = null; else injectedRight = null;
        Volatile.Write(ref pendingReleaseRetry, injectedLeft is { ReleaseRequested: true } || injectedRight is { ReleaseRequested: true } ? 1 : 0);
    }

    private void ReleaseInjectedButtons()
    {
        if (injectedLeft is { } left) ReleaseReplay(left);
        if (injectedRight is { } right) ReleaseReplay(right);
    }

    private static bool SendReplayButton(uint flags, nuint tag)
    {
        var inputs = new[] { new NativeInput { Mouse = new NativeMouseInput { Flags = flags, ExtraInfo = tag } } };
        return SendInput(1, inputs, Marshal.SizeOf<NativeInput>()) == 1;
    }

    private void StopInput()
    {
        if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
        mouseHook = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { samplingWake.Set(); } catch (ObjectDisposedException) { }
        inputThread?.Dispose();
        windowThread?.Dispose();
        if (samplingThread is null)
        {
            StopInput();
            RestoreControlledTarget();
            pixelSampler.Dispose();
            samplingWake.Dispose();
        }
    }

    private IntPtr FindPetWindow()
    {
        if (cachedPetWindow != IntPtr.Zero && IsWindow(cachedPetWindow) && IsWindowVisible(cachedPetWindow)
            && GetWindowThreadProcessId(cachedPetWindow, out var id) != 0 && id == cachedPetProcessId
            && GetWindowRect(cachedPetWindow, out var cachedRect) && cachedRect.Right > cachedRect.Left && cachedRect.Bottom > cachedRect.Top)
            return cachedPetWindow;
        cachedPetWindow = IntPtr.Zero;
        cachedPetProcessId = 0;
        var candidates = new List<(IntPtr Window, uint ProcessId, int Area, string Title)>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0) return true;
            try
            {
                using var process = Process.GetProcessById((int)processId);
                // The settings app and Steam game have different renderers and
                // must never be controlled just because their names are similar.
                if (!process.ProcessName.Equals("BongoCatMver", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { return true; }
            if (GetWindowRect(hwnd, out var rect) && rect.Right - rect.Left > 20 && rect.Bottom - rect.Top > 20)
                candidates.Add((hwnd, processId, (rect.Right - rect.Left) * (rect.Bottom - rect.Top), GetWindowTitle(hwnd)));
            return true;
        }, IntPtr.Zero);
        var selected = candidates.OrderByDescending(c => c.Title.Contains("Bongo Cat", StringComparison.OrdinalIgnoreCase)).ThenByDescending(c => c.Area).FirstOrDefault();
        cachedPetWindow = selected.Window;
        cachedPetProcessId = selected.ProcessId;
        return cachedPetWindow;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0) return "";
        var buffer = new char[length + 1];
        var copied = GetWindowText(hwnd, buffer, buffer.Length);
        return copied <= 0 ? "" : new string(buffer, 0, copied);
    }

    private sealed record PetTarget(IntPtr Window, uint ProcessId, nint OriginalStyle);
    private sealed class MouseReplay(PetTarget target, ControlRequest request, bool left, nuint tag)
    {
        internal PetTarget Target { get; } = target;
        internal ControlRequest Request { get; } = request;
        internal bool Left { get; } = left;
        internal nuint Tag { get; } = tag;
        internal bool Injected { get; set; }
        internal bool ReleaseRequested { get; set; }
    }
    private sealed class ControlRequest(PetTarget? target, bool passThrough, bool lockedState, Point? point,
        PetAlphaFrame? requiredFrame, bool redirectFocus, long sequence)
    {
        private int cancelled;
        internal PetTarget? Target { get; } = target;
        internal bool PassThrough { get; } = passThrough;
        internal bool Locked { get; } = lockedState;
        internal Point? Point { get; } = point;
        internal PetAlphaFrame? RequiredFrame { get; } = requiredFrame;
        internal bool RedirectFocus { get; } = redirectFocus;
        internal long Sequence { get; } = sequence;
        internal TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool IsCancelled => Volatile.Read(ref cancelled) != 0;
        internal void Cancel() => Volatile.Write(ref cancelled, 1);
    }
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);
    private delegate IntPtr MouseHookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData { public Point Point; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public NativeMouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMouseInput { public int X, Y; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int hook, MouseHookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, char[] text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr(IntPtr hwnd, int index, nint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}

internal sealed class PetGestureState
{
    private int heldButtons;
    private bool startedOnPet;
    internal bool HasHeldButtons => heldButtons != 0;
    internal bool Process(int message, bool onBody, bool locked)
    {
        var down = message switch { 0x201 => 1, 0x204 => 2, _ => 0 };
        var up = message switch { 0x202 => 1, 0x205 => 2, _ => 0 };
        if (locked) startedOnPet = false;
        if (down != 0)
        {
            if (heldButtons == 0) startedOnPet = onBody && !locked;
            heldButtons |= down;
        }
        if (up != 0) heldButtons &= ~up;
        var interactive = !locked && startedOnPet && heldButtons != 0;
        if (heldButtons == 0) startedOnPet = false;
        return interactive;
    }
    internal void Cancel() => startedOnPet = false;
    internal void Reset() { heldButtons = 0; startedOnPet = false; }
    internal void ReconcileButtons(bool left, bool right)
    {
        if (!left) heldButtons &= ~1;
        if (!right) heldButtons &= ~2;
        if (heldButtons == 0) startedOnPet = false;
    }
}

/// <summary>A dedicated Win32 message pump. Instances that own low-level hooks
/// must stay independent of UI, capture, and blocking window-control work.</summary>
internal sealed class NativeInputThread : IDisposable
{
    private readonly Action started, stopped;
    private readonly Action<uint, nint, nint>? messageHandler;
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new(false);
    private uint nativeThreadId;
    private int startRequested, stopRequested;
    private readonly ConcurrentQueue<Action> actions = new();
    private const uint ActionMessage = 0x8000 + 172;
    internal int ManagedThreadId => thread.ManagedThreadId;
    internal Exception? StartupException { get; private set; }

    internal NativeInputThread(string name, Action started, Action stopped, Action<uint, nint, nint>? messageHandler = null)
    {
        this.started = started;
        this.stopped = stopped;
        this.messageHandler = messageHandler;
        thread = new Thread(Run) { Name = name, IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
    }
    internal void Start()
    {
        if (Interlocked.Exchange(ref startRequested, 1) == 0) thread.Start();
    }
    internal bool WaitUntilStarted(int milliseconds) => ready.Wait(milliseconds);
    internal bool Post(uint message, nint wParam = 0, nint lParam = 0)
    {
        var id = Volatile.Read(ref nativeThreadId);
        return id != 0 && PostThreadMessage(id, message, wParam, lParam);
    }
    // Hook-owning instances accept only short work. Window-control operations
    // use a separate instance so an unresponsive renderer cannot stall input.
    internal bool Post(Action action)
    {
        actions.Enqueue(action);
        return Post(ActionMessage) || (Volatile.Read(ref nativeThreadId) == 0 && Volatile.Read(ref stopRequested) == 0);
    }
    private void Run()
    {
        try
        {
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // Create the queue before publishing its id.
            Volatile.Write(ref nativeThreadId, GetCurrentThreadId());
            if (Volatile.Read(ref stopRequested) != 0) return;
            started();
            for (var queued = actions.Count; queued > 0; queued--) Post(ActionMessage);
            ready.Set();
            while (Volatile.Read(ref stopRequested) == 0 && GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                try
                {
                    if (message.Id == ActionMessage && actions.TryDequeue(out var action)) action();
                    else messageHandler?.Invoke(message.Id, unchecked((nint)message.WParam), message.LParam);
                }
                catch { /* Never unwind a global input message pump. */ }
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        catch (Exception ex) { StartupException = ex; }
        finally
        {
            try { stopped(); } catch { }
            Volatile.Write(ref nativeThreadId, 0);
            ready.Set();
        }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref stopRequested, 1) != 0) return;
        Post(0x12); // WM_QUIT; never synchronously send to another thread.
        if (thread.IsAlive && Thread.CurrentThread != thread) thread.Join(200);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMessage
    {
        public IntPtr Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern int GetMessage(out NativeMessage message, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref NativeMessage message);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);
}
