using System.Collections.Generic;

namespace Duxel.Core;

/// <summary>An immutable captured input operation dispatched once on the UI consumer thread.</summary>
public interface IUiInputDispatch
{
    IUiImeHandler? RequiredImeHandler => null;
    void Dispatch();
}

public readonly record struct UiInputState(
    UiVector2 MousePosition,
    bool LeftMouseDown,
    bool RightMouseDown,
    bool LeftMousePressed,
    bool LeftMouseReleased,
    bool RightMousePressed,
    bool RightMouseReleased,
    float MouseWheel,
    float MouseWheelHorizontal,
    IReadOnlyList<UiKeyEvent> KeyEvents,
    IReadOnlyList<UiCharEvent> CharEvents,
    UiKeyRepeatSettings KeyRepeatSettings,
    KeyModifiers Modifiers
)
{
    public bool MiddleMouseDown
    {
        get; init;
    }
    public bool MiddleMousePressed
    {
        get; init;
    }
    public bool MiddleMouseReleased
    {
        get; init;
    }
    public IUiInputDispatch? Dispatch
    {
        get; init;
    }
    /// <summary>Native capture-time pixel scale; App clears it after converting to logical coordinates.</summary>
    public float? CoordinateScale
    {
        get; init;
    }
    /// <summary>Event time in seconds on a stable input clock; independent of animation/frame time.</summary>
    public double? EventTimeSeconds
    {
        get; init;
    }
}

public readonly record struct UiIO(
    UiVector2 DisplaySize,
    UiVector2 DisplayFramebufferScale,
    float DeltaTime,
    UiVector2 MousePosition,
    bool LeftMouseDown,
    bool RightMouseDown,
    bool LeftMousePressed,
    bool LeftMouseReleased,
    bool RightMousePressed,
    bool RightMouseReleased,
    float MouseWheel,
    float MouseWheelHorizontal,
    IReadOnlyList<UiKeyEvent> KeyEvents,
    IReadOnlyList<UiCharEvent> CharEvents,
    UiKeyRepeatSettings KeyRepeatSettings,
    KeyModifiers Modifiers
)
{
    public bool MiddleMouseDown
    {
        get; init;
    }
    public bool MiddleMousePressed
    {
        get; init;
    }
    public bool MiddleMouseReleased
    {
        get; init;
    }
}

public readonly record struct UiPlatformIO(
    IUiClipboard? Clipboard,
    IUiImeHandler? ImeHandler
);

