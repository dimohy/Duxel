using System.Diagnostics;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class AppOrderedInputTests
{
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

    internal static void Run(Action<bool, string> check, bool nativeCapture = false)
    {
        var keys = new List<UiKeyEvent> { new(UiKey.A, true, KeyModifiers.Ctrl) };
        var chars = new List<UiCharEvent> { new('X') };
        var original = Idle with
        {
            MousePosition = new(20, 40),
            KeyEvents = keys,
            CharEvents = chars,
            Modifiers = KeyModifiers.Ctrl,
            MiddleMouseDown = true,
            EventTimeSeconds = 10.5
        };
        var steps = new List<UiInputState> { original };
        var copied = AppInputSnapshots.Copy(Snapshot(steps) with
        {
            KeyEvents = keys,
            CharEvents = chars
        });
        keys.Clear();
        chars.Clear();
        steps.Clear();
        check(copied.KeyEvents.Count == 1 && copied.CharEvents.Count == 1, "App capture owns header key and character lists independently of producer reuse");
        check(copied.OrderedInputs is { Count: 1 } && copied.OrderedInputs[0].KeyEvents.Count == 1 && copied.OrderedInputs[0].CharEvents.Count == 1,
            "App capture owns every ordered step and its nested key and character lists");
        var next = AppInputSnapshots.Copy(Snapshot([Idle with { CharEvents = [new('Y')] }]));
        var pending = new AppInputSnapshots.PendingInput();
        pending.Add(copied);
        pending.Add(next);
        var merged = pending.Drain();
        check(merged.OrderedInputs is { Count: 2 } ordered && ordered[0].CharEvents[0].CodePoint == 'X' && ordered[1].CharEvents[0].CodePoint == 'Y',
            "App merges pending ordered packets without grouping their input channels");
        var cleared = pending.Drain();
        check(cleared.OrderedInputs is { Count: 0 } && cleared.KeyEvents.Count == 0 && cleared.CharEvents.Count == 0,
            "App consumption clears ordered events while preserving ordered capture mode");
        pending.Add(next);
        pending.Add(next);
        pending.Add(next);
        check(merged.OrderedInputs!.Count == 2 && merged.OrderedInputs[0].CharEvents[0].CodePoint == 'X',
            "new packets do not mutate a drained frame still owned by the worker");
        var logical = AppInputSnapshots.ToLogical(copied, 2)!;
        check(logical[0].MousePosition == new UiVector2(10, 20) && logical[0].Modifiers == KeyModifiers.Ctrl && logical[0].MiddleMouseDown && logical[0].EventTimeSeconds == 10.5,
            "App converts every ordered pointer without losing modifiers, middle-button state or captured event time");
        check(copied.OrderedInputs![0].MousePosition == new UiVector2(20, 40), "logical coordinate conversion does not mutate the native capture");
        var legacy = Snapshot(null) with
        {
            CharEvents = [new('L')],
            LeftMouseDown = true,
            LeftMousePressedEvent = true
        };
        var legacyCopy = AppInputSnapshots.Copy(legacy);
        var legacyPending = new AppInputSnapshots.PendingInput();
        legacyPending.Add(legacyCopy);
        legacyPending.Add(legacyCopy);
        var legacyMerged = legacyPending.Drain();
        check(legacyMerged.OrderedInputs is null && legacyMerged.CharEvents.Count == 2 && legacyMerged.LeftMousePressedEvent,
            "legacy aggregate input retains its original merge behavior");
        var legacyClear = AppInputSnapshots.Clear(legacyMerged);
        check(legacyClear.OrderedInputs is null && legacyClear.LeftMouseDown && !legacyClear.LeftMousePressedEvent,
            "legacy clearing retains held pointer state and legacy mode");
        foreach (var reverse in new[] { false, true })
        {
            var rejected = false;
            try
            {
                var mixed = new AppInputSnapshots.PendingInput();
                mixed.Add(reverse ? copied : legacy);
                mixed.Add(reverse ? legacy : copied);
            }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, "capture mode changes fail explicitly instead of silently dropping order : " + reverse);
        }
        var scaleRejected = false;
        try
        {
            AppInputSnapshots.ToLogical(copied, 0);
        }
        catch (ArgumentOutOfRangeException) { scaleRejected = true; }
        check(scaleRejected, "invalid ordered-input scale fails before dispatch");
        var glyphs = new HashSet<int>();
        var glyphPacket = Snapshot([Idle with { CharEvents = [new(0xD55C)] }]);
        check(AppInputSnapshots.CaptureGlyphs(glyphPacket, glyphs) == 1 && glyphs.Contains(0xD55C),
            "glyph discovery reads characters present only in ordered input steps");
        check(AppInputSnapshots.CaptureGlyphs(glyphPacket, glyphs) == 0, "ordered glyph discovery keeps existing deduplication");
        var missingOrderRejected = false;
        try
        {
            AppInputSnapshots.Copy(Snapshot([]) with
            {
                CharEvents = [new('M')]
            });
        }
        catch (InvalidOperationException) { missingOrderRejected = true; }
        check(missingOrderRejected, "ordered capture cannot silently dispatch aggregate characters with missing ordered steps");
        var mixedScale = AppInputSnapshots.ToLogical(Snapshot([
            Idle with { MousePosition = new(20, 20), CoordinateScale = 1 },
            Idle with { MousePosition = new(40, 40), CoordinateScale = 2 }
        ]), 4)!;
        check(mixedScale.All(value => value.MousePosition == new UiVector2(20, 20) && value.CoordinateScale is null),
            "merged native input retains each event's capture-time scale instead of applying the latest host scale");
        var recentGlyphs = new HashSet<int>();
        check(AppInputSnapshots.CaptureGlyphs("한😀\r\n", recentGlyphs) == 1 && recentGlyphs.Contains('한'),
            "recent committed text discovers BMP glyphs without publishing surrogate halves");
        var excursion = Snapshot([Idle with { MousePosition = new(40, 60) }, Idle with { MousePosition = default }]);
        check(AppInputSnapshots.HasInputDrivenInvalidation(excursion, Snapshot([])),
            "a pointer excursion ending at the previous position is still pending input");

        const int packetCount = 1024;
        var packets = new InputSnapshot[packetCount];
        for (var i = 0; i < packets.Length; i++)
            packets[i] = AppInputSnapshots.Copy(Snapshot([Idle with { MousePosition = new(i, 0) }]));
        var burst = new AppInputSnapshots.PendingInput();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        foreach (var packet in packets)
            burst.Add(packet);
        var drained = burst.Drain();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        check(drained.OrderedInputs is { Count: packetCount } captured
            && captured.Select((step, index) => step.MousePosition.X == index).All(static correct => correct),
            "the app drains every position in a multi-packet burst in order");
        check(allocated < 1024 * 1024, "the pending burst uses linear storage even when the producer delivers one move per packet");

        foreach (var scale in new[] { 1f, 2f })
            foreach (var charFirst in new[] { false, true })
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
                // Each session owns its Win32 message queue, including its final WM_QUIT.
                var thread = new Thread(() =>
                {
                    try
                    {
                        var session = new DuxelAppSession();
                        using var host = new ScriptedHost(session, scale, charFirst, nativeCapture);
                        using var screen = new ScriptedScreen(host);
                        host.Screen = screen;
                        session.RunCore(new()
                        {
                            Screen = screen,
                            Design = UiCompiledDesign.Windows11Dark,
                            Window = new()
                            {
                                Width = 320,
                                Height = 240,
                                VSync = false
                            },
                            Frame = new()
                            {
                                EnableIdleFrameSkip = true,
                                UseEventDrivenPlatformLoop = true
                            }
                        }, host);
                        var prefix = $"App ordered session : native={nativeCapture}, scale={scale}, charFirst={charFirst}";
                        check(!host.TimedOut && screen.CompletionCallbackRan && screen.LastUi!.GetRendererFrameMetrics().PresentationSequence > screen.SequenceBeforeCompletion,
                            prefix + " / actual Vulkan session acknowledges the completed ordered frame");
                        check(screen.First == (charFirst ? "aX" : "a") && screen.Second == (charFirst ? "b" : "bX"), prefix + " / actual App worker dispatches text and focus in order");
                        check(screen.WorkerThread > 0 && screen.WorkerThread != host.OwnerThread, prefix + " / producer and UI consumer run on separate threads");
                        check(!host.WindowShown, prefix + " / owned window stays hidden and no global input is sent");
                        if (nativeCapture)
                            check(host.ImeHandler!.ConsumeRecentCommittedText() is null,
                                prefix + " / App consumes the recent-text metadata channel after glyph discovery");
                    }
                    catch (Exception ex)
                    {
                        failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
                    }
                })
                {
                    IsBackground = true,
                    Name = "Duxel owned ordered session"
                };
                thread.Start();
                if (!thread.Join(TimeSpan.FromSeconds(20)))
                    throw new TimeoutException("The owned ordered App session exceeded its execution budget.");
                failure?.Throw();
            }
    }

    private static InputSnapshot Snapshot(IReadOnlyList<UiInputState>? inputs) => new(default, false, false, false, false, false, false, false, false, false, 0, 0, [], [], default, default)
    {
        OrderedInputs = inputs
    };

    private sealed class ScriptedHost : IPlatformBackend, IPlatformEventWakeBackend, IInputBackend
    {
        private readonly WindowsPlatformBackend _native;
        private readonly bool _nativeCapture;
        private readonly DuxelAppSession _session;
        private readonly float _scale;
        private readonly bool _charFirst;
        private readonly Stopwatch _deadline = Stopwatch.StartNew();
        private int _presented;
        private int _phase;
        private InputSnapshot _snapshot = AppOrderedInputTests.Snapshot([]);
        internal ScriptedScreen? Screen;
        internal readonly int OwnerThread = Environment.CurrentManagedThreadId;
        internal bool TimedOut => _deadline.Elapsed > TimeSpan.FromSeconds(15);
        internal bool WindowShown => _native.ValidationWindowShown;
        internal ScriptedHost(DuxelAppSession session, float scale, bool charFirst, bool nativeCapture)
        {
            _nativeCapture = nativeCapture;
            _native = new(new()
            {
                Width = 320,
                Height = 240,
                Title = "Duxel owned ordered App input",
                VSync = false,
                TrayOptions = new(),
                KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
                EnableOrderedInput = nativeCapture
            });
            _session = session;
            _native.ValidationInputCoordinateScale = scale;
            _scale = scale;
            _charFirst = charFirst;
        }
        public PlatformSize WindowSize => _native.WindowSize;
        public PlatformSize FramebufferSize => _native.FramebufferSize;
        public float ContentScale => _scale;
        public bool IsInteractingResize => false;
        public bool ShouldClose => _native.ShouldClose || TimedOut;
        public double TimeSeconds => _native.TimeSeconds;
        public IInputBackend Input => this;
        public InputSnapshot Snapshot => _nativeCapture ? _native.Input.Snapshot : _snapshot;
        public IUiImeHandler? ImeHandler => _nativeCapture ? _native.ImeHandler : null;
        public IVulkanSurfaceSource? VulkanSurface => _native.VulkanSurface;
        public IPlatformTextBackend? TextBackend => _native.TextBackend;
        public void WakeEvents() => _native.WakeEvents();
        public void SetMouseCursor(UiMouseCursor cursor) => _native.SetMouseCursor(cursor);
        public void PollEvents()
        {
            _native.PollEvents();
            Publish();
        }
        public void WaitEvents(int timeoutMilliseconds)
        {
            var remaining = (int)Math.Ceiling((TimeSpan.FromSeconds(15) - _deadline.Elapsed).TotalMilliseconds);
            if (remaining <= 0)
                return;
            _native.WaitEvents(timeoutMilliseconds == 0 ? remaining : Math.Min(timeoutMilliseconds, remaining));
            Publish();
        }
        private void Publish()
        {
            _snapshot = AppOrderedInputTests.Snapshot([]);
            if (_phase == 0 && Volatile.Read(ref _presented) > 0)
            {
                _snapshot = AppOrderedInputTests.Snapshot([
                    Idle with { MousePosition = new(100 * _scale, 10 * _scale), LeftMouseDown = true, LeftMousePressed = true },
                    Idle with { MousePosition = new(100 * _scale, 10 * _scale), LeftMouseReleased = true }
                ]);
                if (_nativeCapture)
                {
                    _native.ValidationCaptureOrderedPointer(new(100 * _scale, 10 * _scale), true);
                    _native.ValidationCaptureOrderedPointer(new(100 * _scale, 10 * _scale), false);
                }
                _phase = 1;
            }
            else if (_phase == 1 && Screen!.FirstReleased)
            {
                var click = Idle with
                {
                    MousePosition = new(100 * _scale, 70 * _scale),
                    LeftMouseDown = true,
                    LeftMousePressed = true
                };
                var text = Idle with
                {
                    MousePosition = new(100 * _scale, 10 * _scale),
                    CharEvents = [new('X')]
                };
                var release = Idle with
                {
                    MousePosition = click.MousePosition,
                    LeftMouseReleased = true
                };
                _snapshot = AppOrderedInputTests.Snapshot(_charFirst ? [text, click, release] : [click, text with { MousePosition = click.MousePosition, LeftMouseDown = true }, release]);
                if (_nativeCapture)
                {
                    if (_charFirst)
                        WindowsImeOwnershipTests.SendOrderedNativeCharacter(_native.WindowHandle, 'X');
                    _native.ValidationCaptureOrderedPointer(click.MousePosition, true);
                    if (!_charFirst)
                        WindowsImeOwnershipTests.SendOrderedNativeCharacter(_native.WindowHandle, 'X');
                    _native.ValidationCaptureOrderedPointer(click.MousePosition, false);
                }
                _phase = 2;
            }
        }
        public void NotifyFirstFramePresented()
        {
            Interlocked.Increment(ref _presented);
            _native.WakeEvents();
        }
        internal void Exit() => _session.Exit();
        public void Dispose() => _native.Dispose();
    }

    private sealed class ScriptedScreen : UiScreen, IDisposable
    {
        private readonly ScriptedHost _host;
        private readonly UiWidget _a;
        private readonly UiWidget _b;
        internal volatile string First = "a";
        internal volatile string Second = "b";
        internal volatile bool FirstReleased;
        internal volatile bool CompletionCallbackRan;
        internal ulong SequenceBeforeCompletion;
        internal UiImmediateContext? LastUi;
        internal int WorkerThread;
        internal ScriptedScreen(ScriptedHost host)
        {
            _host = host;
            _a = Fluent.Input(string.Empty, new UiBinding<string>(() => First, value => First = value), 256, 120);
            _b = Fluent.Input(string.Empty, new UiBinding<string>(() => Second, value => Second = value), 256, 120);
        }
        public override void Render(UiImmediateContext ui)
        {
            WorkerThread = Environment.CurrentManagedThreadId;
            LastUi = ui;
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(default);
            _a.Render(ui);
            if (ui.IsItemFocused() && ui.IsMouseReleased(0))
            {
                FirstReleased = true;
                _host.WakeEvents();
            }
            ui.SetCursorScreenPos(new(0, 60));
            _b.Render(ui);
            if (ui.IsItemFocused() && ui.IsMouseReleased(0) && (First.Contains('X') || Second.Contains('X')))
            {
                ui.GetWindowDrawList().AddCallback((_, _) =>
                {
                    SequenceBeforeCompletion = ui.GetRendererFrameMetrics().PresentationSequence;
                    CompletionCallbackRan = true;
                    _host.Exit();
                });
            }
        }
        public void Dispose()
        {
            _a.Dispose();
            _b.Dispose();
        }
    }
}
