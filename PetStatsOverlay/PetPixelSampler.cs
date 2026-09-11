using System.Runtime.InteropServices;

namespace PetStatsOverlay;

/// <summary>
/// Reads the rendered SFML/OpenGL window surface, including its alpha channel.
/// A screen screenshot would include the desktop behind the pet and cannot be
/// used for hit testing. Keeping the window region untouched also lets Live2D
/// animation draw outside the silhouette of the previous frame.
/// </summary>
internal sealed class PetPixelSampler : IDisposable
{
    internal const byte MinimumHitAlpha = 8;
    private IntPtr memoryDc;
    private IntPtr bitmap;
    private IntPtr originalBitmap;
    private IntPtr pixels;

    public bool IsVisiblePixel(IntPtr window, Point screenPoint)
    {
        var point = screenPoint;
        if (!ScreenToClient(window, ref point) || !GetClientRect(window, out var bounds)
            || !ContainsClientPoint(bounds.Right, bounds.Bottom, point))
        {
            return false;
        }

        var sourceDc = GetDC(window);
        if (sourceDc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!EnsureBuffer(sourceDc))
            {
                return false;
            }

            // SRCCOPY, deliberately without CAPTUREBLT: capture only this
            // window's render surface, never another window on top of it.
            if (!BitBlt(memoryDc, 0, 0, 1, 1, sourceDc, point.X, point.Y, 0x00CC0020))
            {
                return false;
            }

            GdiFlush();
            return IsHitAlpha(Marshal.ReadByte(pixels, 3));
        }
        finally
        {
            ReleaseDC(window, sourceDc);
        }
    }

    internal static bool ContainsClientPoint(int width, int height, Point point) =>
        point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;

    internal static bool IsHitAlpha(byte alpha) => alpha >= MinimumHitAlpha;

    private bool EnsureBuffer(IntPtr sourceDc)
    {
        if (memoryDc != IntPtr.Zero)
        {
            return bitmap != IntPtr.Zero;
        }

        memoryDc = CreateCompatibleDC(sourceDc);
        if (memoryDc == IntPtr.Zero)
        {
            return false;
        }

        var info = new BitmapInfo
        {
            Size = (uint)Marshal.SizeOf<BitmapInfo>(),
            Width = 1,
            Height = -1,
            Planes = 1,
            BitCount = 32,
            SizeImage = 4
        };
        bitmap = CreateDIBSection(sourceDc, ref info, 0, out pixels, IntPtr.Zero, 0);
        if (bitmap == IntPtr.Zero)
        {
            Dispose();
            return false;
        }

        originalBitmap = SelectObject(memoryDc, bitmap);
        return originalBitmap != IntPtr.Zero && originalBitmap != new IntPtr(-1);
    }

    public void Dispose()
    {
        if (memoryDc != IntPtr.Zero && originalBitmap != IntPtr.Zero)
        {
            SelectObject(memoryDc, originalBitmap);
        }
        if (bitmap != IntPtr.Zero)
        {
            DeleteObject(bitmap);
        }
        if (memoryDc != IntPtr.Zero)
        {
            DeleteDC(memoryDc);
        }
        memoryDc = bitmap = originalBitmap = pixels = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref Point point);
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
