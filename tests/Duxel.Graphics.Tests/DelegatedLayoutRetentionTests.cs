using Duxel.Core;
using TextareaPresentation = Duxel.Core.UiFluentTextareaPresentation<Duxel.Core.UiFluentMediumTextareaSize, Duxel.Core.UiFluentOutlineInput>;

internal static class DelegatedLayoutRetentionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var settings = UiTextSettings.Default;
        void Frame(Action<UiImmediateContext> render, UiInputState? input = null, float delta = 1f / 60)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), settings);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var data = context.GetDrawData();
        }
        var value = "First\nSecond";
        var reads = 0;
        var editor = new Editor(new(() => { reads++; return value; }, text => value = text));
        var target = new Target();
        var tooltip = new Tooltip(target) { Options = new() { ShowDelaySeconds = .05, HideDelaySeconds = .05 } };
        var composedTarget = new Target();
        var composed = new Composed(composedTarget);
        using var parent = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min), editor, tooltip, composed);
        void Measure(UiImmediateContext ui) => parent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(500, 450)));
        for (var i = 0; i < 3; i++)
            Frame(Measure);
        int Measurements() => editor.Measurements + tooltip.Measurements + target.Measurements + composedTarget.Measurements;
        var measured = Measurements();
        for (var i = 0; i < 8; i++)
            Frame(Measure);
        check(Measurements() == measured, "Textarea and owned Tooltip/composed targets permit existing ancestor measurement reuse");
        check(reads == 0 && !tooltip.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "pure delegated measurement reads no editor binding and schedules no tooltip timing work");
        Frame(parent.Render);
        Frame(parent.Render);
        check(reads == 2 && editor.Renders == 2 && target.Renders == 2 && composedTarget.Renders == 2,
            "retained delegation continues actual editor, Tooltip target and user composition rendering");
        var p = new UiVector2(editor.Bounds.X + 20, editor.Bounds.Y + 15);
        Frame(parent.Render, idle with
        {
            MousePosition = p,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(parent.Render, idle with
        {
            MousePosition = p,
            LeftMouseReleased = true
        });
        Frame(parent.Render, idle with
        {
            KeyEvents = [new(UiKey.End, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        Frame(parent.Render, idle with
        {
            CharEvents = [new('x')]
        });
        check(value == "First\nSecondx", "retained Textarea still edits the actual multiline binding");
        Frame(parent.Render, idle with
        {
            KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        check(value == "First\nSecond", "retained Textarea preserves its owned undo history");
        Frame(parent.Render, idle with
        {
            MousePosition = new(-100, -100),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(parent.Render, idle with
        {
            MousePosition = new(-100, -100),
            LeftMouseReleased = true
        });
        var point = new UiVector2(target.Bounds.X + target.Bounds.Width / 2, target.Bounds.Y + target.Bounds.Height / 2);
        Frame(parent.Render, idle with
        {
            MousePosition = point
        }, .01f);
        check(!tooltip.IsOpen && context.State.NextScheduledFrameTimeSeconds is not null,
            "retained Tooltip target still schedules its actual show delay");
        Frame(parent.Render, idle with
        {
            MousePosition = point
        }, .06f);
        check(tooltip.IsOpen, "retained Tooltip target opens on its scheduled due frame");
        measured = editor.Measurements + tooltip.Measurements;
        var targetSizing = target.Measurements;
        Frame(parent.Render, idle with
        {
            MousePosition = point
        });
        check(tooltip.IsOpen && editor.Measurements + tooltip.Measurements == measured && target.Measurements == targetSizing,
            "open Tooltip preserves actual rendering while reusing validated owned-target sizing");
        Frame(parent.Render, idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!tooltip.IsOpen, "retained Tooltip delegation preserves Escape dismissal");
        measured = editor.Measurements;
        editor.Height = 100;
        Frame(Measure);
        check(editor.Measurements > measured, "Textarea dimensions invalidate the owning parent");
        measured = target.Measurements;
        target.Label = "Changed target";
        Frame(Measure);
        check(target.Measurements > measured, "owned Tooltip target changes invalidate the delegated parent");
        measured = composedTarget.Measurements;
        composedTarget.Label = "Changed composition";
        Frame(Measure);
        check(composedTarget.Measurements > measured, "owned composition child changes propagate through its wrapper");
        var atlasObserverCalls = 0;
        settings = settings with
        {
            MissingGlyphObserver = _ => atlasObserverCalls++
        };
        Frame(Measure);
        measured = Measurements();
        Frame(Measure);
        check(Measurements() == measured && atlasObserverCalls == 0, "native delegated measurement retains reuse without atlas observer callbacks");
        settings = UiTextSettings.Default;
        using var custom = new UiMultilineInput<UiFluentMultilineInputBehavior, UiFluentTextareaPresentation<MutableSize, UiFluentOutlineInput>>(
            "Custom", new UiState<string>("").Binding, width: 200);
        using var customParent = Dux.Wrap(default, custom);
        UiVector2 size = default;
        MutableSize.HeightValue = 40;
        Frame(ui => size = customParent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(500, 450))));
        var previous = size;
        MutableSize.HeightValue = 100;
        Frame(ui => size = customParent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(500, 450))));
        check(!custom.RetainsMeasurement && size.Y > previous.Y, "custom Textarea size traits remain dynamic without an explicit promise");
        using var opaque = new Tooltip(new OpaqueTarget());
        check(!opaque.RetainsMeasurement, "Tooltip does not infer retention from fixed geometry or an opaque target");
        var rejected = false;
        try
        {
            composed.Consent(target);
        }
        catch (InvalidOperationException) { rejected = true; }
        check(rejected, "publicly derivable composition rejects measurement delegation to another owner's child");
        using var unowned = new Target();
        rejected = false;
        try
        {
            composed.Consent(unowned);
        }
        catch (InvalidOperationException) { rejected = true; }
        check(rejected, "measurement delegation cannot silently borrow an unowned child and miss invalidation");
        composedTarget.Dispose();
        rejected = false;
        try
        {
            composed.Consent(composedTarget);
        }
        catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "measurement delegation rejects a disposed owned child instead of reusing stale geometry");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class Editor(UiBinding<string> value) : UiMultilineInput<UiFluentMultilineInputBehavior, TextareaPresentation>("Retained multiline", value, width: 300, height: 80)
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
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            Bounds = new(p.X, p.Y, s.X, s.Y);
        }
    }
    private class Target() : UiFluentButton("Owned target")
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
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            Bounds = new(p.X, p.Y, s.X, s.Y);
        }
    }
    private sealed class OpaqueTarget : Target
    {
        protected override bool CanRetainMeasurement => false;
    }
    private sealed class Tooltip(UiWidget target) : UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(target, "Delayed retained tooltip")
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Composed : UiWidget
    {
        private readonly UiWidget _root;
        internal Composed(UiWidget root)
        {
            _root = root;
            OwnChild("root", root);
        }
        internal bool Consent(UiWidget child) => CanRetainOwnedMeasurement(child);
        protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(_root);
        protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
    }
    private readonly struct MutableSize : IUiTextareaSize
    {
        internal static float HeightValue;
        public static float Height => HeightValue;
        public static float HorizontalPadding => 12;
        public static float VerticalPadding => 6;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    }
}
