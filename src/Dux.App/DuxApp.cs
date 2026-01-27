using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dux.Core;
using Dux.Core.Dsl;
using Dux.Platform.Glfw;
using Dux.Platform.Windows;
using Dux.Vulkan;

namespace Dux.App;

public static class DuxApp
{
    public static void Run(DuxAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var window = options.Window;
        var rendererOptions = options.Renderer;
        var fontOptions = options.Font;
        var frameOptions = options.Frame;
        var theme = options.Theme;

        using var platform = new GlfwPlatformBackend(new GlfwPlatformBackendOptions(
            window.Width,
            window.Height,
            window.Title,
            window.VSync
        ));

        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(
            rendererOptions.MinImageCount,
            rendererOptions.EnableValidationLayers
        ));
        renderer.SetClearColor(theme.WindowBg);
        RenderStartupClear(renderer, platform);

        var glyphBuilder = new UiFontGlyphRangesBuilder();
        foreach (var range in fontOptions.InitialRanges)
        {
            glyphBuilder.AddRange(range.Start, range.End);
        }
        foreach (var text in fontOptions.InitialGlyphs)
        {
            glyphBuilder.AddText(text);
        }

        var fullCodepointSet = new HashSet<int>(glyphBuilder.BuildCodepoints());
        if (fullCodepointSet.Count == 0)
        {
            throw new InvalidOperationException("Font codepoints must not be empty.");
        }

        var activeCodepointSet = fullCodepointSet;
        Task<UiFontAtlas>? fullAtlasTask = null;
        UiFontAtlas fontAtlas;
        if (fontOptions.FastStartup)
        {
            var startupBuilder = new UiFontGlyphRangesBuilder();
            foreach (var range in fontOptions.StartupRanges)
            {
                startupBuilder.AddRange(range.Start, range.End);
            }
            foreach (var text in fontOptions.StartupGlyphs)
            {
                startupBuilder.AddText(text);
            }

            var startupCodepoints = startupBuilder.BuildCodepoints();
            if (startupCodepoints.Count > 0)
            {
                activeCodepointSet = new HashSet<int>(startupCodepoints);
            }

            if (fontOptions.UseBuiltInAsciiAtStartup)
            {
                fontAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(
                    fontOptions.StartupBuiltInScale,
                    fontOptions.StartupPadding,
                    fontOptions.StartupBuiltInColumns
                );
            }
            else
            {
                fontAtlas = BuildFontAtlas(fontOptions, activeCodepointSet, useStartupSettings: true);
            }

            var fullSnapshot = new HashSet<int>(fullCodepointSet);
            fullAtlasTask = Task.Run(() => BuildFontAtlas(fontOptions, fullSnapshot, useStartupSettings: false));
        }
        else
        {
            fontAtlas = BuildFontAtlas(fontOptions, fullCodepointSet, useStartupSettings: false);
        }
        var fontTextureId = options.FontTextureId;
        var whiteTextureId = options.WhiteTextureId;

        var whitePixels = new byte[] { 255, 255, 255, 255 };
        var whiteTextureUpdate = new UiTextureUpdate(
            UiTextureUpdateKind.Create,
            whiteTextureId,
            UiTextureFormat.Rgba8Unorm,
            1,
            1,
            new ReadOnlyMemory<byte>(whitePixels)
        );

        var fontTextureCreated = false;
        var fontTextureWidth = 0;
        var fontTextureHeight = 0;
        var fontTextureFormat = UiTextureFormat.Rgba8Unorm;
        var fontTextureDirty = true;
        var whiteTextureCreated = false;
        var previousLeftDown = false;
        var previousTime = 0d;
        var pendingGlyphs = 0;
        var lastFontRebuildTime = 0d;

        UiContext? uiContext = null;
        UiDslState? dslState = null;
        Action<IUiDslEmitter>? dslRender = null;
        IUiDslEventSink? dslEventSink = null;
        IUiDslValueSource? dslValueSource = null;
        var textureUpdates = new List<UiTextureUpdate>(4);
        var frameCounter = 0;
        var fpsSampleTime = 0f;
        var fpsSampleFrames = 0;
        var fps = 0f;

        IUiImeHandler? imeHandlerForContext = null;
        WindowsImeHandler? win32ImeHandler = null;
        QueuedImeHandler? queuedImeHandler = null;

        if (options.ImeHandler is not null)
        {
            imeHandlerForContext = options.ImeHandler;
        }
        else if (platform is IWin32PlatformBackend win32Platform)
        {
            win32ImeHandler = new WindowsImeHandler(win32Platform.WindowHandle);
            queuedImeHandler = new QueuedImeHandler();
            imeHandlerForContext = queuedImeHandler;
        }

        if (options.Dsl is { } dsl)
        {
            dslState = dsl.State ?? new UiDslState();
            dslRender = dsl.Render;
            dslEventSink = dsl.EventSink ?? dsl.Bindings;
            dslValueSource = dsl.ValueSource ?? dsl.Bindings;
        }
        else
        {
            uiContext = new UiContext(fontAtlas, fontTextureId, whiteTextureId);
            uiContext.SetTheme(theme);
            uiContext.SetScreen(options.Screen!);
            ConfigureContext(uiContext, options, platform, imeHandlerForContext);
            if (options.Debug.Log is { } log)
            {
                uiContext.State.DebugLog = log;
            }
        }

        static InputSnapshot CopySnapshot(InputSnapshot snapshot)
        {
            var keyCount = snapshot.KeyEvents.Count;
            var keyEvents = keyCount == 0 ? Array.Empty<UiKeyEvent>() : new UiKeyEvent[keyCount];
            for (var i = 0; i < keyCount; i++)
            {
                keyEvents[i] = snapshot.KeyEvents[i];
            }

            var charCount = snapshot.CharEvents.Count;
            var charEvents = charCount == 0 ? Array.Empty<UiCharEvent>() : new UiCharEvent[charCount];
            for (var i = 0; i < charCount; i++)
            {
                charEvents[i] = snapshot.CharEvents[i];
            }

            return new InputSnapshot(
                snapshot.MousePosition,
                snapshot.LeftMouseDown,
                snapshot.RightMouseDown,
                snapshot.MiddleMouseDown,
                snapshot.MouseWheel,
                snapshot.MouseWheelHorizontal,
                keyEvents,
                charEvents,
                snapshot.KeyRepeatSettings
            );
        }

        var snapshotLock = new object();
        var latestSnapshot = default(InputSnapshot);
        var latestWindowSize = new PlatformSize(window.Width, window.Height);
        var latestFramebufferSize = latestWindowSize;
        var hasSnapshot = false;
        var stopRequested = false;

        static IReadOnlyList<T> MergeEvents<T>(IReadOnlyList<T> current, IReadOnlyList<T> incoming)
        {
            if (current.Count == 0)
            {
                return incoming.Count == 0 ? Array.Empty<T>() : incoming;
            }

            if (incoming.Count == 0)
            {
                return current;
            }

            var merged = new T[current.Count + incoming.Count];
            for (var i = 0; i < current.Count; i++)
            {
                merged[i] = current[i];
            }
            for (var i = 0; i < incoming.Count; i++)
            {
                merged[current.Count + i] = incoming[i];
            }
            return merged;
        }

        static InputSnapshot MergeSnapshot(InputSnapshot current, InputSnapshot incoming)
        {
            return new InputSnapshot(
                incoming.MousePosition,
                incoming.LeftMouseDown,
                incoming.RightMouseDown,
                incoming.MiddleMouseDown,
                current.MouseWheel + incoming.MouseWheel,
                current.MouseWheelHorizontal + incoming.MouseWheelHorizontal,
                MergeEvents(current.KeyEvents, incoming.KeyEvents),
                MergeEvents(current.CharEvents, incoming.CharEvents),
                incoming.KeyRepeatSettings
            );
        }

        static InputSnapshot ClearEvents(InputSnapshot snapshot)
        {
            if (snapshot.KeyEvents.Count == 0 && snapshot.CharEvents.Count == 0 && snapshot.MouseWheel == 0f && snapshot.MouseWheelHorizontal == 0f)
            {
                return snapshot;
            }

            return snapshot with
            {
                KeyEvents = Array.Empty<UiKeyEvent>(),
                CharEvents = Array.Empty<UiCharEvent>(),
                MouseWheel = 0f,
                MouseWheelHorizontal = 0f
            };
        }

        void UpdateSnapshot()
        {
            var snapshot = CopySnapshot(platform.Input.Snapshot);
            var windowSize = platform.WindowSize;
            var framebufferSize = platform.FramebufferSize;
            lock (snapshotLock)
            {
                latestSnapshot = hasSnapshot ? MergeSnapshot(latestSnapshot, snapshot) : snapshot;
                latestWindowSize = windowSize;
                latestFramebufferSize = framebufferSize;
                hasSnapshot = true;
            }
        }

        UpdateSnapshot();

        var renderThread = new Thread(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            previousTime = stopwatch.Elapsed.TotalSeconds;
            lastFontRebuildTime = previousTime;

            while (!Volatile.Read(ref stopRequested))
            {
                InputSnapshot snapshot;
                PlatformSize windowSize;
                PlatformSize framebufferSize;
                var snapshotReady = false;
                lock (snapshotLock)
                {
                    snapshotReady = hasSnapshot;
                    snapshot = latestSnapshot;
                    windowSize = latestWindowSize;
                    framebufferSize = latestFramebufferSize;
                    if (hasSnapshot)
                    {
                        latestSnapshot = ClearEvents(latestSnapshot);
                    }
                }

                if (!snapshotReady)
                {
                    Thread.Yield();
                    continue;
                }

                var displaySize = new UiVector2(windowSize.Width, windowSize.Height);

                var scaleX = windowSize.Width > 0 ? (float)framebufferSize.Width / windowSize.Width : 1f;
                var scaleY = windowSize.Height > 0 ? (float)framebufferSize.Height / windowSize.Height : 1f;
                var framebufferScale = new UiVector2(scaleX, scaleY);

                var clipRect = new UiRect(0, 0, displaySize.X, displaySize.Y);
                var textSettings = new UiTextSettings(1f, frameOptions.LineHeightScale, frameOptions.PixelSnap, frameOptions.UseBaseline);

                var mouse = snapshot.MousePosition;
                var mouseX = scaleX > 0f ? mouse.X / scaleX : mouse.X;
                var mouseY = scaleY > 0f ? mouse.Y / scaleY : mouse.Y;

                var leftDown = snapshot.LeftMouseDown;
                var leftPressed = leftDown && !previousLeftDown;
                var leftReleased = !leftDown && previousLeftDown;
                previousLeftDown = leftDown;

                var input = new UiInputState(
                    new UiVector2(mouseX, mouseY),
                    leftDown,
                    leftPressed,
                    leftReleased,
                    snapshot.MouseWheel,
                    snapshot.MouseWheelHorizontal,
                    snapshot.KeyEvents,
                    snapshot.CharEvents,
                    snapshot.KeyRepeatSettings
                );

                var newlyAdded = 0;
                foreach (var charEvent in snapshot.CharEvents)
                {
                    if (charEvent.CodePoint <= 0xFFFFu && activeCodepointSet.Add((int)charEvent.CodePoint))
                    {
                        newlyAdded++;
                    }
                }
                pendingGlyphs += newlyAdded;

                var currentTime = stopwatch.Elapsed.TotalSeconds;
                var deltaTime = (float)(currentTime - previousTime);
                previousTime = currentTime;
                fpsSampleTime += deltaTime;
                fpsSampleFrames++;
                if (fpsSampleTime >= 0.5f)
                {
                    fps = fpsSampleFrames / fpsSampleTime;
                    fpsSampleTime = 0f;
                    fpsSampleFrames = 0;
                }

                if (fullAtlasTask is { IsCompleted: true })
                {
                    if (fullAtlasTask.IsFaulted)
                    {
                        throw new InvalidOperationException("Font atlas build task failed.", fullAtlasTask.Exception!);
                    }

                    fontAtlas = fullAtlasTask.Result;
                    if (uiContext is not null)
                    {
                        uiContext.SetFontAtlas(fontAtlas);
                    }

                    activeCodepointSet = fullCodepointSet;
                    fontTextureDirty = true;
                    fullAtlasTask = null;
                }

                if (pendingGlyphs > 0 && (pendingGlyphs >= frameOptions.FontRebuildBatchSize || (currentTime - lastFontRebuildTime) >= frameOptions.FontRebuildMinIntervalSeconds))
                {
                    var useStartupSettings = fontOptions.FastStartup && fullAtlasTask is not null;
                    fontAtlas = BuildFontAtlas(fontOptions, activeCodepointSet, useStartupSettings);
                    if (uiContext is not null)
                    {
                        uiContext.SetFontAtlas(fontAtlas);
                    }

                    fontTextureDirty = true;
                    pendingGlyphs = 0;
                    lastFontRebuildTime = currentTime;
                }

                var frameInfo = new UiFrameInfo(
                    deltaTime,
                    displaySize,
                    framebufferScale
                );

                if (uiContext is not null)
                {
                    uiContext.NewFrame(frameInfo, input, clipRect, textSettings);

                    if (!whiteTextureCreated)
                    {
                        uiContext.QueueTextureUpdate(whiteTextureUpdate);
                        whiteTextureCreated = true;
                    }

                    if (fontTextureDirty)
                    {
                        var mismatch = fontTextureCreated
                            && (fontTextureWidth != fontAtlas.Width || fontTextureHeight != fontAtlas.Height || fontTextureFormat != fontAtlas.Format);
                        if (mismatch)
                        {
                            uiContext.QueueTextureUpdate(new UiTextureUpdate(UiTextureUpdateKind.Destroy, fontTextureId, fontTextureFormat, fontTextureWidth, fontTextureHeight, ReadOnlyMemory<byte>.Empty));
                            fontTextureCreated = false;
                        }

                        var kind = fontTextureCreated ? UiTextureUpdateKind.Update : UiTextureUpdateKind.Create;
                        uiContext.QueueTextureUpdate(fontAtlas.CreateTextureUpdate(fontTextureId, kind));
                        fontTextureCreated = true;
                        fontTextureWidth = fontAtlas.Width;
                        fontTextureHeight = fontAtlas.Height;
                        fontTextureFormat = fontAtlas.Format;
                        fontTextureDirty = false;
                    }

                    uiContext.Render();
                    var drawData = uiContext.GetDrawData();
                    renderer.RenderDrawData(drawData);
                    EmitTrace(options.Debug, ref frameCounter, drawData, fps);
                    drawData.ReleasePooled();
                    continue;
                }

                if (!whiteTextureCreated)
                {
                    textureUpdates.Add(whiteTextureUpdate);
                    whiteTextureCreated = true;
                }

                if (fontTextureDirty)
                {
                    var mismatch = fontTextureCreated
                        && (fontTextureWidth != fontAtlas.Width || fontTextureHeight != fontAtlas.Height || fontTextureFormat != fontAtlas.Format);
                    if (mismatch)
                    {
                        textureUpdates.Add(new UiTextureUpdate(UiTextureUpdateKind.Destroy, fontTextureId, fontTextureFormat, fontTextureWidth, fontTextureHeight, ReadOnlyMemory<byte>.Empty));
                        fontTextureCreated = false;
                    }

                    var kind = fontTextureCreated ? UiTextureUpdateKind.Update : UiTextureUpdateKind.Create;
                    textureUpdates.Add(fontAtlas.CreateTextureUpdate(fontTextureId, kind));
                    fontTextureCreated = true;
                    fontTextureWidth = fontAtlas.Width;
                    fontTextureHeight = fontAtlas.Height;
                    fontTextureFormat = fontAtlas.Format;
                    fontTextureDirty = false;
                }

                var dslContext = new UiDslRenderContext(
                    dslState!,
                    fontAtlas,
                    textSettings,
                    fontAtlas.LineHeight,
                    fontTextureId,
                    whiteTextureId,
                    theme,
                    clipRect,
                    new UiVector2(mouseX, mouseY),
                    leftDown,
                    leftPressed,
                    displaySize,
                    dslEventSink,
                    dslValueSource
                );

                var emitter = new UiDslImmediateEmitter(dslContext);
                dslRender!(emitter);

                var drawLists = emitter.BuildDrawLists();
                var totalVertexCount = 0;
                var totalIndexCount = 0;
                for (var i = 0; i < drawLists.Count; i++)
                {
                    var list = drawLists[i];
                    totalVertexCount += list.Vertices.Count;
                    totalIndexCount += list.Indices.Count;
                }

                var dslDrawData = new UiDrawData(
                    displaySize,
                    new UiVector2(0, 0),
                    framebufferScale,
                    totalVertexCount,
                    totalIndexCount,
                    drawLists,
                    UiPooledList<UiTextureUpdate>.FromArray(textureUpdates.ToArray())
                );

                textureUpdates.Clear();
                renderer.RenderDrawData(dslDrawData);
                EmitTrace(options.Debug, ref frameCounter, dslDrawData, fps);
                dslDrawData.ReleasePooled();
            }
        })
        {
            IsBackground = true,
            Name = "DuxRender"
        };

        renderThread.Start();

        try
        {
            while (!platform.ShouldClose)
            {
                platform.PollEvents();
                if (queuedImeHandler is not null && win32ImeHandler is not null)
                {
                    queuedImeHandler.Flush(win32ImeHandler);
                }
                UpdateSnapshot();
            }
        }
        finally
        {
            Volatile.Write(ref stopRequested, true);
            renderThread.Join();
            uiContext?.Dispose();
        }
    }

    private static void EmitTrace(DuxDebugOptions debug, ref int frameCounter, UiDrawData drawData, float fps)
    {
        frameCounter++;
        var log = debug.Log;
        if (log is null)
        {
            return;
        }

        var interval = Math.Max(1, debug.LogEveryNFrames);
        if (frameCounter % interval != 0)
        {
            return;
        }

        var handler = new DefaultInterpolatedStringHandler(64, 6, CultureInfo.InvariantCulture);
        handler.AppendLiteral("Frame ");
        handler.AppendFormatted(frameCounter);
        handler.AppendLiteral(": FPS=");
        handler.AppendFormatted(fps, "0.0");
        handler.AppendLiteral(", DrawLists=");
        handler.AppendFormatted(drawData.DrawLists.Count);
        handler.AppendLiteral(", Vertices=");
        handler.AppendFormatted(drawData.TotalVertexCount);
        handler.AppendLiteral(", Indices=");
        handler.AppendFormatted(drawData.TotalIndexCount);
        handler.AppendLiteral(", Textures=");
        handler.AppendFormatted(drawData.TextureUpdates.Count);
        log(handler.ToStringAndClear());
    }

    private static UiFontAtlas BuildFontAtlas(DuxFontOptions options, HashSet<int> codepoints, bool useStartupSettings)
    {
        if (string.IsNullOrWhiteSpace(options.PrimaryFontPath))
        {
            throw new InvalidOperationException("Primary font path must be provided.");
        }

        if (!File.Exists(options.PrimaryFontPath))
        {
            throw new FileNotFoundException("Primary font file not found.", options.PrimaryFontPath);
        }

        if (!string.IsNullOrWhiteSpace(options.SecondaryFontPath) && !File.Exists(options.SecondaryFontPath))
        {
            throw new FileNotFoundException("Secondary font file not found.", options.SecondaryFontPath);
        }

        var ascii = new List<int>(codepoints.Count);
        var nonAscii = new List<int>();
        foreach (var codepoint in codepoints)
        {
            if (codepoint <= 0x7E)
            {
                ascii.Add(codepoint);
            }
            else
            {
                nonAscii.Add(codepoint);
            }
        }

        ascii.Sort();
        nonAscii.Sort();

        var fontSize = useStartupSettings ? options.StartupFontSize : options.FontSize;
        var atlasWidth = useStartupSettings ? options.StartupAtlasWidth : options.AtlasWidth;
        var atlasHeight = useStartupSettings ? options.StartupAtlasHeight : options.AtlasHeight;
        var padding = useStartupSettings ? options.StartupPadding : options.Padding;
        var oversample = useStartupSettings ? options.StartupOversample : options.Oversample;

        if (!string.IsNullOrWhiteSpace(options.SecondaryFontPath) && nonAscii.Count > 0)
        {
            var sources = new List<UiFontSource>
            {
                new(options.PrimaryFontPath, ascii, 0, 1f),
                new(options.SecondaryFontPath, nonAscii, 1, options.SecondaryScale)
            };

            return UiFontAtlasBuilder.CreateFromTtfMerged(
                sources,
                fontSize,
                atlasWidth,
                atlasHeight,
                padding,
                oversample
            );
        }

        return UiFontAtlasBuilder.CreateFromTtf(
            options.PrimaryFontPath,
            ascii,
            fontSize,
            atlasWidth,
            atlasHeight,
            padding,
            oversample
        );
    }

    private static void ConfigureContext(UiContext context, DuxAppOptions options, IPlatformBackend platform, IUiImeHandler? imeHandler)
    {
        context.SetTheme(options.Theme);
        if (options.Clipboard is not null)
        {
            context.SetClipboard(options.Clipboard);
        }
        else if (platform is IWin32PlatformBackend)
        {
            context.SetClipboard(new WindowsClipboard());
        }

        if (imeHandler is not null)
        {
            context.SetImeHandler(imeHandler);
        }
    }

    private readonly struct ImeRequest
    {
        public readonly UiRect CaretRect;
        public readonly UiRect InputRect;
        public readonly float FontPixelHeight;
        public readonly float FontPixelWidth;

        public ImeRequest(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            CaretRect = caretRect;
            InputRect = inputRect;
            FontPixelHeight = fontPixelHeight;
            FontPixelWidth = fontPixelWidth;
        }
    }

    private sealed class QueuedImeHandler : IUiImeHandler
    {
        private readonly object _lock = new();
        private bool _hasPending;
        private ImeRequest _pending;
        private bool _hasLastSent;
        private ImeRequest _lastSent;

        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            var request = new ImeRequest(caretRect, inputRect, fontPixelHeight, fontPixelWidth);
            lock (_lock)
            {
                if (_hasLastSent && AreClose(request, _lastSent))
                {
                    return;
                }

                _pending = request;
                _hasPending = true;
            }
        }

        public void Flush(IUiImeHandler target)
        {
            ImeRequest request;
            lock (_lock)
            {
                if (!_hasPending)
                {
                    return;
                }

                request = _pending;
                _hasPending = false;
            }

            if (_hasLastSent && AreClose(request, _lastSent))
            {
                return;
            }

            target.SetCaretRect(request.CaretRect, request.InputRect, request.FontPixelHeight, request.FontPixelWidth);
            _lastSent = request;
            _hasLastSent = true;
        }

        private static bool AreClose(ImeRequest a, ImeRequest b)
        {
            const float epsilon = 0.25f;
            return Close(a.CaretRect.X, b.CaretRect.X, epsilon)
                && Close(a.CaretRect.Y, b.CaretRect.Y, epsilon)
                && Close(a.CaretRect.Width, b.CaretRect.Width, epsilon)
                && Close(a.CaretRect.Height, b.CaretRect.Height, epsilon)
                && Close(a.InputRect.X, b.InputRect.X, epsilon)
                && Close(a.InputRect.Y, b.InputRect.Y, epsilon)
                && Close(a.InputRect.Width, b.InputRect.Width, epsilon)
                && Close(a.InputRect.Height, b.InputRect.Height, epsilon)
                && Close(a.FontPixelHeight, b.FontPixelHeight, epsilon)
                && Close(a.FontPixelWidth, b.FontPixelWidth, epsilon);
        }

        private static bool Close(float a, float b, float epsilon) => MathF.Abs(a - b) <= epsilon;
    }

    private static void ValidateOptions(DuxAppOptions options)
    {
        var hasScreen = options.Screen is not null;
        var hasDsl = options.Dsl is not null;
        if (hasScreen == hasDsl)
        {
            throw new InvalidOperationException("Exactly one of Screen or Dsl must be provided.");
        }

        if (options.Window.Width <= 0 || options.Window.Height <= 0)
        {
            throw new InvalidOperationException("Window size must be greater than zero.");
        }

        if (options.Font.InitialRanges.Count == 0 && options.Font.InitialGlyphs.Count == 0)
        {
            throw new InvalidOperationException("At least one initial font range or glyph string must be provided.");
        }
    }

    private static void RenderStartupClear(IRendererBackend renderer, IPlatformBackend platform)
    {
        var windowSize = platform.WindowSize;
        var framebufferSize = platform.FramebufferSize;
        var displaySize = new UiVector2(windowSize.Width, windowSize.Height);
        var scaleX = windowSize.Width > 0 ? (float)framebufferSize.Width / windowSize.Width : 1f;
        var scaleY = windowSize.Height > 0 ? (float)framebufferSize.Height / windowSize.Height : 1f;
        var framebufferScale = new UiVector2(scaleX, scaleY);

        var drawData = new UiDrawData(
            displaySize,
            new UiVector2(0, 0),
            framebufferScale,
            0,
            0,
            UiPooledList<UiDrawList>.FromArray(Array.Empty<UiDrawList>()),
            UiPooledList<UiTextureUpdate>.FromArray(Array.Empty<UiTextureUpdate>())
        );

        renderer.RenderDrawData(drawData);
    }
}

public sealed record class DuxAppOptions
{
    public DuxWindowOptions Window { get; init; } = new();
    public DuxRendererOptions Renderer { get; init; } = new();
    public DuxFontOptions Font { get; init; } = new();
    public DuxFrameOptions Frame { get; init; } = new();
    public DuxDebugOptions Debug { get; init; } = new();
    public UiTheme Theme { get; init; } = UiTheme.ImGuiDark;

    public UiTextureId FontTextureId { get; init; } = new(1);
    public UiTextureId WhiteTextureId { get; init; } = new(2);

    public UiScreen? Screen { get; init; }
    public DuxDslOptions? Dsl { get; init; }

    public IUiClipboard? Clipboard { get; init; }
    public IUiImeHandler? ImeHandler { get; init; }
}

public sealed record class DuxDebugOptions
{
    public Action<string>? Log { get; init; }
    public int LogEveryNFrames { get; init; } = 60;
}

public sealed record class DuxWindowOptions
{
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public string Title { get; init; } = "Dux";
    public bool VSync { get; init; } = true;
}

public sealed record class DuxRendererOptions
{
    public int MinImageCount { get; init; } = 2;
    public bool EnableValidationLayers { get; init; } = Debugger.IsAttached;
}

public sealed record class DuxFontOptions
{
    public string PrimaryFontPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
    public string? SecondaryFontPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
    public float SecondaryScale { get; init; } = 1f;
    public bool FastStartup { get; init; } = true;
    public bool UseBuiltInAsciiAtStartup { get; init; } = true;
    public int StartupBuiltInScale { get; init; } = 2;
    public int StartupBuiltInColumns { get; init; } = 16;
    public int StartupFontSize { get; init; } = 18;
    public int StartupAtlasWidth { get; init; } = 512;
    public int StartupAtlasHeight { get; init; } = 512;
    public int StartupPadding { get; init; } = 1;
    public int StartupOversample { get; init; } = 1;
    public IReadOnlyList<UiFontRange> StartupRanges { get; init; } = [new UiFontRange(0x20, 0x7E)];
    public IReadOnlyList<string> StartupGlyphs { get; init; } = [];
    public int FontSize { get; init; } = 26;
    public int AtlasWidth { get; init; } = 1024;
    public int AtlasHeight { get; init; } = 1024;
    public int Padding { get; init; } = 2;
    public int Oversample { get; init; } = 2;
    public IReadOnlyList<UiFontRange> InitialRanges { get; init; } = [new UiFontRange(0x20, 0x7E)];
    public IReadOnlyList<string> InitialGlyphs { get; init; } = [];
}

public sealed record class DuxFrameOptions
{
    public float LineHeightScale { get; init; } = 1.2f;
    public bool PixelSnap { get; init; } = true;
    public bool UseBaseline { get; init; } = true;
    public double FontRebuildMinIntervalSeconds { get; init; } = 0.12;
    public int FontRebuildBatchSize { get; init; } = 6;
}

public sealed record class DuxDslOptions
{
    public required Action<IUiDslEmitter> Render { get; init; }
    public UiDslBindings? Bindings { get; init; }
    public IUiDslEventSink? EventSink { get; init; }
    public IUiDslValueSource? ValueSource { get; init; }
    public UiDslState? State { get; init; }
}
