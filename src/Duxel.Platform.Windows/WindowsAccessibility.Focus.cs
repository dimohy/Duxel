using System.ComponentModel;
using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility
{
    private (string Id, long RuntimeId)? _reportedFocus;
    private bool _reportedWindowFocus;

    internal bool IsFocused(UiAccessibilityElement element) => HasFocus && Tree.Root.Focus == element.Node.Id;
    private void RaiseFocusChanged(UiAccessibilityFocusChange change) => SynchronizeFocus();

    internal void SynchronizeFocus()
    {
        if (!Serving) return;
        var windowFocus = HasFocus;
        UiAccessibilityElement? current = windowFocus && Tree.Root.Focus is { } id && Tree.TryGet(id, out var focused) ? focused : null;
        (string Id, long RuntimeId)? identity = current is { } value ? (value.Node.Id, value.RuntimeId) : null;
        if (_reportedWindowFocus == windowFocus && _reportedFocus == identity) return;
        var previous = _reportedFocus;
        var previousWindow = _reportedWindowFocus;
        _reportedFocus = identity;
        _reportedWindowFocus = windowFocus;
        if (Volatile.Read(ref _registered) == 0) return;

        if (previousWindow)
        {
            if (previous is { } old)
            {
                if (Tree.TryGet(old.Id, out var live) && live.RuntimeId == old.RuntimeId && Find(old.Id) is { } oldProvider)
                    RaiseKeyboardFocusProperty(oldProvider, true, false);
            }
            else RaiseKeyboardFocusProperty(Root, true, false);
        }
        if (!windowFocus) return;
        var provider = current is { } next ? Find(next.Node.Id)
            ?? throw new InvalidOperationException("The focus event target must already be published.") : (UiaElementProvider)Root;
        RaiseKeyboardFocusProperty(provider, false, true);
        // Win32 already reports a newly focused empty HWND. A virtual child-to-root
        // transition has no HWND change, so this provider must report that transition.
        if (current is not null || previousWindow && previous is not null)
        {
            var pointer = UiaNative.Pointer<IUiaSimple>(provider);
            try { Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationEvent(pointer, 20005)); }
            finally { Marshal.Release(pointer); }
        }
    }
    private static void RaiseKeyboardFocusProperty(UiaElementProvider provider, bool previous, bool current)
    {
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30008,
                UiaVariant.Boolean(previous), UiaVariant.Boolean(current)));
        }
        finally { Marshal.Release(pointer); }
    }
}

internal static partial class UiaNative
{
    internal static unsafe bool WindowHasKeyboardFocus(nint window)
    {
        if (window == 0 || GetForegroundWindow() != window) return false;
        var info = new NativeGuiThreadInfo { Size = (uint)sizeof(NativeGuiThreadInfo) };
        if (GetGUIThreadInfo(0, ref info) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Reading the foreground thread's keyboard focus failed.");
        return info.Focus == window;
    }
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int GetGUIThreadInfo(uint thread, ref NativeGuiThreadInfo info);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGuiThreadInfo
    {
        internal uint Size, Flags;
        internal nint Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        internal NativeRect CaretBounds;
    }
}
