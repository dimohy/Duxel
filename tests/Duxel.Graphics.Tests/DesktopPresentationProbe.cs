using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Duxel.Core;

internal sealed partial class DesktopPresentationProbe : IDisposable
{
    private static readonly UiRect Marker = new(390, 387, 8, 8);
    private static readonly UiColor First = new(253, 16, 197);
    private static readonly UiColor Second = new(18, 229, 141);
    private readonly nint _window;
    private readonly nint _desktop;
    private bool _alternate;

    internal DesktopPresentationProbe(nint window)
    {
        _window = window;
        _desktop = GetDC(0);
        if (_desktop == 0)
            throw new InvalidOperationException("Could not open the desktop device context.");
    }

    internal void Alternate() => _alternate = !_alternate;

    internal void Paint(UiImmediateContext ui) =>
        ui.GetWindowDrawList().AddRectFilled(Marker, _alternate ? Second : First, ui.WhiteTextureId);

    internal long WaitForMarker(UiVector2 scale)
    {
        var point = new Point
        {
            X = checked((int)MathF.Round((Marker.X + Marker.Width / 2) * scale.X)),
            Y = checked((int)MathF.Round((Marker.Y + Marker.Height / 2) * scale.Y))
        };
        if (ClientToScreen(_window, ref point) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        var color = _alternate ? Second : First;
        var expected = color.Rgba & 0x00ffffffu;
        var start = Stopwatch.GetTimestamp();
        uint actual;
        do
        {
            if (WindowFromPoint(point) != _window)
                throw new InvalidOperationException("The owned presentation marker is obscured.");
            actual = GetPixel(_desktop, point.X, point.Y);
            if (actual == uint.MaxValue)
                throw new InvalidOperationException("The desktop marker pixel could not be read.");
            if (actual == expected)
                return Stopwatch.GetTimestamp();
            Thread.Sleep(1);
        } while (Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(1));

        throw new TimeoutException(
            $"Desktop marker at ({point.X}, {point.Y}) was {actual:X6}; expected {expected:X6}.");
    }

    public void Dispose() => _ = ReleaseDC(0, _desktop);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint window, nint context);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int ClientToScreen(nint window, ref Point point);

    [LibraryImport("user32.dll")]
    private static partial nint WindowFromPoint(Point point);

    [LibraryImport("gdi32.dll")]
    private static partial uint GetPixel(nint context, int x, int y);
}
