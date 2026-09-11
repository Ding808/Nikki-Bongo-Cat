using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using PetStatsOverlay;

internal static class Program
{
    private static int assertions;

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        if (args.Length == 5 && args[0] == "--gesture-backdrop")
        {
            Application.Run(new GestureBackdrop { Bounds = new Rectangle(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4])),
                StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, Text = "Isolated native gesture backdrop" });
            return;
        }
        if (args.Length == 2 && args[0] == "--native-gestures")
        {
            NativeGestures(int.Parse(args[1]));
            Console.WriteLine($"PASS: {assertions} real native gesture assertions.");
            return;
        }
        Check(!PetPixelSampler.IsHitAlpha(0), "Transparent pixels pass through");
        Check(!PetPixelSampler.IsHitAlpha(7), "Barely visible fringe passes through");
        Check(PetPixelSampler.IsHitAlpha(8), "Visible alpha threshold included");
        Check(PetPixelSampler.IsHitAlpha(255), "Opaque pixels are interactive");
        Check(PetPixelSampler.ContainsClientPoint(612, 354, Point.Empty), "Client origin included");
        Check(PetPixelSampler.ContainsClientPoint(612, 354, new Point(611, 353)), "Last client pixel included");
        Check(!PetPixelSampler.ContainsClientPoint(612, 354, new Point(612, 353)), "Right edge excluded");
        Check(!PetPixelSampler.ContainsClientPoint(612, 354, new Point(611, 354)), "Bottom edge excluded");
        Check(!PetPixelSampler.ContainsClientPoint(612, 354, new Point(-1, 0)), "Negative X excluded");
        Check(!PetPixelSampler.ContainsClientPoint(0, 0, Point.Empty), "Empty surface excluded");
        CachedAlpha();
        GestureOrigins();
        InputThreadIsolation();
        HungRendererIsolation();
        if (args.Length == 2 && args[0] == "--native")
        {
            NativePet(int.Parse(args[1]));
            NativeWorkerPet(int.Parse(args[1]));
        }
        Console.WriteLine($"PASS: {assertions} hit-testing/performance assertions.");
    }

    private static void CachedAlpha()
    {
        var data = new byte[3 * 2 * 4];
        data[(1 * 3 + 1) * 4 + 3] = 255;
        var frame = PetAlphaFrame.FromBgra(new IntPtr(1), new Rectangle(-100, -200, 3, 2), data);
        Check(frame.IsVisiblePixel(new Point(-99, -199)), "Cached BGRA alpha maps negative monitor coordinates");
        Check(!frame.IsVisiblePixel(new Point(-100, -200)), "Cached transparent hole passes through");
        Check(!frame.IsVisiblePixel(new Point(-97, -199)), "Cached alpha enforces right boundary");
        Check(frame.Buffer.TryBeginWrite(), "Unleased buffer can be reused");
        Check(!frame.IsVisiblePixel(new Point(-99, -199)), "Sampling in progress never blocks a cache reader");
        frame.Buffer.EndWrite();
        Check(!frame.IsVisiblePixel(new Point(-99, -199)), "Old frame cannot read a buffer reused for a new generation");
        var refreshed = new PetAlphaFrame(frame.Window, frame.Bounds, frame.Buffer, Stopwatch.GetTimestamp());
        Check(refreshed.IsVisiblePixel(new Point(-99, -199)), "New generation can read published pixels");
        Check(frame.Buffer.TryBeginRead(), "Reader lease acquired");
        Check(!frame.Buffer.TryBeginWrite(), "Capture cannot overwrite a paused reader");
        frame.Buffer.EndRead();
        Check(!PetWindowController.IsButtonMessage(0x200), "Mouse moves bypass hit testing");
        Check(!PetWindowController.IsButtonMessage(0x20A), "Mouse wheel bypasses hit testing");
        using var sampler = new PetPixelSampler();
        Check(sampler.Capture(IntPtr.Zero) is null, "Invalid window capture fails open to the desktop");
    }

    private static void GestureOrigins()
    {
        foreach (var (down, up, name) in new[] { (0x201, 0x202, "drag"), (0x204, 0x205, "resize") })
        {
            var gesture = new PetGestureState();
            Check(!gesture.Process(down, false, false), $"Blank-origin {name} is not captured");
            Check(!gesture.Process(0x200, true, false), $"Crossing the body cannot start a held {name}");
            gesture.Process(up, true, false);
            Check(gesture.Process(down, true, false), $"Body-origin {name} starts");
            Check(gesture.Process(0x200, false, false), $"Body {name} continues outside the silhouette");
            Check(!gesture.Process(up, false, false), $"Releasing {name} ends interaction");
            Check(!gesture.Process(down, true, true), $"Locked pet cannot start {name}");
            gesture.Process(up, true, true);
            Check(gesture.Process(down, true, false), $"Unlock restores {name}");
            gesture.Cancel();
            Check(!gesture.Process(0x200, true, false), $"Lock during {name} cancels it until release");
            gesture.ReconcileButtons(false, false);
            Check(!gesture.HasHeldButtons, $"Missed {name} release is recovered");
        }
    }

    private static void InputThreadIsolation()
    {
        using var fixture = new TestWindow();
        using var captureEntered = new ManualResetEventSlim();
        using var releaseCapture = new ManualResetEventSlim();
        using var controller = new PetWindowController(() => fixture.Handle, captureOverride: _ =>
        {
            captureEntered.Set();
            releaseCapture.Wait(5000); // Simulate a stalled GPU; never on the hook thread.
            return null;
        });
        controller.ApplyLockState();
        Check(controller.WaitForInputThread(2000), "Dedicated input thread starts");
        Check(controller.IsMouseHookInstalled, "Mouse hook installs on dedicated message thread");
        Check(controller.InputManagedThreadId != Environment.CurrentManagedThreadId, "Mouse hook is not owned by UI thread");
        Check(captureEntered.Wait(2000), "Independent capture worker is running");
        var worker = (NativeInputThread)typeof(PetWindowController).GetField("inputThread", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(controller)!;
        using var processed = new ManualResetEventSlim();
        double latencyMs = -1;
        var sender = Task.Run(() =>
        {
            var sent = Stopwatch.GetTimestamp();
            worker.Post(() => { latencyMs = Stopwatch.GetElapsedTime(sent).TotalMilliseconds; processed.Set(); });
        });
        // Deliberately do not pump the caller's UI messages while capture is also
        // stalled: input messages must still be serviced promptly.
        Thread.Sleep(250);
        Check(processed.Wait(1000), "Input thread processes work while UI and capture threads are blocked");
        Check(latencyMs >= 0 && latencyMs < 200, "Blocked UI/capture do not add a 250 ms input delay");
        sender.GetAwaiter().GetResult();
        var captures = controller.FrameCaptureCount;
        var styles = controller.StyleUpdateCount;
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 1_000_000; i++) controller.UpdatePointerState(fixture.Handle, new Point(i & 1023, i & 511), 0x200);
        watch.Stop();
        Check(controller.FrameCaptureCount == captures, "One million movement events cause zero captures");
        Check(controller.StyleUpdateCount == styles, "One million movement events cause zero style changes");
        Check(watch.Elapsed.TotalMilliseconds < 2000, "High-rate move path stays below 2 microseconds/event");
        Console.WriteLine($"Input isolation: {latencyMs:F3} ms; 1,000,000 move events: {watch.Elapsed.TotalMilliseconds:F2} ms ({watch.Elapsed.TotalNanoseconds / 1_000_000:F1} ns/event); 0 GPU reads, 0 style changes.");
        releaseCapture.Set();

        using var failed = new NativeInputThread("Test failed hook startup", () => throw new InvalidOperationException("synthetic startup failure"), () => { });
        failed.Start();
        Check(failed.WaitUntilStarted(2000), "Failed input startup signals completion");
        Check(failed.StartupException is InvalidOperationException, "Hook startup failure is captured without crashing the process");

        using var startupGate = new ManualResetEventSlim();
        using var earlyAction = new ManualResetEventSlim();
        using var lateAction = new ManualResetEventSlim();
        using var delayed = new NativeInputThread("Test delayed startup", () => startupGate.Wait(2000), () => { });
        Check(delayed.Post(() => earlyAction.Set()), "Control request queued before thread startup is accepted");
        delayed.Start();
        Check(delayed.Post(() => lateAction.Set()), "Control request queued during startup is accepted");
        startupGate.Set();
        Check(earlyAction.Wait(2000) && lateAction.Wait(2000), "Startup queue wakes every pending control request");
    }

    private static void HungRendererIsolation()
    {
        using var fixture = new TestWindow(visible: true);
        var pixels = new byte[fixture.ClientBounds.Width * fixture.ClientBounds.Height * 4];
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        using var controller = new PetWindowController(() => fixture.Handle, captureOverride: hwnd =>
            PetAlphaFrame.FromBgra(hwnd, fixture.ClientBounds, pixels));
        controller.ApplyLockState();
        Check(controller.WaitForInputThread(2000), "Hung-renderer test starts input worker");
        Check(SpinWait.SpinUntil(() => controller.CachedFrame is not null && IsPassingThrough(), 2000), "Fixture reaches idle pass-through");
        var input = (NativeInputThread)typeof(PetWindowController).GetField("inputThread", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(controller)!;
        var point = new Point(fixture.ClientBounds.Left + 10, fixture.ClientBounds.Top + 10);
        using var buttonReturned = new ManualResetEventSlim();
        fixture.DelayStyles(600);
        double buttonMs = -1;
        var foreground = GetForegroundWindow();
        input.Post(() =>
        {
            var sent = Stopwatch.GetTimestamp();
            controller.UpdatePointerState(fixture.Handle, point, 0x201);
            buttonMs = Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
            buttonReturned.Set();
        });
        Check(fixture.StyleEntered.Wait(2000), "Body press reaches deliberately stalled renderer style callback");
        Check(buttonReturned.Wait(300), "Button hook returns before renderer completes its 600 ms callback");
        Check(buttonMs < 100, "Renderer stall cannot turn bounded button handshake into global input freeze");
        using var inputPing = new ManualResetEventSlim();
        var pingAt = Stopwatch.GetTimestamp();
        input.Post(() => inputPing.Set());
        Check(inputPing.Wait(200), "Hook pump remains responsive while window-control worker is blocked");
        var pingMs = Stopwatch.GetElapsedTime(pingAt).TotalMilliseconds;
        Check(SpinWait.SpinUntil(() => fixture.StyleChangesCompleted >= 2 && IsPassingThrough(), 3000),
            "Late activation is reverted to pass-through even when cleanup style callback also stalls");
        Check(GetForegroundWindow() == foreground, "Timed-out body request does not steal foreground focus later");
        fixture.DelayStyles(0);
        input.Post(() => controller.UpdatePointerState(fixture.Handle, point, 0x202));
        Thread.Sleep(80);
        input.Post(() =>
        {
            controller.UpdatePointerState(fixture.Handle, point, 0x201);
            buttonReturned.Set();
        });
        Check(SpinWait.SpinUntil(() => !IsPassingThrough(), 2000), "A fresh body gesture works after renderer recovers");
        fixture.DelayStyles(600);
        controller.Lock();
        Check(fixture.StyleEntered.Wait(2000), "Lock restoration can stall independently on renderer");
        var shutdownAt = Stopwatch.GetTimestamp();
        controller.Dispose();
        var shutdownMs = Stopwatch.GetElapsedTime(shutdownAt).TotalMilliseconds;
        Check(shutdownMs < 500, "Shutdown does not wait indefinitely for renderer styles");
        Check(!controller.IsMouseHookInstalled, "Shutdown removes mouse hook before blocked renderer cleanup");
        // Let this test's own HWND finish queued cleanup before destroying it.
        fixture.DelayStyles(0);
        Thread.Sleep(750);
        Console.WriteLine($"Hung renderer (600 ms per style message): button {buttonMs:F2} ms; hook ping {pingMs:F2} ms; shutdown {shutdownMs:F2} ms; late result returned to pass-through.");
        bool IsPassingThrough() => (GetWindowLongPtr(fixture.Handle, -20) & 0x80020) == 0x80020;
    }

    private static void NativePet(int processId)
    {
        using var pet = Process.GetProcessById(processId);
        Check(pet.ProcessName.Equals("BongoCatMver", StringComparison.OrdinalIgnoreCase), "Target is isolated pet runtime");
        var hwnd = pet.MainWindowHandle;
        Check(hwnd != IntPtr.Zero && GetWindowRect(hwnd, out _), "Native pet HWND valid");
        using var controller = new PetWindowController(() => hwnd, runWorkers: false);
        controller.ApplyLockState();
        var frame = controller.CachedFrame;
        Check(frame is not null, "Native surface cached");
        var opaque = FindPoint(true);
        var transparent = FindPoint(false);
        Check(opaque.HasValue && transparent.HasValue, "Real Live2D frame has opaque body and transparent background");
        Console.WriteLine($"Native pet {processId}; body={opaque}; transparent={transparent}");
        Check(IsPassingThrough(), "Idle pet stays transparent without per-move style switches");
        Check(WindowFromPoint(transparent!.Value) != hwnd, "Windows routes transparent pixel to background");
        foreach (var (down, up, name) in new[] { (0x201, 0x202, "drag"), (0x204, 0x205, "resize") })
        {
            controller.UpdatePointerState(hwnd, transparent.Value, down);
            controller.UpdatePointerState(hwnd, opaque!.Value, 0x200);
            Check(IsPassingThrough(), $"Blank-origin {name} remains pass-through over body");
            controller.UpdatePointerState(hwnd, opaque.Value, up);
            controller.UpdatePointerState(hwnd, opaque.Value, down);
            Check(!IsPassingThrough(), $"Body-origin {name} activates native pet");
            Check(WindowFromPoint(opaque.Value) == hwnd, "Windows routes body press to renderer");
            controller.UpdatePointerState(hwnd, new Point(frame!.Bounds.Left - 30, frame.Bounds.Top - 30), 0x200);
            Check(!IsPassingThrough(), $"Body {name} continues outside body");
            controller.UpdatePointerState(hwnd, transparent.Value, up);
            Thread.Sleep(60);
            controller.ApplyLockState();
            Check(IsPassingThrough(), $"Released {name} returns to idle pass-through");
        }
        controller.Lock();
        controller.UpdatePointerState(hwnd, opaque!.Value, 0x201);
        Check(IsPassingThrough(), "Locked body passes through");
        controller.UpdatePointerState(hwnd, opaque.Value, 0x202);
        controller.Unlock();
        controller.UpdatePointerState(hwnd, opaque.Value, 0x201);
        Check(!IsPassingThrough(), "Unlock permits body press");
        controller.UpdatePointerState(hwnd, opaque.Value, 0x202);
        Thread.Sleep(60);
        controller.ApplyLockState();

        GetWindowRect(hwnd, out var original);
        SetWindowPos(hwnd, IntPtr.Zero, original.Left + 5, original.Top + 5, 0, 0, 0x15);
        controller.UpdatePointerState(hwnd, opaque.Value, 0x201);
        Check(IsPassingThrough(), "Moving native window invalidates stale frame coordinates before next capture");
        controller.UpdatePointerState(hwnd, opaque.Value, 0x202);
        SetWindowPos(hwnd, IntPtr.Zero, original.Left, original.Top, 0, 0, 0x15);
        controller.ApplyLockState();

        using (var cover = new Form
        {
            FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(opaque.Value.X - 8, opaque.Value.Y - 8, 32, 32), TopMost = true, ShowInTaskbar = false
        })
        {
            cover.Show();
            Application.DoEvents();
            Thread.Sleep(80);
            cover.BringToFront();
            SetWindowPos(cover.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
            Application.DoEvents();
            Console.WriteLine($"Occlusion fixture hit={WindowFromPoint(opaque.Value)}, cover={cover.Handle}, pet={hwnd}");
            controller.UpdatePointerState(hwnd, opaque.Value, 0x201);
            Check(IsPassingThrough(), "Foreground window keeps click even over opaque pet pixel");
            Check(WindowFromPoint(opaque.Value) == cover.Handle, "Windows preserves foreground control routing");
            controller.UpdatePointerState(hwnd, opaque.Value, 0x202);
            cover.Close();
        }
        using var sampler = new PetPixelSampler();
        for (var i = 0; i < 5; i++) sampler.Capture(hwnd);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var captureWatch = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++) Check(sampler.Capture(hwnd) is not null, "Native frame remains capturable");
        captureWatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated < 256_000, "Steady capture reuses full-frame buffers instead of allocating screenshots");
        Console.WriteLine($"100 native captures: {captureWatch.Elapsed.TotalMilliseconds:F2} ms; managed allocation: {allocated:N0} bytes.");

        Point? FindPoint(bool expected)
        {
            for (var y = frame!.Bounds.Top + 10; y < frame.Bounds.Bottom - 10; y += 12)
                for (var x = frame.Bounds.Left + 10; x < frame.Bounds.Right - 10; x += 12)
                {
                    var p = new Point(x, y);
                    if (frame.IsVisiblePixel(p) != expected) continue;
                    if (!expected || new[] { new Point(x - 6, y), new Point(x + 6, y), new Point(x, y - 6), new Point(x, y + 6) }.All(frame.IsVisiblePixel)) return p;
                }
            return null;
        }
        bool IsPassingThrough() => (GetWindowLongPtr(hwnd, -20) & 0x80020) == 0x80020;
    }

    private static void NativeGestures(int processId)
    {
        using var pet = Process.GetProcessById(processId);
        Check(pet.ProcessName == "BongoCatMver" && pet.MainModule!.FileName.Contains("pet-hit-probe", StringComparison.OrdinalIgnoreCase),
            "Real-input regression targets only a separately launched pet-hit-probe renderer");
        var hwnd = pet.MainWindowHandle;
        GetCursorPos(out var savedPointer);
        GetWindowRect(hwnd, out var original);
        var backdropStart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true };
        foreach (var argument in new[] { "--gesture-backdrop", (original.Left - 150).ToString(), (original.Top - 50).ToString(),
            (original.Right - original.Left + 300).ToString(), (original.Bottom - original.Top + 150).ToString() }) backdropStart.ArgumentList.Add(argument);
        using var backdrop = Process.Start(backdropStart)!;
        var backdropReady = backdrop.StandardOutput.ReadLineAsync();
        Check(backdropReady.Wait(3000) && long.TryParse(backdropReady.Result, out _), "Independent backdrop process starts");
        var background = new IntPtr(long.Parse(backdropReady.Result!));
        try
        {
            TestGesture("baseline right enlarge", 60, 0, false, true);
            using var controller = new PetWindowController(() => hwnd);
            controller.ApplyLockState();
            Check(controller.WaitForInputThread(2000), "Real gesture hook starts");
            Check(SpinWait.SpinUntil(() => controller.CachedFrame is not null, 2000), "Real gesture alpha cache ready");
            TestGesture("controller right enlarge", 60, 0, false, true);
            TestGesture("controller right shrink", -60, 0, false, true);
            TestGesture("controller left drag", 48, 24, true, true);
            TestGesture("transparent-origin right drag", 60, 0, false, false);
            controller.Lock();
            PumpFor(100);
            TestGesture("locked body right drag", 60, 0, false, true, locked: true);
            controller.Unlock();
            PumpFor(100);
            TestGesture("unlocked right drag", 60, 0, false, true);
            TestGesture("right drag beyond left edge", -350, 0, false, true);
            ResetWindow();
            FocusBackdrop();
            using (var lockedSampler = new PetPixelSampler())
            {
                var lockedPoint = FindBodyPoint(lockedSampler.Capture(hwnd)!);
                SetCursorPos(lockedPoint.X, lockedPoint.Y);
                SendMouse(0, 0, 0x8);
                PumpFor(130);
                SetCursorPos(lockedPoint.X + 30, lockedPoint.Y);
                PumpFor(100);
                controller.Lock();
                PumpFor(150);
                GetWindowRect(hwnd, out var lockedSize);
                SetCursorPos(lockedPoint.X + 110, lockedPoint.Y);
                PumpFor(150);
                GetWindowRect(hwnd, out var afterLockedMotion);
                Check(EqualRect(lockedSize, afterLockedMotion), "Lock during a held resize releases native gesture before further movement");
                SendMouse(0, 0, 0x10);
                PumpFor(100);
                Check((GetAsyncKeyState(2) & 0x8000) == 0, "Lock cleanup and later physical release do not leave right button held");
                controller.Unlock();
                PumpFor(100);
            }
            TestGesture("unlock after held-resize cancellation", 60, 0, false, true);
            ResetWindow();
            FocusBackdrop();
            using var sampler = new PetPixelSampler();
            var body = FindBodyPoint(sampler.Capture(hwnd)!);
            SetCursorPos(body.X, body.Y);
            PumpFor(50);
            for (var i = 0; i < 10; i++)
            {
                SendMouseBatch(0x8, 0x10);
                PumpFor(90);
            }
            Check((GetAsyncKeyState(2) & 0x8000) == 0, "Immediate down/up batches never leave right button held");
            GetWindowRect(hwnd, out var released);
            SetCursorPos(body.X + 80, body.Y);
            PumpFor(180);
            GetWindowRect(hwnd, out var movedAfterRelease);
            Check(EqualRect(released, movedAfterRelease), "Native resizing stops after rapid click releases");
            Console.WriteLine("10 immediate right-button down/up batches released cleanly.");
            ResetWindow();
            FocusBackdrop();
            var finalFrame = sampler.Capture(hwnd)!;
            body = FindBodyPoint(finalFrame);
            SetCursorPos(body.X, body.Y);
            PumpFor(50);
            SendMouse(0, 0, 0x8);
            PumpFor(20);
            SendMouse(0, 0, 0x10);
            var blankDowns = SendMessage(background, 0x8310, 0, 0).ToInt32();
            SetCursorPos(finalFrame.Bounds.Left + 8, finalFrame.Bounds.Top + 8);
            SendMouseBatch(0x8, 0x10);
            PumpFor(150);
            Check(SendMessage(background, 0x8310, 0, 0).ToInt32() > blankDowns, "Transparent click immediately after body release reaches the underlying application");
        }
        finally
        {
            SendMouse(0, 0, 0x4 | 0x10); // Always release left/right buttons, including failed assertions.
            SetWindowPos(hwnd, IntPtr.Zero, original.Left, original.Top, original.Right - original.Left, original.Bottom - original.Top, 0x14);
            SetCursorPos(savedPointer.X, savedPointer.Y);
            PostMessage(background, 0x10, 0, 0);
            if (!backdrop.WaitForExit(2000)) backdrop.Kill();
        }

        void TestGesture(string name, int dx, int dy, bool left, bool onBody, bool locked = false)
        {
            ResetWindow();
            FocusBackdrop();
            using var sampler = new PetPixelSampler();
            var frame = sampler.Capture(hwnd)!;
            var point = onBody ? FindBodyPoint(frame) : new Point(frame.Bounds.Left + 8, frame.Bounds.Top + 8);
            Check(onBody || !frame.IsVisiblePixel(point), "Transparent test origin has no visible pet pixel");
            var backgroundDowns = SendMessage(background, 0x8310, 0, 0).ToInt32();
            GetWindowRect(hwnd, out var before);
            SetCursorPos(point.X, point.Y);
            PumpFor(80);
            SendMouse(0, 0, left ? 0x2u : 0x8u);
            PumpFor(180);
            var focused = GetForegroundWindow() == hwnd;
            var style = GetWindowLongPtr(hwnd, -20);
            for (var step = 1; step <= 6; step++)
            {
                SetCursorPos(point.X + step * dx / 6, point.Y + step * dy / 6);
                PumpFor(50);
            }
            SendMouse(0, 0, left ? 0x4u : 0x10u);
            PumpFor(120);
            GetWindowRect(hwnd, out var after);
            Console.WriteLine($"Real {name}: focused={focused}, exstyle=0x{style:X}, rect ({before.Left},{before.Top}) {before.Right - before.Left}x{before.Bottom - before.Top} -> ({after.Left},{after.Top}) {after.Right - after.Left}x{after.Bottom - after.Top}.");
            if (!onBody || locked)
            {
                Check(EqualRect(before, after), $"{name}: pet does not move or resize");
                Check(SendMessage(background, 0x8310, 0, 0).ToInt32() > backgroundDowns, $"{name}: underlying independent application receives right press");
            }
            else if (left)
            {
                Check(after.Left >= before.Left + 35 && after.Top >= before.Top + 15, "Held left button actually moves native renderer");
                Check(after.Right - after.Left == before.Right - before.Left, "Left drag preserves size");
            }
            else
            {
                Check(focused, $"{name}: native renderer gains foreground from another process");
                Check(SendMessage(background, 0x8310, 0, 0).ToInt32() == backgroundDowns, $"{name}: body press does not leak to the underlying application");
                Check(dx > 0 ? after.Right - after.Left >= before.Right - before.Left + 40 : after.Right - after.Left <= before.Right - before.Left - 40,
                    $"{name}: held right-button motion actually resizes native renderer");
                SetCursorPos(point.X + dx + 40, point.Y + dy + 40);
                PumpFor(120);
                GetWindowRect(hwnd, out var afterMove);
                Check(EqualRect(after, afterMove), $"{name}: release ends native resize");
            }
        }
        void ResetWindow()
        {
            SetWindowPos(hwnd, IntPtr.Zero, original.Left, original.Top, original.Right - original.Left, original.Bottom - original.Top, 0x14);
            PumpFor(150);
        }
        void FocusBackdrop()
        {
            SetCursorPos(original.Left - 80, original.Top + 40);
            SendMouseBatch(0x2, 0x4);
            PumpFor(80);
            Check(GetForegroundWindow() == background, "A separate application owns focus before the pet press");
        }
    }

    private static Point FindBodyPoint(PetAlphaFrame frame)
    {
        for (var y = frame.Bounds.Top + 16; y < frame.Bounds.Bottom - 16; y += 8)
            for (var x = frame.Bounds.Left + 16; x < frame.Bounds.Right - 16; x += 8)
            {
                var point = new Point(x, y);
                if (new[] { point, new Point(x - 12, y), new Point(x + 12, y), new Point(x, y - 12), new Point(x, y + 12) }.All(frame.IsVisiblePixel)) return point;
            }
        throw new InvalidOperationException("Native frame has no stable opaque body point.");
    }

    private static void PumpFor(int milliseconds)
    {
        var until = Stopwatch.GetTimestamp() + Stopwatch.Frequency * milliseconds / 1000;
        while (Stopwatch.GetTimestamp() < until) { Application.DoEvents(); Thread.Sleep(5); }
    }

    private static void SendMouse(int x, int y, uint flags)
    {
        var inputs = new[] { new Input { Type = 0, Mouse = new MouseInput { X = x, Y = y, Flags = flags } } };
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) != 1) throw new InvalidOperationException("Could not send isolated test mouse input.");
    }
    private static void SendMouseBatch(params uint[] flags)
    {
        var inputs = flags.Select(flag => new Input { Mouse = new MouseInput { Flags = flag } }).ToArray();
        Check(SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length, "Isolated input batch is inserted");
    }
    private static bool EqualRect(Rect a, Rect b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    private static void NativeWorkerPet(int processId)
    {
        using var pet = Process.GetProcessById(processId);
        var hwnd = pet.MainWindowHandle;
        using var controller = new PetWindowController(() => hwnd);
        controller.ApplyLockState();
        Check(controller.WaitForInputThread(2000), "Production native input worker starts");
        Check(SpinWait.SpinUntil(() => controller.CachedFrame is not null && IsPassingThrough(), 3000), "Production native frame and initial pass-through ready");
        var input = (NativeInputThread)typeof(PetWindowController).GetField("inputThread", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(controller)!;
        var successes = 0;
        var latencies = new List<double>();
        for (var i = 0; i < 80; i++)
        {
            var frame = controller.CachedFrame!;
            var point = Point.Empty;
            var found = false;
            for (var y = frame.Bounds.Top + 12; y < frame.Bounds.Bottom - 12 && !found; y += 12)
                for (var x = frame.Bounds.Left + 12; x < frame.Bounds.Right - 12; x += 12)
                {
                    var candidate = new Point(x, y);
                    if (!new[] { candidate, new Point(x - 8, y), new Point(x + 8, y), new Point(x, y - 8), new Point(x, y + 8) }.All(frame.IsVisiblePixel)) continue;
                    point = candidate;
                    found = true;
                    break;
                }
            Check(found, "Production cached frame exposes a stable body pixel");
            var completed = new TaskCompletionSource<(bool, double)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var down = i % 2 == 0 ? 0x201 : 0x204;
            input.Post(() =>
            {
                var started = Stopwatch.GetTimestamp();
                controller.UpdatePointerState(hwnd, point, down);
                completed.SetResult((!IsPassingThrough(), Stopwatch.GetElapsedTime(started).TotalMilliseconds));
                controller.UpdatePointerState(hwnd, point, down + 1);
            });
            Check(completed.Task.Wait(2000), "Production button request completes");
            var result = completed.Task.Result;
            if (result.Item1) successes++;
            latencies.Add(result.Item2);
            Thread.Sleep(80);
            Check(SpinWait.SpinUntil(IsPassingThrough, 2000), "Production release returns to pass-through");
        }
        latencies.Sort();
        Console.WriteLine($"Production native workers: {successes}/80 body drag/resize presses activated; median {latencies[40]:F2} ms, p95 {latencies[76]:F2} ms, max {latencies[^1]:F2} ms.");
        Check(successes == 80, "Every native body press activates within the bounded production handshake");
        bool IsPassingThrough() => (GetWindowLongPtr(hwnd, -20) & 0x80020) == 0x80020;
    }

    private sealed class TestWindow : IDisposable
    {
        private readonly NativeInputThread owner;
        private BlockingForm? window;
        internal IntPtr Handle { get; private set; }
        internal Rectangle ClientBounds { get; private set; }
        internal ManualResetEventSlim StyleEntered => window!.StyleEntered;
        internal int StyleChangesCompleted => window!.StyleChangesCompleted;
        internal void DelayStyles(int milliseconds) => window!.DelayStyles(milliseconds);
        internal TestWindow(bool visible = false)
        {
            owner = new NativeInputThread("Test renderer owner", () =>
            {
                window = new BlockingForm { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None,
                    Bounds = new Rectangle(40, 40, 40, 40), StartPosition = FormStartPosition.Manual, TopMost = visible };
                Handle = window.Handle;
                if (visible) window.Show();
                ClientBounds = new Rectangle(window.PointToScreen(Point.Empty), window.ClientSize);
            }, () => window?.Dispose());
            owner.Start();
            if (!owner.WaitUntilStarted(2000) || Handle == IntPtr.Zero) throw new InvalidOperationException("Test window did not start.");
        }
        public void Dispose() => owner.Dispose();
    }

    private sealed class BlockingForm : Form
    {
        internal readonly ManualResetEventSlim StyleEntered = new(false);
        private int delayMilliseconds, styleChangesCompleted;
        internal int StyleChangesCompleted => Volatile.Read(ref styleChangesCompleted);
        internal void DelayStyles(int milliseconds)
        {
            StyleEntered.Reset();
            Interlocked.Exchange(ref styleChangesCompleted, 0);
            Volatile.Write(ref delayMilliseconds, milliseconds);
        }
        protected override bool ShowWithoutActivation => true;
        protected override void WndProc(ref Message message)
        {
            var delay = Volatile.Read(ref delayMilliseconds);
            if (message.Msg == 0x7C && delay > 0)
            {
                StyleEntered.Set();
                Thread.Sleep(delay);
                Interlocked.Increment(ref styleChangesCompleted);
            }
            base.WndProc(ref message);
        }
    }
    private sealed class GestureBackdrop : Form
    {
        private int rightDowns;
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ShowWindow(Handle, 4);
            Console.WriteLine(Handle.ToInt64());
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x204) rightDowns++;
            if (message.Msg == 0x8310) { message.Result = new IntPtr(rightDowns); return; }
            base.WndProc(ref message);
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        assertions++;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
