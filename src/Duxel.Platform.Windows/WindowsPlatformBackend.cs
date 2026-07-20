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
    bool IntegrateSystemChrome,
    bool UseDuxelTitleBar,
    float DuxelTitleBarHeight,
    bool FollowSystemTheme,
    UiColor ChromeCaptionColor,
    UiColor ChromeTextColor,
    UiColor ChromeBorderColor,
    DuxelTrayOptions TrayOptions,
    IKeyRepeatSettingsProvider KeyRepeatSettingsProvider,
    Action? FrameInvalidated
)
{
    public DuxelTitleBarMode TitleBarMode { get; init; } = DuxelTitleBarMode.Default;
}

[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatformBackend : IPlatformBackend, IWin32PlatformBackend
    , IWindowChromeController
    , IWindowTitleBarPlatform
    , IPlatformThemeProvider
{
    private const string DefaultIconResourceName = "Duxel.Platform.Windows.assets.duxel.ico";
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
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const float DuxelChromeButtonLogicalWidth = 48f;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaAllowNcPaint = 4;
    private const int DwmwaCaptionButtonBounds = 5;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;
    private const uint LrDefaultSize = 0x0040;
    private const uint CsDblClks = 0x0008;

    private const int CwUseDefault = unchecked((int)0x80000000);

    private const uint SwShow = 5;
    private const uint PmRemove = 0x0001;
    private const uint QsAllInput = 0x04FF;
    private const uint MwmoInputAvailable = 0x0004;
    private const uint WaitTimeout = 0x00000102;
    private const uint MonitorDefaultToNearest = 2;

    private const int GwlpUserData = -21;
    private const uint ScMinimize = 0xF020;
    private const uint ScMaximize = 0xF030;
    private const uint ScRestore = 0xF120;
    private const uint ScMove = 0xF010;
    private const uint SwMinimize = 6;

    private const uint WmNccreate = 0x0081;
    private const uint WmDestroy = 0x0002;
    private const uint WmClose = 0x0010;
    private const uint WmQuit = 0x0012;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmNcCalcSize = 0x0083;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmNcMouseMove = 0x00A0;
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
    private const uint WmSettingChange = 0x001A;
    private const uint WmDpiChanged = 0x02E0;
    private const uint WmDwmCompositionChanged = 0x031E;
    private const uint WmNcMouseLeave = 0x02A2;
    private const uint WmApp = 0x8000;
    private const uint WmTrayCallback = WmApp + 1;

    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtMinButton = 8;
    private const int HtMaxButton = 9;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int HtClose = 20;
    private const nuint SizeRestored = 0;
    private const nuint SizeMinimized = 1;
    private const uint WmSysCommand = 0x0112;

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
    private readonly string _windowTitle;
    private readonly bool _resizable;
    private readonly bool _integrateSystemChrome;
    private readonly DuxelTitleBarMode _titleBarMode;
    private readonly bool _useDuxelTitleBar;
    private readonly float _duxelTitleBarHeight;
    private int _duxelTitleBarHeightPx;
    private readonly bool _followSystemTheme;
    private readonly UiColor _chromeCaptionColor;
    private readonly UiColor _chromeTextColor;
    private readonly UiColor _chromeBorderColor;
    private readonly bool _showMinimizeButton;
    private readonly bool _showMaximizeButton;
    private readonly bool _centerOnScreen;
    private readonly bool _centerOnOwner;
    private readonly nint _ownerWindowHandle;
    private readonly nint _largeIconHandle;
    private readonly nint _smallIconHandle;
    private readonly WindowsTrayIconHost? _trayIcon;
    private readonly WindowsImeHandler _imeHandler;
    private readonly Lock _titleBarDragRegionLock = new();
    private UiRect[] _titleBarDragRegions = [];
    private int _titleBarDragRegionCount;
    private int _extendedTitleBarFrameHeightPx;

    private float _dpiScale;

    private bool _shouldClose;
    private bool _disposed;
    private int _sizeMoveActive;
    private int _clientWidth;
    private int _clientHeight;
    private long _interactiveResizeSequence;
    private long _presentedResizeSequence;
    private int _interactiveResizeWaitCancelled;
    private readonly AutoResetEvent _interactiveResizeFramePresented = new(false);
    private UiSystemColorScheme _colorScheme;
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
        _colorScheme = WindowsSystemTheme.GetAppColorScheme();

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

        _windowTitle = string.IsNullOrWhiteSpace(options.Title) ? "Duxel" : options.Title;
        var systemDpi = GetDpiForSystem();
        _dpiScale = systemDpi > 0 ? systemDpi / 96f : 1f;
        _resizable = options.Resizable;
        _integrateSystemChrome = options.IntegrateSystemChrome;
        _titleBarMode = options.TitleBarMode is DuxelTitleBarMode.Default
            ? options.UseDuxelTitleBar ? DuxelTitleBarMode.Duxel : DuxelTitleBarMode.System
            : options.TitleBarMode;
        _useDuxelTitleBar = _titleBarMode is DuxelTitleBarMode.Duxel;
        _duxelTitleBarHeight = MathF.Max(32f, options.DuxelTitleBarHeight);
        _duxelTitleBarHeightPx = Math.Max(1, (int)MathF.Round(_duxelTitleBarHeight * _dpiScale));
        _followSystemTheme = options.FollowSystemTheme;
        _chromeCaptionColor = options.ChromeCaptionColor;
        _chromeTextColor = options.ChromeTextColor;
        _chromeBorderColor = options.ChromeBorderColor;
        _showMinimizeButton = options.ShowMinimizeButton;
        _showMaximizeButton = options.ShowMaximizeButton;
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
                style = CsDblClks,
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

        var scaledWidth = (int)MathF.Round(options.Width * _dpiScale);
        var scaledHeight = (int)MathF.Round(options.Height * _dpiScale);

        var windowTitle = _windowTitle;

        var createWidth = scaledWidth;
        var createHeight = scaledHeight;
        if (_titleBarMode is DuxelTitleBarMode.System)
        {
            var createRect = UnsafeRectForCreate(scaledWidth, scaledHeight);
            if (!AdjustWindowRectEx(ref createRect, _windowStyle, false, 0))
            {
                var error = Marshal.GetLastPInvokeError();
                CleanupRegistrationOnly();
                throw new InvalidOperationException($"AdjustWindowRectEx failed: {error}");
            }

            createWidth = createRect.right - createRect.left;
            createHeight = createRect.bottom - createRect.top;
        }

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

        UpdateCachedClientSize(_windowHandle);

        _imeHandler = new WindowsImeHandler(_windowHandle, options.FrameInvalidated);

        if (_integrateSystemChrome || _titleBarMode is not DuxelTitleBarMode.System)
        {
            ApplyCurrentSystemChrome();
        }
        if (_titleBarMode is DuxelTitleBarMode.ExtendedContent)
        {
            ApplyExtendedContentFrame();
        }
        if (_titleBarMode is not DuxelTitleBarMode.System)
        {
            _ = SetWindowPos(
                _windowHandle,
                nint.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
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
            return new PlatformSize(
                Volatile.Read(ref _clientWidth),
                Volatile.Read(ref _clientHeight));
        }
    }

    public PlatformSize FramebufferSize => WindowSize;

    public float ContentScale => _dpiScale;

    public bool IsInteractingResize => Volatile.Read(ref _sizeMoveActive) == 1;

    public long InteractiveResizeSequence => Volatile.Read(ref _interactiveResizeSequence);

    public bool ShouldClose => _shouldClose;

    public UiSystemColorScheme ColorScheme => _colorScheme;

    public double TimeSeconds => _stopwatch.Elapsed.TotalSeconds;

    public IInputBackend Input => _inputBackend;

    public IVulkanSurfaceSource? VulkanSurface => _vulkanSurface;

    public IPlatformTextBackend? TextBackend => WindowsPlatformTextBackend.Instance;

    public IUiImeHandler? ImeHandler => _imeHandler;

    public IWindowTitleBarPlatform? WindowTitleBar => this;

    public bool TryGetCaptionButtonBounds(out UiRect bounds)
    {
        ThrowIfDisposed();
        bounds = default;
        if (_titleBarMode is not DuxelTitleBarMode.ExtendedContent)
        {
            return false;
        }

        var result = DwmGetWindowAttribute(
            _windowHandle,
            DwmwaCaptionButtonBounds,
            out var buttonBounds,
            Marshal.SizeOf<Rect>());
        if (result != 0
            || buttonBounds.right <= buttonBounds.left
            || buttonBounds.bottom <= buttonBounds.top)
        {
            return false;
        }

        if (!GetWindowRect(_windowHandle, out var windowRect))
        {
            throw new InvalidOperationException($"GetWindowRect failed: {Marshal.GetLastPInvokeError()}.");
        }

        var clientOrigin = new Point();
        if (!ClientToScreen(_windowHandle, ref clientOrigin))
        {
            throw new InvalidOperationException($"ClientToScreen failed: {Marshal.GetLastPInvokeError()}.");
        }

        var clientOffsetX = clientOrigin.x - windowRect.left;
        var clientOffsetY = clientOrigin.y - windowRect.top;
        var inverseScale = 1f / MathF.Max(1f, _dpiScale);
        bounds = new UiRect(
            (buttonBounds.left - clientOffsetX) * inverseScale,
            (buttonBounds.top - clientOffsetY) * inverseScale,
            (buttonBounds.right - buttonBounds.left) * inverseScale,
            (buttonBounds.bottom - buttonBounds.top) * inverseScale);
        return true;
    }

    public void SetTitleBarDragRegions(ReadOnlySpan<UiRect> regions)
    {
        ThrowIfDisposed();
        if (_titleBarMode is not DuxelTitleBarMode.ExtendedContent)
        {
            throw new InvalidOperationException("Title-bar drag regions require DuxelTitleBarMode.ExtendedContent.");
        }

        lock (_titleBarDragRegionLock)
        {
            if (_titleBarDragRegions.Length < regions.Length)
            {
                _titleBarDragRegions = new UiRect[Math.Max(regions.Length, _titleBarDragRegions.Length * 2)];
            }

            regions.CopyTo(_titleBarDragRegions);
            _titleBarDragRegionCount = regions.Length;
        }
    }

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
        CancelInteractiveResizeWait();
        _imeHandler.Dispose();
        _trayIcon?.Dispose();

        if (_windowHandle != nint.Zero)
        {
            _ = DestroyWindow(_windowHandle);
        }

        CleanupRegistrationOnly();
        _interactiveResizeFramePresented.Dispose();
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
        if (_useDuxelTitleBar)
        {
            _minTrackWidth = _logicalMinWidth > 0 ? scaledMinWidth : 0;
            _minTrackHeight = _logicalMinHeight > 0 ? scaledMinHeight : 0;
            return;
        }

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

    public string WindowTitle => _windowTitle;

    public bool CanMinimize => _showMinimizeButton;

    public bool CanMaximize => _showMaximizeButton;

    public bool IsMaximized => IsZoomed(_windowHandle);

    public void BeginWindowMove()
    {
        ThrowIfDisposed();
        BeginWindowMoveCore();
    }

    public void NotifyFramePresented(long interactiveResizeSequence)
    {
        while (true)
        {
            var current = Volatile.Read(ref _presentedResizeSequence);
            if (current >= interactiveResizeSequence)
            {
                return;
            }

            if (Interlocked.CompareExchange(
                    ref _presentedResizeSequence,
                    interactiveResizeSequence,
                    current) == current)
            {
                break;
            }
        }

        _interactiveResizeFramePresented.Set();
    }

    public void CancelInteractiveResizeWait()
    {
        Volatile.Write(ref _interactiveResizeWaitCancelled, 1);
        _interactiveResizeFramePresented.Set();
    }

    private void BeginWindowMoveCore()
    {
        // A Win32 system move loop is not guaranteed to send the matching client WM_LBUTTONUP.
        _inputBackend.CancelMouseButtons();
        _ = SendMessageW(_windowHandle, WmSysCommand, ScMove | HtCaption, 0);
        _inputBackend.CancelMouseButtons();
        _frameInvalidated?.Invoke();
    }

    public void MinimizeWindow()
    {
        ThrowIfDisposed();
        _ = ShowWindow(_windowHandle, SwMinimize);
    }

    public void ToggleMaximizeWindow()
    {
        ThrowIfDisposed();
        _ = SendMessageW(_windowHandle, WmSysCommand, IsMaximized ? ScRestore : ScMaximize, 0);
    }

    public void CloseWindow()
    {
        ThrowIfDisposed();
        _ = SendMessageW(_windowHandle, WmClose, 0, 0);
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

    private static void ApplyIntegratedSystemChrome(nint hwnd, UiColor captionColor, UiColor textColor, UiColor borderColor)
    {
        var cornerPreference = DwmWindowCornerPreferenceRound;
        SetDwmAttribute(hwnd, DwmwaWindowCornerPreference, ref cornerPreference);

        var caption = ToColorRef(captionColor);
        SetDwmAttribute(hwnd, DwmwaCaptionColor, ref caption);

        var text = ToColorRef(textColor);
        SetDwmAttribute(hwnd, DwmwaTextColor, ref text);

        var border = ToColorRef(borderColor);
        SetDwmAttribute(hwnd, DwmwaBorderColor, ref border);

        var darkMode = IsDarkColor(captionColor) ? 1 : 0;
        SetDwmAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode);
    }

    private void ApplyExtendedContentFrame()
    {
        var allowNcPaint = 1;
        SetDwmAttribute(_windowHandle, DwmwaAllowNcPaint, ref allowNcPaint);

        const int smCyCaption = 4;
        const int smCySizeFrame = 33;
        const int smCyPaddedBorder = 92;
        var dpi = Math.Max(96u, GetDpiForWindow(_windowHandle));
        var topFrameHeight = GetSystemMetricsForDpi(smCyCaption, dpi)
            + GetSystemMetricsForDpi(smCySizeFrame, dpi)
            + GetSystemMetricsForDpi(smCyPaddedBorder, dpi);
        _extendedTitleBarFrameHeightPx = Math.Max(1, topFrameHeight);
        var margins = new Margins
        {
            cyTopHeight = _extendedTitleBarFrameHeightPx,
        };
        var result = DwmExtendFrameIntoClientArea(_windowHandle, ref margins);
        if (result != 0)
        {
            throw new InvalidOperationException($"DwmExtendFrameIntoClientArea failed: 0x{result:X8}.");
        }
    }

    private static void SetDwmAttribute(nint hwnd, int attribute, ref int value)
    {
        var result = DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        if (result != 0)
        {
            throw new InvalidOperationException($"DwmSetWindowAttribute({attribute}) failed: 0x{result:X8}.");
        }
    }

    private static int ToColorRef(UiColor color) => unchecked((int)(color.Rgba & 0x00FFFFFFu));

    private static bool IsDarkColor(UiColor color)
    {
        var r = (int)(color.Rgba & 0xFF);
        var g = (int)((color.Rgba >> 8) & 0xFF);
        var b = (int)((color.Rgba >> 16) & 0xFF);
        return ((r * 299) + (g * 587) + (b * 114)) < 128000;
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
            return LoadDefaultWindowIcons();
        }

        var largeHandleFile = LoadImageW(nint.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
        var smallHandleFile = LoadImageW(nint.Zero, iconPath, ImageIcon, GetSystemMetrics(49), GetSystemMetrics(50), LrLoadFromFile);

        return (largeHandleFile, smallHandleFile != nint.Zero ? smallHandleFile : largeHandleFile);
    }

    private static (nint LargeIconHandle, nint SmallIconHandle) LoadDefaultWindowIcons()
    {
        using var stream = typeof(WindowsPlatformBackend).Assembly.GetManifestResourceStream(DefaultIconResourceName);
        if (stream is null)
        {
            return (nint.Zero, nint.Zero);
        }

        var iconData = new byte[stream.Length];
        var read = 0;
        while (read < iconData.Length)
        {
            var count = stream.Read(iconData, read, iconData.Length - read);
            if (count <= 0)
            {
                break;
            }

            read += count;
        }

        if (read != iconData.Length)
        {
            return (nint.Zero, nint.Zero);
        }

        var largeCx = GetSystemMetrics(11);
        var largeCy = GetSystemMetrics(12);
        var smallCx = GetSystemMetrics(49);
        var smallCy = GetSystemMetrics(50);
        var largeHandle = LoadIconFromIcoData(iconData, largeCx, largeCy);
        var smallHandle = LoadIconFromIcoData(iconData, smallCx, smallCy);
        return (largeHandle, smallHandle != nint.Zero ? smallHandle : largeHandle);
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

    private void UpdateCachedClientSize(nint hwnd)
    {
        if (!GetClientRect(hwnd, out var rect))
        {
            throw new InvalidOperationException($"GetClientRect failed: {Marshal.GetLastPInvokeError()}");
        }

        Volatile.Write(ref _clientWidth, rect.right - rect.left);
        Volatile.Write(ref _clientHeight, rect.bottom - rect.top);
    }

    private unsafe void UpdateCachedClientSizeFromSizingRect(nint hwnd, nint lParam)
    {
        if (!GetWindowRect(hwnd, out var currentWindowRect))
        {
            throw new InvalidOperationException($"GetWindowRect failed: {Marshal.GetLastPInvokeError()}");
        }

        var sizingRect = *(Rect*)lParam;
        var currentWindowWidth = currentWindowRect.right - currentWindowRect.left;
        var currentWindowHeight = currentWindowRect.bottom - currentWindowRect.top;
        var nonClientWidth = Math.Max(0, currentWindowWidth - Volatile.Read(ref _clientWidth));
        var nonClientHeight = Math.Max(0, currentWindowHeight - Volatile.Read(ref _clientHeight));
        var predictedClientWidth = Math.Max(0, sizingRect.right - sizingRect.left - nonClientWidth);
        var predictedClientHeight = Math.Max(0, sizingRect.bottom - sizingRect.top - nonClientHeight);

        Volatile.Write(ref _clientWidth, predictedClientWidth);
        Volatile.Write(ref _clientHeight, predictedClientHeight);
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

        if (_titleBarMode is DuxelTitleBarMode.ExtendedContent
            && message is WmNcHitTest or WmNcMouseLeave
            && DwmDefWindowProc(hwnd, message, wParam, lParam, out var dwmResult))
        {
            return dwmResult;
        }

        _inputBackend.HandleMessage(message, wParam, lParam);

        if (_trayIcon?.HandleWindowMessage(message, wParam, lParam) == true)
        {
            return 0;
        }

        switch (message)
        {
            case WmNcCalcSize:
                if (_titleBarMode is not DuxelTitleBarMode.System)
                {
                    if (wParam != 0)
                    {
                        ConstrainMaximizedClientRectToWorkArea(hwnd, lParam);
                    }
                    return 0;
                }

                break;
            case WmNcHitTest:
                if (_useDuxelTitleBar && _resizable && TryHitTestResizeBorder(hwnd, lParam, out var hitTest))
                {
                    return hitTest;
                }

                if (_useDuxelTitleBar && TryHitTestDuxelTitleBar(hwnd, lParam, _duxelTitleBarHeightPx, out hitTest))
                {
                    return hitTest;
                }

                if (_titleBarMode is DuxelTitleBarMode.ExtendedContent)
                {
                    if (TryHitTestNativeCaptionButtons(hwnd, lParam, out hitTest))
                    {
                        return hitTest;
                    }

                    if (_resizable && TryHitTestResizeBorder(hwnd, lParam, out hitTest))
                    {
                        return hitTest;
                    }

                    return IsExtendedTitleBarDragPoint(hwnd, lParam) ? HtCaption : HtClient;
                }

                break;
            case WmLButtonDown:
                if (_useDuxelTitleBar
                    && IsDuxelCaptionDragClientPoint(hwnd, lParam, _duxelTitleBarHeightPx, _showMinimizeButton, _showMaximizeButton, _dpiScale))
                {
                    BeginWindowMoveCore();
                    return 0;
                }

                break;
            case WmLButtonDblClk:
                if (_useDuxelTitleBar
                    && _showMaximizeButton
                    && IsDuxelCaptionDragClientPoint(hwnd, lParam, _duxelTitleBarHeightPx, _showMinimizeButton, _showMaximizeButton, _dpiScale))
                {
                    _inputBackend.CancelMouseButtons();
                    ToggleMaximizeWindow();
                    _frameInvalidated?.Invoke();
                    return 0;
                }

                break;
            case WmEraseBkgnd:
                return 1;
            case WmPaint:
                _ = BeginPaint(hwnd, out var ps);
                _ = EndPaint(hwnd, ref ps);
                return 0;
            case WmEnterSizeMove:
                Volatile.Write(ref _interactiveResizeWaitCancelled, 0);
                Volatile.Write(ref _sizeMoveActive, 1);
                _frameInvalidated?.Invoke();
                break;
            case WmExitSizeMove:
                Volatile.Write(ref _sizeMoveActive, 0);
                _inputBackend.CancelMouseButtons();
                _frameInvalidated?.Invoke();
                break;
            case WmSize:
                Volatile.Write(ref _clientWidth, unchecked((ushort)(nuint)lParam));
                Volatile.Write(ref _clientHeight, unchecked((ushort)((nuint)lParam >> 16)));
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
                _frameInvalidated?.Invoke();
                break;
            case WmWindowPosChanged:
                _frameInvalidated?.Invoke();
                break;
            case WmSizing:
                UpdateCachedClientSizeFromSizingRect(hwnd, lParam);
                var resizeSequence = Interlocked.Increment(ref _interactiveResizeSequence);
                _frameInvalidated?.Invoke();
                while (Volatile.Read(ref _presentedResizeSequence) < resizeSequence
                    && Volatile.Read(ref _interactiveResizeWaitCancelled) == 0)
                {
                    _interactiveResizeFramePresented.WaitOne();
                }
                return 1;
            case WmNcMouseMove:
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
                unsafe
                {
                    var handled = false;
                    var mmi = (MinMaxInfo*)lParam;
                    if (_titleBarMode is not DuxelTitleBarMode.System)
                    {
                        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                        var monitorInfo = new MonitorInfo { cbSize = (uint)sizeof(MonitorInfo) };
                        if (monitor != nint.Zero && GetMonitorInfoW(monitor, ref monitorInfo))
                        {
                            mmi->ptMaxPosition.x = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
                            mmi->ptMaxPosition.y = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;
                            mmi->ptMaxSize.x = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
                            mmi->ptMaxSize.y = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;
                            handled = true;
                        }
                    }

                    if (_minTrackWidth > 0)
                    {
                        mmi->ptMinTrackSize.x = Math.Max(mmi->ptMinTrackSize.x, _minTrackWidth);
                        handled = true;
                    }

                    if (_minTrackHeight > 0)
                    {
                        mmi->ptMinTrackSize.y = Math.Max(mmi->ptMinTrackSize.y, _minTrackHeight);
                        handled = true;
                    }

                    if (handled)
                    {
                        return 0;
                    }
                }

                break;
            case WmDpiChanged:
                {
                    var newDpi = (int)(wParam & 0xFFFF);
                    _dpiScale = newDpi > 0 ? newDpi / 96f : 1f;
                    _duxelTitleBarHeightPx = Math.Max(1, (int)MathF.Round(_duxelTitleBarHeight * _dpiScale));
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
                    if (_titleBarMode is DuxelTitleBarMode.ExtendedContent)
                    {
                        ApplyExtendedContentFrame();
                    }
                    _frameInvalidated?.Invoke();
                    return 0;
                }
            case WmDwmCompositionChanged:
                if (_titleBarMode is DuxelTitleBarMode.ExtendedContent)
                {
                    ApplyExtendedContentFrame();
                    _frameInvalidated?.Invoke();
                    return 0;
                }

                break;
            case WmNcMouseLeave:
                break;
            case WmSettingChange:
                if (IsImmersiveColorSetChange(lParam) && RefreshColorSchemeFromSystem())
                {
                    if (_followSystemTheme)
                    {
                        ApplyCurrentSystemChrome();
                    }

                    _frameInvalidated?.Invoke();
                }
                break;
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

    private static bool TryHitTestResizeBorder(nint hwnd, nint lParam, out nint hitTest)
    {
        hitTest = 0;
        if (!GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var point = PointFromLParam(lParam);
        var borderX = GetResizeBorderWidth(hwnd);
        var borderY = GetResizeBorderHeight(hwnd);
        var left = point.x >= rect.left && point.x < rect.left + borderX;
        var right = point.x < rect.right && point.x >= rect.right - borderX;
        var top = point.y >= rect.top && point.y < rect.top + borderY;
        var bottom = point.y < rect.bottom && point.y >= rect.bottom - borderY;

        hitTest = (top, bottom, left, right) switch
        {
            (true, _, true, _) => HtTopLeft,
            (true, _, _, true) => HtTopRight,
            (_, true, true, _) => HtBottomLeft,
            (_, true, _, true) => HtBottomRight,
            (true, _, _, _) => HtTop,
            (_, true, _, _) => HtBottom,
            (_, _, true, _) => HtLeft,
            (_, _, _, true) => HtRight,
            _ => 0,
        };

        return hitTest != 0;
    }

    private static bool TryHitTestDuxelTitleBar(
        nint hwnd,
        nint lParam,
        int titleBarHeightPx,
        out nint hitTest)
    {
        hitTest = 0;
        if (!GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var point = PointFromLParam(lParam);
        var x = point.x - rect.left;
        var y = point.y - rect.top;
        var width = rect.right - rect.left;
        if (x < 0 || y < 0 || x >= width || y >= titleBarHeightPx)
        {
            return false;
        }

        hitTest = HtClient;
        return true;
    }

    private static bool IsDuxelCaptionDragClientPoint(
        nint hwnd,
        nint lParam,
        int titleBarHeightPx,
        bool showMinimizeButton,
        bool showMaximizeButton,
        float dpiScale)
    {
        if (!GetClientRect(hwnd, out var rect))
        {
            return false;
        }

        var point = ClientPointFromLParam(lParam);
        var width = rect.right - rect.left;
        if (point.x < 0 || point.y < 0 || point.x >= width || point.y >= titleBarHeightPx)
        {
            return false;
        }

        var chromeButtonCount = 1 + (showMinimizeButton ? 1 : 0) + (showMaximizeButton ? 1 : 0);
        var buttonAreaWidth = (int)MathF.Ceiling(DuxelChromeButtonLogicalWidth * MathF.Max(1f, dpiScale) * chromeButtonCount);
        var buttonAreaLeft = Math.Max(0, width - buttonAreaWidth);
        return point.x < buttonAreaLeft;
    }

    private bool IsExtendedTitleBarDragPoint(nint hwnd, nint lParam)
    {
        var point = PointFromLParam(lParam);
        if (!ScreenToClient(hwnd, ref point))
        {
            throw new InvalidOperationException($"ScreenToClient failed: {Marshal.GetLastPInvokeError()}.");
        }

        var inverseScale = 1f / MathF.Max(1f, _dpiScale);
        var x = point.x * inverseScale;
        var y = point.y * inverseScale;

        lock (_titleBarDragRegionLock)
        {
            for (var i = 0; i < _titleBarDragRegionCount; i++)
            {
                var region = _titleBarDragRegions[i];
                if (x >= region.X
                    && y >= region.Y
                    && x < region.X + region.Width
                    && y < region.Y + region.Height)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static unsafe void ConstrainMaximizedClientRectToWorkArea(nint hwnd, nint lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { cbSize = (uint)sizeof(MonitorInfo) };
        if (monitor == nint.Zero || !GetMonitorInfoW(monitor, ref monitorInfo))
        {
            throw new InvalidOperationException($"GetMonitorInfoW failed: {Marshal.GetLastPInvokeError()}.");
        }

        var parameters = (NcCalcSizeParams*)lParam;
        var proposed = parameters->rgrc0;
        var work = monitorInfo.rcWork;
        var coversWorkArea = proposed.left <= work.left
            && proposed.top <= work.top
            && proposed.right >= work.right
            && proposed.bottom >= work.bottom;
        if (coversWorkArea)
        {
            parameters->rgrc0 = work;
        }
    }

    private bool TryHitTestNativeCaptionButtons(nint hwnd, nint lParam, out nint hitTest)
    {
        hitTest = 0;
        if (!GetWindowRect(hwnd, out var windowRect))
        {
            throw new InvalidOperationException($"GetWindowRect failed: {Marshal.GetLastPInvokeError()}.");
        }

        var point = PointFromLParam(lParam);
        var x = point.x - windowRect.left;
        var y = point.y - windowRect.top;
        if (y < 0 || y >= Volatile.Read(ref _extendedTitleBarFrameHeightPx))
        {
            return false;
        }

        var result = DwmGetWindowAttribute(
            hwnd,
            DwmwaCaptionButtonBounds,
            out var bounds,
            Marshal.SizeOf<Rect>());
        if (result != 0 || bounds.right <= bounds.left || bounds.bottom <= bounds.top)
        {
            return false;
        }

        if (x < bounds.left || x >= bounds.right || y < bounds.top || y >= bounds.bottom)
        {
            return false;
        }

        var buttonCount = 1 + (_showMinimizeButton ? 1 : 0) + (_showMaximizeButton ? 1 : 0);
        var buttonIndex = Math.Min(buttonCount - 1, (x - bounds.left) * buttonCount / Math.Max(1, bounds.right - bounds.left));
        if (_showMinimizeButton && buttonIndex-- is 0)
        {
            hitTest = HtMinButton;
            return true;
        }

        if (_showMaximizeButton && buttonIndex-- is 0)
        {
            hitTest = HtMaxButton;
            return true;
        }

        hitTest = HtClose;
        return true;
    }

    private static Point PointFromLParam(nint lParam)
    {
        var value = unchecked((int)lParam);
        return new Point
        {
            x = (short)(value & 0xFFFF),
            y = (short)((value >> 16) & 0xFFFF),
        };
    }

    private static Point ClientPointFromLParam(nint lParam)
    {
        var value = unchecked((int)lParam);
        return new Point
        {
            x = (short)(value & 0xFFFF),
            y = (short)((value >> 16) & 0xFFFF),
        };
    }

    private static int GetResizeBorderWidth(nint hwnd)
    {
        const int smCxSizeFrame = 32;
        const int smCxPaddedBorder = 92;
        var dpi = Math.Max(96u, GetDpiForWindow(hwnd));
        return Math.Max(1, GetSystemMetricsForDpi(smCxSizeFrame, dpi) + GetSystemMetricsForDpi(smCxPaddedBorder, dpi));
    }

    private static int GetResizeBorderHeight(nint hwnd)
    {
        const int smCySizeFrame = 33;
        const int smCyPaddedBorder = 92;
        var dpi = Math.Max(96u, GetDpiForWindow(hwnd));
        return Math.Max(1, GetSystemMetricsForDpi(smCySizeFrame, dpi) + GetSystemMetricsForDpi(smCyPaddedBorder, dpi));
    }

    private void ApplyCurrentSystemChrome()
    {
        if (_followSystemTheme)
        {
            var theme = WindowsSystemTheme.GetAppDesign().Theme;
            ApplyIntegratedSystemChrome(_windowHandle, theme.TitleBgActive, theme.WindowTitleText, theme.Border);
            return;
        }

        ApplyIntegratedSystemChrome(_windowHandle, _chromeCaptionColor, _chromeTextColor, _chromeBorderColor);
    }

    private bool RefreshColorSchemeFromSystem()
    {
        var next = WindowsSystemTheme.GetAppColorScheme();
        if (next == _colorScheme)
        {
            return false;
        }

        _colorScheme = next;
        return true;
    }

    private static bool IsImmersiveColorSetChange(nint lParam)
    {
        if (lParam == nint.Zero)
        {
            return true;
        }

        var area = Marshal.PtrToStringUni(lParam);
        return string.Equals(area, "ImmersiveColorSet", StringComparison.Ordinal);
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

        public void CancelMouseButtons()
        {
            _leftDown = false;
            _rightDown = false;
            _middleDown = false;
            _leftPressedEvent = false;
            _leftReleasedEvent = false;
            _rightPressedEvent = false;
            _rightReleasedEvent = false;
            _middlePressedEvent = false;
            _middleReleasedEvent = false;
            ReleaseCapture();
        }

        public void HandleMessage(uint message, nuint wParam, nint lParam)
        {
            switch (message)
            {
                case WmNcMouseMove:
                    {
                        var point = PointFromLParam(lParam);
                        if (_hwnd != nint.Zero && ScreenToClient(_hwnd, ref point))
                        {
                            _mousePosition = new UiVector2(point.x, point.y);
                        }

                        break;
                    }
                case WmMouseMove:
                    _mousePosition = new UiVector2(GetXFromLParam(lParam), GetYFromLParam(lParam));
                    break;
                case WmLButtonDown:
                case WmLButtonDblClk:
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
    private struct Margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
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
    private struct MonitorInfo
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NcCalcSizeParams
    {
        public Rect rgrc0;
        public Rect rgrc1;
        public Rect rgrc2;
        public nint lppos;
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

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint hMonitor, ref MonitorInfo lpmi);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point lpPoint);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetModuleHandleW(nint lpModuleName);

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(int nVirtKey);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

    [LibraryImport("user32.dll", EntryPoint = "ScreenToClient", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ScreenToClient(nint hWnd, ref Point lpPoint);

    [LibraryImport("user32.dll", EntryPoint = "ClientToScreen", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint hWnd, ref Point lpPoint);

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
    private static partial bool IsZoomed(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

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

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins pMarInset);

    [LibraryImport("dwmapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DwmDefWindowProc(nint hwnd, uint msg, nuint wParam, nint lParam, out nint plResult);

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
