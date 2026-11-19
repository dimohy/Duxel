using Duxel.Core;

internal static class ContentLayoutRetentionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var settings = UiTextSettings.Default;
        void Frame(Action<UiImmediateContext> render, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), settings);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiVector2 Measure(UiWidget widget)
        {
            UiVector2 size = default;
            Frame(ui => size = widget.MeasureConstrained(ui, UiBoxConstraints.Loose(new(600, 400))));
            return size;
        }
        var reads = 0;
        var fraction = .25f;
        var navigations = 0;
        var text = new MeasuredText();
        var link = new MeasuredLink(_ => navigations++);
        var progress = new MeasuredProgress(() => { reads++; return new(fraction); }) { Size = new(120, 4) };
        var badge = new MeasuredBadge();
        using var parent = Dux.Wrap(new(Spacing: 12, RunSpacing: 8), text, link, progress, badge);
        for (var i = 0; i < 3; i++)
            Measure(parent);
        int Counts() => text.Measurements + link.Measurements + progress.Measurements + badge.Measurements;
        var count = Counts();
        for (var i = 0; i < 10; i++)
            Measure(parent);
        check(Counts() == count && reads == 0,
            "static Text/Link/Badge and explicitly sized getter Progress reuse parent measurement without reading the value getter");
        Frame(parent.Render);
        Frame(parent.Render);
        check(text.Renders == 2 && link.Renders == 2 && progress.Renders == 2 && badge.Renders == 2 && reads == 2,
            "all retained content controls render each frame and Progress evaluates its real getter exactly once per render");
        count = Counts();
        fraction = .75f;
        Frame(parent.Render);
        check(Counts() == count && reads == 3,
            "render-only Progress values change without remeasurement or freezing the getter");
        var pointer = new UiVector2(link.Bounds.X + link.Bounds.Width / 2, link.Bounds.Y + link.Bounds.Height / 2);
        Frame(parent.Render, idle with
        {
            MousePosition = pointer,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(parent.Render, idle with
        {
            MousePosition = pointer,
            LeftMouseReleased = true
        });
        check(navigations == 1, "a retained parent continues actual Link pointer activation and caller navigation callback");
        count = text.Measurements;
        text.Source = "Changed static text";
        Measure(parent);
        check(text.Measurements > count, "static Text source mutation invalidates its retained parent");
        count = link.Measurements;
        link.Label = "Changed link";
        link.Width = 100;
        Measure(parent);
        check(link.Measurements > count, "Link label and wrap width invalidate parent measurement");
        count = progress.Measurements;
        progress.Size = new(140, 6);
        Measure(parent);
        check(progress.Measurements > count, "declared getter Progress size invalidates retained measurement");
        count = badge.Measurements;
        badge.Source = "Changed badge label";
        Measure(parent);
        check(badge.Measurements > count, "static Badge source invalidates its retained parent");
        count = Counts();
        settings = settings with
        {
            Scale = 2
        };
        Measure(parent);
        check(Counts() > count, "text environment changes invalidate all newly participating content families");
        var atlasObserverCalls = 0;
        settings = UiTextSettings.Default with
        {
            MissingGlyphObserver = _ => atlasObserverCalls++
        };
        Measure(parent);
        count = Counts();
        Measure(parent);
        check(Counts() == count && atlasObserverCalls == 0, "native content-family measurement retains reuse without atlas observer callbacks");
        settings = UiTextSettings.Default;
        var dynamicText = "A";
        using var getterText = new UiText<Func<string>, UiGetterTextSource, UiFluentTypographyPresentation<UiRegularTypeface, UiFluentType300>>(() => dynamicText);
        using var textParent = Dux.Wrap(default, getterText);
        var before = Measure(textParent);
        dynamicText = "A much longer dynamic text";
        check(Measure(textParent).X > before.X && !getterText.RetainsMeasurement,
            "getter Text remains dynamically measured when its value changes without a setter");
        var dynamicBadge = "Badge";
        using var getterBadge = new UiBadge<Func<string>, UiGetterBadgeBehavior, UiFluentBadgePresentation>(() => dynamicBadge);
        using var badgeParent = Dux.Wrap(default, getterBadge);
        before = Measure(badgeParent);
        dynamicBadge = "A much longer dynamic badge";
        check(Measure(badgeParent).X > before.X && !getterBadge.RetainsMeasurement,
            "getter Badge remains dynamically measured when text changes outside the widget");
        using var customMetrics = new UiText<UiFluentTypographyPresentation<UiRegularTypeface, MutableMetrics>>("Metrics");
        using var metricsParent = Dux.Wrap(default, customMetrics);
        MutableMetrics.Size = 14;
        before = Measure(metricsParent);
        MutableMetrics.Size = 28;
        check(Measure(metricsParent).Y > before.Y && !customMetrics.RetainsMeasurement,
            "custom typography metrics stay dynamic instead of inheriting a fixed Fluent measurement promise");
        using var customFace = new UiText<UiFluentTypographyPresentation<MutableFace, UiFluentType300>>("Face");
        using var faceParent = Dux.Wrap(default, customFace);
        Measure(faceParent);
        var faceCalls = MutableFace.Calls;
        Measure(faceParent);
        check(MutableFace.Calls > faceCalls && !customFace.RetainsMeasurement,
            "custom typeface selectors remain evaluated without an explicit deterministic promise");
        using var iconBadge = new UiBadge<string, UiTextBadgeBehavior, UiFluentBadgePresentation<MutableIcon>>("Badge label");
        using var iconParent = Dux.Wrap(default, iconBadge);
        MutableIcon.Present = false;
        before = Measure(iconParent);
        MutableIcon.Present = true;
        check(Measure(iconParent).X > before.X && !iconBadge.RetainsMeasurement,
            "mutable custom Badge icon presence cannot be frozen by a generic Fluent presentation");
        var customSource = new ProgressSource { Height = 4 };
        using var customProgress = new UiProgress<ProgressSource, MutableProgressSource, UiFluentProgressPresentation>(customSource);
        using var progressParent = Dux.Wrap(default, customProgress);
        before = Measure(progressParent);
        customSource.Height = 12;
        check(Measure(progressParent).Y > before.Y && !customProgress.RetainsMeasurement,
            "custom Progress source measurement remains dynamic unless its source policy opts in");
        using var mutableText = new UiText<MutableTextPresentation>("Text");
        using var mutableParent = Dux.Wrap(default, mutableText);
        MutableTextPresentation.Edge = 12;
        before = Measure(mutableParent);
        MutableTextPresentation.Edge = 24;
        check(Measure(mutableParent).X > before.X && !mutableText.RetainsMeasurement,
            "custom Text presentations do not acquire retention merely from an immutable string source");
        using var counter = new UiCounterBadge<UiFluentBadgePresentation>();
        using var counterParent = Dux.Wrap(default, counter);
        before = Measure(counterParent);
        counter.Count = 5;
        check(Measure(counterParent).Y > before.Y && counter.RetainsMeasurement,
            "static CounterBadge source invalidates a previously retained hidden layout when it becomes visible");
        using var changingBadge = new UiBadge<string, MutableBadgeBehavior, UiFluentBadgePresentation>("Visible badge");
        using var changingBadgeParent = Dux.Wrap(default, changingBadge);
        MutableBadgeBehavior.Visible = false;
        before = Measure(changingBadgeParent);
        MutableBadgeBehavior.Visible = true;
        check(Measure(changingBadgeParent).Y > before.Y && !changingBadge.RetainsMeasurement,
            "custom Badge behavior visibility remains dynamic without an explicit source measurement promise");
        using var derived = new MutableDerivedText();
        using var derivedParent = Dux.Wrap(default, derived);
        before = Measure(derivedParent);
        derived.Value = "A longer derived text";
        check(Measure(derivedParent).X > before.X,
            "derived controls may disable inherited measurement retention for externally mutable ReadText overrides");
        var delayed = new MeasuredSpinner(new(DelaySeconds: .1f)
        {
            Paused = true
        });
        using var delayedParent = Dux.Wrap(default, delayed);
        Frame(delayedParent.Render);
        check(!delayed.RetainsMeasurement && context.State.NextScheduledFrameTimeSeconds is not null,
            "delayed Spinner does not retain its hidden measurement and still schedules its reveal");
        for (var i = 0; i < 20; i++)
            Frame(delayedParent.Render);
        check(delayed.RetainsMeasurement && delayed.Bounds.Height > 0,
            "delayed Spinner reveals through its original behavior before becoming eligible for parent retention");
        count = delayed.Measurements;
        Frame(delayedParent.Render);
        Frame(delayedParent.Render);
        check(delayed.Measurements == count && !context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "visible paused Spinner reuses parent measurement without continuing animation requests");
        var animated = new MeasuredSpinner(default);
        using var animatedParent = Dux.Wrap(default, animated);
        for (var i = 0; i < 3; i++)
            Frame(animatedParent.Render);
        count = animated.Measurements;
        Frame(animatedParent.Render);
        Frame(animatedParent.Render);
        check(animated.Measurements == count && animated.Renders == 5 && context.State.HasActiveAnimations,
            "visible animated Spinner keeps evaluating and requesting animation frames while parent measurement is reused");
        animated.Options = animated.Options with
        {
            Paused = true
        };
        Frame(animatedParent.Render);
        Frame(animatedParent.Render);
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "pausing a retained Spinner still clears continuous animation scheduling");
        using var customSpinner = new UiSpinner<MutableSpinnerBehavior, UiFluentSpinnerPresentation>();
        using var spinnerParent = Dux.Wrap(default, customSpinner);
        MutableSpinnerBehavior.Visible = false;
        before = Measure(spinnerParent);
        MutableSpinnerBehavior.Visible = true;
        check(Measure(spinnerParent).Y > before.Y && !customSpinner.RetainsMeasurement,
            "custom Spinner visibility cannot silently inherit the built-in monotonic visibility promise");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class MeasuredText() : UiText<UiFluentTypographyPresentation<UiRegularTypeface, UiFluentType300>>("Static text")
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private sealed class MeasuredLink(Action<Uri> navigate) : UiLink<UiLinkNavigationBehavior, UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>(
        "Static link", new("/test", UriKind.Relative), navigate)
    {
        internal int Measurements, Renders;
        internal UiRect Bounds;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
            var position = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            Bounds = new(position.X, position.Y, size.X, size.Y);
        }
    }
    private sealed class MeasuredProgress(Func<UiProgressValue> source) : UiProgress<Func<UiProgressValue>, UiGetterProgressSource, UiFluentProgressPresentation>(source)
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private sealed class MeasuredBadge() : UiBadge<string, UiTextBadgeBehavior, UiFluentBadgePresentation>("Badge")
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private readonly struct MutableMetrics : IUiTypographyMetrics
    {
        internal static float Size;
        public static float FontSize => Size;
        public static float LineHeight => Size * 1.5f;
    }
    private readonly struct MutableFace : IUiTypographyTypeface
    {
        internal static int Calls;
        public static UiTypographyFontPaths Select(UiTypographyFonts fonts)
        {
            Calls++;
            return new(fonts.Regular, fonts.SecondaryRegular);
        }
    }
    private readonly struct MutableIcon : IUiBadgeIcon
    {
        internal static bool Present;
        public static bool IsPresent => Present;
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color)
        {
        }
    }
    private sealed class ProgressSource
    {
        internal float Height;
    }
    private readonly struct MutableProgressSource : IUiProgressSource<ProgressSource>
    {
        public static ProgressSource Normalize(ProgressSource source) => source;
        public static UiProgressValue Read(ProgressSource source) => new(.5f);
        public static UiVector2 Measure<TPresentation>(UiControlContext context, ProgressSource source, UiVector2 requestedSize)
            where TPresentation : IUiProgressPresentation => new(100, source.Height);
    }
    private readonly struct MutableTextPresentation : IUiTextPresentation
    {
        internal static float Edge;
        public static UiVector2 Measure(UiControlContext context, string text) => new(Edge, Edge);
        public static void Render(UiControlContext context, string text, UiColor? color)
        {
        }
    }
    private struct MutableBadgeBehavior : IUiBadgeBehavior<string, MutableBadgeBehavior>
    {
        internal static bool Visible;
        public static UiBadgeContent Read(string source, ref MutableBadgeBehavior state) => new(source, Visible);
    }
    private sealed class MutableDerivedText() : UiText<UiFluentTypographyPresentation<UiRegularTypeface, UiFluentType300>>(string.Empty)
    {
        internal string Value = "A";
        protected override bool CanRetainMeasurement => false;
        protected override string ReadText() => Value;
    }
    private sealed class MeasuredSpinner(UiSpinnerOptions options) : UiSpinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>("Spinner", options)
    {
        internal int Measurements, Renders;
        internal UiRect Bounds;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
            var position = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            Bounds = new(position.X, position.Y, size.X, size.Y);
        }
    }
    private struct MutableSpinnerBehavior : IUiSpinnerBehavior<MutableSpinnerBehavior>
    {
        internal static bool Visible;
        public static bool IsVisible(in MutableSpinnerBehavior state, UiSpinnerOptions options, double time) => Visible;
        public static UiSpinnerFrame Evaluate(ref MutableSpinnerBehavior state, UiSpinnerOptions options, double time) => new() { Visible = Visible, ReducedMotion = true };
    }
}
