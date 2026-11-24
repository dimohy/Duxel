using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Duxel.Core;

public sealed partial class UiContext(
    UiFontAtlas fontAtlas,
    UiTextureId fontTexture,
    UiTextureId whiteTexture
    ) : IUiContext
{
    private const string StaticLayerGeometryTagPrefix = "duxel.layer.static:";
    private const string StaticGlobalGeometryTagPrefix = "duxel.global.static:";
    private readonly UiState _state = new();
    private UiFontAtlas _fontAtlas = fontAtlas ?? throw new ArgumentNullException(nameof(fontAtlas));
    private readonly UiTextureId _fontTexture = fontTexture;
    private readonly UiTextureId _whiteTexture = whiteTexture;
    private readonly List<UiTextureUpdate> _textureUpdates = new();

    private UiFrameInfo _frameInfo;
    private UiInputState _inputState;
    private UiKeyRepeatSettings _keyRepeatSettings;
    private IUiImeHandler? _imeHandler;
    private UiTextSettings _textSettings = UiTextSettings.Default;
    private UiRect _clipRect;
    private UiVector2 _displaySize;
    private UiVector2 _framebufferScale;
    private UiTheme _theme = UiCompiledDesign.Default.Theme;
    private UiStyle _style = UiCompiledDesign.Default.Style;
    private UiStyle _baseStyle = UiCompiledDesign.Default.Style;
    private UiDesignTokens _designTokens = UiCompiledDesign.Default.Tokens;
    private UiControlSkins _controlSkins;
    private IUiClipboard? _clipboard;
    private bool _hasFrame;
    private bool _hasInput;
    private bool _hasClip;
    private bool _hasTextSettings;
    private bool _hasDrawData;
    private bool _drawDataClaimed;
    private ulong _drawDataFrameSequence;
    private UiDrawData? _drawData;
    private UiImmediateContext? _immediateContext;
    private long _textureContentRevision;
    private readonly Dictionary<UiTextureId, long> _textureContentVersions = [];
    private UiScreen? _screen;
    private UiAccessibilityTree? _accessibilityTree;
    public void SetAccessibilityTree(UiAccessibilityTree? tree)
    {
        _accessibilityTree?.SetRequestFrameCallback(null);
        _accessibilityTree = tree;
        tree?.SetRequestFrameCallback(_frameInvalidator.Request);
    }
    private readonly UiFrameInvalidator _frameInvalidator = new();
    private Func<float, UiFontResource?>? _resolveFontResource;
    private UiTypographyFonts? _typographyFonts;

    public void SetTypographyFonts(UiTypographyFonts fonts) => _typographyFonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
    private string? _directTextPrimaryFontPath;
    private string? _directTextSecondaryFontPath;
    private float _directTextBaseFontSize;
    private bool _directTextEnabled;
    private bool _directTextFallbackEnabled;
    private float _contentScale = 1f;
    private IPlatformTextBackend? _platformTextBackend;
    private IWindowTitleBarPlatform? _windowTitleBar;
    private UiImageTexture? _windowIcon;
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
    private bool? _queuedRightMouseDown;
    private bool? _queuedMiddleMouseDown;
    private float _queuedMouseWheel;
    private float _queuedMouseWheelHorizontal;
    private bool _appAcceptingEvents = true;
    private bool _previousInjectedLeftDown;
    private bool _previousInjectedRightDown;
    private bool _previousInjectedMiddleDown;
    private char? _pendingHighSurrogate;
    private List<UiKeyEvent>? _mergedKeyEventsBuffer;
    private List<UiCharEvent>? _mergedCharEventsBuffer;

    private enum UiLogTarget
    {
        None,
        Tty,
        Clipboard,
        File,
    }

    public UiState State => _state;

    public UiTheme GetStyle()
    {
        return _theme;
    }

    public UiStyle GetStyleData() => _style;

    public void SetStyle(UiStyle style)
    {
        _baseStyle = style ?? throw new ArgumentNullException(nameof(style));
        _style = _baseStyle;
    }

    public void SetDesign(UiCompiledDesign design)
    {
        _theme = design.Theme;
        SetStyle(design.Style);
        _designTokens = design.Tokens;
        _controlSkins = design.ControlSkins;
    }

    public void SetDesign<TDesign>()
        where TDesign : IUiDesign
    {
        SetDesign(TDesign.Create());
    }

    public void SetScreen(UiScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    public void SetInput(UiInputState input)
    {
        ValidateInputMetadata(input);
        if (_appAcceptingEvents)
        {
            input = ApplyQueuedInput(input);
        }

        SetInputState(input);
    }

    private void SetInputState(UiInputState input)
    {
        _inputState = input;
        _hasInput = true;
        _state.SetKeyRepeatSource(input.KeyRepeatSettings.Source);
        _state.UpdateInput(input.KeyEvents, input.Modifiers);
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

    public void SetFontResourceResolver(Func<float, UiFontResource?>? resolver)
    {
        _resolveFontResource = resolver;
    }

    public void SetRequestFrameCallback(Action? callback)
    {
        _frameInvalidator.Callback = callback;
    }

    public void SetDirectTextPrimaryFontPath(string? fontPath)
    {
        _directTextPrimaryFontPath = string.IsNullOrWhiteSpace(fontPath) ? null : fontPath;
    }

    public void SetDirectTextSecondaryFontPath(string? fontPath)
    {
        _directTextSecondaryFontPath = string.IsNullOrWhiteSpace(fontPath) ? null : fontPath;
    }

    public void SetDirectTextBaseFontSize(float size)
    {
        _directTextBaseFontSize = size;
    }

    public void SetDirectTextEnabled(bool enabled)
    {
        _directTextEnabled = enabled;
    }

    public bool GetDirectTextEnabled() => _directTextEnabled;

    public void SetDirectTextFallbackEnabled(bool enabled)
    {
        _directTextFallbackEnabled = enabled;
    }

    public bool GetDirectTextFallbackEnabled() => _directTextFallbackEnabled;

    public void SetContentScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        _contentScale = scale;
    }

    internal bool UseR8ParagraphCoverage { get; init; }
    internal bool UseProjectedGlyphText { get; set; }

    public void SetPlatformTextBackend(IPlatformTextBackend? textBackend)
    {
        _platformTextBackend = textBackend;
    }

    public void SetWindowTitleBarPlatform(IWindowTitleBarPlatform? windowTitleBar)
    {
        _windowTitleBar = windowTitleBar;
    }

    public void SetWindowIcon(UiImageTexture? windowIcon)
    {
        _windowIcon = windowIcon;
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
        _textureContentRevision++;
        UiImmediateContext.RecordTextureContentChange(_textureContentVersions, update, _textureContentRevision);
        if (_immediateContext is { } immediate)
            immediate.TextureContentRevision = _textureContentRevision;
        if (_textureUpdates.Count > 0)
        {
            var lastIndex = _textureUpdates.Count - 1;
            var last = _textureUpdates[lastIndex];
            if (last.TextureId.Equals(update.TextureId)
                && last.Kind == update.Kind
                && last.CoversEntireTexture
                && update.CoversEntireTexture)
            {
                if (!ReferenceEquals(last.PixelOwner, update.PixelOwner))
                    last.PixelOwner?.Dispose();
                if (!ReferenceEquals(last.PaintGroup, update.PaintGroup))
                    last.PaintGroup?.Dispose();
                _textureUpdates[lastIndex] = update with
                {
                    IsColorImage = last.IsColorImage || update.IsColorImage
                };
                return;
            }
        }

        _textureUpdates.Add(update);
    }

    public void NewFrame(UiFrameInfo frameInfo)
    {
        _inputBatch = null;
        _state.AdvanceTime(frameInfo.DeltaTime);
        _state.BeginFrame();
        if (_state.TryConsumeTheme(out var theme))
        {
            _theme = theme;
        }
        if (_frameInfo.PrefersReducedMotion != frameInfo.PrefersReducedMotion)
            _immediateContext?.MarkAllLayersDirty();
        _frameInfo = frameInfo;
        _displaySize = frameInfo.DisplaySize;
        _framebufferScale = frameInfo.DisplayFramebufferScale;
        _hasFrame = true;
        DiscardUnclaimedDrawData(preserveTextureUpdates: true);
    }

    public void EndFrame()
    {
        if (!_hasFrame)
        {
            throw new InvalidOperationException("NewFrame must be called before EndFrame.");
        }

        _state.EndFrame();
        _inputBatch = null;
        _hasFrame = false;
        DiscardUnclaimedDrawData(preserveTextureUpdates: true);
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
            _inputState.RightMouseDown,
            _inputState.LeftMousePressed,
            _inputState.LeftMouseReleased,
            _inputState.RightMousePressed,
            _inputState.RightMouseReleased,
            _inputState.MouseWheel,
            _inputState.MouseWheelHorizontal,
            _inputState.KeyEvents,
            _inputState.CharEvents,
            _keyRepeatSettings,
            _state.Modifiers
        )
        {
            MiddleMouseDown = _inputState.MiddleMouseDown,
            MiddleMousePressed = _inputState.MiddleMousePressed,
            MiddleMouseReleased = _inputState.MiddleMouseReleased,
        };
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
        DiscardPendingTextureUpdates();
    }

    private void DiscardPendingTextureUpdates()
    {
        foreach (var update in _textureUpdates)
        {
            update.PixelOwner?.Dispose();
            update.PaintGroup?.Dispose();
        }
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
            return;
        }

        if (button == (int)UiMouseButton.Right)
        {
            _queuedRightMouseDown = down;
        }
        if (button == (int)UiMouseButton.Middle)
        {
            _queuedMiddleMouseDown = down;
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
        // Source is not tracked in Duxel.
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
        _queuedRightMouseDown = null;
        _queuedMiddleMouseDown = null;
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
        _queuedRightMouseDown = null;
        _queuedMiddleMouseDown = null;
        _previousInjectedMiddleDown = false;
        _queuedMouseWheel = 0f;
        _queuedMouseWheelHorizontal = 0f;

        if (_hasInput)
        {
            _inputState = _inputState with
            {
                LeftMouseDown = false,
                RightMouseDown = false,
                LeftMousePressed = false,
                LeftMouseReleased = false,
                RightMousePressed = false,
                RightMouseReleased = false,
                MiddleMouseDown = false,
                MiddleMousePressed = false,
                MiddleMouseReleased = false,
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

    private static IReadOnlyList<T> MergeQueuedEvents<T>(IReadOnlyList<T> source, List<T> queued, ref List<T>? buffer)
    {
        if (queued.Count == 0)
            return source;

        var mergedCount = source.Count + queued.Count;
        buffer ??= new(mergedCount);
        if (buffer.Capacity < mergedCount)
            buffer.Capacity = mergedCount;
        if (!ReferenceEquals(source, buffer))
        {
            buffer.Clear();
            for (var i = 0; i < source.Count; i++)
                buffer.Add(source[i]);
        }
        for (var i = 0; i < queued.Count; i++)
            buffer.Add(queued[i]);
        return buffer;
    }

    private UiInputState ApplyQueuedInput(UiInputState input)
    {
        var mousePos = _queuedMousePos ?? input.MousePosition;
        var leftDown = _queuedMouseDown ?? input.LeftMouseDown;
        var rightDown = _queuedRightMouseDown ?? input.RightMouseDown;
        var middleDown = _queuedMiddleMouseDown ?? input.MiddleMouseDown;
        var leftPressed = input.LeftMousePressed;
        var leftReleased = input.LeftMouseReleased;
        var rightPressed = input.RightMousePressed;
        var rightReleased = input.RightMouseReleased;
        var middlePressed = input.MiddleMousePressed;
        var middleReleased = input.MiddleMouseReleased;
        if (_queuedMiddleMouseDown.HasValue)
        {
            middlePressed = middleDown && !_previousInjectedMiddleDown;
            middleReleased = !middleDown && _previousInjectedMiddleDown;
        }
        _previousInjectedMiddleDown = middleDown;

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

        if (_queuedRightMouseDown.HasValue)
        {
            rightPressed = rightDown && !_previousInjectedRightDown;
            rightReleased = !rightDown && _previousInjectedRightDown;
            _previousInjectedRightDown = rightDown;
        }
        else
        {
            _previousInjectedRightDown = input.RightMouseDown;
        }

        var mouseWheel = input.MouseWheel + _queuedMouseWheel;
        var mouseWheelHorizontal = input.MouseWheelHorizontal + _queuedMouseWheelHorizontal;

        var keyEvents = MergeQueuedEvents(input.KeyEvents, _queuedKeyEvents, ref _mergedKeyEventsBuffer);
        var charEvents = MergeQueuedEvents(input.CharEvents, _queuedCharEvents, ref _mergedCharEventsBuffer);

        _queuedKeyEvents.Clear();
        _queuedCharEvents.Clear();
        _queuedMousePos = null;
        _queuedMouseDown = null;
        _queuedRightMouseDown = null;
        _queuedMiddleMouseDown = null;
        _queuedMouseWheel = 0f;
        _queuedMouseWheelHorizontal = 0f;

        return input with
        {
            MousePosition = mousePos,
            LeftMouseDown = leftDown,
            RightMouseDown = rightDown,
            LeftMousePressed = leftPressed,
            LeftMouseReleased = leftReleased,
            RightMousePressed = rightPressed,
            RightMouseReleased = rightReleased,
            MouseWheel = mouseWheel,
            MouseWheelHorizontal = mouseWheelHorizontal,
            KeyEvents = keyEvents,
            CharEvents = charEvents,
            MiddleMouseDown = middleDown,
            MiddleMousePressed = middlePressed,
            MiddleMouseReleased = middleReleased
        };
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
        var measurement = new UiInputProcessingMeasurement(_state.InputProfilingEnabled, _inputBatch?.Count ?? 1);
        var completed = false;
        try
        {
            if (_inputBatch is not { } batch)
                RenderSingleInput(screen, ref measurement);
            else
                RenderInputBatch(screen, batch, ref measurement);
            completed = true;
        }
        finally
        {
            _state.InputProcessingMetrics = measurement.Finish(completed);
        }
    }

    internal void ProcessHiddenHostVisibility(UiHostVisibility visibility)
    {
        // A minimized host can lose its drawable extent before its tooltip lifecycle is observed.
        // Reuse the last layout extent for one input-only pass, without replaying input or submitting a frame.
        if (_screen is null || !_hasFrame || !visibility.IsHidden || visibility == _frameInfo.HostVisibility)
            return;

        SetInputState(new(new(float.MinValue, float.MinValue), false, false, false, false, false, false,
            0, 0, [], [], _inputState.KeyRepeatSettings, default));
        NewFrame(_frameInfo with { DeltaTime = 0, HostVisibility = visibility,
            IsWindowInactive = true, RepaintDiagnosticCleanup = false });
        var measurement = new UiInputProcessingMeasurement(false, 1);
        RenderSingleInput(_screen, ref measurement, UiRenderPhase.Input);
    }

    private void RenderSingleInput(UiScreen screen, ref UiInputProcessingMeasurement measurement,
        UiRenderPhase phase = UiRenderPhase.Presentation)
    {
        var preparationStart = measurement.StartStage();
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

        var ui = _immediateContext;
        var dispatch = _inputState.Dispatch;
        _state.ApplyInputTime(_inputState.EventTimeSeconds);
        _inputState = _inputState with
        {
            Dispatch = null,
            EventTimeSeconds = null
        };
        if (dispatch?.RequiredImeHandler is { } required && !ReferenceEquals(required, _imeHandler))
            throw new InvalidOperationException("Captured native input must be dispatched with its owning IME handler.");
        dispatch?.Dispatch();
        DiscardUnclaimedDrawData(preserveTextureUpdates: true);
        if (ui is null)
        {
            ui = new UiImmediateContext(
                _state,
                _fontAtlas,
                _textSettings,
                _fontAtlas.LineHeight,
                _fontTexture,
                _whiteTexture,
                _theme,
                _style,
                _designTokens,
                _clipRect,
                _inputState.MousePosition,
                _inputState.LeftMouseDown,
                _inputState.RightMouseDown,
                _inputState.LeftMousePressed,
                _inputState.LeftMouseReleased,
                _inputState.RightMousePressed,
                _inputState.RightMouseReleased,
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
                _reserveCommands,
                requestFrame: _frameInvalidator.Request,
                queueTextureUpdate: QueueTextureUpdate,
                resolveFontResource: _resolveFontResource,
                directTextPrimaryFontPath: _directTextPrimaryFontPath,
                directTextSecondaryFontPath: _directTextSecondaryFontPath,
                directTextEnabled: _directTextEnabled,
                directTextFallbackEnabled: _directTextFallbackEnabled,
                platformTextBackend: _platformTextBackend
            ) { UseR8ParagraphCoverage = UseR8ParagraphCoverage };
            _immediateContext = ui;
            ui.TextureContentRevision = _textureContentRevision;
            ui.TextureContentVersions = _textureContentVersions;
        }
        else
        {
            ui.ReinitializeFrame(
                _state,
                _fontAtlas,
                _textSettings,
                _fontAtlas.LineHeight,
                _theme,
                _style,
                _designTokens,
                _clipRect,
                _inputState.MousePosition,
                _inputState.LeftMouseDown,
                _inputState.RightMouseDown,
                _inputState.LeftMousePressed,
                _inputState.LeftMouseReleased,
                _inputState.RightMousePressed,
                _inputState.RightMouseReleased,
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
                _reserveCommands,
                QueueTextureUpdate,
                _resolveFontResource,
                _directTextPrimaryFontPath,
                _directTextSecondaryFontPath,
                _directTextEnabled,
                _directTextFallbackEnabled,
                _platformTextBackend
            );
        }

        ui.SetRenderPhase(phase);
        ui.UseProjectedGlyphText = UseProjectedGlyphText;
        ui.HostIdleWaitSeconds = _frameInfo.IdleWaitSeconds;
        ui.HostPrefersReducedMotion = _frameInfo.PrefersReducedMotion;
        ui.SetMiddleMouseState(_inputState.MiddleMouseDown, _inputState.MiddleMousePressed, _inputState.MiddleMouseReleased);
        ui.SetDirectTextBaseFontSize(_directTextBaseFontSize);
        ui.SetDirectTextEnabled(_directTextEnabled);
        ui.SetDirectTextFallbackEnabled(_directTextFallbackEnabled);
        ui.SetContentScale(_contentScale);
        ui.SetDisplayFramebufferScale(_framebufferScale);
        ui.SetWindowTitleBarPlatform(_windowTitleBar);
        ui.SetWindowIcon(_windowIcon);
        ui.SetDrawDataFrameId(checked(++_drawDataFrameSequence));
        ui.SetControlSkins(_controlSkins);
        ui.SetTypographyFonts(_typographyFonts);
        ui.IsHostWindowActive = !_frameInfo.IsWindowInactive;
        ui.BeginTooltipTargets(_frameInfo.HostVisibility);
        ui.BeginControlNavigation();
        ui.SetAccessibilityTree(_accessibilityTree);
        _accessibilityTree?.BeginFrame();
        measurement.EndPreparation(preparationStart);
        var compositionStart = measurement.StartStage();
        try
        {
            try { screen.Render(ui); }
            finally { ui.EndVectorTextParagraphBatch(); }
            ui.EndControlNavigation();
            ui.EndTooltipTargets();
            _state.EndFrame();
            ui.EndAccessibilityFrame();
        }
        catch
        {
            ui.CancelControlPointerFocus();
            ui.ResetLabelAccessibility();
            _accessibilityTree?.AbortFrame();
            ui.DiscardUnpublishedDrawing();
            throw;
        }
        measurement.EndComposition(compositionStart);
        if (phase == UiRenderPhase.Input)
        {
            ui.DiscardUnpublishedDrawing();
            return;
        }
        var finalizationStart = measurement.StartStage();
        var drawLists = ui.BuildDrawLists();
        var totalVertexCount = 0;
        var totalIndexCount = 0;
        var reserveVertexCount = 0;
        var reserveIndexCount = 0;
        var reserveCommandCount = 0;
        for (var i = 0; i < drawLists.Count; i++)
        {
            var list = drawLists[i];
            totalVertexCount += list.Vertices.Count;
            totalIndexCount += list.Indices.Count;
            // Retained lists borrow existing storage. Reserve only one owned
            // builder chunk, rather than duplicating the entire resident scene.
            if (list.OwnsBuffers)
            {
                reserveVertexCount = Math.Max(reserveVertexCount, list.Vertices.Count);
                reserveIndexCount = Math.Max(reserveIndexCount, list.Indices.Count);
                reserveCommandCount = Math.Max(reserveCommandCount, list.Commands.Count);
            }
        }

        _reserveVertices = Math.Max(256, reserveVertexCount + (reserveVertexCount >> 2));
        _reserveIndices = Math.Max(256, reserveIndexCount + (reserveIndexCount >> 2));
        _reserveCommands = Math.Max(32, reserveCommandCount + (reserveCommandCount >> 2));

        _drawData = new UiDrawData(
            _displaySize,
            new UiVector2(0, 0),
            _framebufferScale,
            totalVertexCount,
            totalIndexCount,
            drawLists,
            UiPooledList<UiTextureUpdate>.RentAndCopy(_textureUpdates),
            ComputeDynamicDirtyRect(drawLists, _clipRect, out var hasDynamicDirtyRect),
            hasDynamicDirtyRect
        )
        {
            FrameSceneLeases = ui.TakeFrameSceneLeases(),
            LayerFrameLease = ui.TakeLayerFrameLease(),
            DrawDataFrameId = ui.GetDrawDataFrameId(),
            RepaintHighlights = ui.RepaintHighlights,
            RepaintDiagnosticCleanup = _frameInfo.RepaintDiagnosticCleanup
        };


        _textureUpdates.Clear();
        _hasDrawData = true;
        measurement.EndFinalization(finalizationStart);
    }


    public UiDrawData GetDrawData()
    {
        if (!_hasDrawData || _drawData is null)
        {
            throw new InvalidOperationException("Render must be called before GetDrawData.");
        }

        _drawDataClaimed = true;
        return _drawData;
    }

    public void Dispose()
    {
        _inputBatch = null;
        _accessibilityTree?.SetRequestFrameCallback(null);
        _accessibilityTree = null;
        _state.ClearScheduledFrame();
        _frameInvalidator.Callback = null;
        DiscardUnclaimedDrawData();
        _immediateContext?.ReleasePresentationResources();
        _immediateContext?.ReleaseIdentifierResources();
        _immediateContext?.ReleaseControlNavigation();
        _immediateContext?.ReleaseControlTypographyResources();
        UiImmediateContext.ReleaseLayerResources(_state);
        _immediateContext?.ReleaseDirectTextPixelOwners();
        _immediateContext?.ReleaseOwnedImageTextures();
        DiscardPendingTextureUpdates();
        _textureContentVersions.Clear();
        _state.ReleasePooledBuffers();
        _hasDrawData = false;
        _hasFrame = false;
    }

    private void DiscardUnclaimedDrawData(bool preserveTextureUpdates = false)
    {
        if (!_drawDataClaimed && _drawData is { } data)
        {
            if (preserveTextureUpdates && data.TextureUpdates.Count > 0)
            {
                // Unclaimed frames have not crossed the producer boundary. Keep their
                // resource commands before newer pending commands, transferring ownership.
                var previous = data.TextureUpdates;
                var pendingCount = _textureUpdates.Count;
                _textureUpdates.EnsureCapacity(checked(previous.Count + pendingCount));
                for (var i = 0; i < previous.Count; i++)
                    _textureUpdates.Add(default);
                for (var i = pendingCount - 1; i >= 0; i--)
                    _textureUpdates[previous.Count + i] = _textureUpdates[i];
                for (var i = 0; i < previous.Count; i++)
                    _textureUpdates[i] = previous[i];
                previous.Return();
            }
            data.ReleasePooled();
        }
        _immediateContext?.ReleasePendingFrameSceneLeases();
        _drawData = null;
        _hasDrawData = false;
        _drawDataClaimed = false;
    }

    private UiRect ComputeDynamicDirtyRect(UiPooledList<UiDrawList> drawLists, UiRect fallbackRect, out bool hasDirtyRect)
    {
        var hasRect = false;
        var minX = 0f;
        var minY = 0f;
        var maxX = 0f;
        var maxY = 0f;

        for (var listIndex = 0; listIndex < drawLists.Count; listIndex++)
        {
            var drawList = drawLists[listIndex];
            if (drawList.Scene3D.HasValue)
            {
                // A camera can move static local geometry anywhere in the viewport.
                hasDirtyRect = true;
                return fallbackRect;
            }
            if (IsStaticGeometryTag(drawList.StaticGeometryKey))
            {
                continue;
            }

            var commandCount = drawList.Commands.Count;
            for (var commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                ref readonly var cmd = ref drawList.Commands.ItemRef(commandIndex);
                if (IsStaticGeometryTag(cmd.UserData))
                {
                    continue;
                }

                var translatedClip = new UiRect(
                    cmd.ClipRect.X + cmd.Translation.X,
                    cmd.ClipRect.Y + cmd.Translation.Y,
                    cmd.ClipRect.Width,
                    cmd.ClipRect.Height);

                if (translatedClip.Width > 0f && translatedClip.Height > 0f)
                {
                    if (!hasRect)
                    {
                        minX = translatedClip.X;
                        minY = translatedClip.Y;
                        maxX = translatedClip.X + translatedClip.Width;
                        maxY = translatedClip.Y + translatedClip.Height;
                        hasRect = true;
                    }
                    else
                    {
                        minX = MathF.Min(minX, translatedClip.X);
                        minY = MathF.Min(minY, translatedClip.Y);
                        maxX = MathF.Max(maxX, translatedClip.X + translatedClip.Width);
                        maxY = MathF.Max(maxY, translatedClip.Y + translatedClip.Height);
                    }
                }
            }
        }

        if (!hasRect)
        {
            hasDirtyRect = false;
            return default;
        }

        var clampedMinX = MathF.Max(fallbackRect.X, minX);
        var clampedMinY = MathF.Max(fallbackRect.Y, minY);
        var clampedMaxX = MathF.Min(fallbackRect.X + fallbackRect.Width, maxX);
        var clampedMaxY = MathF.Min(fallbackRect.Y + fallbackRect.Height, maxY);
        var width = MathF.Max(0f, clampedMaxX - clampedMinX);
        var height = MathF.Max(0f, clampedMaxY - clampedMinY);
        hasDirtyRect = width > 0f && height > 0f;
        return hasDirtyRect
            ? new UiRect(clampedMinX, clampedMinY, width, height)
            : default;
    }

    private bool IsStaticGeometryTag(object? userData)
    {
        return userData is string tag
            && (tag.StartsWith(StaticLayerGeometryTagPrefix, StringComparison.Ordinal)
                || tag.StartsWith(StaticGlobalGeometryTagPrefix, StringComparison.Ordinal));
    }
}

