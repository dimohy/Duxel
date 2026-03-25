using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Duxel.Core;

namespace Duxel.Platform.Windows;

public readonly record struct WindowsPlatformBackendOptions(
    int Width,
    int Height,
    int MinWidth,
    int MinHeight,
    string Title,
    bool VSync,
    bool Resizable,
    bool ShowMinimizeButton,
    bool ShowMaximizeButton,
    bool CenterOnScreen,
    bool CenterOnOwner,
    nint OwnerWindowHandle,
    string? IconPath,
    ReadOnlyMemory<byte> IconData,
    Action<nint>? WindowCreated,
    DuxelTrayOptions TrayOptions,
    IKeyRepeatSettingsProvider KeyRepeatSettingsProvider,
    Action? FrameInvalidated
);

[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatformBackend : IPlatformBackend, IWin32PlatformBackend
{
    private const uint VkStructureTypeWin32SurfaceCreateInfoKhr = 1000009000;
    private const int VkSuccess = 0;

    private const uint WsOverlapped = 0x00000000;
    private const uint WsCaption = 0x00C00000;
    private const uint WsSysMenu = 0x00080000;
    private const uint WsThickFrame = 0x00040000;
    private const uint WsMinimizeBox = 0x00020000;
    private const uint WsMaximizeBox = 0x00010000;
    private const uint WsVisible = 0x10000000;
    private const uint WsOverlappedWindow = WsOverlapped | WsCaption | WsSysMenu | WsThickFrame | WsMinimizeBox | WsMaximizeBox;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;
    private const uint LrDefaultSize = 0x0040;

    private const int CwUseDefault = unchecked((int)0x80000000);

    private const uint SwShow = 5;
    private const uint PmRemove = 0x0001;
    private const uint QsAllInput = 0x04FF;
    private const uint MwmoInputAvailable = 0x0004;
    private const uint WaitTimeout = 0x00000102;

    private const int GwlpUserData = -21;

    private const uint WmNccreate = 0x0081;
    private const uint WmDestroy = 0x0002;
    private const uint WmClose = 0x0010;
    private const uint WmQuit = 0x0012;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmCommand = 0x0111;
    private const uint WmSetCursor = 0x0020;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmChar = 0x0102;
    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmMouseHWheel = 0x020E;
    private const uint WmSize = 0x0005;
    private const uint WmWindowPosChanging = 0x0046;
    private const uint WmWindowPosChanged = 0x0047;
    private const uint WmSizing = 0x0214;
    private const uint WmEnterSizeMove = 0x0231;
    private const uint WmExitSizeMove = 0x0232;
    private const uint WmEraseBkgnd = 0x0014;
    private const uint WmPaint = 0x000F;
    private const uint WmDpiChanged = 0x02E0;
    private const uint WmApp = 0x8000;
    private const uint WmTrayCallback = WmApp + 1;

    private const int HtClient = 1;
    private const nuint SizeRestored = 0;
    private const nuint SizeMinimized = 1;

    private const int VkTab = 0x09;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;
    private const int VkPrior = 0x21;
    private const int VkNext = 0x22;
    private const int VkHome = 0x24;
    private const int VkEnd = 0x23;
    private const int VkInsert = 0x2D;
    private const int VkDelete = 0x2E;
    private const int VkBack = 0x08;
    private const int VkSpace = 0x20;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int VkF1 = 0x70;
    private const int VkF12 = 0x7B;
    private const int VkA = 0x41;
    private const int VkZ = 0x5A;
    private const int VkControl = 0x11;
    private const int VkShift = 0x10;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private const int IdcArrow = 32512;
    private const int IdcIBeam = 32513;
    private const int IdcSizeAll = 32646;
    private const int IdcSizeNs = 32645;
    private const int IdcSizeWe = 32644;
    private const int IdcSizeNesw = 32643;
    private const int IdcSizeNwse = 32642;
    private const int IdcHand = 32649;
    private const int IdcNo = 32648;

    private static readonly IReadOnlyList<string> VulkanExtensions = ["VK_KHR_surface", "VK_KHR_win32_surface"];

    private readonly nint _instanceHandle;
    private readonly nint _windowHandle;
    private readonly nint _classNamePtr;
    private readonly GCHandle _selfHandle;
    private readonly WndProcDelegate _wndProc;
    private readonly Stopwatch _stopwatch;
    private readonly WindowsInputBackend _inputBackend;
    private readonly WindowsVulkanSurfaceSource _vulkanSurface;
    private readonly Dictionary<UiMouseCursor, nint> _cursorHandles;
    private readonly Action? _frameInvalidated;
    private int _minTrackWidth;
    private int _minTrackHeight;
    private readonly int _logicalMinWidth;
    private readonly int _logicalMinHeight;
    private readonly uint _windowStyle;
    private readonly bool _centerOnScreen;
    private readonly bool _centerOnOwner;
    private readonly nint _ownerWindowHandle;
    private readonly nint _largeIconHandle;
    private readonly nint _smallIconHandle;
    private readonly WindowsTrayIconHost? _trayIcon;

    private float _dpiScale;

    private bool _shouldClose;
    private bool _disposed;
    private int _sizeMoveActive;
    private UiMouseCursor _currentCursor = UiMouseCursor.Arrow;

    static WindowsPlatformBackend()
    {
        _ = SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
    }

    public WindowsPlatformBackend(WindowsPlatformBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.KeyRepeatSettingsProvider);
        UiFontAtlasBuilder.ConfigurePlatformGlyphRasterizerFactory(static () => WindowsDirectWriteGlyphRasterizer.Instance);
        LogGlyphRasterizerWiring("GlyphRasterizerPolicy=DWrite(default)");

        _inputBackend = new WindowsInputBackend(options.KeyRepeatSettingsProvider.GetSettings());
        _cursorHandles = CreateCursorMap();
        _frameInvalidated = options.FrameInvalidated;

        if (options.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Window width must be positive.");
        }

        if (options.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Window height must be positive.");
        }

        if (options.MinWidth < 0 || options.MinHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Window minimum size must be zero or greater.");
        }

        _windowStyle = CreateWindowStyle(options.Resizable, options.ShowMinimizeButton, options.ShowMaximizeButton);
        _centerOnScreen = options.CenterOnScreen;
        _centerOnOwner = options.CenterOnOwner;
        _ownerWindowHandle = options.OwnerWindowHandle;
        (_largeIconHandle, _smallIconHandle) = LoadWindowIcons(options.IconPath, options.IconData);

        if (options.MinWidth > 0 || options.MinHeight > 0)
        {
            _logicalMinWidth = options.MinWidth;
            _logicalMinHeight = options.MinHeight;
            RecalculateMinTrackSize();
        }

        _instanceHandle = GetModuleHandleW(nint.Zero);
        if (_instanceHandle == nint.Zero)
        {
            throw new InvalidOperationException($"GetModuleHandleW failed: {Marshal.GetLastPInvokeError()}");
        }

        _wndProc = WindowProc;
        _classNamePtr = Marshal.StringToHGlobalUni($"Duxel.WindowClass.{Guid.NewGuid():N}");
        _selfHandle = GCHandle.Alloc(this);

        unsafe
        {
            var windowClass = new WndClassExW
            {
                cbSize = (uint)sizeof(WndClassExW),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = _instanceHandle,
                hIcon = _largeIconHandle,
                hCursor = nint.Zero,
                hbrBackground = nint.Zero,
                lpszMenuName = nint.Zero,
                lpszClassName = _classNamePtr,
                hIconSm = _smallIconHandle,
            };

            var atom = RegisterClassExW(ref windowClass);
            if (atom == 0)
            {
                var error = Marshal.GetLastPInvokeError();
                _selfHandle.Free();
                Marshal.FreeHGlobal(_classNamePtr);
                throw new InvalidOperationException($"RegisterClassExW failed: {error}");
            }
        }

        var systemDpi = GetDpiForSystem();
        _dpiScale = systemDpi > 0 ? systemDpi / 96f : 1f;

        var scaledWidth = (int)MathF.Round(options.Width * _dpiScale);
        var scaledHeight = (int)MathF.Round(options.Height * _dpiScale);

        var windowTitle = string.IsNullOrWhiteSpace(options.Title) ? "Duxel" : options.Title;

        var createRect = UnsafeRectForCreate(scaledWidth, scaledHeight);
        if (!AdjustWindowRectEx(ref createRect, _windowStyle, false, 0))
        {
            var error = Marshal.GetLastPInvokeError();
            CleanupRegistrationOnly();
            throw new InvalidOperationException($"AdjustWindowRectEx failed: {error}");
        }

        var createWidth = createRect.right - createRect.left;
        var createHeight = createRect.bottom - createRect.top;
        _windowHandle = CreateWindowExW(
            0,
            _classNamePtr,
            windowTitle,
            _windowStyle | WsVisible,
            CwUseDefault,
            CwUseDefault,
            createWidth,
            createHeight,
            _ownerWindowHandle,
            nint.Zero,
            _instanceHandle,
            GCHandle.ToIntPtr(_selfHandle)
        );

        if (_windowHandle == nint.Zero)
        {
            var error = Marshal.GetLastPInvokeError();
            CleanupRegistrationOnly();
            throw new InvalidOperationException($"CreateWindowExW failed: {error}");
        }

        _inputBackend.SetWindowHandle(_windowHandle);
        _ = SetWindowTextW(_windowHandle, windowTitle);
        if (_centerOnOwner && _ownerWindowHandle != nint.Zero)
        {
            CenterWindowOnOwner(_ownerWindowHandle, createWidth, createHeight);
        }
        else if (_centerOnScreen)
        {
            CenterWindowOnPrimaryMonitor(createWidth, createHeight);
        }
        ShowWindow(_windowHandle, SwShow);
        _ = UpdateWindow(_windowHandle);
        options.WindowCreated?.Invoke(_windowHandle);

        if (options.TrayOptions.Enabled)
        {
            var trayIconPath = string.IsNullOrWhiteSpace(options.TrayOptions.IconPath)
                ? options.IconPath
                : options.TrayOptions.IconPath;
            var trayIconData = options.TrayOptions.IconData.Length is 0
                ? options.IconData
                : options.TrayOptions.IconData;
            _trayIcon = new WindowsTrayIconHost(
                _windowHandle,
                options.TrayOptions,
                trayIconPath,
                trayIconData,
                WmTrayCallback,
                WmLButtonDblClk,
                RestoreWindowFromTray);
        }

        _vulkanSurface = new WindowsVulkanSurfaceSource(_instanceHandle, _windowHandle);
        _stopwatch = Stopwatch.StartNew();
    }

    private static void LogGlyphRasterizerWiring(string message)
    {
        var path = Environment.GetEnvironmentVariable("DUXEL_DWRITE_DIAG_LOG");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"{DateTime.UtcNow:O} [WindowsPlatformBackend] {message}";
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    public PlatformSize WindowSize
    {
        get
        {
            if (!GetClientRect(_windowHandle, out var rect))
            {
                throw new InvalidOperationException($"GetClientRect failed: {Marshal.GetLastPInvokeError()}");
            }

            return new PlatformSize(rect.right - rect.left, rect.bottom - rect.top);
        }
    }

    public PlatformSize FramebufferSize => WindowSize;

    public float ContentScale => _dpiScale;

    public bool IsInteractingResize => Volatile.Read(ref _sizeMoveActive) == 1;

    public bool ShouldClose => _shouldClose;

    public double TimeSeconds => _stopwatch.Elapsed.TotalSeconds;

    public IInputBackend Input => _inputBackend;

    public IVulkanSurfaceSource? VulkanSurface => _vulkanSurface;

    public IPlatformTextBackend? TextBackend => WindowsPlatformTextBackend.Instance;

    public nint WindowHandle => _windowHandle;

    public void PollEvents()
    {
        ThrowIfDisposed();
        _inputBackend.BeginFrame();
        PumpPendingMessages();
    }

    public void WaitEvents(int timeoutMilliseconds)
    {
        ThrowIfDisposed();
        _inputBackend.BeginFrame();

        if (timeoutMilliseconds <= 0)
        {
            _ = WaitMessage();
            PumpPendingMessages();
            return;
        }

        var waitResult = MsgWaitForMultipleObjectsEx(0, nint.Zero, (uint)timeoutMilliseconds, QsAllInput, MwmoInputAvailable);
        if (waitResult != WaitTimeout)
        {
            PumpPendingMessages();
        }
    }

    public void SetMouseCursor(UiMouseCursor cursor)
    {
        ThrowIfDisposed();

        if (_currentCursor == cursor)
        {
            return;
        }

        _currentCursor = cursor;
        if (_cursorHandles.TryGetValue(cursor, out var handle) && handle != nint.Zero)
        {
            _ = SetCursor(handle);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _trayIcon?.Dispose();

        if (_windowHandle != nint.Zero)
        {
            _ = DestroyWindow(_windowHandle);
        }

        CleanupRegistrationOnly();
    }

    private static Rect UnsafeRectForCreate(int width, int height) => new()
    {
        left = 0,
        top = 0,
        right = width,
        bottom = height
    };

    private void RecalculateMinTrackSize()
    {
        if (_logicalMinWidth <= 0 && _logicalMinHeight <= 0)
        {
            return;
        }

        var scaledMinWidth = (int)MathF.Round(Math.Max(1, _logicalMinWidth) * _dpiScale);
        var scaledMinHeight = (int)MathF.Round(Math.Max(1, _logicalMinHeight) * _dpiScale);
        var minRect = UnsafeRectForCreate(scaledMinWidth, scaledMinHeight);
        if (!AdjustWindowRectEx(ref minRect, _windowStyle, false, 0))
        {
            return;
        }

        _minTrackWidth = _logicalMinWidth > 0 ? minRect.right - minRect.left : 0;
        _minTrackHeight = _logicalMinHeight > 0 ? minRect.bottom - minRect.top : 0;
    }

    private void CleanupRegistrationOnly()
    {
        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        if (_classNamePtr != nint.Zero)
        {
            _ = UnregisterClassW(_classNamePtr, _instanceHandle);
            Marshal.FreeHGlobal(_classNamePtr);
        }

        if (_smallIconHandle != nint.Zero)
        {
            _ = DestroyIcon(_smallIconHandle);
        }

        if (_largeIconHandle != nint.Zero)
        {
            _ = DestroyIcon(_largeIconHandle);
        }
    }

    private static uint CreateWindowStyle(bool resizable, bool showMinimizeButton, bool showMaximizeButton)
    {
        var style = WsOverlapped | WsCaption | WsSysMenu;

        if (resizable)
        {
            style |= WsThickFrame;
        }

        if (showMinimizeButton)
        {
            style |= WsMinimizeBox;
        }

        if (showMaximizeButton)
        {
            style |= WsMaximizeBox;
        }

        return style;
    }

    private static (nint LargeIconHandle, nint SmallIconHandle) LoadWindowIcons(string? iconPath, ReadOnlyMemory<byte> iconData)
    {
        if (iconData.Length > 0)
        {
            var largeCx = GetSystemMetrics(11);
            var largeCy = GetSystemMetrics(12);
            var smallCx = GetSystemMetrics(49);
            var smallCy = GetSystemMetrics(50);

            var largeHandle = LoadIconFromIcoData(iconData.Span, largeCx, largeCy);
            var smallHandle = LoadIconFromIcoData(iconData.Span, smallCx, smallCy);
            return (largeHandle, smallHandle != nint.Zero ? smallHandle : largeHandle);
        }

        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return (nint.Zero, nint.Zero);
        }

        var largeHandleFile = LoadImageW(nint.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
        var smallHandleFile = LoadImageW(nint.Zero, iconPath, ImageIcon, GetSystemMetrics(49), GetSystemMetrics(50), LrLoadFromFile);

        return (largeHandleFile, smallHandleFile != nint.Zero ? smallHandleFile : largeHandleFile);
    }

    internal static nint LoadIconFromIcoData(ReadOnlySpan<byte> icoData, int desiredWidth, int desiredHeight)
    {
        // ICO header: [2 reserved][2 type=1][2 count]
        if (icoData.Length < 6)
        {
            return nint.Zero;
        }

        var type = BinaryPrimitives.ReadUInt16LittleEndian(icoData[2..]);
        if (type is not 1)
        {
            return nint.Zero;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(icoData[4..]);
        if (count is 0 || icoData.Length < 6 + count * 16)
        {
            return nint.Zero;
        }

        // Find best matching entry (closest size)
        var bestIndex = 0;
        var bestDiff = int.MaxValue;
        for (var i = 0; i < count; i++)
        {
            var entryOffset = 6 + i * 16;
            var w = icoData[entryOffset] is 0 ? 256 : icoData[entryOffset];
            var h = icoData[entryOffset + 1] is 0 ? 256 : icoData[entryOffset + 1];
            var diff = Math.Abs(w - desiredWidth) + Math.Abs(h - desiredHeight);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIndex = i;
            }
        }

        var bestEntryOffset = 6 + bestIndex * 16;
        var imageSize = BinaryPrimitives.ReadInt32LittleEndian(icoData[(bestEntryOffset + 8)..]);
        var imageFileOffset = BinaryPrimitives.ReadInt32LittleEndian(icoData[(bestEntryOffset + 12)..]);

        if (imageFileOffset + imageSize > icoData.Length || imageSize <= 0)
        {
            return nint.Zero;
        }

        var imageSlice = icoData.Slice(imageFileOffset, imageSize);
        unsafe
        {
            fixed (byte* ptr = imageSlice)
            {
                return CreateIconFromResourceEx((nint)ptr, (uint)imageSize, true, 0x00030000, desiredWidth, desiredHeight, 0);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void PumpPendingMessages()
    {
        while (PeekMessageW(out var message, nint.Zero, 0, 0, PmRemove))
        {
            if (message.message == WmQuit)
            {
                _shouldClose = true;
                break;
            }

            _ = TranslateMessage(ref message);
            _ = DispatchMessageW(ref message);
        }
    }

    private void CenterWindowOnPrimaryMonitor(int windowWidth, int windowHeight)
    {
        var screenWidth = GetSystemMetrics(0);
        var screenHeight = GetSystemMetrics(1);
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return;
        }

        var x = Math.Max(0, (screenWidth - windowWidth) / 2);
        var y = Math.Max(0, (screenHeight - windowHeight) / 2);
        _ = SetWindowPos(_windowHandle, nint.Zero, x, y, 0, 0, 0x0001 | 0x0004);
    }

    private void CenterWindowOnOwner(nint ownerWindowHandle, int windowWidth, int windowHeight)
    {
        if (!GetWindowRect(ownerWindowHandle, out var ownerRect))
        {
            CenterWindowOnPrimaryMonitor(windowWidth, windowHeight);
            return;
        }

        var ownerWidth = ownerRect.right - ownerRect.left;
        var ownerHeight = ownerRect.bottom - ownerRect.top;
        if (ownerWidth <= 0 || ownerHeight <= 0)
        {
            CenterWindowOnPrimaryMonitor(windowWidth, windowHeight);
            return;
        }

        var x = ownerRect.left + Math.Max(0, (ownerWidth - windowWidth) / 2);
        var y = ownerRect.top + Math.Max(0, (ownerHeight - windowHeight) / 2);
        _ = SetWindowPos(_windowHandle, nint.Zero, x, y, 0, 0, 0x0001 | 0x0004);
    }

    private void RestoreWindowFromTray()
    {
        _ = ShowWindow(_windowHandle, 9);
        _ = SetActiveWindow(_windowHandle);
        _ = SetForegroundWindow(_windowHandle);
    }

    private void HideWindowToTray()
    {
        _ = ShowWindow(_windowHandle, 0);
    }

    private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        if (message == WmNccreate)
        {
            unsafe
            {
                var createStruct = (CreateStructW*)lParam;
                _ = SetWindowLongPtrW(hwnd, GwlpUserData, createStruct->lpCreateParams);
            }
            return 1;
        }

        _inputBackend.HandleMessage(message, wParam, lParam);

        if (_trayIcon?.HandleWindowMessage(message, wParam, lParam) == true)
        {
            return 0;
        }

        switch (message)
        {
            case WmEraseBkgnd:
                return 1;
            case WmPaint:
                _ = BeginPaint(hwnd, out var ps);
                _ = EndPaint(hwnd, ref ps);
                return 0;
            case WmEnterSizeMove:
                Volatile.Write(ref _sizeMoveActive, 1);
                _frameInvalidated?.Invoke();
                break;
            case WmExitSizeMove:
                Volatile.Write(ref _sizeMoveActive, 0);
                _frameInvalidated?.Invoke();
                break;
            case WmSize:
                if (_trayIcon is not null)
                {
                    if (wParam == SizeMinimized && _trayIcon.HideWindowOnMinimize)
                    {
                        HideWindowToTray();
                        return 0;
                    }

                    if (wParam == SizeRestored)
                    {
                        _trayIcon.NotifyWindowRestored();
                    }
                }

                _frameInvalidated?.Invoke();
                break;

            case WmWindowPosChanging:
            case WmWindowPosChanged:
                if (Volatile.Read(ref _sizeMoveActive) == 0)
                {
                    _frameInvalidated?.Invoke();
                }
                break;
            case WmSizing:
                _frameInvalidated?.Invoke();
                break;
            case WmCommand:
                if (_trayIcon?.HandleCommand(wParam) == true)
                {
                    return 0;
                }

                break;
            case WmSetCursor:
                if (((uint)(ulong)lParam & 0xFFFFu) == HtClient)
                {
                    if (_cursorHandles.TryGetValue(_currentCursor, out var cursorHandle) && cursorHandle != nint.Zero)
                    {
                        _ = SetCursor(cursorHandle);
                        return 1;
                    }
                }
                break;
            case WmGetMinMaxInfo:
                if (_minTrackWidth > 0 || _minTrackHeight > 0)
                {
                    unsafe
                    {
                        var mmi = (MinMaxInfo*)lParam;
                        if (_minTrackWidth > 0)
                        {
                            mmi->ptMinTrackSize.x = Math.Max(mmi->ptMinTrackSize.x, _minTrackWidth);
                        }

                        if (_minTrackHeight > 0)
                        {
                            mmi->ptMinTrackSize.y = Math.Max(mmi->ptMinTrackSize.y, _minTrackHeight);
                        }
                    }

                    return 0;
                }

                break;
            case WmDpiChanged:
            {
                var newDpi = (int)(wParam & 0xFFFF);
                _dpiScale = newDpi > 0 ? newDpi / 96f : 1f;
                RecalculateMinTrackSize();
                unsafe
                {
                    var suggestedRect = (Rect*)lParam;
                    _ = SetWindowPos(
                        hwnd,
                        nint.Zero,
                        suggestedRect->left,
                        suggestedRect->top,
                        suggestedRect->right - suggestedRect->left,
                        suggestedRect->bottom - suggestedRect->top,
                        0x0004 | 0x0010 | 0x0200); // SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS
                }
                _frameInvalidated?.Invoke();
                return 0;
            }
            case WmClose:
                if (_trayIcon?.TryHandleClose(HideWindowToTray) == true)
                {
                    return 0;
                }

                _shouldClose = true;
                // Raymond Chen: Re-enable owner BEFORE DestroyWindow.
                // Otherwise Windows activates an unrelated window (flicker).
                // https://devblogs.microsoft.com/oldnewthing/20040227-00/?p=40463
                if (_ownerWindowHandle != nint.Zero)
                {
                    _ = EnableWindow(_ownerWindowHandle, true);
                }
                _ = DestroyWindow(hwnd);
                return 0;
            case WmDestroy:
                _trayIcon?.Dispose();
                Volatile.Write(ref _sizeMoveActive, 0);
                _shouldClose = true;
                PostQuitMessage(0);
                return 0;
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private static Dictionary<UiMouseCursor, nint> CreateCursorMap()
    {
        return new Dictionary<UiMouseCursor, nint>
        {
            [UiMouseCursor.Arrow] = LoadSystemCursor(IdcArrow),
            [UiMouseCursor.TextInput] = LoadSystemCursor(IdcIBeam),
            [UiMouseCursor.ResizeAll] = LoadSystemCursor(IdcSizeAll),
            [UiMouseCursor.ResizeNS] = LoadSystemCursor(IdcSizeNs),
            [UiMouseCursor.ResizeEW] = LoadSystemCursor(IdcSizeWe),
            [UiMouseCursor.ResizeNESW] = LoadSystemCursor(IdcSizeNesw),
            [UiMouseCursor.ResizeNWSE] = LoadSystemCursor(IdcSizeNwse),
            [UiMouseCursor.Hand] = LoadSystemCursor(IdcHand),
            [UiMouseCursor.NotAllowed] = LoadSystemCursor(IdcNo),
        };
    }

    private static nint LoadSystemCursor(int cursorId) => LoadCursorW(nint.Zero, (nint)cursorId);

    private sealed class WindowsInputBackend(UiKeyRepeatSettings keyRepeatSettings) : IInputBackend
    {
        private readonly List<UiKeyEvent> _keyEvents = [];
        private readonly List<UiCharEvent> _charEvents = [];
        private nint _hwnd;
        private UiVector2 _mousePosition;
        private bool _leftDown;
        private bool _rightDown;
        private bool _middleDown;
        private bool _leftPressedEvent;
        private bool _leftReleasedEvent;
        private bool _rightPressedEvent;
        private bool _rightReleasedEvent;
        private bool _middlePressedEvent;
        private bool _middleReleasedEvent;
        private float _wheel;
        private float _wheelHorizontal;

        public void SetWindowHandle(nint hwnd) => _hwnd = hwnd;

        public InputSnapshot Snapshot => new(
            _mousePosition,
            _leftDown,
            _rightDown,
            _middleDown,
            _leftPressedEvent,
            _leftReleasedEvent,
            _rightPressedEvent,
            _rightReleasedEvent,
            _middlePressedEvent,
            _middleReleasedEvent,
            _wheel,
            _wheelHorizontal,
            _keyEvents,
            _charEvents,
            keyRepeatSettings,
            GetModifiers()
        );

        public void BeginFrame()
        {
            _keyEvents.Clear();
            _charEvents.Clear();
            _leftPressedEvent = false;
            _leftReleasedEvent = false;
            _rightPressedEvent = false;
            _rightReleasedEvent = false;
            _middlePressedEvent = false;
            _middleReleasedEvent = false;
            _wheel = 0;
            _wheelHorizontal = 0;
        }

        public void HandleMessage(uint message, nuint wParam, nint lParam)
        {
            switch (message)
            {
                case WmMouseMove:
                    _mousePosition = new UiVector2(GetXFromLParam(lParam), GetYFromLParam(lParam));
                    break;
                case WmLButtonDown:
                    SetMouseButton(UiMouseButton.Left, true);
                    break;
                case WmLButtonUp:
                    SetMouseButton(UiMouseButton.Left, false);
                    break;
                case WmRButtonDown:
                    SetMouseButton(UiMouseButton.Right, true);
                    break;
                case WmRButtonUp:
                    SetMouseButton(UiMouseButton.Right, false);
                    break;
                case WmMButtonDown:
                    SetMouseButton(UiMouseButton.Middle, true);
                    break;
                case WmMButtonUp:
                    SetMouseButton(UiMouseButton.Middle, false);
                    break;
                case WmMouseWheel:
                {
                    var delta = GetWheelDeltaFromWParam(wParam) / 120f;
                    if (IsKeyDown(VkShift))
                    {
                        _wheelHorizontal += delta;
                    }
                    else
                    {
                        _wheel += delta;
                    }
                    break;
                }
                case WmMouseHWheel:
                    _wheelHorizontal += GetWheelDeltaFromWParam(wParam) / 120f;
                    break;
                case WmKeyDown:
                case WmSysKeyDown:
                {
                    var key = MapVirtualKey((int)wParam);
                    if (key != UiKey.None)
                    {
                        _keyEvents.Add(new UiKeyEvent(key, true, GetModifiers()));
                    }
                    break;
                }
                case WmKeyUp:
                case WmSysKeyUp:
                {
                    var key = MapVirtualKey((int)wParam);
                    if (key != UiKey.None)
                    {
                        _keyEvents.Add(new UiKeyEvent(key, false, GetModifiers()));
                    }
                    break;
                }
                case WmChar:
                    // Control characters (< 0x20) are handled via UiKeyEvent, not char events.
                    // Feeding them into charEvents triggers font atlas rebuild for non-renderable glyphs.
                    if (wParam >= 32)
                        _charEvents.Add(new UiCharEvent((uint)wParam));
                    break;
            }
        }

        private static float GetXFromLParam(nint lParam) => (short)((long)lParam & 0xFFFF);

        private static float GetYFromLParam(nint lParam) => (short)(((long)lParam >> 16) & 0xFFFF);

        private static int GetWheelDeltaFromWParam(nuint wParam) => (short)((ulong)wParam >> 16);

        private static KeyModifiers GetModifiers()
        {
            var modifiers = KeyModifiers.None;
            if (IsKeyDown(VkControl))
            {
                modifiers |= KeyModifiers.Ctrl;
            }

            if (IsKeyDown(VkShift))
            {
                modifiers |= KeyModifiers.Shift;
            }

            if (IsKeyDown(VkMenu))
            {
                modifiers |= KeyModifiers.Alt;
            }

            if (IsKeyDown(VkLWin) || IsKeyDown(VkRWin))
            {
                modifiers |= KeyModifiers.Super;
            }

            return modifiers;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            var state = GetKeyState(virtualKey);
            return (state & 0x8000) != 0;
        }

        private void SetMouseButton(UiMouseButton button, bool isDown)
        {
            var wasAnyDown = _leftDown || _rightDown || _middleDown;

            switch (button)
            {
                case UiMouseButton.Left:
                    if (_leftDown != isDown)
                    {
                        if (isDown)
                        {
                            _leftPressedEvent = true;
                        }
                        else
                        {
                            _leftReleasedEvent = true;
                        }
                    }

                    _leftDown = isDown;
                    break;
                case UiMouseButton.Right:
                    if (_rightDown != isDown)
                    {
                        if (isDown)
                        {
                            _rightPressedEvent = true;
                        }
                        else
                        {
                            _rightReleasedEvent = true;
                        }
                    }

                    _rightDown = isDown;
                    break;
                case UiMouseButton.Middle:
                    if (_middleDown != isDown)
                    {
                        if (isDown)
                        {
                            _middlePressedEvent = true;
                        }
                        else
                        {
                            _middleReleasedEvent = true;
                        }
                    }

                    _middleDown = isDown;
                    break;
            }

            var isAnyDown = _leftDown || _rightDown || _middleDown;
            if (!wasAnyDown && isAnyDown)
            {
                SetCapture(_hwnd);
            }
            else if (wasAnyDown && !isAnyDown)
            {
                ReleaseCapture();
            }
        }

        private static UiKey MapVirtualKey(int virtualKey)
        {
            return virtualKey switch
            {
                VkTab => UiKey.Tab,
                VkLeft => UiKey.LeftArrow,
                VkRight => UiKey.RightArrow,
                VkUp => UiKey.UpArrow,
                VkDown => UiKey.DownArrow,
                VkPrior => UiKey.PageUp,
                VkNext => UiKey.PageDown,
                VkHome => UiKey.Home,
                VkEnd => UiKey.End,
                VkInsert => UiKey.Insert,
                VkDelete => UiKey.Delete,
                VkBack => UiKey.Backspace,
                VkSpace => UiKey.Space,
                VkReturn => UiKey.Enter,
                VkEscape => UiKey.Escape,
                >= VkA and <= VkZ => (UiKey)(UiKey.A + (virtualKey - VkA)),
                >= VkF1 and <= VkF12 => (UiKey)(UiKey.F1 + (virtualKey - VkF1)),
                _ => UiKey.None,
            };
        }
    }

    private sealed class WindowsVulkanSurfaceSource(nint instanceHandle, nint windowHandle) : IVulkanSurfaceSource
    {
        public IReadOnlyList<string> RequiredInstanceExtensions => VulkanExtensions;

        public nuint CreateSurface(nuint instanceHandleValue)
        {
            unsafe
            {
                var createInfo = new VkWin32SurfaceCreateInfoKhr
                {
                    sType = VkStructureTypeWin32SurfaceCreateInfoKhr,
                    pNext = null,
                    flags = 0,
                    hinstance = instanceHandle,
                    hwnd = windowHandle,
                };

                var result = vkCreateWin32SurfaceKHR(instanceHandleValue, &createInfo, null, out var surface);
                if (result != VkSuccess)
                {
                    throw new InvalidOperationException($"vkCreateWin32SurfaceKHR failed: {result}");
                }

                return (nuint)surface;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public Point pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct CreateStructW
    {
        public nint lpCreateParams;
        public nint hInstance;
        public nint hMenu;
        public nint hwndParent;
        public int cy;
        public int cx;
        public int y;
        public int x;
        public int style;
        public nint lpszName;
        public nint lpszClass;
        public uint dwExStyle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WndClassExW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public nint lpszMenuName;
        public nint lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct VkWin32SurfaceCreateInfoKhr
    {
        public uint sType;
        public void* pNext;
        public uint flags;
        public nint hinstance;
        public nint hwnd;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WndProcDelegate(nint hwnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AdjustWindowRectEx(ref Rect lpRect, uint dwStyle, [MarshalAs(UnmanagedType.Bool)] bool bMenu, uint dwExStyle);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint CreateWindowExW(
        uint dwExStyle,
        nint lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [LibraryImport("user32.dll")]
    private static partial nint DefWindowProcW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint hIcon);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint DispatchMessageW(ref Msg lpMsg);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hWnd, out Rect lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetModuleHandleW(nint lpModuleName);

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(int nVirtKey);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint LoadCursorW(nint hInstance, nint lpCursorName);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadImageW(nint hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint CreateIconFromResourceEx(nint presbits, uint dwResSize, [MarshalAs(UnmanagedType.Bool)] bool fIcon, uint dwVer, int cxDesired, int cyDesired, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint MsgWaitForMultipleObjectsEx(uint nCount, nint pHandles, uint dwMilliseconds, uint dwWakeMask, uint dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessageW(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [LibraryImport("user32.dll")]
    private static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial ushort RegisterClassExW(ref WndClassExW lpwcx);

    [LibraryImport("user32.dll")]
    private static partial nint SetCursor(nint hCursor);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowTextW(nint hWnd, [MarshalAs(UnmanagedType.LPWStr)] string lpString);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    private static partial nint SetCapture(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, uint nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TranslateMessage(ref Msg lpMsg);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial ushort UnregisterClassW(nint lpClassName, nint hInstance);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial nint SetActiveWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WaitMessage();

    [LibraryImport("user32.dll")]
    private static partial nint BeginPaint(nint hWnd, out PaintStruct lpPaint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndPaint(nint hWnd, ref PaintStruct lpPaint);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();

    private static readonly nint DpiAwarenessContextPerMonitorAwareV2 = -4;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(nint value);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public nint hdc;
        public int fErase;
        public Rect rcPaint;
        public int fRestore;
        public int fIncUpdate;
        private unsafe fixed byte rgbReserved[32];
    }

    [LibraryImport("vulkan-1")]
    private static unsafe partial int vkCreateWin32SurfaceKHR(nuint instance, VkWin32SurfaceCreateInfoKhr* pCreateInfo, void* pAllocator, out ulong pSurface);
}