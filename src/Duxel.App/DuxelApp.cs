using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Duxel.Core;
using Duxel.Core.Dsl;
using Duxel.Vulkan;

namespace Duxel.App;

public static class DuxelApp
{
    private static AutoResetEvent? s_frameWakeSignal;
    private static int s_pendingFrameRequests;
    private static Action<DuxelAppOptions>? s_registeredRunner;

    public static void RegisterRunner(Action<DuxelAppOptions> runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        var current = Interlocked.CompareExchange(ref s_registeredRunner, runner, null);
        if (current is null || AreEquivalentRunner(current, runner))
        {
            return;
        }

        throw new InvalidOperationException("A Duxel platform runner is already registered.");
    }

    private static bool AreEquivalentRunner(Action<DuxelAppOptions> left, Action<DuxelAppOptions> right)
    {
        return left.Method == right.Method && ReferenceEquals(left.Target, right.Target);
    }

    public static void RequestFrame()
    {
        Interlocked.Increment(ref s_pendingFrameRequests);
        Volatile.Read(ref s_frameWakeSignal)?.Set();
    }

    private static bool TryConsumeFrameRequest()
    {
        while (true)
        {
            var current = Volatile.Read(ref s_pendingFrameRequests);
            if (current <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref s_pendingFrameRequests, 0, current) == current)
            {
                return true;
            }
        }
    }

    public static void Run(DuxelAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runner = Volatile.Read(ref s_registeredRunner);
        if (runner is null)
        {
            throw new InvalidOperationException(
                "No platform runner is registered. Add a platform package such as Duxel.Windows.App so that DuxelApp.Run can be routed."
            );
        }

        runner(options);
    }

    public static void RunCore(DuxelAppOptions options, IPlatformBackend platform)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(platform);
        ValidateOptions(options);

        var window = options.Window;
        var rendererOptions = options.Renderer;
        var fontOptions = options.Font;
        var frameOptions = options.Frame;
        var theme = options.Theme;
        var startupLog = options.Debug.LogStartupTimings
            ? options.Debug.Log ?? Console.WriteLine
            : null;
        var startupStopwatch = startupLog is null ? null : Stopwatch.StartNew();
        var startupFirstFrameLogged = 0;
        var startupFullAtlasLogged = 0;
        var previousFontAtlasDiagnosticsLog = UiFontAtlasBuilder.DiagnosticsLog;
        if (startupLog is not null)
        {
            UiFontAtlasBuilder.DiagnosticsLog = message => startupLog($"{message} @ {startupStopwatch!.Elapsed.TotalMilliseconds:0.00}ms");
        }

        void EmitStartupTiming(string phase)
        {
            if (startupLog is null || startupStopwatch is null)
            {
                return;
            }

            startupLog($"StartupTiming[{phase}]={startupStopwatch.Elapsed.TotalMilliseconds:0.00}ms");
        }

        if (options.ImageDecoder is not null)
        {
            UiImageTexture.ImageDecoder = options.ImageDecoder;
        }

        var requestedMsaaSamples = rendererOptions.MsaaSamples > 0
            ? rendererOptions.MsaaSamples
            : rendererOptions.Profile switch
            {
                DuxelPerformanceProfile.Render => 1,
                _ => 4,
            };
        var forceValidation = ParseBooleanEnvironmentFlag(Environment.GetEnvironmentVariable("DUXEL_VK_VALIDATION"));
        var enableValidationLayers = rendererOptions.EnableValidationLayers || forceValidation;

        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(
            rendererOptions.MinImageCount,
            enableValidationLayers,
            window.VSync,
            requestedMsaaSamples,
            rendererOptions.EnableGlobalStaticGeometryCache,
            rendererOptions.EnableTaaIfSupported,
            rendererOptions.EnableFxaaIfSupported,
            rendererOptions.TaaExcludeFont,
            rendererOptions.TaaCurrentFrameWeight,
            rendererOptions.FontLinearSampling,
            options.FontTextureId
        ));
        renderer.SetClearColor(theme.WindowBg);
        EmitStartupTiming("StartupClear");

        var captureOutputDirectory = options.Debug.CaptureOutputDirectory;
        if (string.IsNullOrWhiteSpace(captureOutputDirectory))
        {
            captureOutputDirectory = Environment.GetEnvironmentVariable("DUXEL_CAPTURE_OUT_DIR");
        }

        var captureFrames = options.Debug.CaptureFrameIndices.Count > 0
            ? options.Debug.CaptureFrameIndices
            : ParseCaptureFrameList(Environment.GetEnvironmentVariable("DUXEL_CAPTURE_FRAMES"));

        var pendingCaptureFrames = new HashSet<int>();
        for (var i = 0; i < captureFrames.Count; i++)
        {
            if (captureFrames[i] > 0)
            {
                pendingCaptureFrames.Add(captureFrames[i]);
            }
        }

        if (pendingCaptureFrames.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(captureOutputDirectory))
            {
                captureOutputDirectory = Path.Combine(Environment.CurrentDirectory, "captures");
            }

            Directory.CreateDirectory(captureOutputDirectory);
        }

        var renderedFrameNumber = 0;
        var appStartUtc = DateTime.UtcNow;
        var autoExitSeconds = ParseAutoExitSeconds(Environment.GetEnvironmentVariable("DUXEL_SAMPLE_AUTO_EXIT_SECONDS"));

        void TryCaptureFrameIfRequested(int frameNumber)
        {
            if (pendingCaptureFrames.Count == 0 || !pendingCaptureFrames.Remove(frameNumber))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(captureOutputDirectory))
            {
                return;
            }

            try
            {
                var rgba = renderer.CaptureBackbufferRgba(out var width, out var height);
                var filePath = Path.Combine(captureOutputDirectory, $"frame_{frameNumber:D4}.bmp");
                WriteRgbaToBmp(filePath, width, height, rgba);
                options.Debug.Log?.Invoke($"CaptureSaved: {filePath}");
            }
            catch (Exception ex)
            {
                options.Debug.Log?.Invoke($"CaptureFailed(frame={frameNumber}): {ex.Message}");
            }
        }

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
        Task<UiFontAtlas>? incrementalAtlasTask = null;
        var atlasBuildVersion = 0;
        var incrementalAtlasTaskVersion = 0;
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
            foreach (var text in fontOptions.InitialGlyphs)
            {
                startupBuilder.AddText(text);
            }

            var startupCodepoints = startupBuilder.BuildCodepoints();
            if (startupCodepoints.Count > 0)
            {
                activeCodepointSet = new HashSet<int>(startupCodepoints);
            }

            var hasNonAsciiStartupCodepoint = false;
            for (var i = 0; i < startupCodepoints.Count; i++)
            {
                if (startupCodepoints[i] > 0x7E)
                {
                    hasNonAsciiStartupCodepoint = true;
                    break;
                }
            }

            if (fontOptions.UseBuiltInAsciiAtStartup && !hasNonAsciiStartupCodepoint)
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
            EmitStartupTiming("StartupAtlasReady");
        }
        else
        {
            fontAtlas = BuildFontAtlas(fontOptions, fullCodepointSet, useStartupSettings: false);
            EmitStartupTiming("FullAtlasReadySync");
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
        using var frameWakeSignal = new AutoResetEvent(false);
        Volatile.Write(ref s_frameWakeSignal, frameWakeSignal);
        Interlocked.Exchange(ref s_pendingFrameRequests, 0);
        const int imeTempAtlasCacheCapacity = 4;
        const double imeTempAtlasCacheTtlSeconds = 1.2;
        var imeTempAtlasCache = new Dictionary<ulong, (UiFontAtlas Atlas, double ExpiresAt)>();
        var imeTempAtlasOrder = new Queue<ulong>();
        var pendingImeCacheStore = false;
        var pendingImeCacheKey = 0UL;

        UiContext? uiContext = null;
        UiDslState? dslState = null;
        Action<IUiDslEmitter>? dslRender = null;
        IUiDslEventSink? dslEventSink = null;
        IUiDslValueSource? dslValueSource = null;
        IUiClipboard? dslClipboard = null;
        var textureUpdates = new List<UiTextureUpdate>(4);
        var frameCounter = 0;
        var fpsSampleTime = 0f;
        var fpsSampleFrames = 0;
        var fps = 0f;
        var cursorValue = (int)UiMouseCursor.Arrow;

        IUiImeHandler? imeHandlerForContext = null;
        IUiImeHandler? platformImeHandler = null;
        QueuedImeHandler? queuedImeHandler = null;
        var dynamicFontResourceCache = new Dictionary<int, UiFontResource>();
        var dynamicFontBuildInFlight = new HashSet<int>();
        var dynamicFontCacheLock = new object();
        nuint nextDynamicFontTextureId = 1_100_000_000;
        const float dynamicFontRequestBucketStep = 0.25f;
        var dynamicFontPrewarmEnabled = ParseBooleanEnvironmentFlag(Environment.GetEnvironmentVariable("DUXEL_DYNAMIC_FONT_PREWARM"));
        HashSet<int> frameCodepointSnapshot = [];
        var fontAtlasDiagEnabled = ParseBooleanEnvironmentFlag(Environment.GetEnvironmentVariable("DUXEL_FONT_ATLAS_DIAG"));
        var fontAtlasDiagLogPath = Environment.GetEnvironmentVariable("DUXEL_FONT_ATLAS_DIAG_LOG");
        var fontAtlasDumpDir = Environment.GetEnvironmentVariable("DUXEL_FONT_ATLAS_DUMP_DIR");
        var fontAtlasTraceText = Environment.GetEnvironmentVariable("DUXEL_FONT_ATLAS_TRACE_TEXT");
        var fontAtlasDumpMax = ParsePositiveIntEnvironment(Environment.GetEnvironmentVariable("DUXEL_FONT_ATLAS_DUMP_MAX"), 8);
        var fontAtlasDumpSequence = 0;
        var fontAtlasDiagLock = new object();
        var fontAtlasDiagLog = options.Debug.Log ?? Console.WriteLine;

        void EmitFontAtlasDiag(string message)
        {
            if (!fontAtlasDiagEnabled)
            {
                return;
            }

            var line = $"[duxel-font-atlas] {message}";
            fontAtlasDiagLog(line);

            if (string.IsNullOrWhiteSpace(fontAtlasDiagLogPath))
            {
                return;
            }

            try
            {
                lock (fontAtlasDiagLock)
                {
                    var directory = Path.GetDirectoryName(fontAtlasDiagLogPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(fontAtlasDiagLogPath, $"{DateTime.UtcNow:O} {line}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }

        static float QuantizeFontSize(float requestedSize, float step)
        {
            if (step <= 0f)
            {
                return requestedSize;
            }

            return MathF.Floor(requestedSize / step) * step;
        }

        static ulong ComputePixelSignature(ReadOnlySpan<byte> pixels)
        {
            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            for (var i = 0; i < pixels.Length; i++)
            {
                hash ^= pixels[i];
                hash *= prime;
            }

            return hash;
        }

        void DumpFontAtlasSnapshot(UiFontAtlas atlas, string reason)
        {
            if (string.IsNullOrWhiteSpace(fontAtlasDumpDir) || fontAtlasDumpSequence >= fontAtlasDumpMax)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(fontAtlasDumpDir);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
                var safeReason = SanitizeFileNameToken(reason);
                var fileStem = $"font-atlas-{fontAtlasDumpSequence:000}-{timestamp}-{safeReason}";
                var bmpPath = Path.Combine(fontAtlasDumpDir, fileStem + ".bmp");
                var tracePath = Path.Combine(fontAtlasDumpDir, fileStem + ".txt");

                WriteRgbaToBmp(bmpPath, atlas.Width, atlas.Height, atlas.Pixels);

                var traceSeed = string.IsNullOrWhiteSpace(fontAtlasTraceText)
                    ? "The quick brown fox jumps over the lazy dog. 동해물과 백두산이 마르고 닳도록"
                    : fontAtlasTraceText;
                var traceCodepoints = new SortedSet<int>();
                for (var i = 0; i < traceSeed.Length; i++)
                {
                    traceCodepoints.Add(traceSeed[i]);
                }

                using (var writer = new StreamWriter(tracePath, false))
                {
                    writer.WriteLine($"reason={reason}");
                    writer.WriteLine($"atlas={atlas.Width}x{atlas.Height} glyphCount={atlas.Glyphs.Count} fallback={atlas.FallbackCodepoint}");
                    writer.WriteLine($"lineHeight={atlas.LineHeight:0.###} ascent={atlas.Ascent:0.###} descent={atlas.Descent:0.###} lineGap={atlas.LineGap:0.###}");
                    writer.WriteLine($"traceText={traceSeed}");

                    foreach (var codepoint in traceCodepoints)
                    {
                        if (!atlas.GetGlyphOrFallback(codepoint, out var glyph))
                        {
                            writer.WriteLine($"cp=U+{codepoint:X4} missing=1");
                            continue;
                        }

                        var uv = glyph.UvRect;
                        var pxX = (int)MathF.Round(uv.X * atlas.Width);
                        var pxY = (int)MathF.Round(uv.Y * atlas.Height);
                        var pxW = (int)MathF.Round(uv.Width * atlas.Width);
                        var pxH = (int)MathF.Round(uv.Height * atlas.Height);
                        writer.WriteLine(
                            $"cp=U+{codepoint:X4} char='{EscapeForTrace((char)codepoint)}' adv={glyph.AdvanceX:0.###} off=({glyph.OffsetX:0.###},{glyph.OffsetY:0.###}) size=({glyph.Width:0.###},{glyph.Height:0.###}) uv=({uv.X:0.######},{uv.Y:0.######},{uv.Width:0.######},{uv.Height:0.######}) px=({pxX},{pxY},{pxW},{pxH})");
                    }
                }

                fontAtlasDumpSequence++;
                EmitFontAtlasDiag($"dumped reason={reason} bmp={bmpPath} trace={tracePath}");
            }
            catch (Exception ex)
            {
                EmitFontAtlasDiag($"dump-failed reason={reason} error={ex.GetType().Name}:{ex.Message}");
            }
        }

        DumpFontAtlasSnapshot(fontAtlas, "initial");

        UiFontResource? BuildDynamicFontResource(int targetSize, HashSet<int> codepoints)
        {
            if (codepoints.Count == 0)
            {
                return null;
            }

            var signature = ComputeCodepointSignature(codepoints);
            UiFontAtlas sizedAtlas;
            var atlasWidth = fontOptions.AtlasWidth;
            var atlasHeight = fontOptions.AtlasHeight;
            const int maxDynamicAtlasSize = 4096;
            var retryCount = 0;

            EmitFontAtlasDiag($"request size={targetSize} codepoints={codepoints.Count} sig=0x{signature:X16} baseAtlas={atlasWidth}x{atlasHeight}");

            while (true)
            {
                try
                {
                    sizedAtlas = BuildFontAtlasWithSize(fontOptions, codepoints, targetSize, atlasWidth, atlasHeight);
                    var pixelSignature = ComputePixelSignature(sizedAtlas.Pixels);
                    EmitFontAtlasDiag($"built size={targetSize} atlas={atlasWidth}x{atlasHeight} retries={retryCount} sig=0x{signature:X16} pixels=0x{pixelSignature:X16}");
                    DumpFontAtlasSnapshot(sizedAtlas, $"dynamic-{targetSize}px");
                    break;
                }
                catch (InvalidOperationException ex)
                    when (ex.Message.Contains("Font atlas size is insufficient", StringComparison.Ordinal)
                          && (atlasWidth < maxDynamicAtlasSize || atlasHeight < maxDynamicAtlasSize))
                {
                    var previousWidth = atlasWidth;
                    var previousHeight = atlasHeight;
                    atlasWidth = Math.Min(maxDynamicAtlasSize, atlasWidth * 2);
                    atlasHeight = Math.Min(maxDynamicAtlasSize, atlasHeight * 2);
                    retryCount++;
                    EmitFontAtlasDiag($"retry size={targetSize} reason=atlas-insufficient from={previousWidth}x{previousHeight} to={atlasWidth}x{atlasHeight} retry={retryCount}");
                }
            }

            UiFontResource replaced = default;
            var hasReplaced = false;

            lock (dynamicFontCacheLock)
            {
                if (dynamicFontResourceCache.TryGetValue(targetSize, out var cached)
                    && cached.CodepointSignature == signature)
                {
                    EmitFontAtlasDiag($"cache-hit size={targetSize} tex={cached.TextureId.Value} sig=0x{signature:X16}");
                    return cached;
                }

                if (dynamicFontResourceCache.TryGetValue(targetSize, out var stale))
                {
                    replaced = stale;
                    hasReplaced = true;
                }

                var resource = new UiFontResource(sizedAtlas, new UiTextureId(nextDynamicFontTextureId++), signature);
                dynamicFontResourceCache[targetSize] = resource;
                var pixelSignature = ComputePixelSignature(resource.FontAtlas.Pixels);
                EmitFontAtlasDiag($"cache-store size={targetSize} tex={resource.TextureId.Value} sig=0x{signature:X16} pixels=0x{pixelSignature:X16} replaced={(hasReplaced ? replaced.TextureId.Value : 0)}");

                if (hasReplaced && uiContext is not null)
                {
                    uiContext.QueueTextureUpdate(new UiTextureUpdate(
                        UiTextureUpdateKind.Destroy,
                        replaced.TextureId,
                        replaced.FontAtlas.Format,
                        replaced.FontAtlas.Width,
                        replaced.FontAtlas.Height,
                        ReadOnlyMemory<byte>.Empty));
                }

                return resource;
            }
        }

        void QueueDynamicFontPrewarm(int targetSize, HashSet<int> codepoints)
        {
            if (targetSize < 8 || targetSize > 96)
            {
                return;
            }

            var signature = ComputeCodepointSignature(codepoints);

            lock (dynamicFontCacheLock)
            {
                if ((dynamicFontResourceCache.TryGetValue(targetSize, out var cached)
                        && cached.CodepointSignature == signature)
                    || dynamicFontBuildInFlight.Contains(targetSize))
                {
                    return;
                }

                dynamicFontBuildInFlight.Add(targetSize);
            }

            var snapshot = new HashSet<int>(codepoints);
            _ = Task.Run(() =>
            {
                try
                {
                    var built = BuildDynamicFontResource(targetSize, snapshot);
                    if (built is not null)
                    {
                        RequestFrame();
                    }
                }
                finally
                {
                    lock (dynamicFontCacheLock)
                    {
                        dynamicFontBuildInFlight.Remove(targetSize);
                    }
                }
            });
        }

        UiFontResource? ResolveDynamicFontResource(float requestedSize)
        {
            if (requestedSize <= 0f)
            {
                return null;
            }

            var bucketedSize = QuantizeFontSize(requestedSize, dynamicFontRequestBucketStep);
            bucketedSize = Math.Clamp(bucketedSize, 8f, 96f);
            var targetSize = Math.Clamp((int)MathF.Floor(bucketedSize), 8, 96);

            // Use per-frame frozen snapshot + baseline full set to prevent transient glyph dropout.
            var codepoints = new HashSet<int>(frameCodepointSnapshot);
            codepoints.UnionWith(fullCodepointSet);
            var signature = ComputeCodepointSignature(codepoints);
            lock (dynamicFontCacheLock)
            {
                if (dynamicFontResourceCache.TryGetValue(targetSize, out var cached)
                    && cached.CodepointSignature == signature)
                {
                    EmitFontAtlasDiag($"resolve-hit req={requestedSize:0.###} bucket={bucketedSize:0.###} size={targetSize} tex={cached.TextureId.Value} sig=0x{signature:X16}");
                    return cached;
                }
            }

            var resource = BuildDynamicFontResource(targetSize, codepoints);
            if (resource is null)
            {
                return null;
            }

            EmitFontAtlasDiag($"resolve-built req={requestedSize:0.###} bucket={bucketedSize:0.###} size={targetSize} tex={resource.Value.TextureId.Value} sig=0x{signature:X16}");

            if (dynamicFontPrewarmEnabled)
            {
                QueueDynamicFontPrewarm(targetSize - 1, codepoints);
                QueueDynamicFontPrewarm(targetSize + 1, codepoints);
            }
            return resource;
        }

        if (options.ImeHandler is not null)
        {
            imeHandlerForContext = options.ImeHandler;
        }
        else if (options.ImeHandlerFactory is not null)
        {
            platformImeHandler = options.ImeHandlerFactory(platform);
            if (platformImeHandler is not null)
            {
                queuedImeHandler = new QueuedImeHandler();
                imeHandlerForContext = queuedImeHandler;
            }
        }

        if (options.Dsl is { } dsl)
        {
            dslState = dsl.State ?? new UiDslState();
            dslRender = dsl.Render;
            dslEventSink = dsl.EventSink ?? dsl.Bindings;
            dslValueSource = dsl.ValueSource ?? dsl.Bindings;
            if (options.Clipboard is not null)
            {
                dslClipboard = options.Clipboard;
            }
            else if (options.ClipboardFactory is not null)
            {
                dslClipboard = options.ClipboardFactory(platform);
            }
        }
        else
        {
            uiContext = new UiContext(fontAtlas, fontTextureId, whiteTextureId, rendererOptions.EnableGlobalStaticGeometryCache);
            uiContext.SetRequestFrameCallback(RequestFrame);
            uiContext.SetFontResourceResolver(ResolveDynamicFontResource);
            uiContext.SetDirectTextPrimaryFontPath(fontOptions.PrimaryFontPath);
            uiContext.SetDirectTextSecondaryFontPath(fontOptions.SecondaryFontPath);
            uiContext.SetDirectTextBaseFontSize(fontOptions.FontSize);
            uiContext.SetTheme(theme);
            uiContext.SetScreen(options.Screen!);
            ConfigureContext(uiContext, options, platform, imeHandlerForContext);
            uiContext.State.VSync = window.VSync;
            uiContext.State.MsaaSamples = requestedMsaaSamples;
            uiContext.State.TaaEnabled = rendererOptions.EnableTaaIfSupported;
            uiContext.State.FxaaEnabled = rendererOptions.EnableFxaaIfSupported;
            uiContext.State.TaaExcludeFont = rendererOptions.TaaExcludeFont;
            uiContext.State.TaaCurrentFrameWeight = rendererOptions.TaaCurrentFrameWeight;
            uiContext.State.ConsumeRendererAaSettingsDirty();
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
                snapshot.LeftMousePressedEvent,
                snapshot.LeftMouseReleasedEvent,
                snapshot.RightMousePressedEvent,
                snapshot.RightMouseReleasedEvent,
                snapshot.MiddleMousePressedEvent,
                snapshot.MiddleMouseReleasedEvent,
                snapshot.MouseWheel,
                snapshot.MouseWheelHorizontal,
                keyEvents,
                charEvents,
                snapshot.KeyRepeatSettings,
                snapshot.Modifiers
            );
        }

        static ulong ComputeCodepointSignature(HashSet<int> codepoints)
        {
            if (codepoints.Count == 0)
            {
                return 0UL;
            }

            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;

            var pool = ArrayPool<int>.Shared;
            var buffer = pool.Rent(codepoints.Count);
            try
            {
                var i = 0;
                foreach (var cp in codepoints)
                {
                    buffer[i++] = cp;
                }

                Array.Sort(buffer, 0, i);
                var hash = offset;
                for (var index = 0; index < i; index++)
                {
                    hash ^= (uint)buffer[index];
                    hash *= prime;
                }

                return hash;
            }
            finally
            {
                pool.Return(buffer, clearArray: false);
            }
        }

        static ulong ComposeImeTempAtlasKey(HashSet<int> codepoints, bool useStartupSettings)
        {
            var hash = ComputeCodepointSignature(codepoints);
            return useStartupSettings ? hash ^ 0x9E3779B97F4A7C15UL : hash ^ 0xC2B2AE3D27D4EB4FUL;
        }

        void PruneImeTempAtlasCache(double currentTime)
        {
            if (imeTempAtlasCache.Count == 0)
            {
                return;
            }

            while (imeTempAtlasOrder.Count > 0)
            {
                var key = imeTempAtlasOrder.Peek();
                if (!imeTempAtlasCache.TryGetValue(key, out var entry))
                {
                    imeTempAtlasOrder.Dequeue();
                    continue;
                }

                if (entry.ExpiresAt > currentTime && imeTempAtlasCache.Count <= imeTempAtlasCacheCapacity)
                {
                    break;
                }

                imeTempAtlasOrder.Dequeue();
                imeTempAtlasCache.Remove(key);
            }
        }

        bool TryGetImeTempAtlas(ulong key, double currentTime, out UiFontAtlas atlas)
        {
            PruneImeTempAtlasCache(currentTime);
            if (imeTempAtlasCache.TryGetValue(key, out var entry) && entry.ExpiresAt > currentTime)
            {
                atlas = entry.Atlas;
                return true;
            }

            atlas = null!;
            return false;
        }

        void StoreImeTempAtlas(ulong key, UiFontAtlas atlas, double currentTime)
        {
            imeTempAtlasCache[key] = (atlas, currentTime + imeTempAtlasCacheTtlSeconds);
            imeTempAtlasOrder.Enqueue(key);
            PruneImeTempAtlasCache(currentTime);
        }

        var snapshotLock = new object();
        var latestSnapshot = default(InputSnapshot);
        var latestWindowSize = new PlatformSize(window.Width, window.Height);
        var latestFramebufferSize = latestWindowSize;
        var hasSnapshot = false;
        var stopRequested = false;
        var renderThreadWaiting = 0;

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
                current.LeftMousePressedEvent || incoming.LeftMousePressedEvent,
                current.LeftMouseReleasedEvent || incoming.LeftMouseReleasedEvent,
                current.RightMousePressedEvent || incoming.RightMousePressedEvent,
                current.RightMouseReleasedEvent || incoming.RightMouseReleasedEvent,
                current.MiddleMousePressedEvent || incoming.MiddleMousePressedEvent,
                current.MiddleMouseReleasedEvent || incoming.MiddleMouseReleasedEvent,
                current.MouseWheel + incoming.MouseWheel,
                current.MouseWheelHorizontal + incoming.MouseWheelHorizontal,
                MergeEvents(current.KeyEvents, incoming.KeyEvents),
                MergeEvents(current.CharEvents, incoming.CharEvents),
                incoming.KeyRepeatSettings,
                incoming.Modifiers
            );
        }

        static InputSnapshot ClearEvents(InputSnapshot snapshot)
        {
            if (snapshot.KeyEvents.Count == 0
                && snapshot.CharEvents.Count == 0
                && snapshot.MouseWheel == 0f
                && snapshot.MouseWheelHorizontal == 0f
                && !snapshot.LeftMousePressedEvent
                && !snapshot.LeftMouseReleasedEvent
                && !snapshot.RightMousePressedEvent
                && !snapshot.RightMouseReleasedEvent
                && !snapshot.MiddleMousePressedEvent
                && !snapshot.MiddleMouseReleasedEvent)
            {
                return snapshot;
            }

            return snapshot with
            {
                LeftMousePressedEvent = false,
                LeftMouseReleasedEvent = false,
                RightMousePressedEvent = false,
                RightMouseReleasedEvent = false,
                MiddleMousePressedEvent = false,
                MiddleMouseReleasedEvent = false,
                KeyEvents = Array.Empty<UiKeyEvent>(),
                CharEvents = Array.Empty<UiCharEvent>(),
                MouseWheel = 0f,
                MouseWheelHorizontal = 0f
            };
        }

        bool UpdateSnapshot()
        {
            var snapshot = CopySnapshot(platform.Input.Snapshot);
            var windowSize = platform.WindowSize;
            var framebufferSize = platform.FramebufferSize;
            var hasChange = false;
            lock (snapshotLock)
            {
                hasChange = !hasSnapshot
                    || snapshot.KeyEvents.Count > 0
                    || snapshot.CharEvents.Count > 0
                    || snapshot.LeftMousePressedEvent
                    || snapshot.LeftMouseReleasedEvent
                    || snapshot.RightMousePressedEvent
                    || snapshot.RightMouseReleasedEvent
                    || snapshot.MiddleMousePressedEvent
                    || snapshot.MiddleMouseReleasedEvent
                    || MathF.Abs(snapshot.MouseWheel) > 0.0001f
                    || MathF.Abs(snapshot.MouseWheelHorizontal) > 0.0001f
                    || snapshot.LeftMouseDown != latestSnapshot.LeftMouseDown
                    || snapshot.RightMouseDown != latestSnapshot.RightMouseDown
                    || snapshot.MiddleMouseDown != latestSnapshot.MiddleMouseDown
                    || MathF.Abs(snapshot.MousePosition.X - latestSnapshot.MousePosition.X) > 0.01f
                    || MathF.Abs(snapshot.MousePosition.Y - latestSnapshot.MousePosition.Y) > 0.01f
                    || windowSize.Width != latestWindowSize.Width
                    || windowSize.Height != latestWindowSize.Height
                    || framebufferSize.Width != latestFramebufferSize.Width
                    || framebufferSize.Height != latestFramebufferSize.Height;

                latestSnapshot = hasSnapshot ? MergeSnapshot(latestSnapshot, snapshot) : snapshot;
                latestWindowSize = windowSize;
                latestFramebufferSize = framebufferSize;
                hasSnapshot = true;
            }

            return hasChange;
        }

        _ = UpdateSnapshot();

        var renderThread = new Thread(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            previousTime = stopwatch.Elapsed.TotalSeconds;
            lastFontRebuildTime = previousTime;
            var lastPresentedTime = previousTime;
            var lastObservedMousePosition = latestSnapshot.MousePosition;
            var lastRenderedBlinkState = true;

            static bool HasInputDrivenInvalidation(InputSnapshot snapshot, UiVector2 lastMousePosition)
            {
                if (snapshot.KeyEvents.Count > 0 || snapshot.CharEvents.Count > 0)
                {
                    return true;
                }

                if (MathF.Abs(snapshot.MouseWheel) > 0.0001f || MathF.Abs(snapshot.MouseWheelHorizontal) > 0.0001f)
                {
                    return true;
                }

                if (snapshot.LeftMousePressedEvent
                    || snapshot.LeftMouseReleasedEvent
                    || snapshot.RightMousePressedEvent
                    || snapshot.RightMouseReleasedEvent
                    || snapshot.MiddleMousePressedEvent
                    || snapshot.MiddleMouseReleasedEvent)
                {
                    return true;
                }

                if (snapshot.LeftMouseDown || snapshot.RightMouseDown || snapshot.MiddleMouseDown)
                {
                    return true;
                }

                return MathF.Abs(snapshot.MousePosition.X - lastMousePosition.X) > 0.01f
                    || MathF.Abs(snapshot.MousePosition.Y - lastMousePosition.Y) > 0.01f;
            }

            try
            {
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

                var hasInputInvalidation = HasInputDrivenInvalidation(snapshot, lastObservedMousePosition);
                lastObservedMousePosition = snapshot.MousePosition;
                var hasPendingRenderWork = fontTextureDirty || pendingGlyphs > 0 || fullAtlasTask is not null || incrementalAtlasTask is not null;
                var hasAnimationWork = (frameOptions.IsAnimationActiveProvider?.Invoke() ?? false)
                    || (uiContext?.State.HasActiveAnimations ?? false);
                var hasRequestedFrame = TryConsumeFrameRequest();
                if (frameOptions.EnableIdleFrameSkip && uiContext is not null && renderedFrameNumber > 0)
                {
                    if (!hasInputInvalidation && !hasPendingRenderWork && !hasAnimationWork && !hasRequestedFrame)
                    {
                        var hasImeComposition = !string.IsNullOrEmpty(imeHandlerForContext?.GetCompositionText());
                        var hasActiveTextInput = uiContext.State.HasActiveTextInput();

                        Volatile.Write(ref renderThreadWaiting, 1);
                        if (hasImeComposition || hasActiveTextInput)
                        {
                            const double blinkIntervalSeconds = 0.5;
                            var caretElapsed = uiContext.State.TimeSeconds - uiContext.State.CaretBlinkStartSeconds;
                            var wallOffset = stopwatch.Elapsed.TotalSeconds - previousTime;
                            var totalElapsed = Math.Max(0.0, caretElapsed + wallOffset);
                            var currentBlinkState = ((int)Math.Floor(totalElapsed / blinkIntervalSeconds) & 1) == 0;

                            if (currentBlinkState == lastRenderedBlinkState)
                            {
                                var phase = totalElapsed % blinkIntervalSeconds;
                                var sleepMs = (int)Math.Max(1, (blinkIntervalSeconds - phase) * 1000.0);
                                frameWakeSignal.WaitOne(sleepMs);
                                continue;
                            }

                            lastRenderedBlinkState = currentBlinkState;
                        }
                        else
                        {
                            frameWakeSignal.WaitOne(Timeout.Infinite);
                            continue;
                        }
                    }
                }

                Volatile.Write(ref renderThreadWaiting, 0);

                var displaySize = new UiVector2(windowSize.Width, windowSize.Height);
                var missingGlyphsFromRender = 0;

                void OnMissingGlyph(int codepoint)
                {
                    if ((uint)codepoint > 0xFFFFu)
                    {
                        return;
                    }

                    if (activeCodepointSet.Add(codepoint))
                    {
                        missingGlyphsFromRender++;
                    }
                }

                var scaleX = windowSize.Width > 0 ? (float)framebufferSize.Width / windowSize.Width : 1f;
                var scaleY = windowSize.Height > 0 ? (float)framebufferSize.Height / windowSize.Height : 1f;
                var framebufferScale = new UiVector2(scaleX, scaleY);

                // Freeze codepoints for the entire frame — prevents mid-frame atlas variance
                frameCodepointSnapshot = new HashSet<int>(activeCodepointSet);

                var clipRect = new UiRect(0, 0, displaySize.X, displaySize.Y);
                var textSettings = new UiTextSettings(1f, frameOptions.LineHeightScale, frameOptions.PixelSnap, frameOptions.UseBaseline, true);

                var mouse = snapshot.MousePosition;
                var mouseX = scaleX > 0f ? mouse.X / scaleX : mouse.X;
                var mouseY = scaleY > 0f ? mouse.Y / scaleY : mouse.Y;

                var leftDown = snapshot.LeftMouseDown;
                var leftPressed = snapshot.LeftMousePressedEvent || (leftDown && !previousLeftDown);
                var leftReleased = snapshot.LeftMouseReleasedEvent || (!leftDown && previousLeftDown);
                previousLeftDown = leftDown;

                var input = new UiInputState(
                    new UiVector2(mouseX, mouseY),
                    leftDown,
                    snapshot.RightMouseDown,
                    leftPressed,
                    leftReleased,
                    snapshot.RightMousePressedEvent,
                    snapshot.RightMouseReleasedEvent,
                    snapshot.MouseWheel,
                    snapshot.MouseWheelHorizontal,
                    snapshot.KeyEvents,
                    snapshot.CharEvents,
                    snapshot.KeyRepeatSettings,
                    snapshot.Modifiers
                );

                var newlyAdded = 0;
                foreach (var charEvent in snapshot.CharEvents)
                {
                    if (charEvent.CodePoint <= 0xFFFFu && activeCodepointSet.Add((int)charEvent.CodePoint))
                    {
                        newlyAdded++;
                    }
                }

                var compositionText = imeHandlerForContext?.GetCompositionText();
                var imeComposing = !string.IsNullOrEmpty(compositionText);
                var allowMissingGlyphFallback = fullAtlasTask is null && incrementalAtlasTask is null && !imeComposing;
                textSettings = new UiTextSettings(1f, frameOptions.LineHeightScale, frameOptions.PixelSnap, frameOptions.UseBaseline, allowMissingGlyphFallback, OnMissingGlyph);
                var composingGlyphsAdded = 0;
                if (!string.IsNullOrEmpty(compositionText))
                {
                    foreach (var rune in compositionText.EnumerateRunes())
                    {
                        if (rune.Value <= 0xFFFF && activeCodepointSet.Add(rune.Value))
                        {
                            newlyAdded++;
                            composingGlyphsAdded++;
                        }
                    }
                }

                var committedTextForGlyphs = imeHandlerForContext?.ConsumeRecentCommittedText();
                var committedGlyphsAdded = 0;
                if (!string.IsNullOrEmpty(committedTextForGlyphs))
                {
                    foreach (var rune in committedTextForGlyphs.EnumerateRunes())
                    {
                        if (rune.Value <= 0xFFFF && activeCodepointSet.Add(rune.Value))
                        {
                            newlyAdded++;
                            committedGlyphsAdded++;
                        }
                    }
                }

                pendingGlyphs += newlyAdded;

                var forceRebuildForImeGlyphs = committedGlyphsAdded > 0 || composingGlyphsAdded > 0;
                if (forceRebuildForImeGlyphs)
                {
                    pendingGlyphs = Math.Max(pendingGlyphs, frameOptions.FontRebuildBatchSize);
                }

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

                    atlasBuildVersion++;
                    fontAtlas = fullAtlasTask.Result;
                    DumpFontAtlasSnapshot(fontAtlas, "full-atlas-task");
                    if (uiContext is not null)
                    {
                        uiContext.SetFontAtlas(fontAtlas);
                    }

                    activeCodepointSet = fullCodepointSet;
                    fontTextureDirty = true;
                    fullAtlasTask = null;
                    if (Interlocked.Exchange(ref startupFullAtlasLogged, 1) == 0)
                    {
                        EmitStartupTiming("FullAtlasReady");
                    }
                }

                if (incrementalAtlasTask is { IsCompleted: true })
                {
                    if (incrementalAtlasTask.IsFaulted)
                    {
                        throw new InvalidOperationException("Incremental font atlas build task failed.", incrementalAtlasTask.Exception!);
                    }

                    if (incrementalAtlasTaskVersion == atlasBuildVersion)
                    {
                        fontAtlas = incrementalAtlasTask.Result;
                        DumpFontAtlasSnapshot(fontAtlas, pendingImeCacheStore ? "incremental-ime" : "incremental");
                        if (pendingImeCacheStore)
                        {
                            StoreImeTempAtlas(pendingImeCacheKey, fontAtlas, currentTime);
                        }
                        if (uiContext is not null)
                        {
                            uiContext.SetFontAtlas(fontAtlas);
                        }

                        fontTextureDirty = true;
                    }

                    pendingImeCacheStore = false;
                    pendingImeCacheKey = 0UL;
                    incrementalAtlasTask = null;
                }

                if (incrementalAtlasTask is null
                    && pendingGlyphs > 0
                    && (forceRebuildForImeGlyphs
                        || (!imeComposing
                            && (pendingGlyphs >= frameOptions.FontRebuildBatchSize
                                || (currentTime - lastFontRebuildTime) >= frameOptions.FontRebuildMinIntervalSeconds))))
                {
                    var useStartupSettings = fontOptions.FastStartup && fullAtlasTask is not null;
                    var snapshotCodepoints = new HashSet<int>(activeCodepointSet);
                    if (forceRebuildForImeGlyphs)
                    {
                        var imeCacheKey = ComposeImeTempAtlasKey(snapshotCodepoints, useStartupSettings);
                        if (TryGetImeTempAtlas(imeCacheKey, currentTime, out var cachedImeAtlas))
                        {
                            fontAtlas = cachedImeAtlas;
                            DumpFontAtlasSnapshot(fontAtlas, "ime-cache-hit");
                            if (uiContext is not null)
                            {
                                uiContext.SetFontAtlas(fontAtlas);
                            }

                            fontTextureDirty = true;
                            pendingGlyphs = 0;
                            lastFontRebuildTime = currentTime;
                            continue;
                        }

                        pendingImeCacheStore = true;
                        pendingImeCacheKey = imeCacheKey;
                    }
                    else
                    {
                        pendingImeCacheStore = false;
                        pendingImeCacheKey = 0UL;
                    }

                    var scheduledVersion = atlasBuildVersion + 1;
                    atlasBuildVersion = scheduledVersion;
                    incrementalAtlasTaskVersion = scheduledVersion;
                    incrementalAtlasTask = Task.Run(() => BuildFontAtlas(fontOptions, snapshotCodepoints, useStartupSettings));
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
                    var t0 = Stopwatch.GetTimestamp();
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

                    var t1 = Stopwatch.GetTimestamp();
                    uiContext.Render();
                    var t2 = Stopwatch.GetTimestamp();

                    Volatile.Write(ref cursorValue, (int)uiContext.State.MouseCursor);
                    if (uiContext.State.ConsumeVSyncDirty())
                    {
                        renderer.SetVSync(uiContext.State.VSync);
                    }
                    if (uiContext.State.ConsumeRendererAaSettingsDirty())
                    {
                        renderer.SetMsaaSamples(uiContext.State.MsaaSamples);
                        renderer.SetTaaEnabled(uiContext.State.TaaEnabled);
                        renderer.SetFxaaEnabled(uiContext.State.FxaaEnabled);
                        renderer.SetTaaExcludeFont(uiContext.State.TaaExcludeFont);
                        renderer.SetTaaCurrentFrameWeight(uiContext.State.TaaCurrentFrameWeight);
                    }

                    if (platform.ShouldClose || Volatile.Read(ref stopRequested))
                    {
                        break;
                    }

                    var drawData = uiContext.GetDrawData();
                    var t3 = Stopwatch.GetTimestamp();
                    renderer.RenderDrawData(drawData);
                    lastPresentedTime = stopwatch.Elapsed.TotalSeconds;
                    renderedFrameNumber++;
                    {
                        var blinkElapsed = Math.Max(0.0, uiContext.State.TimeSeconds - uiContext.State.CaretBlinkStartSeconds);
                        lastRenderedBlinkState = ((int)Math.Floor(blinkElapsed / 0.5) & 1) == 0;
                    }
                    TryCaptureFrameIfRequested(renderedFrameNumber);
                    var t4 = Stopwatch.GetTimestamp();
                    if (Interlocked.Exchange(ref startupFirstFrameLogged, 1) == 0)
                    {
                        EmitStartupTiming("FirstFramePresented");
                    }

                    var tickFreq = (float)Stopwatch.Frequency / 1000f;
                    uiContext.State.NewFrameTimeMs = (t1 - t0) / tickFreq;
                    uiContext.State.RenderTimeMs = (t2 - t1) / tickFreq;
                    uiContext.State.SubmitTimeMs = (t4 - t3) / tickFreq;

                    EmitTrace(options.Debug, ref frameCounter, drawData, fps);
                    drawData.ReleasePooled();
                    if (missingGlyphsFromRender > 0)
                    {
                        pendingGlyphs += missingGlyphsFromRender;
                        pendingGlyphs = Math.Max(pendingGlyphs, frameOptions.FontRebuildBatchSize);
                    }
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

                dslState!.UiState.AdvanceTime(deltaTime);
                dslState.UiState.BeginFrame();
                dslState.UiState.UpdateInput(snapshot.KeyEvents, snapshot.Modifiers);

                var dslContext = new UiDslRenderContext(
                    dslState!,
                    fontAtlas,
                    textSettings,
                    fontAtlas.LineHeight,
                    fontTextureId,
                    whiteTextureId,
                    theme,
                    UiStyle.Default,
                    clipRect,
                    new UiVector2(mouseX, mouseY),
                    leftDown,
                    snapshot.RightMouseDown,
                    leftPressed,
                    leftReleased,
                    snapshot.RightMousePressedEvent,
                    snapshot.RightMouseReleasedEvent,
                    snapshot.MouseWheel,
                    snapshot.MouseWheelHorizontal,
                    snapshot.KeyEvents,
                    snapshot.CharEvents,
                    dslClipboard,
                    displaySize,
                    snapshot.KeyRepeatSettings,
                    imeHandlerForContext,
                    0,
                    0,
                    0,
                    dslEventSink,
                    dslValueSource
                );

                var emitter = new UiDslImmediateEmitter(dslContext);
                dslRender!(emitter);
                dslState.UiState.EndFrame();
                Volatile.Write(ref cursorValue, (int)dslState.UiState.MouseCursor);

                if (platform.ShouldClose || Volatile.Read(ref stopRequested))
                {
                    break;
                }

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
                lastPresentedTime = stopwatch.Elapsed.TotalSeconds;
                renderedFrameNumber++;
                {
                    var blinkElapsed = Math.Max(0.0, dslState!.UiState.TimeSeconds - dslState.UiState.CaretBlinkStartSeconds);
                    lastRenderedBlinkState = ((int)Math.Floor(blinkElapsed / 0.5) & 1) == 0;
                }
                TryCaptureFrameIfRequested(renderedFrameNumber);
                EmitTrace(options.Debug, ref frameCounter, dslDrawData, fps);
                dslDrawData.ReleasePooled();
                if (missingGlyphsFromRender > 0)
                {
                    pendingGlyphs += missingGlyphsFromRender;
                    pendingGlyphs = Math.Max(pendingGlyphs, frameOptions.FontRebuildBatchSize);
                }
                    if (Interlocked.Exchange(ref startupFirstFrameLogged, 1) == 0)
                    {
                        EmitStartupTiming("FirstFramePresented");
                    }
                }
            }
            catch (Exception ex) when ((Volatile.Read(ref stopRequested) || platform.ShouldClose) && IsShutdownTimeVulkanException(ex))
            {
            }
            catch (Exception ex)
            {
                options.Debug.Log?.Invoke($"RenderThreadException: {ex}");
                Volatile.Write(ref stopRequested, true);
            }
        })
        {
            IsBackground = true,
            Name = "DuxelRender"
        };

        renderThread.Start();

        try
        {
            while (!platform.ShouldClose && !Volatile.Read(ref stopRequested))
            {
                if (platform.ShouldClose)
                {
                    Volatile.Write(ref stopRequested, true);
                    frameWakeSignal.Set();
                    break;
                }

                if (autoExitSeconds > 0d && (DateTime.UtcNow - appStartUtc).TotalSeconds >= autoExitSeconds)
                {
                    Volatile.Write(ref stopRequested, true);
                    frameWakeSignal.Set();
                    break;
                }

                if (frameOptions.EnableIdleFrameSkip && Volatile.Read(ref renderThreadWaiting) == 1)
                {
                    var waitTimeoutMs = autoExitSeconds > 0d ? 16 : 0;
                    platform.WaitEvents(waitTimeoutMs);
                }
                else
                {
                    platform.PollEvents();
                }

                if (queuedImeHandler is not null && platformImeHandler is not null)
                {
                    queuedImeHandler.Flush(platformImeHandler);
                }

                if (platform.ShouldClose)
                {
                    break;
                }

                var hasInputChange = UpdateSnapshot();
                if (hasInputChange)
                {
                    RequestFrame();
                    frameWakeSignal.Set();
                }
                platform.SetMouseCursor((UiMouseCursor)Volatile.Read(ref cursorValue));
            }
        }
        finally
        {
            Volatile.Write(ref stopRequested, true);
            frameWakeSignal.Set();
            renderThread.Join();
            uiContext?.Dispose();
            UiFontAtlasBuilder.DiagnosticsLog = previousFontAtlasDiagnosticsLog;
            Interlocked.Exchange(ref s_pendingFrameRequests, 0);
            Volatile.Write(ref s_frameWakeSignal, null);
        }
    }

    private static void EmitTrace(DuxelDebugOptions debug, ref int frameCounter, UiDrawData drawData, float fps)
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

    private static bool IsShutdownTimeVulkanException(Exception ex)
    {
        if (ex is not InvalidOperationException)
        {
            return false;
        }

        var message = ex.Message;
        return message.Contains("SurfaceLost", StringComparison.OrdinalIgnoreCase)
            || message.Contains("OutOfDate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Vulkan call failed", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<int> ParseCaptureFrameList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<int>();
        }

        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return Array.Empty<int>();
        }

        var frames = new List<int>(tokens.Length);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame) && frame > 0)
            {
                frames.Add(frame);
            }
        }

        return frames;
    }

    private static double ParseAutoExitSeconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0d;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0d
            ? seconds
            : 0d;
    }

    private static bool ParseBooleanEnvironmentFlag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParsePositiveIntEnvironment(string? raw, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static string SanitizeFileNameToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "atlas";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
            {
                chars[i] = '-';
            }
        }

        var sanitized = new string(chars).Trim('-');
        return sanitized.Length == 0 ? "atlas" : sanitized;
    }

    private static string EscapeForTrace(char value)
    {
        return value switch
        {
            '\'' => "\\'",
            '\r' => "\\r",
            '\n' => "\\n",
            '\t' => "\\t",
            _ => char.IsControl(value) ? $"\\u{(int)value:X4}" : value.ToString(),
        };
    }

    private static void WriteRgbaToBmp(string filePath, int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Capture size must be positive.");
        }

        var expectedLength = checked(width * height * 4);
        if (rgba.Length < expectedLength)
        {
            throw new InvalidOperationException("Capture buffer length is smaller than expected.");
        }

        var rowStride = ((width * 3) + 3) & ~3;
        var pixelDataSize = checked(rowStride * height);
        var fileSize = checked(54 + pixelDataSize);

        using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(54);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        var padCount = rowStride - (width * 3);
        for (var y = height - 1; y >= 0; y--)
        {
            var rowStart = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var pixelIndex = rowStart + x * 4;
                var r = rgba[pixelIndex + 0];
                var g = rgba[pixelIndex + 1];
                var b = rgba[pixelIndex + 2];
                writer.Write(b);
                writer.Write(g);
                writer.Write(r);
            }

            for (var i = 0; i < padCount; i++)
            {
                writer.Write((byte)0);
            }
        }
    }

    private static UiFontAtlas BuildFontAtlas(DuxelFontOptions options, HashSet<int> codepoints, bool useStartupSettings)
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
        const bool includeKerning = false;

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
                oversample,
                includeKerning
            );
        }

        return UiFontAtlasBuilder.CreateFromTtf(
            options.PrimaryFontPath,
            ascii,
            fontSize,
            atlasWidth,
            atlasHeight,
            padding,
            oversample,
            includeKerning
        );
    }

    private static UiFontAtlas BuildFontAtlasWithSize(DuxelFontOptions options, HashSet<int> codepoints, int fontSize, int atlasWidth, int atlasHeight)
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

        var padding = options.Padding;
        var oversample = options.Oversample;
        const bool includeKerning = false;

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
                oversample,
                includeKerning
            );
        }

        return UiFontAtlasBuilder.CreateFromTtf(
            options.PrimaryFontPath,
            ascii,
            fontSize,
            atlasWidth,
            atlasHeight,
            padding,
            oversample,
            includeKerning
        );
    }

    private static void ConfigureContext(UiContext context, DuxelAppOptions options, IPlatformBackend platform, IUiImeHandler? imeHandler)
    {
        context.SetTheme(options.Theme);
        context.SetPlatformTextBackend(platform.TextBackend);
        if (options.Clipboard is not null)
        {
            context.SetClipboard(options.Clipboard);
        }
        else if (options.ClipboardFactory is not null)
        {
            var clipboard = options.ClipboardFactory(platform);
            if (clipboard is not null)
            {
                context.SetClipboard(clipboard);
            }
        }

        if (imeHandler is not null)
        {
            context.SetImeHandler(imeHandler);
        }
    }

    private sealed class DefaultKeyRepeatSettingsProvider : IKeyRepeatSettingsProvider
    {
        public static DefaultKeyRepeatSettingsProvider Shared { get; } = new();

        private static readonly UiKeyRepeatSettings Settings = new(
            InitialDelaySeconds: 0.5,
            RepeatIntervalSeconds: 1.0 / 30.0
        );

        public UiKeyRepeatSettings GetSettings() => Settings;
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
        private bool _hasPendingOwner;
        private string? _pendingOwner;
        private string? _compositionText;
        private IUiImeHandler? _target;

        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            var request = new ImeRequest(caretRect, inputRect, fontPixelHeight, fontPixelWidth);
            lock (_lock)
            {
                _pending = request;
                _hasPending = true;
            }
        }

        public void Flush(IUiImeHandler target)
        {
            _target = target;

            ImeRequest request;
            string? pendingOwner;
            var hasOwner = false;
            lock (_lock)
            {
                if (_hasPendingOwner)
                {
                    pendingOwner = _pendingOwner;
                    _hasPendingOwner = false;
                    hasOwner = true;
                }
                else
                {
                    pendingOwner = null;
                }

                if (!_hasPending)
                {
                    if (hasOwner)
                    {
                        target.SetCompositionOwner(pendingOwner);
                    }
                    _compositionText = target.GetCompositionText();
                    return;
                }

                request = _pending;
                _hasPending = false;
            }

            if (hasOwner)
            {
                target.SetCompositionOwner(pendingOwner);
            }

            target.SetCaretRect(request.CaretRect, request.InputRect, request.FontPixelHeight, request.FontPixelWidth);
            _compositionText = target.GetCompositionText();
        }

        public string? GetCompositionText()
        {
            var target = _target;
            return target?.GetCompositionText() ?? Volatile.Read(ref _compositionText);
        }

        public void SetCompositionOwner(string? inputId)
        {
            lock (_lock)
            {
                _pendingOwner = inputId;
                _hasPendingOwner = true;
            }
        }

        public string? ConsumeCommittedText(string inputId)
        {
            if (string.IsNullOrEmpty(inputId))
            {
                return null;
            }

            var target = _target;
            return target?.ConsumeCommittedText(inputId);
        }

        public string? ConsumeRecentCommittedText()
        {
            var target = _target;
            return target?.ConsumeRecentCommittedText();
        }

    }

    private static void ValidateOptions(DuxelAppOptions options)
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

}

public sealed record class DuxelAppOptions
{
    public DuxelWindowOptions Window { get; init; } = new();
    public DuxelRendererOptions Renderer { get; init; } = new();
    public DuxelFontOptions Font { get; init; } = new();
    public DuxelFrameOptions Frame { get; init; } = new();
    public DuxelDebugOptions Debug { get; init; } = new();
    public UiTheme Theme { get; init; } = UiTheme.ImGuiDark;

    public UiTextureId FontTextureId { get; init; } = new(1);
    public UiTextureId WhiteTextureId { get; init; } = new(2);

    public UiScreen? Screen { get; init; }
    public DuxelDslOptions? Dsl { get; init; }

    public IUiClipboard? Clipboard { get; init; }
    public IUiImeHandler? ImeHandler { get; init; }
    public IUiImageDecoder? ImageDecoder { get; init; }
    public IKeyRepeatSettingsProvider? KeyRepeatSettingsProvider { get; init; }
    public Func<IPlatformBackend, IUiClipboard?>? ClipboardFactory { get; init; }
    public Func<IPlatformBackend, IUiImeHandler?>? ImeHandlerFactory { get; init; }
}

public sealed record class DuxelDebugOptions
{
    public Action<string>? Log { get; init; }
    public int LogEveryNFrames { get; init; } = 60;
    public bool LogStartupTimings { get; init; } = false;
    public string? CaptureOutputDirectory { get; init; }
    public IReadOnlyList<int> CaptureFrameIndices { get; init; } = Array.Empty<int>();
}

public sealed record class DuxelWindowOptions
{
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public string Title { get; init; } = "Duxel";
    public bool VSync { get; init; } = true;
}

public sealed record class DuxelRendererOptions
{
    public int MinImageCount { get; init; } = 3;
    public bool EnableValidationLayers { get; init; } = Debugger.IsAttached;
    public DuxelPerformanceProfile Profile { get; init; } = DuxelPerformanceProfile.Display;
    public int MsaaSamples { get; init; } = 0;
    public bool EnableGlobalStaticGeometryCache { get; init; } = true;
    public bool EnableTaaIfSupported { get; init; } = false;
    public bool EnableFxaaIfSupported { get; init; } = false;
    public bool EnableDWriteText { get; init; } = true;
    public bool TaaExcludeFont { get; init; } = true;
    public float TaaCurrentFrameWeight { get; init; } = 0.18f;
    public bool FontLinearSampling { get; init; } = false;
}

public enum DuxelPerformanceProfile
{
    Display,
    Render,
}

public sealed record class DuxelFontOptions
{
    public string PrimaryFontPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
    public string? SecondaryFontPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
    public float SecondaryScale { get; init; } = 1f;
    public bool FastStartup { get; init; } = true;
    public bool UseBuiltInAsciiAtStartup { get; init; } = true;
    public int StartupBuiltInScale { get; init; } = 2;
    public int StartupBuiltInColumns { get; init; } = 16;
    public int StartupFontSize { get; init; } = 16;
    public int StartupAtlasWidth { get; init; } = 512;
    public int StartupAtlasHeight { get; init; } = 512;
    public int StartupPadding { get; init; } = 1;
    public int StartupOversample { get; init; } = 1;
    public IReadOnlyList<UiFontRange> StartupRanges { get; init; } = [new UiFontRange(0x20, 0x7E)];
    public IReadOnlyList<string> StartupGlyphs { get; init; } = [];
    public int FontSize { get; init; } = 16;
    public int AtlasWidth { get; init; } = 1024;
    public int AtlasHeight { get; init; } = 1024;
    public int Padding { get; init; } = 2;
    public int Oversample { get; init; } = 2;
    public IReadOnlyList<UiFontRange> InitialRanges { get; init; } = [new UiFontRange(0x20, 0x7E)];
    public IReadOnlyList<string> InitialGlyphs { get; init; } = [];
}

public sealed record class DuxelFrameOptions
{
    public float LineHeightScale { get; init; } = 1.2f;
    public bool PixelSnap { get; init; } = true;
    public bool UseBaseline { get; init; } = true;
    public double FontRebuildMinIntervalSeconds { get; init; } = 0.25;
    public int FontRebuildBatchSize { get; init; } = 16;
    public bool EnableIdleFrameSkip { get; init; } = true;
    public int IdleSleepMilliseconds { get; init; } = 2;
    public int IdleWakeCheckMilliseconds { get; init; } = 1000;
    public int IdleEventWaitMilliseconds { get; init; } = 0;
    public IReadOnlyDictionary<string, int> WindowTargetFps { get; init; } = new Dictionary<string, int>();
    public Func<bool>? IsAnimationActiveProvider { get; init; }
}

public sealed record class DuxelDslOptions
{
    public required Action<IUiDslEmitter> Render { get; init; }
    public UiDslBindings? Bindings { get; init; }
    public IUiDslEventSink? EventSink { get; init; }
    public IUiDslValueSource? ValueSource { get; init; }
    public UiDslState? State { get; init; }
}

