using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PetStatsOverlay;

/// <summary>
/// Captures the renderer on a worker thread. Mouse hooks only consult the latest
/// managed alpha frame; they never read the GPU or a window device context.
/// </summary>
internal sealed class PetPixelSampler : IDisposable
{
    internal const byte MinimumHitAlpha = 8;
    private const int MaximumPixels = 16 * 1024 * 1024;
    private readonly PetAlphaBuffer[] buffers = [new(), new(), new()];
    private IntPtr memoryDc, bitmap, originalBitmap, pixels;
    private int bitmapWidth, bitmapHeight;
    private PetAlphaFrame? latestFrame;
    internal long CaptureCount { get; private set; }

    public PetAlphaFrame? Capture(IntPtr window)
    {
        if (window == IntPtr.Zero || !GetClientRect(window, out var bounds)) return null;
        var width = bounds.Right;
        var height = bounds.Bottom;
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels) return null;
        var origin = Point.Empty;
        if (!ClientToScreen(window, ref origin)) return null;
        var buffer = buffers.FirstOrDefault(candidate => candidate != latestFrame?.Buffer && candidate.TryBeginWrite());
        if (buffer is null) return latestFrame;
        try
        {
            var sourceDc = GetDC(window);
            if (sourceDc == IntPtr.Zero) return null;
            try
            {
                if (!EnsureSurface(sourceDc, width, height)) return null;
                // Never use CAPTUREBLT or a desktop DC: their pixels may include
                // other applications, rather than the pet's transparent surface.
                if (!BitBlt(memoryDc, 0, 0, width, height, sourceDc, 0, 0, 0x00CC0020)) return null;
                GdiFlush();
                var length = checked(width * height * 4);
                buffer.EnsureCapacity(length);
                Marshal.Copy(pixels, buffer.Pixels, 0, length);
                CaptureCount++;
                latestFrame = new PetAlphaFrame(window, new Rectangle(origin, new Size(width, height)), buffer, Stopwatch.GetTimestamp());
                return latestFrame;
            }
            finally { ReleaseDC(window, sourceDc); }
        }
        finally { buffer.EndWrite(); }
    }

    internal static bool ContainsClientPoint(int width, int height, Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;
    internal static bool IsHitAlpha(byte alpha) => alpha >= MinimumHitAlpha;

    private bool EnsureSurface(IntPtr sourceDc, int width, int height)
    {
        if (bitmap != IntPtr.Zero && bitmapWidth == width && bitmapHeight == height) return true;
        if (memoryDc == IntPtr.Zero)
        {
            memoryDc = CreateCompatibleDC(sourceDc);
            if (memoryDc == IntPtr.Zero) return false;
        }
        if (bitmap != IntPtr.Zero)
        {
            SelectObject(memoryDc, originalBitmap);
            DeleteObject(bitmap);
            bitmap = IntPtr.Zero;
        }
        var info = new BitmapInfo
        {
            Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = width, Height = -height,
            Planes = 1, BitCount = 32, SizeImage = checked((uint)(width * height * 4))
        };
        bitmap = CreateDIBSection(sourceDc, ref info, 0, out pixels, IntPtr.Zero, 0);
        if (bitmap == IntPtr.Zero) return false;
        var previous = SelectObject(memoryDc, bitmap);
        if (previous == IntPtr.Zero || previous == new IntPtr(-1))
        {
            DeleteObject(bitmap);
            bitmap = IntPtr.Zero;
            return false;
        }
        originalBitmap = previous;
        bitmapWidth = width;
        bitmapHeight = height;
        return true;
    }

    public void Dispose()
    {
        if (memoryDc != IntPtr.Zero && originalBitmap != IntPtr.Zero) SelectObject(memoryDc, originalBitmap);
        if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
        if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
        memoryDc = bitmap = originalBitmap = pixels = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
}

internal sealed class PetAlphaFrame(IntPtr window, Rectangle bounds, PetAlphaBuffer buffer, long capturedAt)
{
    public IntPtr Window { get; } = window;
    public Rectangle Bounds { get; } = bounds;
    public long CapturedAt { get; } = capturedAt;
    internal PetAlphaBuffer Buffer { get; } = buffer;
    private readonly int bufferGeneration = buffer.Generation;

    public bool IsVisiblePixel(Point point)
    {
        if (!Bounds.Contains(point) || !Buffer.TryBeginRead()) return false;
        try
        {
            if (Buffer.Generation != bufferGeneration) return false;
            var offset = checked(((point.Y - Bounds.Top) * Bounds.Width + point.X - Bounds.Left) * 4 + 3);
            return offset < Buffer.Pixels.Length && PetPixelSampler.IsHitAlpha(Buffer.Pixels[offset]);
        }
        finally { Buffer.EndRead(); }
    }

    internal static PetAlphaFrame FromBgra(IntPtr window, Rectangle bounds, byte[] pixels)
    {
        if (pixels.Length != checked(bounds.Width * bounds.Height * 4)) throw new ArgumentException("Wrong BGRA frame length.", nameof(pixels));
        return new PetAlphaFrame(window, bounds, new PetAlphaBuffer { Pixels = pixels }, Stopwatch.GetTimestamp());
    }
}

// Reuse large managed arrays instead of allocating a full screenshot every
// frame. A reader lease prevents a paused callback from observing a reused
// buffer with a different size. Contention fails open to the desktop; no waits.
internal sealed class PetAlphaBuffer
{
    private int users;
    private int generation;
    internal int Generation => Volatile.Read(ref generation);
    internal byte[] Pixels = [];
    internal bool TryBeginWrite()
    {
        if (Interlocked.CompareExchange(ref users, -1, 0) != 0) return false;
        Interlocked.Increment(ref generation);
        return true;
    }
    internal void EndWrite() => Volatile.Write(ref users, 0);
    internal bool TryBeginRead()
    {
        var observed = Volatile.Read(ref users);
        return observed >= 0 && Interlocked.CompareExchange(ref users, observed + 1, observed) == observed;
    }
    internal void EndRead() => Interlocked.Decrement(ref users);
    internal void EnsureCapacity(int count)
    {
        if (Pixels.Length < count)
        {
            var capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)count);
            Pixels = GC.AllocateUninitializedArray<byte>(capacity);
        }
    }
}
