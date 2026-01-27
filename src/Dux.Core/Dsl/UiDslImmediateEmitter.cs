using System;
using System.Collections.Generic;
using Dux.Core;

namespace Dux.Core.Dsl;

public sealed class UiDslRenderContext
{
    public UiDslRenderContext(
        UiDslState state,
        UiFontAtlas fontAtlas,
        UiTextSettings textSettings,
        float lineHeight,
        UiTextureId fontTexture,
        UiTextureId whiteTexture,
        UiTheme theme,
        UiStyle style,
        UiRect clipRect,
        UiVector2 mousePosition,
        bool leftMouseDown,
        bool leftMousePressed,
        bool leftMouseReleased,
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
        State = state ?? throw new ArgumentNullException(nameof(state));
        FontAtlas = fontAtlas;
        TextSettings = textSettings;
        LineHeight = lineHeight;
        FontTexture = fontTexture;
        WhiteTexture = whiteTexture;
        Theme = theme;
        Style = style;
        ClipRect = clipRect;
        MousePosition = mousePosition;
        LeftMouseDown = leftMouseDown;
        LeftMousePressed = leftMousePressed;
        LeftMouseReleased = leftMouseReleased;
        MouseWheel = mouseWheel;
        MouseWheelHorizontal = mouseWheelHorizontal;
        KeyEvents = keyEvents ?? throw new ArgumentNullException(nameof(keyEvents));
        CharEvents = charEvents ?? throw new ArgumentNullException(nameof(charEvents));
        Clipboard = clipboard;
        DisplaySize = displaySize;
        KeyRepeatSettings = keyRepeatSettings;
        ImeHandler = imeHandler;
        ReserveVertices = reserveVertices;
        ReserveIndices = reserveIndices;
        ReserveCommands = reserveCommands;
        EventSink = eventSink;
        ValueSource = valueSource;
    }

    public UiDslState State { get; }
    public UiFontAtlas FontAtlas { get; }
    public UiTextSettings TextSettings { get; }
    public float LineHeight { get; }
    public UiTextureId FontTexture { get; }
    public UiTextureId WhiteTexture { get; }
    public UiTheme Theme { get; }
    public UiStyle Style { get; }
    public UiRect ClipRect { get; }
    public UiVector2 MousePosition { get; }
    public bool LeftMouseDown { get; }
    public bool LeftMousePressed { get; }
    public bool LeftMouseReleased { get; }
    public float MouseWheel { get; }
    public float MouseWheelHorizontal { get; }
    public IReadOnlyList<UiKeyEvent> KeyEvents { get; }
    public IReadOnlyList<UiCharEvent> CharEvents { get; }
    public IUiClipboard? Clipboard { get; }
    public UiVector2 DisplaySize { get; }
    public UiKeyRepeatSettings KeyRepeatSettings { get; }
    public IUiImeHandler? ImeHandler { get; }
    public int ReserveVertices { get; }
    public int ReserveIndices { get; }
    public int ReserveCommands { get; }
    public IUiDslEventSink? EventSink { get; }
    public IUiDslValueSource? ValueSource { get; }
}

public sealed class UiDslImmediateEmitter : IUiDslEmitter
{
    private readonly UiDslRenderContext _ctx;
    private readonly UiImmediateContext _ui;
    private readonly UiDslRuntimeState _runtimeState = new();
    private int _skipDepth;

    public UiDslImmediateEmitter(UiDslRenderContext context)
    {
        _ctx = context;
        _ui = new UiImmediateContext(
            context.State.UiState,
            context.FontAtlas,
            context.TextSettings,
            context.LineHeight,
            context.FontTexture,
            context.WhiteTexture,
            context.Theme,
            context.Style,
            context.ClipRect,
            context.MousePosition,
            context.LeftMouseDown,
            context.LeftMousePressed,
            context.LeftMouseReleased,
            context.MouseWheel,
            context.MouseWheelHorizontal,
            context.KeyEvents,
            context.CharEvents,
            context.Clipboard,
            context.DisplaySize,
            context.KeyRepeatSettings,
            context.ImeHandler,
            context.ReserveVertices,
            context.ReserveIndices,
            context.ReserveCommands
        );
    }

    public UiPooledList<UiDrawList> BuildDrawLists() => _ui.BuildDrawLists();

    public void BeginNode(string name, IReadOnlyList<string> args)
    {
        if (_skipDepth > 0)
        {
            if (UiDslWidgetDispatcher.IsContainer(name))
            {
                _skipDepth++;
            }
            return;
        }

        var result = UiDslWidgetDispatcher.BeginOrInvoke(_ui, _ctx, _runtimeState, name, args);
        if (result == UiDslBeginResult.SkipChildren)
        {
            _skipDepth = 1;
        }
    }

    public void EndNode(string name)
    {
        if (_skipDepth > 0)
        {
            if (UiDslWidgetDispatcher.IsContainer(name))
            {
                _skipDepth--;
            }
            return;
        }

        UiDslWidgetDispatcher.End(_ui, _ctx, _runtimeState, name);
    }
}
