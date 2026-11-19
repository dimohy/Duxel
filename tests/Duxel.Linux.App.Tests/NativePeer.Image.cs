using System.Numerics;
using System.Runtime.InteropServices;

internal sealed unsafe partial class NativePeer
{
    private bool _observedWindowDestroyed;
    internal bool HasNativeFocus()
    {
        GetNativeFocus(_display, out var window, out _);
        return window == _window;
    }
    internal void BeginNativeObservation()
    {
        ObserveInput(_display, _window, 1 | 2 | 8 | 16 | 32 | 64 | 32768 | 131072 | 2097152);
        Flush(_display);
    }

    internal int[] DrainNativeObservation()
    {
        List<int> events = [];
        while (EventsPending(_display) != 0)
        {
            NextObserved(_display, out var value);
            events.Add(value.Type);
            if (value.Type == 17)
                _observedWindowDestroyed = true;
        }
        return events.ToArray();
    }
    [StructLayout(LayoutKind.Explicit, Size = 136)]
    private struct WindowAttributes
    {
        [FieldOffset(8)] internal int Width;
        [FieldOffset(12)] internal int Height;
        [FieldOffset(92)] internal int MapState;
    }

    internal bool CanCapture(int width, int height, out string state)
    {
        if (GetWindowAttributes(_display, _window, out var attributes) == 0
            || TranslateCoordinates(_display, _window, DefaultRootWindow(_display), 0, 0, out var x, out var y, out _) == 0)
            throw new InvalidOperationException("The live fixture window geometry could not be queried.");
        var screen = DefaultScreen(_display);
        var screenWidth = DisplayWidth(_display, screen);
        var screenHeight = DisplayHeight(_display, screen);
        state = $"map={attributes.MapState};client={attributes.Width}x{attributes.Height};root={x},{y};screen={screenWidth}x{screenHeight}";
        return attributes.MapState == 2 && attributes.Width >= width && attributes.Height >= height
            && x >= 0 && y >= 0 && x + width <= screenWidth && y + height <= screenHeight;
    }
    // Public XImage header fields, Linux LP64. Pixels are queried through Xlib.
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    private struct ImageHeader
    {
        [FieldOffset(0)] internal int Width;
        [FieldOffset(4)] internal int Height;
        [FieldOffset(56)] internal nuint Red;
        [FieldOffset(64)] internal nuint Green;
        [FieldOffset(72)] internal nuint Blue;
    }

    internal int Capture(string file, int width, int height, Action<byte[]>? observe = null)
    {
        if (_observedWindowDestroyed)
            throw new InvalidOperationException("The owned X11 fixture window retired before capture.");
        var image = GetImage(_display, _window, 0, 0, (uint)width, (uint)height, nuint.MaxValue, 2);
        if (image == 0)
            throw new InvalidOperationException("The owned, mapped X11 window could not be captured.");
        try
        {
            var header = *(ImageHeader*)image;
            if (header.Width != width || header.Height != height || header.Red == 0 || header.Green == 0 || header.Blue == 0)
                throw new NotSupportedException("Window capture requires the requested TrueColor XImage.");
            static byte Channel(nuint pixel, nuint mask)
            {
                var shift = BitOperations.TrailingZeroCount((ulong)mask);
                var maximum = (ulong)mask >> shift;
                return checked((byte)(((((ulong)pixel & (ulong)mask) >> shift) * 255 + maximum / 2) / maximum));
            }
            var pixels = new byte[checked(width * height * 3)];
            var colors = new HashSet<nuint>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = GetPixel(image, x, y);
                    colors.Add(value);
                    var index = (y * width + x) * 3;
                    pixels[index] = Channel(value, header.Red);
                    pixels[index + 1] = Channel(value, header.Green);
                    pixels[index + 2] = Channel(value, header.Blue);
                }
            }
            using var stream = File.Create(file);
            observe?.Invoke(pixels);
            stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
            stream.Write(pixels);
            return colors.Count;
        }
        finally { DestroyImage(image); }
    }

    [LibraryImport("libX11.so.6", EntryPoint = "XGetImage")] private static partial nint GetImage(nint display, nuint drawable, int x, int y, uint width, uint height, nuint planes, int format);
    [LibraryImport("libX11.so.6", EntryPoint = "XGetInputFocus")] private static partial int GetNativeFocus(nint display, out nuint window, out int revertTo);
    [LibraryImport("libX11.so.6", EntryPoint = "XSelectInput")] private static partial int ObserveInput(nint display, nuint window, nint mask);
    [LibraryImport("libX11.so.6", EntryPoint = "XPending")] private static partial int EventsPending(nint display);
    [LibraryImport("libX11.so.6", EntryPoint = "XNextEvent")] private static partial int NextObserved(nint display, out Event value);
    [LibraryImport("libX11.so.6", EntryPoint = "XGetPixel")] private static partial nuint GetPixel(nint image, int x, int y);
    [LibraryImport("libX11.so.6", EntryPoint = "XDestroyImage")] private static partial int DestroyImage(nint image);
    [LibraryImport("libX11.so.6", EntryPoint = "XGetWindowAttributes")] private static partial int GetWindowAttributes(nint display, nuint window, out WindowAttributes attributes);
    [LibraryImport("libX11.so.6", EntryPoint = "XTranslateCoordinates")] private static partial int TranslateCoordinates(nint display, nuint from, nuint to, int x, int y, out int rootX, out int rootY, out nuint child);
    [LibraryImport("libX11.so.6", EntryPoint = "XDefaultScreen")] private static partial int DefaultScreen(nint display);
    [LibraryImport("libX11.so.6", EntryPoint = "XDisplayWidth")] private static partial int DisplayWidth(nint display, int screen);
    [LibraryImport("libX11.so.6", EntryPoint = "XDisplayHeight")] private static partial int DisplayHeight(nint display, int screen);
}
