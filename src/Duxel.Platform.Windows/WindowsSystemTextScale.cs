using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Duxel.Platform.Windows;

/// <summary>Owns UISettings2 and its event subscription on the platform owner thread.</summary>
internal sealed unsafe partial class WindowsSystemTextScale : IDisposable
{
    private readonly object _sync = new();
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly ScaleChangedHandler _handler;
    private Action? _wake;
    private nint _settings;
    private long _token;
    private bool _initialized, _subscribed;
    private int _pending;
    private float _scale;

    internal WindowsSystemTextScale(Action wake)
    {
        _handler = new(this);
        _wake = wake;
        var apartmentResult = CoGetApartmentType(out var apartment, out _);
        if (apartmentResult < 0 && apartmentResult != unchecked((int)0x800401F0))
            Marshal.ThrowExceptionForHR(apartmentResult);
        Marshal.ThrowExceptionForHR(RoInitialize(apartmentResult >= 0 && apartment is 0 or 3 ? 0u : 1u));
        _initialized = true;
        nint name = 0, instance = 0;
        try
        {
            const string className = "Windows.UI.ViewManagement.UISettings";
            fixed (char* chars = className)
                Marshal.ThrowExceptionForHR(WindowsCreateString(chars, className.Length, out name));
            Marshal.ThrowExceptionForHR(RoActivateInstance(name, out instance));
            var iid = new Guid("bad82401-2721-44f9-bb91-2bb228be442f");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(instance, in iid, out _settings));
            _scale = ReadScale();
            var callback = ComInterfaceMarshaller<IWindowsTextScaleChanged>.ConvertToUnmanaged(_handler);
            try
            {
                fixed (long* token = &_token)
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, void*, long*, int>)Table[7])(_settings, callback, token));
                _subscribed = true;
            }
            finally { ComInterfaceMarshaller<IWindowsTextScaleChanged>.Free(callback); }
        }
        catch { Dispose(); throw; }
        finally
        {
            if (instance != 0)
                Marshal.Release(instance);
            if (name != 0)
                WindowsDeleteString(name);
        }
    }

    private void** Table => *(void***)_settings;
    internal float Scale => Volatile.Read(ref _scale);
    private float ReadScale()
    {
        double value;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, double*, int>)Table[6])(_settings, &value));
        if (!double.IsFinite(value) || value <= 0 || value > float.MaxValue)
            throw new InvalidOperationException("UISettings returned an invalid text scale.");
        return (float)value;
    }
    internal bool Refresh()
    {
        if (Environment.CurrentManagedThreadId != _owner)
            throw new InvalidOperationException("UISettings must be read on the platform owner thread.");
        if (Interlocked.Exchange(ref _pending, 0) == 0)
            return false;
        var value = ReadScale();
        var changed = value != Scale;
        Volatile.Write(ref _scale, value);
        return changed;
    }
    private void Changed()
    {
        lock (_sync)
        {
            if (_wake is null)
                return;
            Volatile.Write(ref _pending, 1);
            _wake();
        }
    }
    public void Dispose()
    {
        lock (_sync)
            _wake = null;
        try
        {
            if (_subscribed)
            {
                _subscribed = false;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, long, int>)Table[8])(_settings, _token));
            }
        }
        finally
        {
            if (_settings != 0)
            {
                Marshal.Release(_settings);
                _settings = 0;
            }
            if (_initialized)
            {
                RoUninitialize();
                _initialized = false;
            }
        }
    }
    [GeneratedComClass]
    private sealed partial class ScaleChangedHandler(WindowsSystemTextScale owner) : IWindowsTextScaleChanged
    {
        public void Invoke(nint sender, nint args) => owner.Changed();
    }
    [LibraryImport("combase.dll")] private static partial int RoInitialize(uint type);
    [LibraryImport("combase.dll")] private static partial void RoUninitialize();
    [LibraryImport("combase.dll")] private static partial int WindowsCreateString(char* text, int length, out nint value);
    [LibraryImport("combase.dll")] private static partial int WindowsDeleteString(nint value);
    [LibraryImport("combase.dll")] private static partial int RoActivateInstance(nint name, out nint instance);
    [LibraryImport("ole32.dll")] private static partial int CoGetApartmentType(out int type, out int qualifier);
}

// Microsoft win32metadata windows.ui.viewmanagement.h: UISettings/Object typed event delegate.
[GeneratedComInterface, Guid("2dbdba9d-20da-519d-9078-09f835bc5bc7")]
internal partial interface IWindowsTextScaleChanged
{
    void Invoke(nint sender, nint args);
}
