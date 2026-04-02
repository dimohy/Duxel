using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Duxel.Core;
using Duxel.Vulkan;

namespace Duxel.App;

public sealed class DuxelAppSession
{
    private AutoResetEvent? _frameWakeSignal;
    private int _pendingFrameRequests;
    private int _exitRequested;

    public bool IsExitRequested => Volatile.Read(ref _exitRequested) != 0;

    public void RequestFrame()
    {
        Interlocked.Increment(ref _pendingFrameRequests);
        Volatile.Read(ref _frameWakeSignal)?.Set();
    }

    public void Exit()
    {
        Interlocked.Exchange(ref _exitRequested, 1);
        Volatile.Read(ref _frameWakeSignal)?.Set();
    }

    public void RunCore(DuxelAppOptions options, IPlatformBackend platform)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(platform);
        DuxelApp.ValidateOptions(options);
        Interlocked.Exchange(ref _exitRequested, 0);

        var window = options.Window;
        var rendererOptions = options.Renderer;
        var fontOptions = options.Font;
        var frameOptions = options.Frame;
        var theme = options.Theme;
        var contentScale = platform.ContentScale;
        var logicalBaseFontSize = fontOptions.FontSize;

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
        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(
            rendererOptions.MinImageCount,
            window.VSync,
            requestedMsaaSamples,
            rendererOptions.FontLinearSampling,
            options.FontTextureId
        ));
        renderer.SetClearColor(theme.WindowBg);
        EmitStartupTiming("StartupClear");

        var renderedFrameNumber = 0;
        var appStartUtc = DateTime.UtcNow;
        var autoExitSeconds = DuxelApp.ParseAutoExitSeconds(Environment.GetEnvironmentVariable("DUXEL_SAMPLE_AUTO_EXIT_SECONDS"));

        var glyphBuilder = new UiFontGlyphRangesBuilder();
        foreach (var range in fontOptions.InitialRanges)
        {
            glyphBuilder.AddRange(range.Start, range.End);
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
                fontAtlas = DuxelApp.BuildFontAtlas(fontOptions, activeCodepointSet, useStartupSettings: true);
            }

            var fullSnapshot = new HashSet<int>(fullCodepointSet);
            fullAtlasTask = Task.Run(() => DuxelApp.BuildFontAtlas(fontOptions, fullSnapshot, useStartupSettings: false));
            fullAtlasTask.ContinueWith(_ => RequestFrame(), TaskContinuationOptions.ExecuteSynchronously);
            EmitStartupTiming("StartupAtlasReady");
        }
        else
        {
            fontAtlas = DuxelApp.BuildFontAtlas(fontOptions, fullCodepointSet, useStartupSettings: false);
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
        Volatile.Write(ref _frameWakeSignal, frameWakeSignal);
        Interlocked.Exchange(ref _pendingFrameRequests, 0);

        UiContext uiContext = new(fontAtlas, fontTextureId, whiteTextureId);
        var frameCounter = 0;
        var fpsSampleTime = 0f;
        var fpsSampleFrames = 0;
        var fps = 0f;
        var cursorValue = (int)UiMouseCursor.Arrow;

        uiContext.SetRequestFrameCallback(RequestFrame);
        uiContext.SetDirectTextPrimaryFontPath(fontOptions.PrimaryFontPath);
        uiContext.SetDirectTextSecondaryFontPath(fontOptions.SecondaryFontPath);
        uiContext.SetDirectTextBaseFontSize(logicalBaseFontSize * contentScale);
        uiContext.SetContentScale(contentScale);
        uiContext.SetTheme(theme);
        uiContext.SetScreen(options.Screen);
        DuxelApp.ConfigureContext(uiContext, options, platform);
        uiContext.State.VSync = window.VSync;
        uiContext.State.MsaaSamples = requestedMsaaSamples;
        uiContext.State.ConsumeRendererSettingsDirty();
        if (options.Debug.Log is { } log)
        {
            uiContext.State.DebugLog = log;
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
                while (!Volatile.Read(ref stopRequested) && !IsExitRequested)
                {
                    if (IsExitRequested)
                    {
                        Volatile.Write(ref stopRequested, true);
                        break;
                    }

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
                        || uiContext.State.HasActiveAnimations;
                    var hasRequestedFrame = TryConsumeFrameRequest();
                    if (frameOptions.EnableIdleFrameSkip && renderedFrameNumber > 0)
                    {
                        if (!hasInputInvalidation && !hasPendingRenderWork && !hasAnimationWork && !hasRequestedFrame)
                        {
                            var hasActiveTextInput = uiContext.State.HasActiveTextInput();

                            Volatile.Write(ref renderThreadWaiting, 1);
                            if (hasActiveTextInput)
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

                    var currentContentScale = platform.ContentScale;
                    if (MathF.Abs(currentContentScale - contentScale) > 0.001f)
                    {
                        contentScale = currentContentScale;
                        uiContext.SetContentScale(contentScale);
                        uiContext.SetDirectTextBaseFontSize(logicalBaseFontSize * contentScale);
                    }

                    var logicalWidth = contentScale > 0f ? windowSize.Width / contentScale : windowSize.Width;
                    var logicalHeight = contentScale > 0f ? windowSize.Height / contentScale : windowSize.Height;
                    var displaySize = new UiVector2(logicalWidth, logicalHeight);
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

                    var fbScaleX = logicalWidth > 0f ? (float)framebufferSize.Width / logicalWidth : 1f;
                    var fbScaleY = logicalHeight > 0f ? (float)framebufferSize.Height / logicalHeight : 1f;
                    var framebufferScale = new UiVector2(fbScaleX, fbScaleY);

                    var clipRect = new UiRect(0, 0, displaySize.X, displaySize.Y);
                    var textSettings = new UiTextSettings(1f, frameOptions.LineHeightScale, frameOptions.PixelSnap, frameOptions.UseBaseline, true);

                    var mouse = snapshot.MousePosition;
                    var mouseX = contentScale > 0f ? mouse.X / contentScale : mouse.X;
                    var mouseY = contentScale > 0f ? mouse.Y / contentScale : mouse.Y;

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

                    const bool imeComposing = false;
                    var allowMissingGlyphFallback = fullAtlasTask is null && incrementalAtlasTask is null;
                    textSettings = new UiTextSettings(1f, frameOptions.LineHeightScale, frameOptions.PixelSnap, frameOptions.UseBaseline, allowMissingGlyphFallback, OnMissingGlyph);

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

                        atlasBuildVersion++;
                        fontAtlas = fullAtlasTask.Result;
                        uiContext.SetFontAtlas(fontAtlas);

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
                            uiContext.SetFontAtlas(fontAtlas);

                            fontTextureDirty = true;
                        }

                        incrementalAtlasTask = null;
                    }

                    if (incrementalAtlasTask is null
                        && fullAtlasTask is null
                        && pendingGlyphs > 0
                        && (!imeComposing
                            && (pendingGlyphs >= frameOptions.FontRebuildBatchSize
                                || (currentTime - lastFontRebuildTime) >= frameOptions.FontRebuildMinIntervalSeconds)))
                    {
                        var useStartupSettings = fontOptions.FastStartup && fullAtlasTask is not null;
                        var snapshotCodepoints = new HashSet<int>(activeCodepointSet);

                        var scheduledVersion = atlasBuildVersion + 1;
                        atlasBuildVersion = scheduledVersion;
                        incrementalAtlasTaskVersion = scheduledVersion;
                        incrementalAtlasTask = Task.Run(() => DuxelApp.BuildFontAtlas(fontOptions, snapshotCodepoints, useStartupSettings));
                        incrementalAtlasTask.ContinueWith(_ => RequestFrame(), TaskContinuationOptions.ExecuteSynchronously);
                        pendingGlyphs = 0;
                        lastFontRebuildTime = currentTime;
                    }

                    var frameInfo = new UiFrameInfo(
                        deltaTime,
                        displaySize,
                        framebufferScale
                    );

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
                    if (uiContext.State.ConsumeRendererSettingsDirty())
                    {
                        renderer.SetMsaaSamples(uiContext.State.MsaaSamples);
                    }

                    if (platform.ShouldClose || Volatile.Read(ref stopRequested) || IsExitRequested)
                    {
                        Volatile.Write(ref stopRequested, true);
                        break;
                    }

                    var drawData = uiContext.GetDrawData();
                    var t3 = Stopwatch.GetTimestamp();
                    renderer.RenderDrawData(drawData);
                    renderedFrameNumber++;
                    {
                        var blinkElapsed = Math.Max(0.0, uiContext.State.TimeSeconds - uiContext.State.CaretBlinkStartSeconds);
                        lastRenderedBlinkState = ((int)Math.Floor(blinkElapsed / 0.5) & 1) == 0;
                    }
                    var t4 = Stopwatch.GetTimestamp();
                    if (Interlocked.Exchange(ref startupFirstFrameLogged, 1) == 0)
                    {
                        EmitStartupTiming("FirstFramePresented");
                    }

                    var tickFreq = (float)Stopwatch.Frequency / 1000f;
                    uiContext.State.NewFrameTimeMs = (t1 - t0) / tickFreq;
                    uiContext.State.RenderTimeMs = (t2 - t1) / tickFreq;
                    uiContext.State.SubmitTimeMs = (t4 - t3) / tickFreq;

                    DuxelApp.EmitTrace(options.Debug, ref frameCounter, drawData, fps);
                    drawData.ReleasePooled();
                    if (missingGlyphsFromRender > 0)
                    {
                        pendingGlyphs += missingGlyphsFromRender;
                        pendingGlyphs = Math.Max(pendingGlyphs, frameOptions.FontRebuildBatchSize);
                    }
                }
            }
            catch (Exception ex) when ((Volatile.Read(ref stopRequested) || platform.ShouldClose) && DuxelApp.IsShutdownTimeVulkanException(ex))
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
            while (!platform.ShouldClose && !Volatile.Read(ref stopRequested) && !IsExitRequested)
            {
                if (platform.ShouldClose || IsExitRequested)
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

                if (platform.ShouldClose || IsExitRequested)
                {
                    Volatile.Write(ref stopRequested, true);
                    frameWakeSignal.Set();
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
            uiContext.Dispose();
            UiFontAtlasBuilder.DiagnosticsLog = previousFontAtlasDiagnosticsLog;
            Interlocked.Exchange(ref _pendingFrameRequests, 0);
            Interlocked.Exchange(ref _exitRequested, 0);
            Volatile.Write(ref _frameWakeSignal, null);
        }
    }

    private bool TryConsumeFrameRequest()
    {
        while (true)
        {
            var current = Volatile.Read(ref _pendingFrameRequests);
            if (current <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _pendingFrameRequests, 0, current) == current)
            {
                return true;
            }
        }
    }
}
