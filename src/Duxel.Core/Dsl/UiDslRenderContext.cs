using System;
using System.Collections.Generic;

namespace Duxel.Core.Dsl;

public sealed class UiDslRenderContext(
    UiDslState state,
    UiFontAtlas? fontAtlas,
    UiTextSettings textSettings,
    float lineHeight,
    UiTextureId fontTexture,
    UiTextureId whiteTexture,
    UiTheme theme,
    UiStyle style,
    UiRect clipRect,
    UiVector2 mousePosition,
    bool leftMouseDown,
    bool rightMouseDown,
    bool leftMousePressed,
    bool leftMouseReleased,
    bool rightMousePressed,
    bool rightMouseReleased,
    float mouseWheel,
    float mouseWheelHorizontal,
    IReadOnlyList<UiKeyEvent> keyEvents,
    IReadOnlyList<UiCharEvent> charEvents,
    IUiClipboard? clipboard,
    UiVector2 displaySize,
    UiKeyRepeatSettings keyRepeatSettings,
    IUiImeHandler? imeHandler,
    int reserveVertices,
    int reserveIndices,
    int reserveCommands,
    IUiDslEventSink? eventSink = null,
    IUiDslValueSource? valueSource = null
    )
{
    public UiDslState State { get; } = state ?? throw new ArgumentNullException(nameof(state));
    public UiFontAtlas? FontAtlas { get; } = fontAtlas;
    public UiTextSettings TextSettings { get; } = textSettings;
    public float LineHeight { get; } = lineHeight;
    public UiTextureId FontTexture { get; } = fontTexture;
    public UiTextureId WhiteTexture { get; } = whiteTexture;
    public UiTheme Theme { get; } = theme;
    public UiStyle Style { get; } = style;
    public UiRect ClipRect { get; } = clipRect;
    public UiVector2 MousePosition { get; } = mousePosition;
    public bool LeftMouseDown { get; } = leftMouseDown;
    public bool RightMouseDown { get; } = rightMouseDown;
    public bool LeftMousePressed { get; } = leftMousePressed;
    public bool LeftMouseReleased { get; } = leftMouseReleased;
    public bool RightMousePressed { get; } = rightMousePressed;
    public bool RightMouseReleased { get; } = rightMouseReleased;
    public float MouseWheel { get; } = mouseWheel;
    public float MouseWheelHorizontal { get; } = mouseWheelHorizontal;
    public IReadOnlyList<UiKeyEvent> KeyEvents { get; } = keyEvents ?? throw new ArgumentNullException(nameof(keyEvents));
    public IReadOnlyList<UiCharEvent> CharEvents { get; } = charEvents ?? throw new ArgumentNullException(nameof(charEvents));
    public IUiClipboard? Clipboard { get; } = clipboard;
    public UiVector2 DisplaySize { get; } = displaySize;
    public UiKeyRepeatSettings KeyRepeatSettings { get; } = keyRepeatSettings;
    public IUiImeHandler? ImeHandler { get; } = imeHandler;
    public int ReserveVertices { get; } = reserveVertices;
    public int ReserveIndices { get; } = reserveIndices;
    public int ReserveCommands { get; } = reserveCommands;
    public IUiDslEventSink? EventSink { get; } = eventSink;
    public IUiDslValueSource? ValueSource { get; } = valueSource;
}

