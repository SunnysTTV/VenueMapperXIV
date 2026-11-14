using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace VenueMapper.Services;

public readonly record struct ScreenRect(float X, float Y, float Width, float Height)
{
    public static ScreenRect FromCurrentWindow()
    {
        var pos = Dalamud.Bindings.ImGui.ImGui.GetWindowPos();
        var size = Dalamud.Bindings.ImGui.ImGui.GetWindowSize();
        return new ScreenRect(pos.X, pos.Y, size.X, size.Y);
    }
}

#if DEBUG
public sealed record CapturedFrame(int Width, int Height, byte[] Bgra);

public static class ScreenCapture
{
    private const int SrcCopy = 0x00CC0020;
    private const int CaptureBlt = 0x40000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr src, int srcX, int srcY, int rop);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfoHeader info, uint usage);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);

    public static CapturedFrame? CaptureGameWindow(out string error)
    {
        error = string.Empty;
        var window = Process.GetCurrentProcess().MainWindowHandle;
        if (window == IntPtr.Zero || !GetClientRect(window, out var rect))
        {
            error = "game window not found";
            return null;
        }
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            error = "game window has no size";
            return null;
        }
        var origin = new Point();
        ClientToScreen(window, ref origin);

        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var old = SelectObject(memory, bitmap);
        try
        {
            if (!BitBlt(memory, 0, 0, width, height, screen, origin.X, origin.Y, SrcCopy | CaptureBlt))
            {
                error = "copying the screen failed";
                return null;
            }
            var info = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
            };
            var pixels = new byte[width * height * 4];
            if (GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref info, 0) == 0)
            {
                error = "reading the pixels failed";
                return null;
            }
            return new CapturedFrame(width, height, pixels);
        }
        finally
        {
            SelectObject(memory, old);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    public static bool LooksBlack(CapturedFrame frame)
    {
        var step = Math.Max(4, frame.Bgra.Length / 4096 / 4 * 4);
        for (var i = 0; i < frame.Bgra.Length; i += step)
        {
            if (frame.Bgra[i] > 8 || frame.Bgra[i + 1] > 8 || frame.Bgra[i + 2] > 8) return false;
        }
        return true;
    }

    public static void SavePng(CapturedFrame frame, string path)
    {
        var stride = frame.Width * 3;
        var raw = new byte[(stride + 1) * frame.Height];
        for (var y = 0; y < frame.Height; y++)
        {
            var row = y * (stride + 1);
            raw[row] = 0;
            var src = y * frame.Width * 4;
            for (var x = 0; x < frame.Width; x++)
            {
                var o = row + 1 + x * 3;
                var s = src + x * 4;
                raw[o] = frame.Bgra[s + 2];
                raw[o + 1] = frame.Bgra[s + 1];
                raw[o + 2] = frame.Bgra[s];
            }
        }

        byte[] compressed;
        using (var buffer = new MemoryStream())
        {
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw, 0, raw.Length);
            compressed = buffer.ToArray();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        WriteBigEndian(header, 0, frame.Width);
        WriteBigEndian(header, 4, frame.Height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(file, "IHDR", header);
        WriteChunk(file, "IDAT", compressed);
        WriteChunk(file, "IEND", []);
    }

    private static void WriteBigEndian(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc(typeBytes, data);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        stream.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
#endif
