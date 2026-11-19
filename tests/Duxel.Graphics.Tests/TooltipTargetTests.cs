using Duxel.Core;

internal static class TooltipTargetTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string name) => check(value, "tooltip target : " + name);
        using var scene = new Scene();
        scene.Frame(0.01f, new(30, 70));
        Check(!scene.First.IsOpen && scene.Paints == 0, "hover waits before painting");
        Check(scene.State.NextScheduledFrameTimeSeconds is { } due && Math.Abs(due - 0.26) < 0.0001,
            "show deadline is scheduled on the UI clock");
        scene.Frame(0.24f, new(30, 70));
        Check(!scene.First.IsOpen && scene.Paints == 0, "not visible before the deadline");
        scene.Frame(0.011f, new(30, 70));
        Check(scene.First.IsOpen && scene.Paints == 1, "stationary hover opens on the due frame");
        Check(scene.CursorBefore == scene.CursorAfter, "deferred painting does not advance target layout");
        scene.Frame(0.01f, new(30, 30));
        Check(scene.First.IsOpen && scene.State.NextScheduledFrameTimeSeconds is null,
            "surface hover keeps the owner without animation or extra deadline");
        scene.Frame(0.01f, new(-100, -100));
        Check(scene.First.IsOpen && scene.Paints == 1, "mouseout starts a hide delay");
        scene.Frame(0.24f, new(30, 70));
        Check(scene.First.IsOpen && scene.State.NextScheduledFrameTimeSeconds is null, "returning cancels pending hide");
        scene.Frame(0.01f, new(150, 70));
        Check(!scene.First.IsOpen && scene.Second.IsOpen && scene.Paints == 1 && scene.LastText == "Second",
            "handover paints only the new target even when the old target rendered first");
        scene.Frame(0.01f, new(150, 70), [new(UiKey.Escape, true, KeyModifiers.None)]);
        Check(!scene.Second.IsOpen && scene.Paints == 0 && !scene.EscapePressed,
            "Escape closes before child and public key handlers");
        scene.Frame(1, new(150, 70));
        Check(!scene.Second.IsOpen, "stationary hover cannot reopen an Escape-dismissed owner");
        scene.Frame(0.01f, new(-100, -100));
        scene.Frame(0.01f, new(150, 70));
        scene.Frame(0.26f, new(150, 70));
        Check(scene.Second.IsOpen, "leaving and reentering permits a fresh show");
        scene.HideSecond = true;
        scene.Frame(0.01f, new(150, 70));
        Check(!scene.Second.IsOpen && scene.State.NextScheduledFrameTimeSeconds is null,
            "omitting the owner releases visible and pending work");
        scene.HideSecond = false;
        scene.Clip = new(20, 60, 10, 24);
        scene.Frame(0.01f, new(50, 70));
        scene.Frame(0.26f, new(50, 70));
        Check(!scene.First.IsOpen, "clipped-away pointer cannot start a tooltip");
        scene.Frame(0.01f, new(25, 70));
        scene.Frame(0.26f, new(25, 70));
        Check(scene.First.IsOpen && scene.LastBounds.Y == 20, "visible target escapes the ancestor clip through a popup layer");
        scene.Clip = new(250, 200, 10, 10);
        scene.Frame(0.01f, new(25, 70));
        Check(!scene.First.IsOpen, "fully clipped targets dismiss immediately");
        scene.Clip = null;
        scene.Disabled = true;
        scene.Frame(0.01f, new(30, 70));
        scene.Frame(0.26f, new(30, 70));
        Check(scene.First.IsOpen, "disabled target hover can explain enablement");
        scene.PopupAfter = true;
        scene.Frame(0.01f, new(30, 70));
        Check(!scene.First.IsOpen && scene.Paints == 0, "a newly opened higher popup suppresses earlier target registration");
        scene.PopupAfter = false;
        scene.Disabled = false;
        scene.Frame(0.01f, new(-100, -100));
        scene.Frame(0.01f, new(-100, -100));
        scene.Frame(0.01f, new(-100, -100), [new(UiKey.Tab, true, KeyModifiers.None)]);
        scene.FirstId = scene.State.FocusedId!;
        scene.Frame(0.26f, new(-100, -100));
        Check(scene.First.IsOpen, "keyboard focus opens without pointer hover");
        var focused = scene.State.FocusedId;
        scene.Frame(0.01f, new(-100, -100), [new(UiKey.Escape, true, KeyModifiers.None)]);
        Check(!scene.First.IsOpen && scene.State.FocusedId == focused, "Escape retains target focus");
        scene.State.FocusedId = null;
        scene.Frame(0.01f, new(-100, -100));
        scene.State.FocusedId = scene.FirstId;
        scene.Frame(0.01f, new(-100, -100));
        scene.Frame(0.26f, new(-100, -100));
        scene.State.FocusedId = null;
        scene.Frame(0.01f, new(-100, -100));
        Check(!scene.First.IsOpen, "focus blur dismisses without mouse hide delay");
        scene.Frame(0.01f, new(30, 70));
        scene.Frame(0.26f, new(30, 70));
        using var other = new Scene();
        other.Frame(0.01f, new(-100, -100), moved : scene.First);
        Check(!scene.First.IsOpen, "context transfer releases the previous window owner");
        scene.Frame(0.01f, new(30, 70));
        scene.Frame(0.26f, new(30, 70));
        scene.First.Dispose();
        Check(!scene.First.IsOpen && scene.Requests > 0, "disposal clears ownership and requests repaint");
        var invalid = false;
        try
        {
            other.First.Options = new() { ShowDelaySeconds = double.NaN };
        }
        catch (ArgumentOutOfRangeException)
        {
            invalid = true;
        }
        Check(invalid, "nonfinite delays fail explicitly");
        using var input = new Scene(inputTarget : true);
        input.Frame(0.01f, new(30, 70), press : true);
        input.Frame(0.26f, new(30, 70));
        var activeInput = input.State.ActiveId;
        Check(input.First.IsOpen && activeInput is not null, "real active text input hover opens its tooltip");
        input.Frame(0.01f, new(30, 70), [new(UiKey.Escape, true, KeyModifiers.None)]);
        Check(!input.First.IsOpen && input.State.ActiveId == activeInput,
            "consumed Escape does not deactivate the actual child input kernel");
        input.Frame(0.01f, new(-100, -100));
        input.Frame(0.01f, new(-100, -100), [new(UiKey.Escape, true, KeyModifiers.None)]);
        Check(input.State.ActiveId is null, "a later Escape reaches the child when no tooltip is open");
        using var delayed = new Scene();
        delayed.Frame(0.01f, new(30, 70));
        delayed.Frame(0.1f, new(-100, -100));
        Check(delayed.State.NextScheduledFrameTimeSeconds is null && !delayed.First.IsOpen,
            "leaving during show delay cancels the pending owner");
        delayed.Frame(0.01f, new(30, 70));
        delayed.Frame(0.26f, new(30, 70));
        delayed.Frame(0.01f, new(-100, -100));
        delayed.Frame(0.26f, new(-100, -100));
        Check(!delayed.First.IsOpen && delayed.Paints == 0 && delayed.State.NextScheduledFrameTimeSeconds is null,
            "hide deadline closes once and leaves no scheduled work");
        RunControlled(Check);
        RunOverlappingSurfaces(Check);
        RunScrollbars(Check);
    }

    private static void RunScrollbars(Action<bool, string> check)
    {
        using var scene = new Scene(firstVisible : true, secondVisible : false) { OverlapSurfaces = true };
        scene.First.Options = scene.First.Options with { ShowScrollbars = true };
        var outside = new UiVector2(-100, -100);
        scene.Frame(.01f, outside);
        scene.Frame(.01f, new(175, 25), press : true);
        check(!scene.LeftPressed && scene.State.ActiveId is not null && scene.FirstScroll == default,
            "vertical thumb captures the press before underlying widgets");
        scene.Frame(.26f, new(175, 80), hold : true);
        check(scene.First.IsOpen && scene.FirstScroll == new UiVector2(0, 64),
            "vertical thumb drag outside the popup clamps and keeps its owner open");
        scene.Frame(.01f, new(175, 80), release : true);
        check(!scene.LeftReleased && scene.State.ActiveId is null, "releasing the thumb consumes the captured release");

        scene.Frame(.01f, new(30, 43), press : true);
        scene.Frame(.01f, new(80, 43), hold : true);
        check(scene.FirstScroll == new UiVector2(16, 64), "horizontal thumb drag changes only its own axis");
        scene.First.Visible = false;
        scene.Frame(.01f, new(80, 43), hold : true);
        check(!scene.First.IsOpen && scene.State.ActiveId is null, "external dismissal releases scrollbar capture");

        scene.First.Visible = true;
        scene.Frame(.01f, outside, release : true);
        check(scene.FirstScroll == default, "reopening resets both scrollbar offsets");
        scene.Frame(.01f, new(175, 37), press : true);
        check(!scene.LeftPressed && scene.FirstScroll.Y > 0, "track press scrolls and consumes the underlying click");
        scene.Frame(.01f, new(175, 37), [new(UiKey.Escape, true, KeyModifiers.None)], hold : true);
        check(!scene.EscapePressed && scene.State.ActiveId is null && scene.First.IsOpen,
            "Escape cancels scrollbar capture even when controlled dismissal is declined");

        using var automatic = new Scene() { OverlapSurfaces = true };
        automatic.First.Options = automatic.First.Options with { ShowScrollbars = true, ShowDelaySeconds = 0 };
        automatic.Frame(.01f, new(30, 70));
        automatic.Frame(.01f, new(175, 25), press : true);
        automatic.Frame(.40f, new(180, 120), hold : true);
        check(automatic.First.IsOpen && automatic.FirstScroll.Y == 64,
            "automatic tooltip remains open while its scrollbar owns the pointer outside the surface");
        automatic.OverlapSurfaces = false;
        automatic.Frame(.01f, new(180, 120), hold : true);
        check(automatic.State.ActiveId is null && automatic.FirstScroll == default,
            "shrinking content clamps both offsets and cancels a disappearing scrollbar");

        using var rtl = new Scene(firstVisible : true, secondVisible : false) { OverlapSurfaces = true };
        rtl.First.Options = rtl.First.Options with { ShowScrollbars = true, Positioning = new() { RightToLeft = true } };
        rtl.Frame(.01f, outside);
        check(rtl.FirstScroll == new UiVector2(16, 0), "an RTL tooltip initially paints the physical right edge");
        rtl.Frame(.01f, new(170, 43), press : true);
        rtl.Frame(.01f, new(20, 43), hold : true);
        check(rtl.FirstScroll == default && rtl.State.ActiveId is not null, "the RTL tooltip thumb follows a physical drag to the left");
        rtl.Frame(.01f, outside, release : true);
        rtl.Frame(.01f, new(80, 30), wheelX : -1);
        check(rtl.FirstScroll == new UiVector2(16, 0), "the physical wheel returns the RTL tooltip to the right edge");
    }

    private static void RunOverlappingSurfaces(Action<bool, string> check)
    {
        using var scene = new Scene(firstVisible : true, secondVisible : true) { OverlapSurfaces = true };
        scene.First.Options = scene.First.Options with { ScrollStep = 16 };
        scene.Second.Options = scene.Second.Options with { ScrollStep = 16 };
        var firstChanges = new List<UiTooltipVisibilityChange>();
        var secondChanges = new List<UiTooltipVisibilityChange>();
        scene.First.VisibleChangeRequested = firstChanges.Add;
        scene.Second.VisibleChangeRequested = secondChanges.Add;
        var outside = new UiVector2(-100, -100);
        var overlap = new UiVector2(150, 30);
        scene.Frame(.01f, outside);
        scene.Frame(.01f, overlap, wheel : -1, wheelX : -1);
        check(scene.FirstScroll == default && scene.SecondScroll == new UiVector2(16, 16) && scene.RemainingWheel == 0,
            "frontmost overlapping surface alone receives both wheel axes");
        scene.Frame(.01f, overlap, wheel : -1000);
        scene.Frame(.01f, overlap, wheel : -1);
        check(scene.FirstScroll == default && scene.SecondScroll == new UiVector2(16, 64) && scene.RemainingWheel == -1,
            "frontmost surface chains at its edge without scrolling the occluded tooltip");
        scene.Frame(.01f, outside);
        scene.Frame(.26f, outside);
        check(firstChanges.Count == 0 && secondChanges.SequenceEqual([new(false, UiTooltipVisibilityReason.Interaction)]),
            "leaving overlap requests dismissal only for the frontmost surface");
        scene.Second.Visible = false;
        scene.Frame(.01f, overlap, wheel : -1);
        check(scene.Paints == 1 && scene.FirstScroll == new UiVector2(0, 16) && scene.RemainingWheel == 0,
            "externally closing the front surface transfers wheel ownership immediately");
    }

    private static void RunControlled(Action<bool, string> check)
    {
        using var scene = new Scene(firstVisible : false, secondVisible : false);
        var firstChanges = new List<UiTooltipVisibilityChange>();
        var secondChanges = new List<UiTooltipVisibilityChange>();
        var outside = new UiVector2(-100, -100);
        var first = new UiVector2(30, 70);
        var escape = new UiKeyEvent[] { new(UiKey.Escape, true, KeyModifiers.None) };
        scene.First.VisibleChangeRequested = firstChanges.Add;
        scene.Second.VisibleChangeRequested = secondChanges.Add;
        scene.Frame(.01f, first);
        scene.Frame(.10f, outside);
        check(firstChanges.Count == 0 && scene.State.NextScheduledFrameTimeSeconds is null,
            "controlled show cancels on leave before the deadline");
        scene.Frame(.01f, first);
        scene.Frame(.26f, first);
        check(firstChanges.SequenceEqual([new(true, UiTooltipVisibilityReason.Interaction)])
            && !scene.First.IsOpen && scene.Paints == 0, "controlled show request can be declined");
        scene.Frame(1, first);
        check(firstChanges.Count == 1 && scene.State.NextScheduledFrameTimeSeconds is null,
            "declined show is not retried each frame");
        scene.Frame(.01f, outside);
        scene.First.Visible = true;
        scene.Frame(.01f, outside);
        scene.Frame(.26f, outside);
        check(scene.First.IsOpen && scene.Paints == 1 && firstChanges.Count == 1,
            "external open without interaction does not request a change or hide itself");
        scene.Second.Visible = true;
        scene.Frame(.01f, outside);
        check(scene.First.IsOpen && scene.Second.IsOpen && scene.Paints == 2
            && firstChanges.Last() == new UiTooltipVisibilityChange(false, UiTooltipVisibilityReason.Handover)
            && secondChanges.Count == 0, "declined handover keeps both controlled tooltips visible");
        scene.Frame(.01f, outside, escape);
        check(!scene.EscapePressed && scene.Paints == 2 && firstChanges.Count == 3 && secondChanges.Count == 0
            && firstChanges.Last() == new UiTooltipVisibilityChange(false, UiTooltipVisibilityReason.Escape),
            "Escape is consumed by the first registered visible tooltip");
        scene.First.VisibleChangeRequested = change => scene.First.Visible = change.Visible;
        scene.Frame(.01f, outside, escape);
        check(scene.First.Visible == false && scene.First.IsOpen && scene.Paints == 2,
            "callback acknowledgement applies to the next render snapshot");
        scene.Frame(.01f, outside);
        check(!scene.First.IsOpen && scene.Second.IsOpen && scene.Paints == 1,
            "closing one controlled tooltip preserves the other");
        scene.Frame(.01f, outside, escape);
        check(secondChanges.SequenceEqual([new(false, UiTooltipVisibilityReason.Escape)]),
            "Escape moves to the remaining visible tooltip");
        scene.HideSecond = true;
        scene.Frame(.01f, outside);
        check(!scene.Second.IsOpen && scene.Second.Visible == true && secondChanges.Count == 1,
            "omission releases the surface and preserves the supplied value");
        var modeChangeRejected = false;
        try
        {
            scene.First.Visible = null;
        }
        catch (InvalidOperationException)
        {
            modeChangeRejected = true;
        }
        check(modeChangeRejected, "visibility mode is fixed on first render");

        using var mixed = new Scene(firstVisible : true);
        var mixedChanges = new List<UiTooltipVisibilityChange>();
        mixed.First.VisibleChangeRequested = mixedChanges.Add;
        mixed.Frame(.01f, new(150, 70));
        check(mixed.Paints == 2 && mixedChanges.SequenceEqual([new(false, UiTooltipVisibilityReason.Handover)]),
            "automatic handover can coexist with a controlled tooltip that declines dismissal");
        mixed.Frame(.01f, first);
        mixed.Frame(.01f, outside);
        mixed.Frame(.26f, outside);
        check(mixed.First.IsOpen && mixedChanges.Count == 2 && mixedChanges.Last() ==
            new UiTooltipVisibilityChange(false, UiTooltipVisibilityReason.Interaction),
            "leaving a controlled trigger requests a delayed hide without forcing it");

        using var lifecycle = new Scene(firstVisible : true);
        var lifecycleChanges = 0;
        lifecycle.First.VisibleChangeRequested = _ => lifecycleChanges++;
        lifecycle.Clip = new(100, 0, 220, 240);
        lifecycle.Frame(.01f, outside);
        check(!lifecycle.First.IsOpen && lifecycle.First.Visible == true && lifecycleChanges == 0,
            "clipping releases presentation without requesting a change");
        lifecycle.Clip = null;
        lifecycle.Frame(.01f, outside);
        check(lifecycle.First.IsOpen && lifecycleChanges == 0, "unclipping restores a controlled true value");
        using var destination = new Scene();
        destination.Frame(.01f, outside, moved : lifecycle.First);
        check(lifecycle.First.IsOpen && destination.Paints == 1 && lifecycleChanges == 0,
            "context transfer paints the supplied value in the destination");
        lifecycle.First.VisibleChangeRequested = _ => lifecycle.First.Dispose();
        destination.Frame(.01f, outside, escape, moved : lifecycle.First);
        check(!lifecycle.First.IsOpen && destination.Paints == 0,
            "disposal in a visibility callback prevents deferred painting");

        using var throwing = new Scene(firstVisible : true);
        var expected = new InvalidOperationException("Tooltip callback failure");
        var calls = 0;
        throwing.First.VisibleChangeRequested = _ =>
        {
            calls++;
            throw expected;
        };
        var propagated = false;
        try
        {
            throwing.Frame(.01f, outside, escape);
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, expected))
        {
            propagated = true;
        }
        throwing.Frame(.01f, outside);
        check(propagated && calls == 1 && throwing.First.IsOpen && throwing.Paints == 1,
            "callback failures propagate and the next frame recovers without replay");
    }

    private sealed class Scene : IDisposable
    {
        private readonly UiContext _context;
        internal readonly UiTooltip<UiHoverFocusTooltipBehavior, Surface> First;
        internal readonly UiTooltip<UiHoverFocusTooltipBehavior, Surface> Second;
        internal UiState State => _context.State;
        internal string FirstId = string.Empty;
        internal bool HideSecond, Disabled, PopupAfter, EscapePressed;
        internal bool OverlapSurfaces;
        internal bool LeftPressed, LeftReleased;
        internal float RemainingWheel;
        internal UiVector2 FirstScroll, SecondScroll;
        internal UiRect? Clip;
        internal int Paints, Requests;
        internal string LastText = string.Empty;
        internal UiRect LastBounds;
        internal UiVector2 CursorBefore, CursorAfter;
        internal Scene(bool inputTarget = false, bool? firstVisible = null, bool? secondVisible = null)
        {
            var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
                code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255], glyphs,
                new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
            _context.SetRequestFrameCallback(() => Requests++);
            var value = "Editable";
            First = inputTarget
                ? new(new UiInput<UiStandardTextInputBehavior, UiContextInputPresentation>(string.Empty,
                    new(() => value, next => value = next), 128, 80), "First")
                : Create("First");
            Second = Create("Second");
            First.Visible = firstVisible;
            Second.Visible = secondVisible;
            Frame(0, new(-100, -100));
        }
        private static UiTooltip<UiHoverFocusTooltipBehavior, Surface> Create(string text) =>
            new(new UiButton<UiReleaseButtonBehavior, Button>(text) { Size = new(80, 24) }, text);
        internal void Frame(float delta, UiVector2 mouse, UiKeyEvent[]? keys = null,
            UiTooltip<UiHoverFocusTooltipBehavior, Surface>? moved = null, bool press = false,
            float wheel = 0, float wheelX = 0, bool hold = false, bool release = false)
        {
            Paints = 0;
            Surface.Current = this;
            _context.NewFrame(new(delta, new(320, 240), new(1, 1)),
                new(mouse, press || hold, false, press, release, false, false, wheel, wheelX, keys ?? [], [], default, default),
                new(0, 0, 320, 240), UiTextSettings.Default);
            UiImmediateContext? rendered = null;
            _context.Render(new Screen(ui =>
            {
                rendered = ui;
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 60));
                using var clip = new UiControlContext(ui).Clip(Clip ?? new(0, 0, 320, 240));
                ui.BeginDisabled(Disabled);
                try
                {
                    (moved ?? First).Render(ui);
                    if (!HideSecond)
                    {
                        ui.SetCursorScreenPos(new(140, 60));
                        Second.Render(ui);
                    }
                }
                finally
                {
                    ui.EndDisabled();
                }
                if (PopupAfter)
                {
                    State.AddPopupBlockingRect(new(0, 0, 320, 240), 1);
                }
                CursorBefore = ui.GetCursorScreenPos();
                EscapePressed = ui.IsKeyPressed(UiKey.Escape);
                LeftPressed = ui.IsMouseClicked((int)UiMouseButton.Left);
                LeftReleased = ui.IsMouseReleased((int)UiMouseButton.Left);
                RemainingWheel = ui.GetMouseWheel();
            }));
            CursorAfter = rendered!.GetCursorScreenPos();
            using var draw = _context.GetDrawData();
        }
        public void Dispose()
        {
            First.Dispose();
            Second.Dispose();
            _context.Dispose();
        }
    }

    private readonly struct Button : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => requestedSize;
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction)
        {
            context.Frame(bounds, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
        }
    }
    private readonly struct Surface : IUiTooltipPresentation
    {
        internal static Scene Current = null!;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => Current.OverlapSurfaces
                ? new(new(target.X, target.Y - 40, 160, 28), UiPopupPosition.Above) { ContentSize = new(176, 92) }
                : new(new(target.X, target.Y - 40, 80, 28), UiPopupPosition.Above);
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout)
        {
            var bounds = layout.Bounds;
            Current.Paints++;
            Current.LastText = text;
            Current.LastBounds = bounds;
            if (text == "First")
            {
                Current.FirstScroll = layout.ScrollOffset;
            }
            else if (text == "Second")
            {
                Current.SecondScroll = layout.ScrollOffset;
            }
            context.Frame(bounds, new(220, 240, 255), new(0, 0, 0, 0), 4, 0);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
