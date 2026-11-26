using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Duxel.Core;
using Duxel.Vulkan;

namespace Duxel.App;

public sealed partial class DuxelAppSession
{
    private static readonly UiTextureId WindowIconTextureId = new(1_000_000_001);

    private AutoResetEvent? _frameWakeSignal;
    private int _pendingFrameRequests;
    private int _exitRequested;
    private readonly object _platformWakeGate = new();
    private Action? _signalPlatformExit;

    public bool IsExitRequested => Volatile.Read(ref _exitRequested) != 0;

    public void RequestFrame()
    {
        Interlocked.Increment(ref _pendingFrameRequests);
        lock (_platformWakeGate)
            _frameWakeSignal?.Set();
    }

    public void Exit()
    {
        Interlocked.Exchange(ref _exitRequested, 1);
        lock (_platformWakeGate)
        {
            _frameWakeSignal?.Set();
            _signalPlatformExit?.Invoke();
        }
    }

    public void RunCore(DuxelAppOptions options, IPlatformBackend platform)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(platform);
        DuxelApp.ValidateOptions(options);
        Interlocked.Exchange(ref _exitRequested, 0);

        var previousFontAtlasDiagnosticsLog = UiFontAtlasBuilder.DiagnosticsLog;
        try
        {
            RunSession(options, platform);
        }
        finally
        {
            if (options.Debug.LogStartupTimings)
                UiFontAtlasBuilder.DiagnosticsLog = previousFontAtlasDiagnosticsLog;
            Interlocked.Exchange(ref _pendingFrameRequests, 0);
            Interlocked.Exchange(ref _exitRequested, 0);
        }
    }

    private void RunSession(DuxelAppOptions options, IPlatformBackend platform)
    {
        var window = options.Window;
        var rendererOptions = options.Renderer;
        var fontOptions = options.Font;
        var frameOptions = options.Frame;
        var platformEventWake = platform as IPlatformEventWakeBackend;
        if (frameOptions.UseEventDrivenPlatformLoop && platformEventWake is null)
            throw new NotSupportedException("An event-driven platform loop requires IPlatformEventWakeBackend.");
        var effectiveDesign = DuxelApp.ResolveEffectiveDesign(options, platform);
        var theme = effectiveDesign.Theme;
        var platformThemeProvider = platform as IPlatformThemeProvider;
        var windowThemeController = platform as IWindowThemeController;
        WindowThemeUpdate? pendingWindowTheme = null;
        (UiColor Caption, UiColor Text, UiColor Border)? queuedWindowColors = null;
        var observedColorScheme = platformThemeProvider?.ColorScheme;
        var observedForcedColors = platformThemeProvider?.ForcedColors;
        var followsPlatformTheme = DuxelApp.UsesPlatformDefaultTheme(options);
        var contentScale = platform.ContentScale;
        var logicalBaseFontSize = fontOptions.FontSize;
        var appProfilingEnabled = ParseProfilingEnabled("DUXEL_APP_PROFILE");
        var appMemoryProfilingEnabled = Environment.GetEnvironmentVariable("DUXEL_APP_PROFILE_MEMORY") switch
        {
            null or "" or "0" => false,
            "1" => true,
            _ => throw new ArgumentException("DUXEL_APP_PROFILE_MEMORY must be 0 or 1."),
        };
        var appProfilingInterval = ParsePositiveIntEnvironment("DUXEL_APP_PROFILE_EVERY", 120);
        var appProfilingLogPath = Environment.GetEnvironmentVariable("DUXEL_APP_PROFILE_OUT");
        var appProfilingFrameCounter = 0;
        var textRenderingMode = ResolveTextRenderingMode(rendererOptions);
        var directTextEnabled = textRenderingMode is DuxelTextRenderingMode.DirectText;
        var directTextFallbackEnabled = textRenderingMode is DuxelTextRenderingMode.Auto;

        var startupLog = options.Debug.LogStartupTimings
            ? options.Debug.Log ?? Console.WriteLine
            : null;
        var startupStopwatch = startupLog is null ? null : Stopwatch.StartNew();
        var startupFirstFrameLogged = 0;
        var startupFullAtlasLogged = 0;
        if (startupLog is not null)
        {
            UiFontAtlasBuilder.DiagnosticsLog = message => startupLog($"{message} @ {startupStopwatch!.Elapsed.TotalMilliseconds:0.00}ms");
        }

        void EmitAppTiming(string category, string phase)
        {
            if (startupLog is null || startupStopwatch is null)
            {
                return;
            }

            startupLog($"{category}Timing[{phase}]={startupStopwatch.Elapsed.TotalMilliseconds:0.00}ms");
        }

        void EmitStartupTiming(string phase) => EmitAppTiming("Startup", phase);

        Action<string>? rendererDiagnosticsLog = startupLog is null
            ? null
            : message => startupLog($"{message} @ {startupStopwatch!.Elapsed.TotalMilliseconds:0.00}ms");

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
        EmitStartupTiming("RendererBegin");
        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(
            rendererOptions.MinImageCount,
            window.VSync,
            requestedMsaaSamples,
            rendererOptions.FontLinearSampling,
            options.FontTextureId,
            options.WhiteTextureId,
            rendererDiagnosticsLog,
            rendererOptions.MaxFramesInFlight
        ));
        renderer.SetClearColor(theme.WindowBg);
        EmitStartupTiming("StartupClear");

        var renderedFrameNumber = 0;
        long lastPresentedResizeSequence = 0;
        var appStartTimestamp = Stopwatch.GetTimestamp();
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
        Interlocked.Exchange(ref _pendingFrameRequests, 0);

        var uiContext = new UiContext(fontAtlas, fontTextureId, whiteTextureId)
        {
            UseR8ParagraphCoverage = true
        };
        using var contextLifetime = new UiContextLifetime(uiContext,
            startupLog is null ? null : () => EmitAppTiming("Shutdown", "UiContextDisposed"));
        var frameCounter = 0;
        var fpsSampleTime = 0f;
        var fpsSampleFrames = 0;
        var fps = 0f;
        var cursorValue = (int)UiMouseCursor.Arrow;

        uiContext.SetRequestFrameCallback(RequestFrame);
        if (platform is IPlatformAccessibilityBackend accessibility)
            uiContext.SetAccessibilityTree(accessibility.AccessibilityTree);
        uiContext.SetTypographyFonts(fontOptions.Typography);
        uiContext.SetDirectTextPrimaryFontPath(fontOptions.PrimaryFontPath);
        uiContext.SetDirectTextSecondaryFontPath(fontOptions.DirectTextSecondaryFontPath ?? fontOptions.SecondaryFontPath);
        uiContext.SetDirectTextBaseFontSize(logicalBaseFontSize * contentScale);
        uiContext.SetDirectTextEnabled(directTextEnabled);
        uiContext.SetDirectTextFallbackEnabled(directTextFallbackEnabled);
        uiContext.SetContentScale(contentScale);
        var windowIcon = CreateWindowIconTexture(options.Window, platform);
        uiContext.SetWindowIcon(windowIcon);
        var titleBarMode = ResolveTitleBarMode(options.Window);
        UiScreen activeScreen = titleBarMode switch
        {
            DuxelTitleBarMode.Duxel when platform is IWindowChromeController chrome
                => new DuxelWindowChromeScreen(options.Screen, chrome, options.Window.DuxelTitleBarHeight, windowIcon),
            DuxelTitleBarMode.ExtendedContent when platform is IWindowChromeController chrome
                && platform.WindowTitleBar is { } titleBar
                => new ExtendedContentWindowChromeScreen(options.Screen, chrome, titleBar),
            _ => new DuxelRootContentScreen(options.Screen),
        };
#if !DUXEL_COMPACT
        using var fileDialogScreen = platform is IPlatformFileDialogs ? new FileDialogScreen(activeScreen, this) : null;
        if (fileDialogScreen is not null)
            activeScreen = fileDialogScreen;
#endif
        uiContext.SetScreen(activeScreen);
        DuxelApp.ConfigureContext(uiContext, options, platform);
        uiContext.State.VSync = window.VSync;
        uiContext.State.MsaaSamples = requestedMsaaSamples;
        uiContext.State.ConsumeRendererSettingsDirty();
        if (options.Debug.Log is { } log)
        {
            uiContext.State.DebugLog = log;
        }


        var snapshotLock = new object();
        var pendingInput = new AppInputSnapshots.PendingInput();
        var latestWindowSize = new PlatformSize(window.Width, window.Height);
        var latestFramebufferSize = latestWindowSize;
        var latestHostVisibility = default(UiHostVisibility);
        var stopRequested = false;
        var renderThreadWaiting = 0;


        bool UpdateSnapshot()
        {
            var snapshot = AppInputSnapshots.Copy(platform.Input.Snapshot);
            var windowSize = platform.WindowSize;
            var framebufferSize = platform.FramebufferSize;
            var hostVisibility = platform.HostVisibility;
            var hasChange = false;
            lock (snapshotLock)
            {
                var latestSnapshot = pendingInput.Latest;
                hasChange = !pendingInput.HasSnapshot
                    || snapshot.OrderedInputs is { Count: > 0 }
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
                    || framebufferSize.Height != latestFramebufferSize.Height
                    || snapshot.IsWindowInactive != latestSnapshot.IsWindowInactive
                    || hostVisibility != latestHostVisibility;

                pendingInput.Add(snapshot);
                latestWindowSize = windowSize;
                latestFramebufferSize = framebufferSize;
                latestHostVisibility = hostVisibility;
            }

            return hasChange;
        }

        _ = UpdateSnapshot();

        System.Runtime.ExceptionServices.ExceptionDispatchInfo? renderFailure = null;
        var renderThread = new Thread(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            previousTime = stopwatch.Elapsed.TotalSeconds;
            lastFontRebuildTime = previousTime;
            var lastObservedInput = default(InputSnapshot);
            var lastRenderedBlinkState = true;
            var lastSubmitTimeMs = 0f;
            long idleWaitTicks = 0;

            void WaitForRenderWork(int timeout = Timeout.Infinite)
            {
                var start = Stopwatch.GetTimestamp();
                frameWakeSignal.WaitOne(timeout);
                idleWaitTicks += Stopwatch.GetTimestamp() - start;
            }

            void RenderFrame(DuxelRenderWork work)
            {
                renderer.SetVSync(work.VSync);
                renderer.SetMsaaSamples(work.MsaaSamples);
                renderer.SetClearColor(work.ClearColor);
                var t3 = Stopwatch.GetTimestamp();
                var rendererAllocatedBefore = work.AllocationSample.HasValue ? GC.GetAllocatedBytesForCurrentThread() : 0;
                var frameAccepted = renderer.TryRenderDrawData(work.Data);
                var rendererAllocated = work.AllocationSample.HasValue
                    ? GC.GetAllocatedBytesForCurrentThread() - rendererAllocatedBefore : 0;
                var framePresented = frameAccepted && !renderer.LastFrameMetrics.PresentationReused;
                uiContext.State.RendererFrameMetrics = renderer.LastFrameMetrics;
                if (renderer.LastFrameMetrics.RepaintHighlightActive)
                    frameWakeSignal.Set();
                if (framePresented)
                {
                    var presentedCount = Interlocked.Increment(ref renderedFrameNumber);
                    if (presentedCount == 1)
                        platform.NotifyFirstFramePresented();
                    if (work.ResizeSequence > 0)
                    {
                        Volatile.Write(ref lastPresentedResizeSequence, work.ResizeSequence);
                        platform.NotifyFramePresented(work.ResizeSequence);
                    }
                    if (Interlocked.Exchange(ref startupFirstFrameLogged, 1) == 0)
                        EmitStartupTiming("FirstFramePresented");
                }
                // Out-of-date acquisition can retire a swapchain without presenting.
                // Event-driven idle must retry with fresh synchronization objects.
                if (!frameAccepted || renderer.HasPendingPresentationWork)
                    RequestFrame();
                var t4 = Stopwatch.GetTimestamp();
                Volatile.Write(ref lastSubmitTimeMs, (t4 - t3) * (1000f / Stopwatch.Frequency));
                if (appProfilingEnabled && work.ProfileFrame % appProfilingInterval == 0)
                    LogAppProfileFrame(appProfilingLogPath, work.ProfileFrame,
                        work.NewFrameStart, work.RenderStart, work.RenderEnd, t3, t4, work.Data, work.Fps,
                        work.AllocationSample, rendererAllocated);
            }

            try
            {
                using var renderQueue = frameOptions.EnableRenderPipeline
                    ? new DuxelRenderQueue(RenderFrame, () =>
                    {
                        Volatile.Write(ref stopRequested, true);
                        frameWakeSignal.Set();
                        platform.CancelInteractiveResizeWait();
                        platformEventWake?.WakeEvents();
                    })
                    : null;
                // Keep the loop small so an on-stack JIT recompile doesn't interrupt a frame to compile the entire frame body.
                while (!Volatile.Read(ref stopRequested) && !IsExitRequested && RenderNextFrame())
                {
                }

                bool RenderNextFrame()
                {
                    // Admit before sampling input/building UI, so the producer does
                    // not build a third stale CPU frame behind active and queued work.
                    renderQueue?.WaitForCapacity();
                    if (Volatile.Read(ref stopRequested))
                        return false;
                    if (IsExitRequested)
                    {
                        Volatile.Write(ref stopRequested, true);
                        return false;
                    }

                    InputSnapshot snapshot;
                    PlatformSize windowSize;
                    PlatformSize framebufferSize;
                    UiHostVisibility hostVisibility;
                    var snapshotReady = false;
                    lock (snapshotLock)
                    {
                        snapshotReady = pendingInput.HasSnapshot;
                        snapshot = snapshotReady ? pendingInput.Drain() : default;
                        windowSize = latestWindowSize;
                        framebufferSize = latestFramebufferSize;
                        hostVisibility = latestHostVisibility;
                    }

                    if (!snapshotReady)
                    {
                        Thread.Yield();
                        return true;
                    }

                    var isInteractingResize = platform.IsInteractingResize;
                    var interactiveResizeSequence = platform.InteractiveResizeSequence;
                    // A committed WM_SIZE may still be awaiting presentation
                    // after a nested native message ends the sizing gesture.
                    // Its UI snapshot must not depend on the outer event loop
                    // returning from that same WM_SIZE first.
                    if (isInteractingResize || interactiveResizeSequence > 0)
                    {
                        while (true)
                        {
                            var sequenceBeforeSizeRead = platform.InteractiveResizeSequence;
                            windowSize = platform.WindowSize;
                            framebufferSize = platform.FramebufferSize;
                            interactiveResizeSequence = platform.InteractiveResizeSequence;
                            if (sequenceBeforeSizeRead == interactiveResizeSequence)
                            {
                                break;
                            }
                        }
                    }

                    // Closing and minimized hosts have no drawable client region. Do not enter widget layout.
                    if (platform.ShouldClose || Volatile.Read(ref stopRequested) || IsExitRequested)
                    {
                        Volatile.Write(ref stopRequested, true);
                        return false;
                    }
                    if (windowSize.Width <= 0 || windowSize.Height <= 0 || framebufferSize.Width <= 0 || framebufferSize.Height <= 0)
                    {
                        uiContext.ProcessHiddenHostVisibility(hostVisibility);
                        Volatile.Write(ref renderThreadWaiting, 1);
                        WaitForRenderWork();
                        Volatile.Write(ref renderThreadWaiting, 0);
                        return true;
                    }

                    var hasInputInvalidation = AppInputSnapshots.HasInputDrivenInvalidation(snapshot, lastObservedInput);
                    lastObservedInput = AppInputSnapshots.Clear(snapshot);
                    var hasPendingRenderWork = fontTextureDirty || pendingGlyphs > 0 || fullAtlasTask is not null || incrementalAtlasTask is not null;
                    // A native size commit can arrive after sampling the size but
                    // before consuming its wake. Keep scheduling until it presents.
                    var hasUnpresentedResize = interactiveResizeSequence > Volatile.Read(ref lastPresentedResizeSequence);
                    // Widget animations declare deadlines; only an explicit continuous
                    // producer bypasses the interruptible event/deadline wait.
                    var hasAnimationWork = frameOptions.IsAnimationActiveProvider?.Invoke() ?? false;
                    var hasRequestedFrame = TryConsumeFrameRequest();
                    if (frameOptions.EnableIdleFrameSkip && !hasUnpresentedResize && Volatile.Read(ref renderedFrameNumber) > 0)
                    {
                        if (!hasInputInvalidation && !hasPendingRenderWork && !hasAnimationWork && !hasRequestedFrame)
                        {
                            var wallOffset = Math.Max(0.0, stopwatch.Elapsed.TotalSeconds - previousTime);
                            var sleepMs = uiContext.State.GetScheduledFrameWaitMilliseconds(uiContext.State.TimeSeconds + wallOffset);
                            if (sleepMs != 0)
                            {
                                var shouldWait = true;
                                if (uiContext.State.HasActiveTextInput())
                                {
                                    const double blinkIntervalSeconds = 0.5;
                                    var caretElapsed = uiContext.State.TimeSeconds - uiContext.State.CaretBlinkStartSeconds;
                                    var totalElapsed = Math.Max(0.0, caretElapsed + wallOffset);
                                    var currentBlinkState = ((int)Math.Floor(totalElapsed / blinkIntervalSeconds) & 1) == 0;
                                    if (currentBlinkState != lastRenderedBlinkState)
                                    {
                                        lastRenderedBlinkState = currentBlinkState;
                                        shouldWait = false;
                                    }
                                    else
                                    {
                                        var phase = totalElapsed % blinkIntervalSeconds;
                                        var caretWait = (int)Math.Max(1, Math.Ceiling((blinkIntervalSeconds - phase) * 1000.0));
                                        sleepMs = sleepMs == Timeout.Infinite ? caretWait : Math.Min(sleepMs, caretWait);
                                    }
                                }
                                if (shouldWait)
                                {
                                    Volatile.Write(ref renderThreadWaiting, 1);
                                    WaitForRenderWork(sleepMs);
                                    return true;
                                }
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
                    var textScale = platform.TextScale;
                    if (!float.IsFinite(textScale) || textScale <= 0f)
                        throw new InvalidOperationException("The platform supplied an invalid text scale.");
                    var textSettings = new UiTextSettings(textScale, frameOptions.LineHeightScale, frameOptions.PixelSnap, frameOptions.UseBaseline, true);

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
                    )
                    {
                        MiddleMouseDown = snapshot.MiddleMouseDown,
                        MiddleMousePressed = snapshot.MiddleMousePressedEvent,
                        MiddleMouseReleased = snapshot.MiddleMouseReleasedEvent,
                    };

                    var newlyAdded = AppInputSnapshots.CaptureGlyphs(snapshot, activeCodepointSet);

                    const bool imeComposing = false;
                    var allowMissingGlyphFallback = fullAtlasTask is null && incrementalAtlasTask is null;
                    textSettings = new UiTextSettings(textScale, frameOptions.LineHeightScale, frameOptions.PixelSnap, frameOptions.UseBaseline, allowMissingGlyphFallback, OnMissingGlyph);

                    pendingGlyphs += newlyAdded;

                    var currentTime = stopwatch.Elapsed.TotalSeconds;
                    var deltaTime = (float)(currentTime - previousTime);
                    previousTime = currentTime;
                    var idleWaitSeconds = (double)idleWaitTicks / Stopwatch.Frequency;
                    idleWaitTicks = 0;
                    var activeTime = Math.Max(0, deltaTime - (float)idleWaitSeconds);
                    fpsSampleTime += activeTime;
                    if (activeTime > 0)
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
                    )
                    {
                        IdleWaitSeconds = idleWaitSeconds,
                        PrefersReducedMotion = platformThemeProvider?.PrefersReducedMotion == true,
                        HostVisibility = hostVisibility,
                        IsWindowInactive = snapshot.IsWindowInactive,
                        RepaintDiagnosticCleanup = !hasInputInvalidation && !hasPendingRenderWork && !hasAnimationWork
                            && !hasRequestedFrame && uiContext.State.NextScheduledFrameTimeSeconds is null
                            && uiContext.State.RendererFrameMetrics.RepaintHighlightActive
                    };

                    if ((followsPlatformTheme || options.RespectSystemForcedColors) && platformThemeProvider is not null)
                    {
                        var colorScheme = platformThemeProvider.ColorScheme;
                        var forcedColors = platformThemeProvider.ForcedColors;
                        if ((followsPlatformTheme && observedColorScheme != colorScheme)
                            || (options.RespectSystemForcedColors && observedForcedColors != forcedColors))
                        {
                            observedColorScheme = colorScheme;
                            observedForcedColors = forcedColors;
                            effectiveDesign = DuxelApp.ResolveEffectiveDesign(options, platform);
                            theme = effectiveDesign.Theme;
                            uiContext.SetDesign(effectiveDesign);
                        }
                    }

                    var sampleAllocations = appProfilingEnabled && appMemoryProfilingEnabled
                        && (appProfilingFrameCounter + 1) % appProfilingInterval == 0;
                    var newFrameAllocatedStart = sampleAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
                    var t0 = Stopwatch.GetTimestamp();
                    var orderedInputs = AppInputSnapshots.ToLogical(snapshot, contentScale);
                    if (orderedInputs is { Count: > 0 })
                        uiContext.NewInputBatch(frameInfo, orderedInputs, clipRect, textSettings);
                    else
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
                    var renderAllocatedStart = sampleAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
                    var frameMsaaSamples = uiContext.State.MsaaSamples;
                    var useProjectedGlyphText = renderer.CanUseProjectedGlyphText(frameMsaaSamples);
                    uiContext.UseProjectedGlyphText = useProjectedGlyphText;
                    uiContext.Render();
                    var requestedSamples = uiContext.State.MsaaSamples;
                    if (requestedSamples != frameMsaaSamples)
                    {
                        if (renderer.CanUseProjectedGlyphText(requestedSamples) == useProjectedGlyphText)
                            frameMsaaSamples = requestedSamples;
                        else
                            RequestFrame(); // Keep this frame's glyph commands compatible with its render settings.
                    }
                    if (windowThemeController is not null)
                    {
                        var current = uiContext.GetStyle();
                        var colors = (Caption : current.TitleBgActive, Text : current.WindowTitleText, Border : current.Border);
                        if (queuedWindowColors != colors)
                        {
                            queuedWindowColors = colors;
                            Interlocked.Exchange(ref pendingWindowTheme, new(colors.Caption, colors.Text, colors.Border));
                            platformEventWake?.WakeEvents();
                        }
                    }
                    var committedGlyphs = AppInputSnapshots.CaptureGlyphs(platform.ImeHandler?.ConsumeRecentCommittedText(), activeCodepointSet);
                    if (committedGlyphs > 0)
                    {
                        pendingGlyphs += committedGlyphs;
                        RequestFrame();
                    }
                    var t2 = Stopwatch.GetTimestamp();
                    var drawDataAllocatedStart = sampleAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;

                    var nextCursor = (int)uiContext.State.MouseCursor;
                    if (Interlocked.Exchange(ref cursorValue, nextCursor) != nextCursor)
                        platformEventWake?.WakeEvents();
                    uiContext.State.ConsumeVSyncDirty();
                    uiContext.State.ConsumeRendererSettingsDirty();

                    if (platform.ShouldClose || Volatile.Read(ref stopRequested) || IsExitRequested)
                    {
                        Volatile.Write(ref stopRequested, true);
                        return false;
                    }

                    var drawData = uiContext.GetDrawData();
                    DuxelFrameAllocationSample? allocationSample = sampleAllocations ? new(
                        renderAllocatedStart - newFrameAllocatedStart,
                        drawDataAllocatedStart - renderAllocatedStart,
                        GC.GetAllocatedBytesForCurrentThread() - drawDataAllocatedStart) : null;
                    var ownershipTransferred = false;
                    try
                    {
                        DuxelApp.EmitTrace(options.Debug, ref frameCounter, drawData, fps);
                        var work = new DuxelRenderWork(drawData, uiContext.State.VSync,
                            frameMsaaSamples, uiContext.GetStyle().WindowBg, interactiveResizeSequence,
                            ++appProfilingFrameCounter, t0, t1, t2, fps, allocationSample);
                        if (renderQueue is not null)
                        {
                            var waitForCallbacks = renderQueue.Submit(work);
                            ownershipTransferred = true;
                            if (waitForCallbacks)
                                renderQueue.WaitForIdle();
                        }
                        else
                            RenderFrame(work);
                    }
                    finally
                    {
                        if (!ownershipTransferred)
                            drawData.Dispose();
                    }
                    {
                        var blinkElapsed = Math.Max(0.0, uiContext.State.TimeSeconds - uiContext.State.CaretBlinkStartSeconds);
                        lastRenderedBlinkState = ((int)Math.Floor(blinkElapsed / 0.5) & 1) == 0;
                    }
                    var tickFreq = (float)Stopwatch.Frequency / 1000f;
                    uiContext.State.NewFrameTimeMs = (t1 - t0) / tickFreq;
                    uiContext.State.RenderTimeMs = (t2 - t1) / tickFreq;
                    uiContext.State.SubmitTimeMs = Volatile.Read(ref lastSubmitTimeMs);
                    uiContext.State.PreviousFrameWorkTimeMs = (float)Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                    if (missingGlyphsFromRender > 0)
                    {
                        pendingGlyphs += missingGlyphsFromRender;
                        pendingGlyphs = Math.Max(pendingGlyphs, frameOptions.FontRebuildBatchSize);
                    }
                    return true;
                }
            }
            catch (Exception ex) when ((Volatile.Read(ref stopRequested) || platform.ShouldClose) && DuxelApp.IsShutdownTimeVulkanException(ex))
            {
                platform.CancelInteractiveResizeWait();
                platformEventWake?.WakeEvents();
            }
            catch (Exception ex)
            {
                renderFailure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
                platform.CancelInteractiveResizeWait();
                if (options.Debug.Log is { } log)
                {
                    log($"RenderThreadException: {ex}");
                }
                else
                {
                    Console.Error.WriteLine($"RenderThreadException: {ex}");
                }

                Volatile.Write(ref stopRequested, true);
                platformEventWake?.WakeEvents();
            }
        })
        {
            IsBackground = true,
            Name = "DuxelRender"
        };

        var started = false;
        try
        {
            lock (_platformWakeGate)
            {
                _frameWakeSignal = frameWakeSignal;
                _signalPlatformExit = () =>
                {
                    platform.CancelInteractiveResizeWait();
                    platformEventWake?.WakeEvents();
                };
                _fileDialogs = platform as IPlatformFileDialogs;
                _wakeFileDialogs = platformEventWake;
            }
            renderThread.Start();
            started = true;

            while (!platform.ShouldClose && !Volatile.Read(ref stopRequested) && !IsExitRequested)
            {
                if (platform.ShouldClose || IsExitRequested)
                {
                    Volatile.Write(ref stopRequested, true);
                    frameWakeSignal.Set();
                    break;
                }

                if (autoExitSeconds > 0d && Stopwatch.GetElapsedTime(appStartTimestamp).TotalSeconds >= autoExitSeconds)
                {
                    Volatile.Write(ref stopRequested, true);
                    frameWakeSignal.Set();
                    break;
                }

                if (frameOptions.UseEventDrivenPlatformLoop
                    || (frameOptions.EnableIdleFrameSkip && Volatile.Read(ref renderThreadWaiting) == 1))
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

                DispatchFileDialog();
                var hasInputChange = UpdateSnapshot();
                if (hasInputChange)
                {
                    RequestFrame();
                    frameWakeSignal.Set();
                }
                if (Interlocked.Exchange(ref pendingWindowTheme, null) is { } windowColors)
                    windowThemeController!.SetWindowColors(windowColors.Caption, windowColors.Text, windowColors.Border);
                platform.SetMouseCursor((UiMouseCursor)Volatile.Read(ref cursorValue));
            }
        }
        finally
        {
            try
            {
                if (started)
                {
                    try
                    {
                        EmitAppTiming("Shutdown", "Begin");
                    }
                    finally
                    {
                        Volatile.Write(ref stopRequested, true);
                        frameWakeSignal.Set();
                        platform.CancelInteractiveResizeWait();
                        // The render thread can be waiting for WSI to dispatch a native message.
                        while (!renderThread.Join(0))
                            platform.WaitEvents(1);
                    }
                    EmitAppTiming("Shutdown", "RenderThreadStopped");
                }
            }
            finally
            {
                lock (_platformWakeGate)
                {
                    _fileDialogRequest?.Completion.TrySetException(new InvalidOperationException("The session closed before the file dialog completed."));
                    _fileDialogRequest = null;
                    _fileDialogs = null;
                    _wakeFileDialogs = null;
                    _signalPlatformExit = null;
                    _frameWakeSignal = null;
                }
            }
        }
        renderFailure?.Throw();
    }

    private readonly struct UiContextLifetime(UiContext context, Action? afterDispose) : IDisposable
    {
        public void Dispose()
        {
            context.Dispose();
            afterDispose?.Invoke();
        }
    }

    private sealed record WindowThemeUpdate(UiColor Caption, UiColor Text, UiColor Border);

    internal static DuxelTitleBarMode ResolveTitleBarMode(DuxelWindowOptions options)
        => options.TitleBarMode is DuxelTitleBarMode.Default
            ? options.UseDuxelTitleBar ? DuxelTitleBarMode.Duxel : DuxelTitleBarMode.System
            : options.TitleBarMode;

    private static UiImageTexture? CreateWindowIconTexture(DuxelWindowOptions window, IPlatformBackend platform)
    {
        if (platform is IWindowIconProvider iconProvider)
        {
            var image = iconProvider.GetWindowIconImage();
            return new UiImageTexture(WindowIconTextureId, image.Width, image.Height, image.RgbaPixels);
        }

        if (string.IsNullOrWhiteSpace(window.IconPath))
        {
            return null;
        }

        if (!File.Exists(window.IconPath))
        {
            throw new FileNotFoundException("The window icon file does not exist.", window.IconPath);
        }

        return UiImageTexture.LoadFromFile(window.IconPath, WindowIconTextureId);
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

    private static bool ParseProfilingEnabled(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParsePositiveIntEnvironment(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var value) && value > 0
            ? value
            : defaultValue;
    }

    private static DuxelTextRenderingMode ResolveTextRenderingMode(DuxelRendererOptions rendererOptions)
    {
        return ParseTextRenderingMode(Environment.GetEnvironmentVariable("DUXEL_TEXT_RENDERING"))
            ?? rendererOptions.TextRendering;
    }

    private static DuxelTextRenderingMode? ParseTextRenderingMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim() switch
        {
            var value when string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase) => DuxelTextRenderingMode.Auto,
            var value when string.Equals(value, "direct", StringComparison.OrdinalIgnoreCase) => DuxelTextRenderingMode.DirectText,
            var value when string.Equals(value, "directtext", StringComparison.OrdinalIgnoreCase) => DuxelTextRenderingMode.DirectText,
            var value when string.Equals(value, "platform", StringComparison.OrdinalIgnoreCase) => DuxelTextRenderingMode.DirectText,
            var value when string.Equals(value, "atlas", StringComparison.OrdinalIgnoreCase) => DuxelTextRenderingMode.Atlas,
            var value when string.Equals(value, "glyph", StringComparison.OrdinalIgnoreCase) => DuxelTextRenderingMode.Atlas,
            var value when string.Equals(value, "glyphatlas", StringComparison.OrdinalIgnoreCase) => DuxelTextRenderingMode.Atlas,
            _ => null,
        };
    }

    private static void LogAppProfileFrame(
        string? path,
        int frame,
        long t0,
        long t1,
        long t2,
        long t3,
        long t4,
        UiDrawData drawData,
        float fps,
        DuxelFrameAllocationSample? allocationSample,
        long rendererAllocated)
    {
        var newFrameUs = TicksToMicroseconds(t1 - t0);
        var renderUs = TicksToMicroseconds(t2 - t1);
        var getDrawDataUs = TicksToMicroseconds(t3 - t2);
        var rendererUs = TicksToMicroseconds(t4 - t3);
        var totalUs = TicksToMicroseconds(t4 - t0);
        var line = $"[duxel-app-prof] frame={frame} fps={fps:0.0} new={newFrameUs:0.000} render={renderUs:0.000} getDraw={getDrawDataUs:0.000} renderer={rendererUs:0.000} total={totalUs:0.000} lists={drawData.DrawLists.Count} vertices={drawData.TotalVertexCount} indices={drawData.TotalIndexCount}";
        if (allocationSample is { } allocation)
        {
            // Optional diagnostics only: GetTotalMemory(false) does not request
            // collection. GCMemoryInfo describes the most recent collection.
            var memory = GC.GetGCMemoryInfo();
            line += $" gcHeap={GC.GetTotalMemory(false)} gcCommitted={memory.TotalCommittedBytes} gcLastHeap={memory.HeapSizeBytes} gcGen0={GC.CollectionCount(0)} gcGen1={GC.CollectionCount(1)} gcGen2={GC.CollectionCount(2)} gcAllocated={GC.GetTotalAllocatedBytes(precise: false)}";
            line += $" allocNewFrame={allocation.NewFrame} allocRender={allocation.Render} allocDrawData={allocation.DrawData} allocRenderer={rendererAllocated}";
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine(line);
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(path, line + Environment.NewLine);
    }

    private static double TicksToMicroseconds(long ticks)
    {
        return ticks * (1000000.0 / Stopwatch.Frequency);
    }
}
