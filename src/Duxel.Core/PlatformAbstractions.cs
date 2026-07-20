using Duxel.Core;

namespace Duxel.Core;

public readonly record struct PlatformSize(int Width, int Height);

public enum UiSystemColorScheme
{
    Light,
    Dark,
}

public enum DuxelTitleBarMode
{
    Default,
    System,
    Duxel,
    ExtendedContent,
}

public readonly record struct InputSnapshot(
    UiVector2 MousePosition,
    bool LeftMouseDown,
    bool RightMouseDown,
    bool MiddleMouseDown,
    bool LeftMousePressedEvent,
    bool LeftMouseReleasedEvent,
    bool RightMousePressedEvent,
    bool RightMouseReleasedEvent,
    bool MiddleMousePressedEvent,
    bool MiddleMouseReleasedEvent,
    float MouseWheel,
    float MouseWheelHorizontal,
    IReadOnlyList<UiKeyEvent> KeyEvents,
    IReadOnlyList<UiCharEvent> CharEvents,
    UiKeyRepeatSettings KeyRepeatSettings,
    KeyModifiers Modifiers
);

public interface IKeyRepeatSettingsProvider
{
    UiKeyRepeatSettings GetSettings();
}

public interface IInputBackend
{
    InputSnapshot Snapshot { get; }
}

public interface IVulkanSurfaceSource
{
    IReadOnlyList<string> RequiredInstanceExtensions { get; }
    nuint CreateSurface(nuint instanceHandle);
}

public interface IPlatformBackend : IDisposable
{
    PlatformSize WindowSize { get; }
    PlatformSize FramebufferSize { get; }
    float ContentScale => 1f;
    bool IsInteractingResize { get; }
    bool ShouldClose { get; }
    double TimeSeconds { get; }
    IInputBackend Input { get; }
    IVulkanSurfaceSource? VulkanSurface { get; }
    IPlatformTextBackend? TextBackend { get; }
    IUiImeHandler? ImeHandler => null;
    IWindowTitleBarPlatform? WindowTitleBar => null;
    long InteractiveResizeSequence => 0;

    void PollEvents();
    void WaitEvents(int timeoutMilliseconds);
    void SetMouseCursor(UiMouseCursor cursor);
    void NotifyFramePresented(long interactiveResizeSequence) { }
    void CancelInteractiveResizeWait() { }
}

public interface IWindowTitleBarPlatform
{
    bool TryGetCaptionButtonBounds(out UiRect bounds);
    void SetTitleBarDragRegions(ReadOnlySpan<UiRect> regions);
    UiCaptionButtonVisualState CaptionButtonVisualState => default;
}

public enum UiCaptionButtonKind
{
    None,
    Minimize,
    Maximize,
    Close,
}

public readonly record struct UiCaptionButtonVisualState(
    UiCaptionButtonKind Hovered,
    UiCaptionButtonKind Pressed);

public interface IPlatformThemeProvider
{
    UiSystemColorScheme ColorScheme { get; }
}

public interface IWin32PlatformBackend
{
    nint WindowHandle { get; }
}

public interface IWindowIconProvider
{
    UiImageData GetWindowIconImage();
}

public interface IWindowChromeController
{
    string WindowTitle { get; }
    bool CanMinimize { get; }
    bool CanMaximize { get; }
    bool IsMaximized { get; }

    void BeginWindowMove();
    void MinimizeWindow();
    void ToggleMaximizeWindow();
    void CloseWindow();
}

public sealed record class DuxelTrayOptions
{
    public bool Enabled { get; init; }
    public string? ToolTip { get; init; }
    public string? IconPath { get; init; }
    public ReadOnlyMemory<byte> IconData { get; init; }
    public bool HideWindowOnMinimize { get; init; }
    public bool HideWindowOnClose { get; init; }
    public Action? DoubleClick { get; init; }
    public IReadOnlyList<DuxelTrayMenuItem> MenuItems { get; init; } = [];
}

public sealed record class DuxelTrayMenuItem
{
    public string Text { get; init; } = string.Empty;
    public bool IsSeparator { get; init; }
    public bool Enabled { get; init; } = true;
    public Action? Invoked { get; init; }
}

