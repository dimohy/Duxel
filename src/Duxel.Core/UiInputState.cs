using System.Collections.Generic;

namespace Duxel.Core;

public readonly record struct UiInputState(
    UiVector2 MousePosition,
    bool LeftMouseDown,
    bool LeftMousePressed,
    bool LeftMouseReleased,
    float MouseWheel,
    float MouseWheelHorizontal,
    IReadOnlyList<UiKeyEvent> KeyEvents,
    IReadOnlyList<UiCharEvent> CharEvents,
    UiKeyRepeatSettings KeyRepeatSettings
);

public readonly record struct UiIO(
    UiVector2 DisplaySize,
    UiVector2 DisplayFramebufferScale,
    float DeltaTime,
    UiVector2 MousePosition,
    bool LeftMouseDown,
    bool LeftMousePressed,
    bool LeftMouseReleased,
    float MouseWheel,
    float MouseWheelHorizontal,
    IReadOnlyList<UiKeyEvent> KeyEvents,
    IReadOnlyList<UiCharEvent> CharEvents,
    UiKeyRepeatSettings KeyRepeatSettings,
    KeyModifiers Modifiers
);

public readonly record struct UiPlatformIO(
    IUiClipboard? Clipboard,
    IUiImeHandler? ImeHandler
);

