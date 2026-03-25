using Duxel.Core;

namespace Duxel.Core;

public readonly record struct PlatformSize(int Width, int Height);

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

    void PollEvents();
    void WaitEvents(int timeoutMilliseconds);
    void SetMouseCursor(UiMouseCursor cursor);
}

public interface IWin32PlatformBackend
{
    nint WindowHandle { get; }
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

