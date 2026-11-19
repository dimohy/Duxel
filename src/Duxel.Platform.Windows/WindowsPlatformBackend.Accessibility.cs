using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformBackend : IPlatformAccessibilityBackend
{
    private readonly WindowsAccessibility? _accessibilityHost;
    internal (int Cached,int Retired,int Disconnected,int AlreadyUnavailable,int Capacity) AccessibilityStorage=>
        _accessibilityHost?.Storage??default;
    private const uint WmAccessibilityCleanup=WmApp+71;
    private const uint WmAccessibilityFocus = WmApp + 72;
    private int _accessibilityFocusPosted;
    private void RequestAccessibilityFocusSync()
    {
        if (_disposed || _accessibilityHost is not { Serving : true }
            || Interlocked.Exchange(ref _accessibilityFocusPosted, 1) != 0) return;
        if (!PostMessageW(_windowHandle, WmAccessibilityFocus, 0, 0))
            throw new InvalidOperationException($"Posting UIA focus synchronization failed : {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
    }
    private void RequestAccessibilityCleanup()
    {
        if(!PostMessageW(_windowHandle,WmAccessibilityCleanup,0,0))
            throw new InvalidOperationException($"Posting UIA cleanup failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
    }

    public UiAccessibilityTree AccessibilityTree =>
        _accessibilityHost?.Tree ?? throw new InvalidOperationException("The native window has not been created.");
}
