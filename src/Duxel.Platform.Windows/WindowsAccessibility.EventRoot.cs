using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Core;

namespace Duxel.Platform.Windows;

// UIA owns the event graph until its asynchronous handlers release the source.
// Event snapshots expose no actions and never re-enter a destroyed HWND or a disposed tree.
[GeneratedComClass]
internal sealed partial class UiaEventRootProvider : UiaElementProvider, IUiaFragmentRoot
{
    private readonly UiaBounds _bounds;
    private readonly UiaEventHostReference _hostProvider;

    internal UiaEventRootProvider(WindowsAccessibility host) : base(host, null, 0)
    {
        _bounds = host.Bounds();
        Marshal.ThrowExceptionForHR(UiaNative.UiaHostProviderFromHwnd(host.Window, out var pointer));
        _hostProvider = new(pointer);
    }

    protected override bool Read(out UiAccessibilityElement element)
    {
        element = default;
        return true;
    }

    public override int GetHostProvider(out nint value)
    {
        var acquired = false;
        try
        {
            _hostProvider.DangerousAddRef(ref acquired);
            value = _hostProvider.DangerousGetHandle();
            Marshal.AddRef(value);
            return 0;
        }
        finally
        {
            if (acquired)
                _hostProvider.DangerousRelease();
        }
    }

    public override int Navigate(int direction, out nint value)
    {
        value = 0;
        return direction is >= 0 and <= 4 ? 0 : UiaNative.InvalidArgument;
    }

    public override int GetBounds(out UiaBounds value)
    {
        value = _bounds;
        return 0;
    }

    public override int SetFocus() => UiaNative.Unavailable;

    public override int GetFragmentRoot(out nint value)
    {
        value = UiaNative.Pointer<IUiaFragmentRoot>(this);
        return 0;
    }

    public int ElementFromPoint(double x, double y, out nint value)
    {
        value = 0;
        return 0;
    }

    public int GetFocus(out nint value)
    {
        value = 0;
        return 0;
    }
}

internal sealed class UiaEventHostReference : SafeHandle
{
    internal UiaEventHostReference(nint pointer) : base(0, ownsHandle : true) => SetHandle(pointer);
    public override bool IsInvalid => handle == 0;
    protected override bool ReleaseHandle()
    {
        Marshal.Release(handle);
        return true;
    }
}
