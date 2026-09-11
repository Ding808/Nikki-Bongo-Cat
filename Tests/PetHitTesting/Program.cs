using System.Diagnostics;
using System.Runtime.InteropServices;
using PetStatsOverlay;

internal static class Program
{
    private static int assertions;

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Check(!PetPixelSampler.IsHitAlpha(0), "Fully transparent pixels pass through");
        Check(!PetPixelSampler.IsHitAlpha(7), "Barely visible antialias fringe passes through");
        Check(PetPixelSampler.IsHitAlpha(8), "Visible alpha threshold is included");
        Check(PetPixelSampler.IsHitAlpha(255), "Opaque pixels are interactive");
        Check(PetPixelSampler.ContainsClientPoint(612, 354, new Point(0, 0)), "Client origin included");
        Check(PetPixelSampler.ContainsClientPoint(612, 354, new Point(611, 353)), "Last client pixel included");
        Check(!PetPixelSampler.ContainsClientPoint(612, 354, new Point(612, 353)), "Right edge excluded");
        Check(!PetPixelSampler.ContainsClientPoint(612, 354, new Point(611, 354)), "Bottom edge excluded");
        Check(!PetPixelSampler.ContainsClientPoint(612, 354, new Point(-1, 0)), "Negative X excluded");
        Check(!PetPixelSampler.ContainsClientPoint(612, 354, new Point(0, -1)), "Negative Y excluded");
        Check(!PetPixelSampler.ContainsClientPoint(0, 0, Point.Empty), "Empty surface excluded");
        if (args.Length == 2 && args[0] == "--native")
        {
            NativePet(int.Parse(args[1]));
        }
        Console.WriteLine($"PASS: {assertions} hit-testing assertions.");
    }

    private static void NativePet(int processId)
    {
        using var pet = Process.GetProcessById(processId);
        Check(pet.ProcessName.Equals("BongoCatMver", StringComparison.OrdinalIgnoreCase), "Target is isolated pet runtime");
        var hwnd = pet.MainWindowHandle;
        Check(hwnd != IntPtr.Zero && GetWindowRect(hwnd, out _), "Native pet HWND is valid");
        using var controller = new PetWindowController();
        Check(controller.GetPetRect() is not null, "Controller discovers renderer");
        GetWindowRect(hwnd, out var rect);
        using var sampler = new PetPixelSampler();
        var opaque = FindPoint(true);
        var transparent = FindPoint(false);
        Check(opaque.HasValue, "Actual rendered Live2D frame contains opaque body pixels");
        Check(transparent.HasValue, "Actual rendered Live2D frame contains transparent pixels");
        Console.WriteLine($"Native pet {processId}; body={opaque}; transparent={transparent}");

        controller.UpdatePointerState(hwnd, transparent!.Value, 0x0200);
        Check(IsPassingThrough(), "Transparent surface enables native pass-through");
        Check(WindowFromPoint(transparent.Value) != hwnd, "Windows routes transparent pixel to underlying window");
        Check(sampler.IsVisiblePixel(hwnd, opaque!.Value), "Adding pass-through preserves rendered body alpha");
        controller.UpdatePointerState(hwnd, opaque.Value, 0x0200);
        Check(!IsPassingThrough(), "Body re-enables interaction");
        Check(WindowFromPoint(opaque.Value) == hwnd, "Windows routes opaque pixel to pet");

        foreach (var (down, up, name) in new[] { (0x0201, 0x0202, "drag"), (0x0204, 0x0205, "resize") })
        {
            controller.UpdatePointerState(hwnd, transparent.Value, down);
            controller.UpdatePointerState(hwnd, opaque.Value, 0x0200);
            Check(IsPassingThrough(), $"A {name} started in blank space cannot attach to the pet");
            controller.UpdatePointerState(hwnd, opaque.Value, up);
            controller.UpdatePointerState(hwnd, opaque.Value, down);
            controller.UpdatePointerState(hwnd, new Point(rect.Left - 30, rect.Top - 30), 0x0200);
            Check(!IsPassingThrough(), $"A body {name} continues across transparent pixels");
            controller.UpdatePointerState(hwnd, transparent.Value, up);
            Check(IsPassingThrough(), $"Releasing {name} restores pixel hit testing");
        }

        controller.Lock();
        Check(controller.IsMouseHookInstalled, "Windows low-level mouse hook installs successfully");
        controller.UpdatePointerState(hwnd, opaque.Value, 0x0201);
        Check(IsPassingThrough(), "Locked opaque pixels pass through");
        controller.UpdatePointerState(hwnd, opaque.Value, 0x0202);
        controller.Unlock();
        controller.UpdatePointerState(hwnd, opaque.Value, 0x0200);
        Check(!IsPassingThrough(), "Unlock restores body interaction");

        // An opaque foreground tool window must keep its own click even if the
        // pet has an opaque pixel at the same screen coordinate.
        using (var cover = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(opaque.Value.X - 8, opaque.Value.Y - 8, 32, 32),
            TopMost = true,
            ShowInTaskbar = false
        })
        {
            cover.Show();
            Application.DoEvents();
            controller.UpdatePointerState(hwnd, opaque.Value, 0x0200);
            Check(IsPassingThrough(), "An overlapping window is never intercepted by the pet");
            Check(WindowFromPoint(opaque.Value) == cover.Handle, "Windows preserves foreground control routing");
            cover.Close();
        }
        Check(!sampler.IsVisiblePixel(IntPtr.Zero, Point.Empty), "Invalid window capture safely passes through");

        Point? FindPoint(bool expected)
        {
            for (var y = rect.Top + 10; y < rect.Bottom - 10; y += 12)
                for (var x = rect.Left + 10; x < rect.Right - 10; x += 12)
                {
                    var p = new Point(x, y);
                    if (sampler.IsVisiblePixel(hwnd, p) == expected) return p;
                }
            return null;
        }
        bool IsPassingThrough() => (GetWindowLongPtr(hwnd, -20) & 0x80020) == 0x80020;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        assertions++;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(IntPtr hwnd, int index);
}
