using Duxel.Core;

internal static class OwnedChildSizingTests
{
    private sealed class AtlasProbe : UiControlWidget
    {
        internal int Measurements;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return context.LayoutConstraints.Constrain(context.MeasureText("Atlas"));
        }
        protected override void RenderControl(UiImmediateContext ui) => ui.Text("Atlas");
    }
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var settings = UiTextSettings.Default;
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), settings);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var data = context.GetDrawData();
        }
        var reads = 0;
        var input = new Input(new(() => { reads++; return "Value"; }, static _ => { }));
        var target = new Target();
        var field = Fluent.Field(input);
        var tooltip = Fluent.Tooltip(target, "Tooltip text");
        using var root = Dux.SizedBox(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min), field, tooltip), 400, 200);
        for (var i = 0; i < 3; i++)
            Frame(root.Render);
        var inputMeasures = input.Measurements;
        var targetMeasures = target.Measurements;
        for (var i = 0; i < 8; i++)
            Frame(root.Render);
        check(input.Measurements == inputMeasures && target.Measurements == targetMeasures,
            "natural Field and Tooltip reuse validated owned-child sizes during actual repeated render");
        check(reads == 11 && target.Renders == 11,
            "owned size retention keeps actual editor getters and target rendering live every frame");
        input.Width = 260;
        target.Label = "Changed target";
        Frame(root.Render);
        check(input.Measurements > inputMeasures && target.Measurements > targetMeasures,
            "owned-child property changes invalidate both natural size consumers");
        inputMeasures = input.Measurements;
        targetMeasures = target.Measurements;
        field.Options = new()
        {
            Label = "Field",
            Hint = "Hint",
            Disabled = true
        };
        tooltip.Text = "Changed tooltip text";
        Frame(root.Render);
        check(input.Measurements > inputMeasures && target.Measurements > targetMeasures,
            "owner options and tooltip changes invalidate previously validated child sizing");
        inputMeasures = input.Measurements;
        targetMeasures = target.Measurements;
        settings = settings with
        {
            Scale = 1.25f
        };
        Frame(root.Render);
        check(input.Measurements > inputMeasures && target.Measurements > targetMeasures,
            "owned size caches follow the existing resource and text environment key");
        inputMeasures = input.Measurements;
        targetMeasures = target.Measurements;
        root.Options = root.Options with
        {
            Width = 300
        };
        Frame(root.Render);
        check(input.Measurements > inputMeasures && target.Measurements > targetMeasures,
            "new parent constraints invalidate validated natural child sizes");
        var observedGlyphs = 0;
        settings = UiTextSettings.Default with
        {
            MissingGlyphObserver = _ => observedGlyphs++
        };
        Frame(root.Render);
        inputMeasures = input.Measurements;
        targetMeasures = target.Measurements;
        Frame(root.Render);
        check(input.Measurements == inputMeasures && target.Measurements == targetMeasures && observedGlyphs == 0,
            "native text retains owned sizing because it does not consult the atlas missing-glyph observer");
        context.SetDirectTextEnabled(false);
        using var atlasProbe = new AtlasProbe();
        using var atlasBox = Dux.SizedBox(atlasProbe, 100, 40);
        Frame(atlasBox.Render);
        var atlasMeasures = atlasProbe.Measurements;
        var atlasObservations = observedGlyphs;
        Frame(atlasBox.Render);
        check(atlasProbe.Measurements > atlasMeasures && observedGlyphs > atlasObservations && atlasObservations > 0,
            "actual atlas glyph observation remains live and prevents owned-child size retention");
        context.SetDirectTextEnabled(true);
        settings = UiTextSettings.Default;
        using var opaqueTarget = new OpaqueTarget();
        using var opaqueTooltip = Fluent.Tooltip(opaqueTarget, "Opaque");
        using var fixedBox = Dux.SizedBox(opaqueTooltip, 200, 40);
        Frame(fixedBox.Render);
        var opaqueMeasures = opaqueTarget.Measurements;
        Frame(fixedBox.Render);
        check(opaqueTarget.Measurements > opaqueMeasures, "fixed allocation never skips measurement of an opaque owned target");
        opaqueTarget.Reject = true;
        var rejected = false;
        Frame(ui => { try { fixedBox.Render(ui); } catch (InvalidOperationException exception) when (exception.Message == "Opaque validation") { rejected = true; } });
        check(rejected, "opaque target validation still executes after an earlier successful fixed-size render");
        var opaqueFieldChild = new OpaqueTarget();
        using var opaqueField = Dux.SizedBox(Fluent.Field(opaqueFieldChild), 200, 64);
        Frame(opaqueField.Render);
        opaqueMeasures = opaqueFieldChild.Measurements;
        Frame(opaqueField.Render);
        check(opaqueFieldChild.Measurements > opaqueMeasures, "natural Field also measures an opaque child on every render");
        using var reentrant = new Target();
        using var reentrantTooltip = Fluent.Tooltip(reentrant, "Invalidated");
        reentrant.OnMeasure = () => { reentrant.OnMeasure = null; reentrant.RequestRebuild(); };
        Frame(reentrantTooltip.Render);
        var attempts = reentrant.Measurements;
        Frame(reentrantTooltip.Render);
        check(reentrant.Measurements > attempts, "invalidation during owned-child sizing prevents a stale cache commit");
        attempts = reentrant.Measurements;
        Frame(reentrantTooltip.Render);
        check(reentrant.Measurements == attempts, "a clean attempt can retain sizing after a reentrant invalidation");
        using var failing = new Target();
        using var failingTooltip = Fluent.Tooltip(failing, "Failed");
        failing.OnMeasure = static () => throw new InvalidOperationException("Expected size failure");
        rejected = false;
        Frame(ui => { try { failingTooltip.Render(ui); } catch (InvalidOperationException exception) when (exception.Message == "Expected size failure") { rejected = true; } });
        failing.OnMeasure = null;
        attempts = failing.Measurements;
        Frame(failingTooltip.Render);
        check(rejected && failing.Measurements > attempts, "failed owned-child measurement cannot become a reusable result");
        using var changed = new Target();
        using var changedTooltip = Fluent.Tooltip(changed, "Changed in frame");
        rejected = false;
        Frame(ui =>
        {
            changedTooltip.MeasureConstrained(ui, UiBoxConstraints.Loose(new(400, 200)));
            changed.OnMeasure = static () => throw new InvalidOperationException("Changed size validation");
            changed.RequestRebuild();
            try
            {
                changedTooltip.Render(ui);
            }
            catch (InvalidOperationException exception) when (exception.Message == "Changed size validation") { rejected = true; }
        });
        changed.OnMeasure = null;
        check(rejected, "same-frame invalidation never borrows a stale prepared child measurement");
        using var legacyChild = new Target();
        using var legacyContent = new UiFieldContent<UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>>();
        void Legacy(UiImmediateContext ui)
        {
            ui.AutoField<UiLabelFieldBehavior, UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>>(
                legacyChild, 200, default, legacyContent);
        }
        Frame(Legacy);
        var legacyMeasures = legacyChild.Measurements;
        Frame(Legacy);
        check(legacyChild.Measurements > legacyMeasures,
            "legacy immediate AutoField keeps borrowed control sizing live without an ownership cache");
        target.Dispose();
        rejected = false;
        Frame(ui => { try { tooltip.Render(ui); } catch (ObjectDisposedException) { rejected = true; } });
        check(rejected, "cached sizing cannot conceal disposal of its owned target");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class Input(UiBinding<string> value) : UiFluentInput("Field input", value, 64)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private class Target() : UiFluentButton("Target")
    {
        internal int Measurements, Renders;
        internal Action? OnMeasure;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            OnMeasure?.Invoke();
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private sealed class OpaqueTarget : Target
    {
        internal bool Reject;
        protected override bool CanRetainMeasurement => false;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            if (Reject)
                throw new InvalidOperationException("Opaque validation");
            return base.MeasureContent(context);
        }
    }
}
