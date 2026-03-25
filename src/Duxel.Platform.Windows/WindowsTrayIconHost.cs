using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Duxel.Core;

namespace Duxel.Platform.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsTrayIconHost : IDisposable
{
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;
    private const uint LrDefaultSize = 0x0040;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint MfDisabled = 0x00000002;
    private const uint MfGrayed = 0x00000001;
    private const uint TpmLeftAlign = 0x0000;
    private const uint TpmBottomAlign = 0x0020;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmContextMenu = 0x007B;
    private const uint WmNull = 0x0000;
    private const int SmallIconMetricX = 49;
    private const int SmallIconMetricY = 50;

    private readonly nint _windowHandle;
    private readonly DuxelTrayOptions _options;
    private readonly Action _restoreWindow;
    private readonly Dictionary<ushort, Action?> _commands = [];
    private readonly nint _menuHandle;
    private readonly nint _iconHandle;
    private readonly uint _callbackMessage;
    private readonly uint _doubleClickMessage;
    private bool _disposed;

    public WindowsTrayIconHost(
        nint windowHandle,
        DuxelTrayOptions options,
        string? iconPath,
        ReadOnlyMemory<byte> iconData,
        uint callbackMessage,
        uint doubleClickMessage,
        Action restoreWindow)
    {
        _windowHandle = windowHandle;
        _options = options;
        _callbackMessage = callbackMessage;
        _doubleClickMessage = doubleClickMessage;
        _restoreWindow = restoreWindow;
        _iconHandle = LoadTrayIcon(iconPath, iconData);
        _menuHandle = CreatePopupMenu();

        BuildMenu();
        AddNotifyIcon();
    }

    public bool HideWindowOnMinimize => _options.HideWindowOnMinimize;

    public bool TryHandleClose(Action hideWindow)
    {
        if (!_options.HideWindowOnClose)
        {
            return false;
        }

        hideWindow();
        return true;
    }

    public void NotifyWindowRestored()
    {
    }

    public bool HandleCommand(nuint wParam)
    {
        var commandId = unchecked((ushort)((ulong)wParam & 0xFFFF));
        if (!_commands.TryGetValue(commandId, out var command))
        {
            return false;
        }

        command?.Invoke();
        return true;
    }

    public bool HandleWindowMessage(uint message, nuint wParam, nint lParam)
    {
        if (message != _callbackMessage)
        {
            return false;
        }

        var notification = unchecked((uint)((ulong)lParam & 0xFFFF));
        switch (notification)
        {
            case WmLButtonUp:
                if (_options.DoubleClick is not null)
                {
                    _options.DoubleClick();
                }
                else
                {
                    _restoreWindow();
                }

                return true;
            case WmRButtonUp:
            case WmContextMenu:
                ShowContextMenu();
                return true;
            default:
                if (notification == _doubleClickMessage)
                {
                    if (_options.DoubleClick is not null)
                    {
                        _options.DoubleClick();
                    }
                    else
                    {
                        _restoreWindow();
                    }
                    return true;
                }

                return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DeleteNotifyIcon();

        if (_menuHandle != nint.Zero)
        {
            _ = DestroyMenu(_menuHandle);
        }

        if (_iconHandle != nint.Zero)
        {
            _ = DestroyIcon(_iconHandle);
        }
    }

    private void BuildMenu()
    {
        ushort commandId = 0x2000;
        foreach (var item in _options.MenuItems)
        {
            if (item.IsSeparator)
            {
                _ = AppendMenuW(_menuHandle, MfSeparator, 0, string.Empty);
                continue;
            }

            var flags = MfString;
            if (!item.Enabled)
            {
                flags |= MfDisabled | MfGrayed;
            }

            _commands[commandId] = item.Invoked;
            _ = AppendMenuW(_menuHandle, flags, commandId, item.Text);
            commandId++;
        }
    }

    private void AddNotifyIcon()
    {
        var data = CreateNotifyIconData();
        _ = Shell_NotifyIconW(NimAdd, ref data);
    }

    private void DeleteNotifyIcon()
    {
        var data = CreateNotifyIconData();
        _ = Shell_NotifyIconW(NimDelete, ref data);
    }

    private NotifyIconData CreateNotifyIconData()
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = _callbackMessage,
            hIcon = _iconHandle,
            szTip = Truncate(_options.ToolTip ?? "Duxel", 127),
        };
    }

    private void ShowContextMenu()
    {
        _ = SetForegroundWindow(_windowHandle);

        if (!GetCursorPos(out var point))
        {
            return;
        }

        var command = TrackPopupMenuEx(_menuHandle, TpmLeftAlign | TpmBottomAlign | TpmRightButton | TpmReturnCmd, point.X, point.Y, _windowHandle, nint.Zero);
        if (command > 0 && _commands.TryGetValue(unchecked((ushort)command), out var action))
        {
            action?.Invoke();
        }

        _ = PostMessageW(_windowHandle, WmNull, 0, 0);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static nint LoadTrayIcon(string? iconPath, ReadOnlyMemory<byte> iconData)
    {
        if (iconData.Length > 0)
        {
            var sizeX = GetSystemMetrics(SmallIconMetricX);
            var sizeY = GetSystemMetrics(SmallIconMetricY);
            return WindowsPlatformBackend.LoadIconFromIcoData(iconData.Span, sizeX, sizeY);
        }

        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return nint.Zero;
        }

        var sx = GetSystemMetrics(SmallIconMetricX);
        var sy = GetSystemMetrics(SmallIconMetricY);
        return LoadImageW(nint.Zero, iconPath, ImageIcon, sx, sy, LrLoadFromFile | LrDefaultSize);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenuW(nint hMenu, uint uFlags, ushort uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint LoadImageW(nint hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(nint hMenu, uint uFlags, int x, int y, nint hwnd, nint lptpm);
}