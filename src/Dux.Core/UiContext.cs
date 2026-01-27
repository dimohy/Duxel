using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dux.Core;

public sealed class UiContext : IUiContext
{
    private readonly UiState _state = new();
    private UiFontAtlas _fontAtlas;
    private readonly UiTextureId _fontTexture;
    private readonly UiTextureId _whiteTexture;
    private readonly List<UiTextureUpdate> _textureUpdates = new();

    private UiFrameInfo _frameInfo;
    private UiInputState _inputState;
    private UiKeyRepeatSettings _keyRepeatSettings;
    private IUiImeHandler? _imeHandler;
    private UiTextSettings _textSettings = UiTextSettings.Default;
    private UiRect _clipRect;
    private UiVector2 _displaySize;
    private UiVector2 _framebufferScale;
    private UiTheme _theme = UiTheme.ImGuiDark;
    private UiStyle _style = UiStyle.Default;
    private IUiClipboard? _clipboard;
    private bool _hasFrame;
    private bool _hasInput;
    private bool _hasClip;
    private bool _hasTextSettings;
    private bool _hasDrawData;
    private UiDrawData? _drawData;
    private UiScreen? _screen;
    private int _reserveVertices;
    private int _reserveIndices;
    private int _reserveCommands;
    private readonly StringBuilder _logBuffer = new();
    private bool _logActive;
    private UiLogTarget _logTarget;
    private string? _logFilePath;
    private string _iniSettings = string.Empty;
    private UiMemAlloc _memAlloc = static size => new byte[size];
    private UiMemFree _memFree = static _ => { };
    private readonly List<UiKeyEvent> _queuedKeyEvents = new();
    private readonly List<UiCharEvent> _queuedCharEvents = new();
    private UiVector2? _queuedMousePos;
    private bool? _queuedMouseDown;
    private float _queuedMouseWheel;
    private float _queuedMouseWheelHorizontal;
    private bool _appAcceptingEvents = true;
    private bool _previousInjectedLeftDown;
    private char? _pendingHighSurrogate;

    private enum UiLogTarget
    {
        None,
        Tty,
        Clipboard,
        File,
    }

    public UiContext(
        UiFontAtlas fontAtlas,
        UiTextureId fontTexture,
        UiTextureId whiteTexture
    )
    {
        _fontAtlas = fontAtlas ?? throw new ArgumentNullException(nameof(fontAtlas));
        _fontTexture = fontTexture;
        _whiteTexture = whiteTexture;
    }

    public UiState State => _state;

    public UiTheme GetStyle()
    {
        return _theme;
    }

    public UiStyle GetStyleData() => _style;

    public void SetStyle(UiStyle style)
    {
        _style = style ?? throw new ArgumentNullException(nameof(style));
    }

    public void SetScreen(UiScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    public void SetInput(UiInputState input)
    {
        if (_appAcceptingEvents)
        {
            input = ApplyQueuedInput(input);
        }

        _inputState = input;
        _hasInput = true;
        _state.UpdateInput(input.KeyEvents);
        _keyRepeatSettings = input.KeyRepeatSettings;
    }

    public void SetClipRect(UiRect clipRect)
    {
        _clipRect = clipRect;
        _hasClip = true;
    }

    public void SetTextSettings(UiTextSettings settings)
    {
        _textSettings = settings;
        _hasTextSettings = true;
    }

    public void SetClipboard(IUiClipboard clipboard)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
    }

    public void SetFontAtlas(UiFontAtlas fontAtlas)
    {
        _fontAtlas = fontAtlas ?? throw new ArgumentNullException(nameof(fontAtlas));
    }

    public void SetTheme(UiTheme theme)
    {
        _theme = theme;
    }

    public void StyleColorsDark()
    {
        _theme = UiTheme.ImGuiDark;
    }

    public void StyleColorsLight()
    {
        _theme = UiTheme.ImGuiLight;
    }

    public void StyleColorsClassic()
    {
        _theme = UiTheme.ImGuiClassic;
    }

    public void SetImeHandler(IUiImeHandler imeHandler)
    {
        _imeHandler = imeHandler ?? throw new ArgumentNullException(nameof(imeHandler));
    }

    public void BeginFrame(UiFrameInfo frameInfo, UiInputState input, UiRect clipRect, UiTextSettings settings)
    {
        NewFrame(frameInfo, input, clipRect, settings);
    }

    public void NewFrame(UiFrameInfo frameInfo, UiInputState input, UiRect clipRect, UiTextSettings settings)
    {
        SetInput(input);
        SetClipRect(clipRect);
        SetTextSettings(settings);
        NewFrame(frameInfo);
    }

    public void QueueTextureUpdate(UiTextureUpdate update)
    {
        if (_textureUpdates.Count > 0)
        {
            var lastIndex = _textureUpdates.Count - 1;
            var last = _textureUpdates[lastIndex];
            if (last.TextureId.Equals(update.TextureId) && last.Kind == update.Kind)
            {
                _textureUpdates[lastIndex] = update;
                return;
            }
        }

        _textureUpdates.Add(update);
    }

    public void NewFrame(UiFrameInfo frameInfo)
    {
        _state.AdvanceTime(frameInfo.DeltaTime);
        _state.BeginFrame();
        if (_state.TryConsumeTheme(out var theme))
        {
            _theme = theme;
        }
        _frameInfo = frameInfo;
        _displaySize = frameInfo.DisplaySize;
        _framebufferScale = frameInfo.DisplayFramebufferScale;
        _hasFrame = true;
        _hasDrawData = false;
        _drawData = null;
    }

    public void EndFrame()
    {
        if (!_hasFrame)
        {
            throw new InvalidOperationException("NewFrame must be called before EndFrame.");
        }

        _state.EndFrame();
        _hasFrame = false;
        _hasDrawData = false;
        _drawData = null;
    }

    public UiIO GetIO()
    {
        if (!_hasFrame)
        {
            throw new InvalidOperationException("NewFrame must be called before GetIO.");
        }

        if (!_hasInput)
        {
            throw new InvalidOperationException("Input must be set before GetIO.");
        }

        return new UiIO(
            _displaySize,
            _framebufferScale,
            _frameInfo.DeltaTime,
            _inputState.MousePosition,
            _inputState.LeftMouseDown,
            _inputState.LeftMousePressed,
            _inputState.LeftMouseReleased,
            _inputState.MouseWheel,
            _inputState.MouseWheelHorizontal,
            _inputState.KeyEvents,
            _inputState.CharEvents,
            _keyRepeatSettings,
            _state.Modifiers
        );
    }

    public UiPlatformIO GetPlatformIO()
    {
        return new UiPlatformIO(_clipboard, _imeHandler);
    }

    public void LogToTTY()
    {
        StartLog(UiLogTarget.Tty, null);
    }

    public void LogToClipboard()
    {
        StartLog(UiLogTarget.Clipboard, null);
    }

    public void LogToFile(string? filePath = null)
    {
        StartLog(UiLogTarget.File, string.IsNullOrWhiteSpace(filePath) ? "imgui_log.txt" : filePath);
    }

    public void LogButtons()
    {
        // UI helper in ImGui; no-op here (logging is controlled via explicit LogTo* calls).
    }

    public void LogText(string text)
    {
        if (!_logActive)
        {
            return;
        }

        _logBuffer.Append(text ?? string.Empty);
    }

    public void LogTextV(string text)
    {
        LogText(text);
    }

    public void LogFinish()
    {
        if (!_logActive)
        {
            return;
        }

        var payload = _logBuffer.ToString();
        _logBuffer.Clear();
        _logActive = false;

        switch (_logTarget)
        {
            case UiLogTarget.Tty:
                if (!string.IsNullOrEmpty(payload))
                {
                    Console.Write(payload);
                }
                break;
            case UiLogTarget.Clipboard:
                if (_clipboard is null)
                {
                    throw new InvalidOperationException("Clipboard is not configured.");
                }
                _clipboard.SetText(payload);
                break;
            case UiLogTarget.File:
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    throw new InvalidOperationException("Log file path is not configured.");
                }
                File.WriteAllText(_logFilePath, payload);
                break;
            case UiLogTarget.None:
            default:
                break;
        }
    }

    public void DebugTextEncoding(string text)
    {
        text ??= string.Empty;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsValid(rune.Value))
            {
                throw new ArgumentException("Invalid Unicode code point detected.", nameof(text));
            }
        }
    }

    public void DebugFlashStyleColor()
    {
        _state.LogDebug("DebugFlashStyleColor invoked.");
    }

    public void DebugStartItemPicker()
    {
        _state.LogDebug("DebugStartItemPicker invoked.");
    }

    public bool DebugCheckVersionAndDataLayout()
    {
        return true;
    }

    public void DebugLog(string text)
    {
        _state.LogDebug(text);
    }

    public void DebugLogV(string text)
    {
        DebugLog(text);
    }

    public void ClearPlatformHandlers()
    {
        _clipboard = null;
        _imeHandler = null;
    }

    public void ClearRendererHandlers()
    {
        _textureUpdates.Clear();
    }

    public void SetAllocatorFunctions(UiMemAlloc allocFunc, UiMemFree freeFunc)
    {
        _memAlloc = allocFunc ?? throw new ArgumentNullException(nameof(allocFunc));
        _memFree = freeFunc ?? throw new ArgumentNullException(nameof(freeFunc));
    }

    public void GetAllocatorFunctions(out UiMemAlloc allocFunc, out UiMemFree freeFunc)
    {
        allocFunc = _memAlloc;
        freeFunc = _memFree;
    }

    public byte[] MemAlloc(int size)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        return _memAlloc(size);
    }

    public void MemFree(byte[] block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _memFree(block);
    }

    public void LoadIniSettingsFromDisk(string iniFilename)
    {
        if (string.IsNullOrWhiteSpace(iniFilename))
        {
            throw new ArgumentException("INI filename must not be empty.", nameof(iniFilename));
        }

        _iniSettings = File.ReadAllText(iniFilename);
    }

    public void LoadIniSettingsFromMemory(string iniData)
    {
        _iniSettings = iniData ?? string.Empty;
    }

    public void SaveIniSettingsToDisk(string iniFilename)
    {
        if (string.IsNullOrWhiteSpace(iniFilename))
        {
            throw new ArgumentException("INI filename must not be empty.", nameof(iniFilename));
        }

        File.WriteAllText(iniFilename, _iniSettings);
    }

    public string SaveIniSettingsToMemory()
    {
        return _iniSettings;
    }

    public void AddKeyEvent(UiKey key, bool down)
    {
        if (!_appAcceptingEvents)
        {
            return;
        }

        _queuedKeyEvents.Add(new UiKeyEvent(key, down, _state.Modifiers));
    }

    public void AddKeyAnalogEvent(UiKey key, bool down, float value)
    {
        _ = value;
        AddKeyEvent(key, down);
    }

    public void AddMousePosEvent(float x, float y)
    {
        if (!_appAcceptingEvents)
        {
            return;
        }

        _queuedMousePos = new UiVector2(x, y);
    }

    public void AddMouseButtonEvent(int button, bool down)
    {
        if (!_appAcceptingEvents)
        {
            return;
        }

        if (button == (int)UiMouseButton.Left)
        {
            _queuedMouseDown = down;
        }
    }

    public void AddMouseWheelEvent(float wheelX, float wheelY)
    {
        if (!_appAcceptingEvents)
        {
            return;
        }

        _queuedMouseWheelHorizontal += wheelX;
        _queuedMouseWheel += wheelY;
    }

    public void AddMouseSourceEvent()
    {
        // Source is not tracked in Dux.
    }

    public void AddFocusEvent(bool focused)
    {
        if (!focused)
        {
            _state.ActiveId = null;
            _state.FocusedId = null;
        }
    }

    public void AddInputCharacter(uint c)
    {
        if (!_appAcceptingEvents)
        {
            return;
        }

        _queuedCharEvents.Add(new UiCharEvent(c));
    }

    public void AddInputCharacterUTF16(char c)
    {
        if (!_appAcceptingEvents)
        {
            return;
        }

        if (char.IsHighSurrogate(c))
        {
            _pendingHighSurrogate = c;
            return;
        }

        if (char.IsLowSurrogate(c) && _pendingHighSurrogate is { } high)
        {
            var codepoint = (uint)char.ConvertToUtf32(high, c);
            _pendingHighSurrogate = null;
            AddInputCharacter(codepoint);
            return;
        }

        _pendingHighSurrogate = null;
        AddInputCharacter(c);
    }

    public void AddInputCharactersUTF8(string utf8Text)
    {
        if (!_appAcceptingEvents)
        {
            return;
        }

        utf8Text ??= string.Empty;
        foreach (var rune in utf8Text.EnumerateRunes())
        {
            AddInputCharacter((uint)rune.Value);
        }
    }

    public void SetKeyEventNativeData(UiKey key, int nativeKeycode, int nativeScancode, int nativeLegacyIndex)
    {
        _ = key;
        _ = nativeKeycode;
        _ = nativeScancode;
        _ = nativeLegacyIndex;
    }

    public void SetAppAcceptingEvents(bool acceptingEvents)
    {
        _appAcceptingEvents = acceptingEvents;
        if (!acceptingEvents)
        {
            ClearEventsQueue();
        }
    }

    public void ClearEventsQueue()
    {
        _queuedKeyEvents.Clear();
        _queuedCharEvents.Clear();
        _queuedMousePos = null;
        _queuedMouseDown = null;
        _queuedMouseWheel = 0f;
        _queuedMouseWheelHorizontal = 0f;
        _pendingHighSurrogate = null;
    }

    public void ClearInputKeys()
    {
        _state.ClearInputKeys();
    }

    public void ClearInputMouse()
    {
        _queuedMousePos = null;
        _queuedMouseDown = null;
        _queuedMouseWheel = 0f;
        _queuedMouseWheelHorizontal = 0f;

        if (_hasInput)
        {
            _inputState = _inputState with
            {
                LeftMouseDown = false,
                LeftMousePressed = false,
                LeftMouseReleased = false,
                MouseWheel = 0f,
                MouseWheelHorizontal = 0f
            };
        }
    }

    private void StartLog(UiLogTarget target, string? filePath)
    {
        _logTarget = target;
        _logFilePath = filePath;
        _logBuffer.Clear();
        _logActive = true;
    }

    private UiInputState ApplyQueuedInput(UiInputState input)
    {
        var mousePos = _queuedMousePos ?? input.MousePosition;
        var leftDown = _queuedMouseDown ?? input.LeftMouseDown;
        var leftPressed = input.LeftMousePressed;
        var leftReleased = input.LeftMouseReleased;

        if (_queuedMouseDown.HasValue)
        {
            leftPressed = leftDown && !_previousInjectedLeftDown;
            leftReleased = !leftDown && _previousInjectedLeftDown;
            _previousInjectedLeftDown = leftDown;
        }
        else
        {
            _previousInjectedLeftDown = input.LeftMouseDown;
        }

        var mouseWheel = input.MouseWheel + _queuedMouseWheel;
        var mouseWheelHorizontal = input.MouseWheelHorizontal + _queuedMouseWheelHorizontal;

        var keyEvents = input.KeyEvents;
        if (_queuedKeyEvents.Count > 0)
        {
            var merged = new UiKeyEvent[keyEvents.Count + _queuedKeyEvents.Count];
            for (var i = 0; i < keyEvents.Count; i++)
            {
                merged[i] = keyEvents[i];
            }
            for (var i = 0; i < _queuedKeyEvents.Count; i++)
            {
                merged[keyEvents.Count + i] = _queuedKeyEvents[i];
            }
            keyEvents = merged;
        }

        var charEvents = input.CharEvents;
        if (_queuedCharEvents.Count > 0)
        {
            var merged = new UiCharEvent[charEvents.Count + _queuedCharEvents.Count];
            for (var i = 0; i < charEvents.Count; i++)
            {
                merged[i] = charEvents[i];
            }
            for (var i = 0; i < _queuedCharEvents.Count; i++)
            {
                merged[charEvents.Count + i] = _queuedCharEvents[i];
            }
            charEvents = merged;
        }

        _queuedKeyEvents.Clear();
        _queuedCharEvents.Clear();
        _queuedMousePos = null;
        _queuedMouseDown = null;
        _queuedMouseWheel = 0f;
        _queuedMouseWheelHorizontal = 0f;

        return new UiInputState(
            mousePos,
            leftDown,
            leftPressed,
            leftReleased,
            mouseWheel,
            mouseWheelHorizontal,
            keyEvents,
            charEvents,
            input.KeyRepeatSettings
        );
    }

    public void Render()
    {
        if (_screen is null)
        {
            throw new InvalidOperationException("Screen is not set.");
        }

        Render(_screen);
    }

    public void Render(UiScreen screen)
    {
        if (!_hasFrame)
        {
            throw new InvalidOperationException("NewFrame must be called before Render.");
        }

        if (!_hasInput)
        {
            throw new InvalidOperationException("Input must be set before Render.");
        }

        if (!_hasClip)
        {
            throw new InvalidOperationException("ClipRect must be set before Render.");
        }

        if (!_hasTextSettings)
        {
            throw new InvalidOperationException("TextSettings must be set before Render.");
        }

        if (screen is null)
        {
            throw new ArgumentNullException(nameof(screen));
        }

        var ui = new UiImmediateContext(
            _state,
            _fontAtlas,
            _textSettings,
            _fontAtlas.LineHeight,
            _fontTexture,
            _whiteTexture,
            _theme,
            _style,
            _clipRect,
            _inputState.MousePosition,
            _inputState.LeftMouseDown,
            _inputState.LeftMousePressed,
            _inputState.LeftMouseReleased,
            _inputState.MouseWheel,
            _inputState.MouseWheelHorizontal,
            _inputState.KeyEvents,
            _inputState.CharEvents,
            _clipboard,
            _displaySize,
            _keyRepeatSettings,
            _imeHandler,
            _reserveVertices,
            _reserveIndices,
            _reserveCommands
        );

        screen.Render(ui);
        _state.EndFrame();
        var drawLists = ui.BuildDrawLists();
        var totalVertexCount = 0;
        var totalIndexCount = 0;
        var totalCommandCount = 0;
        for (var i = 0; i < drawLists.Count; i++)
        {
            var list = drawLists[i];
            totalVertexCount += list.Vertices.Count;
            totalIndexCount += list.Indices.Count;
            totalCommandCount += list.Commands.Count;
        }

        _reserveVertices = Math.Max(256, totalVertexCount + (totalVertexCount >> 2));
        _reserveIndices = Math.Max(256, totalIndexCount + (totalIndexCount >> 2));
        _reserveCommands = Math.Max(32, totalCommandCount + (totalCommandCount >> 2));

        _drawData = new UiDrawData(
            _displaySize,
            new UiVector2(0, 0),
            _framebufferScale,
            totalVertexCount,
            totalIndexCount,
            drawLists,
            UiPooledList<UiTextureUpdate>.FromArray(_textureUpdates.ToArray())
        );


        _textureUpdates.Clear();
        _hasDrawData = true;
    }


    public UiDrawData GetDrawData()
    {
        if (!_hasDrawData || _drawData is null)
        {
            throw new InvalidOperationException("Render must be called before GetDrawData.");
        }

        return _drawData;
    }

    public void Dispose()
    {
        _textureUpdates.Clear();
        _state.ReleasePooledBuffers();
        _hasDrawData = false;
        _hasFrame = false;
    }
}
