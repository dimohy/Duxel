// FBA: Extended title-bar sample with application tabs and Duxel caption visuals backed by Windows behavior
#:property TargetFramework=net10.0
#:property OutputType=WinExe
#:property AllowUnsafeBlocks=true
#:property OptimizationPreference=Size
#:property InvariantGlobalization=true
#:property DebuggerSupport=false
#:property EventSourceSupport=false
#:property MetricsSupport=false
#:property MetadataUpdaterSupport=false
#:property StackTraceSupport=false
#:property UseSystemResourceKeys=true
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;
using System.Runtime.InteropServices;

var diagnosticOutputPath = Environment.GetEnvironmentVariable("DUXEL_EXTENDED_TITLEBAR_DIAG_OUT");
try
{
    DuxelWindowsApp.Run(new DuxelAppOptions
    {
        Window = new DuxelWindowOptions
        {
            Title = "Duxel Extended Title Bar",
            Width = 1100,
            Height = 700,
            MinWidth = 440,
            MinHeight = 320,
            TitleBarMode = DuxelTitleBarMode.ExtendedContent,
            DuxelTitleBarHeight = ExtendedTitleBarScreen.TitleBarHeight,
            IntegrateSystemChrome = true,
            WindowCreated = ExtendedTitleBarDiagnostics.SetWindowHandle,
        },
        Screen = new ExtendedTitleBarScreen(),
    });
}
catch (Exception exception) when (!string.IsNullOrWhiteSpace(diagnosticOutputPath))
{
    File.WriteAllText(diagnosticOutputPath, $"FAIL|unhandled-exception|{exception}");
    throw;
}

public sealed class ExtendedTitleBarScreen : UiScreen
{
    public const float TitleBarHeight = 48f;
    private int _activeTab;
    private int _newTabCount;
    private readonly string? _diagnosticOutputPath = Environment.GetEnvironmentVariable("DUXEL_EXTENDED_TITLEBAR_DIAG_OUT");
    private int _diagnosticStage;
    private int _frameCount;

    public override void Render(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var width = viewport.Size.X;
        var hasWindowIcon = ui.TryGetWindowIcon(out _);
        var hasCaptionBounds = ui.TryGetCaptionButtonBounds(out var captionBounds);
        var captionLeft = hasCaptionBounds ? captionBounds.X : width;
        var dragRegion = DrawApplicationTitleBar(ui, captionLeft);

        if (dragRegion.Width > 0f)
        {
            ui.SetTitleBarDragRegions([
                new UiRect(0f, 0f, 48f, TitleBarHeight),
                dragRegion,
            ]);
        }
        else
        {
            ui.SetTitleBarDragRegions([new UiRect(0f, 0f, 48f, TitleBarHeight)]);
        }

        DrawContent(ui, viewport, hasCaptionBounds, captionBounds);

        _frameCount++;
        if (_diagnosticStage == 0
            && _frameCount >= 10
            && hasCaptionBounds
            && !string.IsNullOrWhiteSpace(_diagnosticOutputPath))
        {
            _diagnosticStage = 1;
            ExtendedTitleBarDiagnostics.Begin(
                _diagnosticOutputPath,
                hasWindowIcon,
                captionBounds,
                new UiRect(48f, 8f, 112f, 40f),
                dragRegion);
        }
        else if (_diagnosticStage == 1
            && hasCaptionBounds
            && ExtendedTitleBarDiagnostics.TryComplete(captionBounds))
        {
            _diagnosticStage = 2;
            DuxelApp.Exit();
        }
    }

    private UiRect DrawApplicationTitleBar(UiImmediateContext ui, float captionLeft)
    {
        var drawList = ui.GetBackgroundDrawList();
        var viewport = ui.GetMainViewport();
        var width = viewport.Size.X;
        var background = ui.GetColorU32(UiStyleColor.TitleBgActive);
        var border = ui.GetColorU32(UiStyleColor.Border);
        drawList.AddRectFilled(new UiRect(0f, 0f, width, TitleBarHeight), background);
        drawList.AddRectFilled(new UiRect(0f, TitleBarHeight - 1f, width, 1f), border);

        const float iconLeft = 12f;
        const float iconSize = 24f;
        const float left = 48f;
        const float top = 8f;
        const float tabWidth = 112f;
        const float tabHeight = 40f;
        const float gap = 4f;

        if (ui.TryGetWindowIcon(out var windowIcon))
        {
            windowIcon.Prepare(ui, UiImageEffects.Default);
            var iconTop = MathF.Max(0f, TitleBarHeight - iconSize) * 0.5f;
            var iconRect = new UiRect(iconLeft, iconTop, iconSize, iconSize);
            drawList.AddImage(
                windowIcon.TextureId,
                new UiVector2(iconRect.X, iconRect.Y),
                new UiVector2(iconRect.X + iconRect.Width, iconRect.Y + iconRect.Height),
                new UiVector2(0f, 0f),
                new UiVector2(1f, 1f),
                new UiColor(0xFFFFFFFF),
                iconRect);
        }

        var x = left;
        for (var i = 0; i < 2; i++)
        {
            var label = i == 0 ? "Home" : "Documents";
            if (DrawTitleTab(ui, label, $"##extended-tab-{i}", new UiRect(x, top, tabWidth, tabHeight), _activeTab == i))
            {
                _activeTab = i;
            }

            x += tabWidth + gap;
        }

        if (DrawNewTabButton(ui, new UiRect(x, top, 36f, tabHeight)))
        {
            _newTabCount++;
        }

        x += 36f + 14f;
        var dragRight = MathF.Max(x, captionLeft - 8f);
        if (dragRight > x)
        {
            var label = "Drag this area";
            var labelSize = ui.CalcTextSize(label);
            ui.GetForegroundDrawList().AddText(
                new UiVector2(x + MathF.Max(0f, dragRight - x - labelSize.X) * 0.5f, 15f),
                ui.GetColorU32(UiStyleColor.TextDisabled),
                label);
        }

        return new UiRect(x, 0f, MathF.Max(0f, dragRight - x), TitleBarHeight);
    }

    private static bool DrawTitleTab(UiImmediateContext ui, string label, string id, UiRect rect, bool active)
    {
        ui.SetCursorScreenPos(new UiVector2(rect.X, rect.Y));
        var clicked = ui.InvisibleButton(id, new UiVector2(rect.Width, rect.Height));
        var hovered = ui.IsItemHovered();
        var drawList = ui.GetForegroundDrawList();
        var visualRect = active
            ? new UiRect(rect.X, rect.Y - 1f, rect.Width, rect.Height + 1f)
            : new UiRect(rect.X, rect.Y + 3f, rect.Width, rect.Height - 3f);
        var background = active
            ? ui.GetColorU32(UiStyleColor.TabActive)
            : hovered
                ? ui.GetColorU32(UiStyleColor.TabHovered)
                : ui.GetColorU32(UiStyleColor.Tab);

        drawList.AddRectFilledRounded(visualRect, background, ui.WhiteTextureId, 7f, visualRect);
        drawList.AddRect(visualRect, ui.GetColorU32(UiStyleColor.Border), rounding: 7f, thickness: 1f);
        if (active)
        {
            drawList.AddRectFilled(
                new UiRect(visualRect.X + 10f, visualRect.Y + visualRect.Height - 3f, visualRect.Width - 20f, 2f),
                ui.GetColorU32(UiStyleColor.CheckMark),
                ui.WhiteTextureId,
                visualRect);
        }

        var textSize = ui.CalcTextSize(label);
        drawList.AddText(
            new UiVector2(
                visualRect.X + MathF.Max(0f, visualRect.Width - textSize.X) * 0.5f,
                visualRect.Y + MathF.Max(0f, visualRect.Height - textSize.Y) * 0.5f),
            ui.GetColorU32(UiStyleColor.TabText),
            label);
        return clicked;
    }

    private static bool DrawNewTabButton(UiImmediateContext ui, UiRect rect)
    {
        ui.SetCursorScreenPos(new UiVector2(rect.X, rect.Y));
        var clicked = ui.InvisibleButton("##extended-new-tab", new UiVector2(rect.Width, rect.Height));
        var hovered = ui.IsItemHovered();
        var drawList = ui.GetForegroundDrawList();
        var visualRect = new UiRect(rect.X, rect.Y + 3f, rect.Width, rect.Height - 3f);
        var background = hovered
            ? ui.GetColorU32(UiStyleColor.TabHovered)
            : ui.GetColorU32(UiStyleColor.Tab);
        drawList.AddRectFilledRounded(visualRect, background, ui.WhiteTextureId, 7f, visualRect);
        drawList.AddRect(visualRect, ui.GetColorU32(UiStyleColor.Border), rounding: 7f, thickness: 1f);

        var center = new UiVector2(visualRect.X + visualRect.Width * 0.5f, visualRect.Y + visualRect.Height * 0.5f);
        var color = ui.GetColorU32(UiStyleColor.TabText);
        drawList.AddLine(new UiVector2(center.X - 4f, center.Y), new UiVector2(center.X + 4f, center.Y), color, 1.5f);
        drawList.AddLine(new UiVector2(center.X, center.Y - 4f), new UiVector2(center.X, center.Y + 4f), color, 1.5f);
        return clicked;
    }

    private void DrawContent(UiImmediateContext ui, UiViewport viewport, bool hasCaptionBounds, UiRect captionBounds)
    {
        const float margin = 24f;
        ui.SetNextWindowPos(new UiVector2(margin, TitleBarHeight + margin));
        ui.SetNextWindowSize(new UiVector2(
            MathF.Max(1f, viewport.Size.X - margin * 2f),
            MathF.Max(1f, viewport.Size.Y - TitleBarHeight - margin * 2f)));
        ui.BeginWindow("Extended title-bar validation");

        ui.PushFontSize(24f);
        ui.Text(_activeTab == 0 ? "Home" : "Documents");
        ui.PopFontSize();
        ui.Separator();
        ui.Text("The app draws the effective icon and tabs; Duxel draws caption glyphs while Windows owns their behavior.");
        ui.Text("TryGetWindowIcon, TryGetCaptionButtonBounds, and SetTitleBarDragRegions are public Duxel APIs.");
        ui.Text("Drag the empty title-bar area, double-click it, resize the borders, or hover Maximize for Snap Layouts.");
        ui.Text($"New tab clicks: {_newTabCount}");
        ui.Spacing();

        if (hasCaptionBounds)
        {
            ui.Text($"Duxel caption bounds: X={captionBounds.X:0.0}, Y={captionBounds.Y:0.0}, W={captionBounds.Width:0.0}, H={captionBounds.Height:0.0}");
        }
        else
        {
            ui.Text("Duxel caption bounds are temporarily unavailable while the window is not visible.");
        }

        ui.EndWindow();
    }
}

public static partial class ExtendedTitleBarDiagnostics
{
    private const int GwlStyle = -16;
    private const uint WsCaption = 0x00C00000;
    private const uint WsSysMenu = 0x00080000;
    private const uint WsThickFrame = 0x00040000;
    private const uint WsMinimizeBox = 0x00020000;
    private const uint WsMaximizeBox = 0x00010000;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmNcLButtonDblClk = 0x00A3;
    private const uint WmSysCommand = 0x0112;
    private const uint ScRestore = 0xF120;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtMinButton = 8;
    private const int HtMaxButton = 9;
    private const int HtTopLeft = 13;
    private const int HtClose = 20;
    private const int DwmwaCaptionButtonBounds = 5;
    private const int DwmwaExtendedFrameBounds = 9;
    private const uint MonitorDefaultToNearest = 2;
    private const float CaptionButtonWidth = 48f;
    private const float CaptionButtonHeight = 48f;
    private static nint _windowHandle;
    private static List<string>? _checks;
    private static string? _outputPath;

    public static void SetWindowHandle(nint windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public static unsafe void Begin(string outputPath, bool hasWindowIcon, UiRect publicCaptionBounds, UiRect tabRegion, UiRect dragRegion)
    {
        var checks = new List<string>();
        _checks = checks;
        _outputPath = outputPath;
        var hwnd = _windowHandle;
        AddCheck(checks, "window-handle", hwnd != nint.Zero, $"0x{hwnd:X}");
        AddCheck(checks, "window-icon-api", hasWindowIcon, $"available={hasWindowIcon}");

        var style = unchecked((uint)GetWindowLongPtrW(hwnd, GwlStyle).ToInt64());
        AddCheck(checks, "style-caption", (style & WsCaption) == WsCaption, $"0x{style:X8}");
        AddCheck(checks, "style-system-menu", (style & WsSysMenu) != 0, $"0x{style:X8}");
        AddCheck(checks, "style-thick-frame", (style & WsThickFrame) != 0, $"0x{style:X8}");
        AddCheck(checks, "style-minimize-box", (style & WsMinimizeBox) != 0, $"0x{style:X8}");
        AddCheck(checks, "style-maximize-box", (style & WsMaximizeBox) != 0, $"0x{style:X8}");
        AddCheck(checks, "alt-space-system-menu-contract",
            (style & WsSysMenu) != 0 && GetSystemMenu(hwnd, false) != nint.Zero,
            $"style=0x{style:X8}");

        var captionHr = DwmGetWindowAttribute(hwnd, DwmwaCaptionButtonBounds, out var nativeCaptionBounds, Marshal.SizeOf<Rect>());
        AddCheck(checks, "dwm-caption-bounds", captionHr == 0 && nativeCaptionBounds.Width > 0 && nativeCaptionBounds.Height > 0,
            $"hr=0x{captionHr:X8};rect={nativeCaptionBounds}");

        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi / 96f;
        var clientRectOk = GetClientRect(hwnd, out var clientRect);
        var expectedCaptionBounds = new UiRect(
            clientRect.Width / scale - CaptionButtonWidth * 3f,
            0f,
            CaptionButtonWidth * 3f,
            CaptionButtonHeight);
        AddCheck(checks, "public-caption-bounds",
            clientRectOk && RectsApproximatelyEqual(publicCaptionBounds, expectedCaptionBounds, 0.1f),
            $"public={publicCaptionBounds};expected={expectedCaptionBounds};native={nativeCaptionBounds};dpi={dpi}");

        var captionButtonY = publicCaptionBounds.Y + publicCaptionBounds.Height * 0.5f;
        AddHitTest(checks, "hit-minimize", hwnd,
            publicCaptionBounds.X + CaptionButtonWidth * 0.5f, captionButtonY, scale, HtMinButton);
        AddHitTest(checks, "hit-maximize", hwnd,
            publicCaptionBounds.X + CaptionButtonWidth * 1.5f, captionButtonY, scale, HtMaxButton);
        AddHitTest(checks, "hit-close", hwnd,
            publicCaptionBounds.X + CaptionButtonWidth * 2.5f, captionButtonY, scale, HtClose);
        AddHitTest(checks, "hit-maximize-full-height", hwnd,
            publicCaptionBounds.X + CaptionButtonWidth * 1.5f,
            publicCaptionBounds.Y + publicCaptionBounds.Height - 4f,
            scale,
            HtMaxButton);
        AddHitTest(checks, "hit-tab-client", hwnd,
            tabRegion.X + tabRegion.Width * 0.5f, tabRegion.Y + tabRegion.Height * 0.5f, scale, HtClient);
        AddHitTest(checks, "hit-drag-caption", hwnd,
            dragRegion.X + dragRegion.Width * 0.5f, dragRegion.Y + dragRegion.Height * 0.5f, scale, HtCaption);
        AddHitTest(checks, "hit-resize-top-left", hwnd, 1f, 1f, scale, HtTopLeft);

        var minMaxInfo = new MinMaxInfo();
        _ = SendMessageW(hwnd, WmGetMinMaxInfo, 0, (nint)(&minMaxInfo));
        AddCheck(checks, "maximized-work-area-contract",
            minMaxInfo.MaxPosition.X == 0 && minMaxInfo.MaxPosition.Y == 0 && minMaxInfo.MaxSize.X > 0 && minMaxInfo.MaxSize.Y > 0,
            $"position={minMaxInfo.MaxPosition};size={minMaxInfo.MaxSize}");

        var dragPoint = ToScreenPoint(hwnd,
            dragRegion.X + dragRegion.Width * 0.5f,
            dragRegion.Y + dragRegion.Height * 0.5f,
            scale);
        _ = SendMessageW(hwnd, WmNcLButtonDblClk, HtCaption, PackPoint(dragPoint));
        AddCheck(checks, "double-click-maximize", IsZoomed(hwnd), $"isZoomed={IsZoomed(hwnd)}");
    }

    public static bool TryComplete(UiRect publicCaptionBounds)
    {
        var hwnd = _windowHandle;
        var checks = _checks ?? throw new InvalidOperationException("Extended title-bar diagnostics were not started.");
        var outputPath = _outputPath ?? throw new InvalidOperationException("Extended title-bar diagnostics output path is missing.");

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        var monitorInfoOk = GetMonitorInfoW(monitor, ref monitorInfo);
        var frameHr = DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var visibleFrame, Marshal.SizeOf<Rect>());
        var windowRectOk = GetWindowRect(hwnd, out var maximizedWindowRect);
        var clientRectOk = GetClientRect(hwnd, out var clientRect);
        var clientOrigin = new Point(0, 0);
        var clientOriginOk = ClientToScreen(hwnd, ref clientOrigin);
        var clientScreenRect = new Rect(
            clientOrigin.X,
            clientOrigin.Y,
            clientOrigin.X + clientRect.Width,
            clientOrigin.Y + clientRect.Height);
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi / 96f;
        var expectedMaximizedCaption = new UiRect(
            clientRect.Width / scale - CaptionButtonWidth * 3f,
            0f,
            CaptionButtonWidth * 3f,
            CaptionButtonHeight);
        AddCheck(checks, "maximized-public-caption-bounds",
            clientRectOk && RectsApproximatelyEqual(publicCaptionBounds, expectedMaximizedCaption, 0.1f),
            $"public={publicCaptionBounds};expected={expectedMaximizedCaption};dpi={dpi}");
        AddHitTest(checks, "maximized-hit-maximize-full-height", hwnd,
            publicCaptionBounds.X + CaptionButtonWidth * 1.5f,
            publicCaptionBounds.Y + publicCaptionBounds.Height - 4f,
            scale,
            HtMaxButton);
        var respectsWorkArea = monitorInfoOk
            && frameHr == 0
            && windowRectOk
            && clientScreenRect.Left == monitorInfo.Work.Left
            && clientScreenRect.Top == monitorInfo.Work.Top
            && clientScreenRect.Right == monitorInfo.Work.Right
            && clientScreenRect.Bottom == monitorInfo.Work.Bottom;
        AddCheck(checks, "maximized-work-area", respectsWorkArea,
            $"window={maximizedWindowRect};client={clientScreenRect};frame={visibleFrame};work={monitorInfo.Work};hr=0x{frameHr:X8}");

        _ = SendMessageW(hwnd, WmSysCommand, ScRestore, 0);
        AddCheck(checks, "restore-after-maximize", !IsZoomed(hwnd), $"isZoomed={IsZoomed(hwnd)}");

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(outputPath, checks);
        return true;
    }

    private static void AddHitTest(List<string> checks, string name, nint hwnd, float logicalX, float logicalY, float scale, int expected)
    {
        var point = ToScreenPoint(hwnd, logicalX, logicalY, scale);
        var actual = (int)SendMessageW(hwnd, WmNcHitTest, 0, PackPoint(point));
        AddCheck(checks, name, actual == expected, $"expected={expected};actual={actual};point={point}");
    }

    private static Point ToScreenPoint(nint hwnd, float logicalX, float logicalY, float scale)
    {
        var point = new Point(
            (int)MathF.Round(logicalX * scale),
            (int)MathF.Round(logicalY * scale));
        if (!ClientToScreen(hwnd, ref point))
        {
            throw new InvalidOperationException($"ClientToScreen failed: {Marshal.GetLastPInvokeError()}.");
        }

        return point;
    }

    private static nint PackPoint(Point point)
    {
        var packed = unchecked((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16));
        return unchecked((nint)(int)packed);
    }

    private static bool RectsApproximatelyEqual(UiRect left, UiRect right, float tolerance)
        => MathF.Abs(left.X - right.X) <= tolerance
            && MathF.Abs(left.Y - right.Y) <= tolerance
            && MathF.Abs(left.Width - right.Width) <= tolerance
            && MathF.Abs(left.Height - right.Height) <= tolerance;

    private static void AddCheck(List<string> checks, string name, bool passed, string details)
    {
        checks.Add($"{(passed ? "PASS" : "FAIL")}|{name}|{details}");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Point(int X, int Y)
    {
        public override string ToString() => $"({X},{Y})";
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Rect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint GetWindowLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll")]
    private static partial nint GetSystemMenu(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint hwnd, ref Point point);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint hwnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsZoomed(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MonitorInfo monitorInfo);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint hwnd, int attribute, out Rect value, int valueSize);
}
