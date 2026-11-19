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
)
{
    /// <summary>Ordered complete input states in native client pixels. Null selects legacy aggregate
    /// input; an empty list selects ordered capture with no pending events. Empty ordered capture cannot
    /// publish aggregate transient events. Capture mode is fixed per session.</summary>
    public IReadOnlyList<UiInputState>? OrderedInputs
    {
        get; init;
    }
    /// <summary>Captured native top-level window activation; false preserves hosts without activation events.</summary>
    public bool IsWindowInactive { get; init; }
}

public interface IKeyRepeatSettingsProvider
{
    UiKeyRepeatSettings GetSettings();
}

public interface IInputBackend
{
    InputSnapshot Snapshot
    {
        get;
    }
}

public interface IVulkanSurfaceSource
{
    IReadOnlyList<string> RequiredInstanceExtensions
    {
        get;
    }
    nuint CreateSurface(nuint instanceHandle);
}

public interface IPlatformBackend : IDisposable
{
    PlatformSize WindowSize
    {
        get;
    }
    PlatformSize FramebufferSize
    {
        get;
    }
    float ContentScale => 1f;
    /// <summary>Native accessibility/desktop font scaling, independent of display DPI.</summary>
    float TextScale => 1f;
    bool IsInteractingResize
    {
        get;
    }
    bool ShouldClose
    {
        get;
    }
    double TimeSeconds
    {
        get;
    }
    IInputBackend Input
    {
        get;
    }
    IVulkanSurfaceSource? VulkanSurface
    {
        get;
    }
    IPlatformTextBackend? TextBackend
    {
        get;
    }
    IUiImeHandler? ImeHandler => null;
    IWindowTitleBarPlatform? WindowTitleBar => null;
    long InteractiveResizeSequence => 0;
    UiHostVisibility HostVisibility => default;

    void PollEvents();
    /// <summary>Waits for native events or an explicit wake, then dispatches pending events. Zero means an indefinite wait.</summary>
    void WaitEvents(int timeoutMilliseconds);
    void SetMouseCursor(UiMouseCursor cursor);
    void NotifyFirstFramePresented()
    {
    }
    void NotifyFramePresented(long interactiveResizeSequence)
    {
    }
    void CancelInteractiveResizeWait()
    {
    }
}

/// <summary>A host contract for retaining an already-presented native surface until its token changes.</summary>
public interface IPlatformPresentationInvalidationBackend
{
    /// <summary>Thread-safe monotonic token advanced whenever native contents require a fresh presentation.</summary>
    long PresentationInvalidationSequence
    {
        get;
    }
}

/// <summary>Thread-safe, latched wake for the owner thread's WaitEvents call,
/// including a signal issued before waiting begins.</summary>
public interface IPlatformEventWakeBackend
{
    void WakeEvents();
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
    UiForcedColorPalette? ForcedColors => null;
    UiSystemColorScheme ColorScheme
    {
        get;
    }
}

public interface IWin32PlatformBackend
{
    nint WindowHandle
    {
        get;
    }
}

public interface IWindowIconProvider
{
    UiImageData GetWindowIconImage();
}

/// <summary>Receives application window colors on the platform's owning event thread.</summary>
public interface IWindowThemeController
{
    void SetWindowColors(UiColor caption, UiColor text, UiColor border);
}

public interface IWindowChromeController
{
    string WindowTitle
    {
        get;
    }
    bool CanMinimize
    {
        get;
    }
    bool CanMaximize
    {
        get;
    }
    bool IsMaximized
    {
        get;
    }

    void BeginWindowMove();
    void MinimizeWindow();
    void ToggleMaximizeWindow();
    void CloseWindow();
}

public sealed record class DuxelTrayOptions
{
    public bool Enabled
    {
        get; init;
    }
    public string? ToolTip
    {
        get; init;
    }
    public string? IconPath
    {
        get; init;
    }
    public ReadOnlyMemory<byte> IconData
    {
        get; init;
    }
    public bool HideWindowOnMinimize
    {
        get; init;
    }
    public bool HideWindowOnClose
    {
        get; init;
    }
    public Action? DoubleClick
    {
        get; init;
    }
    public IReadOnlyList<DuxelTrayMenuItem> MenuItems { get; init; } = [];
}

public sealed record class DuxelTrayMenuItem
{
    public string Text { get; init; } = string.Empty;
    public bool IsSeparator
    {
        get; init;
    }
    public bool Enabled { get; init; } = true;
    public Action? Invoked
    {
        get; init;
    }
}

