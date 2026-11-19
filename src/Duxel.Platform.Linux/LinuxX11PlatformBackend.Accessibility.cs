using Duxel.Core;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxX11PlatformBackend : IPlatformAccessibilityBackend
{
    private readonly UiAccessibilityTree _accessibilityTree = new();
    private readonly LinuxAtSpiBridge? _atSpi;
    private AtSpiWindowGeometry _accessibilityGeometry = new(0, 0, 0, 0, false, false);
    private bool _accessibilityMapped, _accessibilityFocused;
    public UiAccessibilityTree AccessibilityTree => _accessibilityTree;
    public string? AccessibilityBusName => _atSpi?.BusName;

    private void UpdateAccessibilityGeometry()
    {
        var previous = Volatile.Read(ref _accessibilityGeometry);
        int x = previous.X, y = previous.Y;
        if (_window != 0 && X11Native.XTranslateCoordinates(_display, _window, X11Native.XDefaultRootWindow(_display), 0, 0, out x, out y, out _) == 0)
            throw new InvalidOperationException("X11 accessibility geometry could not be translated to the root window.");
        Volatile.Write(ref _accessibilityGeometry, new(x, y, _size.Width, _size.Height, _accessibilityMapped && !_hidden, _accessibilityFocused, ContentScale));
        _atSpi?.RequestRefresh();
    }
}
