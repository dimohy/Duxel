using Dux.Core;

namespace Dux.Core;

public readonly record struct PlatformSize(int Width, int Height);

public readonly record struct InputSnapshot(
    UiVector2 MousePosition,
    bool LeftMouseDown,
    bool RightMouseDown,
    bool MiddleMouseDown,
    float MouseWheel,
    float MouseWheelHorizontal,
    IReadOnlyList<UiKeyEvent> KeyEvents,
    IReadOnlyList<UiCharEvent> CharEvents,
    UiKeyRepeatSettings KeyRepeatSettings
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
    bool ShouldClose { get; }
    double TimeSeconds { get; }
    IInputBackend Input { get; }
    IVulkanSurfaceSource? VulkanSurface { get; }

    void PollEvents();
    void SetMouseCursor(UiMouseCursor cursor);
}

public interface IWin32PlatformBackend
{
    nint WindowHandle { get; }
}
