using Duxel.Core;

internal static class TooltipSurfaceTests
{
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false,
        false, false, 0, 0, [], [], default, default);

    internal static void Run(Action<bool, string> check)
    {
        var key = new UiImmediateContext.FluentPopupSurfaceKey(92, 100, 0, 0, 1, default, -1, true, false);
        var firstRaster = UiImmediateContext.RasterizeFluentPopupSurface(key, 48);
        var originalPixels = firstRaster.RgbaPixels.AsSpan().ToArray();
        _ = UiImmediateContext.RasterizeFluentPopupSurface(key with { Width = 140, Height = 80 }, 48);
        var repeatedRaster = UiImmediateContext.RasterizeFluentPopupSurface(key, 48);
        check(firstRaster.RgbaPixels.AsSpan().SequenceEqual(originalPixels)
            && repeatedRaster.RgbaPixels.AsSpan().SequenceEqual(originalPixels),
            "Fluent raster scratch reuse keeps padding clear and leaves previously published pixels unchanged");

        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        UiTextureUpdate[] Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(.01f, new(320, 240), new(1, 1)), input ?? Idle,
                new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(draw));
            using var data = context.GetDrawData();
            return data.TextureUpdates.AsSpan().ToArray();
        }
        var layout = new UiPopupLayout(new(112, 80, 96, 28), UiPopupPosition.Above);
        static bool SurfaceCreate(UiTextureUpdate update) => update.Kind == UiTextureUpdateKind.Create && update.Width >= 96;
        var first = Frame(ui => new UiControlContext(ui).FluentTooltipSurface(layout));
        check(first.Count(SurfaceCreate) == 1, "Fluent tooltip creates one bounded texture");
        var id = first.Single(SurfaceCreate).TextureId;
        check(id.Value is >= 1_000_000_002 and < 1_100_000_000,
            "Fluent tooltip RGBA texture never uses the font coverage ID range");
        var same = Frame(ui => new UiControlContext(ui).FluentTooltipSurface(layout));
        check(!same.Any(SurfaceCreate), "Fluent tooltip unchanged surface reuses texture");
        var moved = Frame(ui => new UiControlContext(ui).FluentTooltipSurface(layout with
        {
            Bounds = layout.Bounds with { X = 113 }
        }));
        check(!moved.Any(SurfaceCreate), "Fluent tooltip integral translation reuses texture");
        var fractional = Frame(ui => new UiControlContext(ui).FluentTooltipSurface(layout with
        {
            Bounds = layout.Bounds with { X = 112.5f }
        }));
        check(fractional.Any(update => update.Kind == UiTextureUpdateKind.Destroy && update.TextureId == id)
            && fractional.Count(SurfaceCreate) == 1, "Fluent tooltip fractional phase replaces previous texture");
        foreach (var dark in new[] { false, true })
        {
            context.SetTheme(dark ? UiCompiledDesign.Windows11Dark.Theme : UiCompiledDesign.Windows11.Theme);
            foreach (var inverted in new[] { false, true })
            {
                var update = Frame(ui => new UiControlContext(ui).FluentTooltipSurface(layout, inverted)).Single(SurfaceCreate);
                var pixels = update.RgbaPixels.Span;
                var center = ((48 + 14) * update.Width + 48 + 48) * 4;
                var expected = inverted ? dark ? 61 : 51 : dark ? 41 : 255;
                check(pixels[center] == expected && pixels[center + 3] == 255,
                    "Fluent tooltip body palette : " + dark + " / " + inverted);
                var shadow = ((48 + 28 + 12) * update.Width + 48 + 48) * 4;
                check(pixels[shadow] == 0 && pixels[shadow + 3] > 0,
                    "Fluent tooltip key shadow extends beyond opaque body : " + dark + " / " + inverted);
            }
        }
        context.SetContentScale(2);
        var scaled = Frame(ui => new UiControlContext(ui).FluentTooltipSurface(layout)).Single(SurfaceCreate);
        check(scaled.Width == 384 && scaled.Height == 248, "Fluent tooltip raster follows content scale");
        context.SetContentScale(1);
        Frame(ui =>
        {
            var control = new UiControlContext(ui);
            using var typography = control.BeginTypography<UiRegularTypeface, UiFluentType200>();
            var block = control.MeasureTextBlock("Tip");
            var natural = UiFluentTooltipPresentation.Measure(control, "Tip", default);
            check(natural.Size == new UiVector2(block.Size.X + 24, block.Size.Y + 12),
                "Fluent tooltip border and asymmetric padding yield Type200 height");
            var constrained = UiFluentTooltipPresentation.Measure(control,
                "A long tooltip must wrap into several lines at this width.", new(80, 28));
            check(constrained.Size == new UiVector2(80, 28) && constrained.ContentSize.Y > 28,
                "Fluent tooltip retains wrapped content extent under height cap");
            var wide = UiFluentTooltipPresentation.Measure(control, "Tip", new(400));
            check(wide.Size.X == 240, "Fluent tooltip border box respects CSS maximum width");
            var rejected = false;
            try
            {
                control.FluentTooltipSurface(layout with { Bounds = new(0, 0, 5000, 28) });
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            check(rejected, "Fluent tooltip oversized raster fails explicitly");
        });
        var guarded = false;
        var guardedUpdates = Frame(ui =>
        {
            var control = new UiControlContext(ui);
            control.FluentTooltipSurface(layout);
            try
            {
                control.FluentTooltipSurface(layout with { Bounds = layout.Bounds with { Width = 97 } });
            }
            catch (InvalidOperationException)
            {
                guarded = true;
            }
        });
        var guardedId = guardedUpdates.Single(SurfaceCreate).TextureId;
        check(guarded && !guardedUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy
            && update.TextureId == guardedId), "Fluent tooltip rejects a second shape before retiring the current frame texture");
        using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(
            new UiButton<UiReleaseButtonBehavior, Button>("Target") { Size = new(80, 24) }, "Tip",
            new() { ShowDelaySeconds = 0, Positioning = new() { MatchTargetWidth = true, ArrowHeight = 6 } });
        var hover = Idle with { MousePosition = new(125, 155) };
        Frame(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(120, 150));
            tooltip.Render(ui);
        }, hover);
        check(tooltip.IsOpen, "Fluent surface opens through real tooltip owner");
        var omitted = Frame(static _ => { });
        check(!tooltip.IsOpen && omitted.Any(update => update.Kind == UiTextureUpdateKind.Destroy),
            "Fluent tooltip omission retires cached surface");
        Frame(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(120, 150));
            tooltip.Render(ui);
        }, hover);
        check(tooltip.IsOpen, "Fluent tooltip reopens after omission with a fresh owner surface");
        tooltip.Dispose();
        var disposed = Frame(static _ => { });
        check(!tooltip.IsOpen && disposed.Any(update => update.Kind == UiTextureUpdateKind.Destroy),
            "Fluent tooltip disposal retires its live surface");

        var options = ControlledOptions;
        using var normal = new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(
            ControlledButton(), "Tip", options) { Visible = true };
        using var invertedTooltip = new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentInvertedTooltipPresentation>(
            ControlledButton(), "Tip", options) { Visible = true };
        var both = Frame(ui => ControlledScene(ui, normal, invertedTooltip));
        var ids = both.Where(SurfaceCreate).Select(update => update.TextureId).ToArray();
        check(ids.Length == 2 && ids[0] != ids[1], "controlled surfaces upload distinct texture IDs in the same frame");
        var cached = Frame(ui => ControlledScene(ui, normal, invertedTooltip));
        check(!cached.Any(SurfaceCreate), "both controlled surfaces reuse their own textures");
        normal.Visible = false;
        var onlyInverted = Frame(ui => ControlledScene(ui, normal, invertedTooltip));
        check(ids.Length == 2 && onlyInverted.Where(update => update.Kind == UiTextureUpdateKind.Destroy)
            .Select(update => update.TextureId).SequenceEqual([ids[0]]) && !onlyInverted.Any(SurfaceCreate),
            "hiding one controlled surface retires only its texture");
    }

    private static UiTooltipOptions ControlledOptions => new()
    {
        Positioning = new() { Position = UiPopupPosition.AboveStart, Pinned = true, MatchTargetWidth = true }
    };

    private static UiButton<UiReleaseButtonBehavior, Button> ControlledButton() => new("Target") { Size = new(80, 24) };

    private static void ControlledScene(UiImmediateContext ui, UiWidget normal, UiWidget inverted)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(128, 128, 128), new(0, 0, 0, 0), 0, 0);
        ui.SetCursorScreenPos(new(20, 100));
        normal.Render(ui);
        ui.SetCursorScreenPos(new(190, 100));
        inverted.Render(ui);
    }

    internal static void RenderControlled(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check)
    {
        using var normal = new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(
            ControlledButton(), "Tip", ControlledOptions) { Visible = true };
        using var inverted = new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentInvertedTooltipPresentation>(
            ControlledButton(), "Tip", ControlledOptions) { Visible = true };
        var changes = new List<UiTooltipVisibilityChange>();
        normal.VisibleChangeRequested = changes.Add;
        var dark = false;
        void Scene(UiImmediateContext ui)
        {
            dark = new UiControlContext(ui).FluentColorScheme == UiFluentColorScheme.Dark;
            ControlledScene(ui, normal, inverted);
        }
        setInput(Idle);
        var both = capture("tooltip-controlled-both", Scene);
        var left = (80 * 320 + 80) * 4;
        var right = (80 * 320 + 250) * 4;
        check(normal.IsOpen && inverted.IsOpen && both[left] == (dark ? 41 : 255) && both[right] == (dark ? 61 : 51),
            "both controlled surfaces paint independently verified palette colors");
        var handoverCount = changes.Count;
        setInput(Idle with { KeyEvents = [new(UiKey.Escape, true, KeyModifiers.None)] });
        var declined = capture("tooltip-controlled-declined-escape", Scene);
        check(changes.Count > handoverCount && changes.Last() == new UiTooltipVisibilityChange(false, UiTooltipVisibilityReason.Escape)
            && declined.AsSpan().SequenceEqual(both), "declined Escape leaves both GPU surfaces unchanged");
        setInput(Idle);
        normal.Visible = false;
        var one = capture("tooltip-controlled-one", Scene);
        check(one[left] == 128 && one[right] == both[right] && inverted.IsOpen && !normal.IsOpen,
            "external dismissal clears only the selected GPU surface");
    }

    internal static void Render(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check)
    {
        foreach (var variant in new[] { "none", "top", "bottom", "left", "right", "inverted" })
        {
            var position = variant switch
            {
                "bottom" => UiPopupPosition.BelowStart,
                "left" => UiPopupPosition.BeforeTop,
                "right" => UiPopupPosition.AfterTop,
                _ => UiPopupPosition.AboveStart
            };
            var arrowHeight = variant == "none" ? 0 : 6;
            var body = variant switch
            {
                "bottom" => new UiRect(120, 184, 80, 28),
                // Upstream core1.6.8 arrow alignment shifts these bodies by -0.242.
                "left" => new UiRect(30, 149.758f, 80, 28),
                "right" => new UiRect(210, 149.758f, 80, 28),
                _ => new UiRect(120, 118 - arrowHeight, 80, 28)
            };
            UiPopupArrow? arrow = variant switch
            {
                "none" => null,
                "bottom" => new(new(160, 178), new(155.758f, 184), new(164.242f, 184)),
                "left" => new(new(116, 162), new(110, 157.758f), new(110, 166.242f)),
                "right" => new(new(204, 162), new(210, 157.758f), new(210, 166.242f)),
                _ => new(new(160, 146), new(155.758f, 140), new(164.242f, 140))
            };
            var options = new UiTooltipOptions
            {
                ShowDelaySeconds = 0,
                Positioning = new() { Position = position, Pinned = true, MatchTargetWidth = true, ArrowHeight = arrowHeight }
            };
            var dark = false;
            using UiWidget owner = variant == "inverted"
                ? new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentInvertedTooltipPresentation>(
                    new UiButton<UiReleaseButtonBehavior, Button>("Target") { Size = new(80, 24) }, "Tip", options)
                : new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(
                    new UiButton<UiReleaseButtonBehavior, Button>("Target") { Size = new(80, 24) }, "Tip", options);
            UiPopupLayout observed = default;
            void Scene(UiImmediateContext ui, bool literal)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var control = new UiControlContext(ui);
                dark = control.FluentColorScheme == UiFluentColorScheme.Dark;
                control.Frame(new(0, 0, 320, 240), new(128, 128, 128), new(0, 0, 0, 0), 0, 0);
                if (!literal)
                {
                    ui.SetCursorScreenPos(new(120, 150));
                    owner.Render(ui);
                    observed = UiFluentTooltipPresentation.Arrange(control, "Tip", new(120, 150, 80, 24),
                        new(0, 0, 320, 240), options);
                    return;
                }
                control.Frame(new(120, 150, 80, 24), new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                control.FluentTooltipSurface(new(body, position, arrow), variant == "inverted");
                using var typography = control.BeginTypography<UiRegularTypeface, UiFluentType200>();
                var text = control.MeasureTextBlock("Tip");
                control.TypographyTextBlock(text, new(body.X + 12, body.Y + 5, 56, 16),
                    variant == "inverted" || control.FluentColorScheme == UiFluentColorScheme.Dark
                        ? new(255, 255, 255) : new(36, 36, 36));
            }
            setInput(Idle);
            var expected = capture("tooltip-fluent-surface-" + variant + "-expected", ui => Scene(ui, true));
            setInput(Idle with { MousePosition = new(125, 155) });
            var actual = capture("tooltip-fluent-surface-" + variant + "-actual", ui => Scene(ui, false));
            if (!actual.AsSpan().SequenceEqual(expected))
            {
                Console.WriteLine("Surface literal : " + new UiPopupLayout(body, position, arrow) + " resolved : " + observed);
            }
            check(actual.AsSpan().SequenceEqual(expected), "Fluent tooltip actual owner matches literal placement/text : " + variant);
            var center = (((int)body.Y + 14) * 320 + (int)body.X + 40) * 4;
            var fill = variant == "inverted" ? dark ? 61 : 51 : dark ? 41 : 255;
            check(actual[center] == fill && actual[center + 1] == fill && actual[center + 2] == fill,
                "Fluent tooltip GPU body matches independent palette bytes : " + variant);
            var shadow = (((int)body.Y + 31) * 320 + (int)body.X + 70) * 4;
            check(actual[shadow] < 128 && actual[shadow + 1] == actual[shadow],
                "Fluent tooltip GPU shadow darkens background rather than acting as a white font mask : " + variant);
            var reused = capture("tooltip-fluent-surface-" + variant + "-reused", ui => Scene(ui, false));
            check(actual.AsSpan().SequenceEqual(reused), "Fluent tooltip surface cache preserves GPU pixels : " + variant);
        }
        setInput(Idle);
    }

    internal static void RenderCss(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        foreach (var side in new[] { "none", "top", "bottom", "left", "right", "inverted" })
        {
            var dark = false;
            var actual = capture("tooltip-css-raster-" + side, ui =>
            {
                var control = new UiControlContext(ui);
                dark = control.FluentColorScheme == UiFluentColorScheme.Dark;
                control.Frame(new(0, 0, 320, 240), new(128, 128, 128), new(0, 0, 0, 0), 0, 0);
                UiPopupArrow? arrow = side switch
                {
                    "none" => null,
                    "bottom" => new(new(160, 74), new(155.758f, 80), new(164.242f, 80)),
                    "left" => new(new(214, 94), new(208, 89.758f), new(208, 98.242f)),
                    "right" => new(new(106, 94), new(112, 89.758f), new(112, 98.242f)),
                    _ => new(new(160, 114), new(155.758f, 108), new(164.242f, 108))
                };
                control.FluentTooltipSurface(new(new(112, 80, 96, 28), UiPopupPosition.Above, arrow), side == "inverted");
            });
            var name = (dark ? "dark-" : "light-") + side;
            var reference = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TooltipSurface", name + ".rgba"));
            if (reference.Length != actual.Length)
            {
                throw new InvalidOperationException("CSS reference dimensions differ from GPU readback.");
            }
            var sumSquares = 0d;
            var outliers = 0;
            var maximum = 0;
            // Fixed before comparison: the body plus its full 48-unit shadow neighborhood.
            const int area = 192 * 128;
            for (var y = 32; y < 160; y++)
            {
                for (var x = 64; x < 256; x++)
                {
                    var offset = (y * 320 + x) * 4;
                    var largest = 0;
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var delta = Math.Abs(actual[offset + channel] - reference[offset + channel]);
                        largest = Math.Max(largest, delta);
                        sumSquares += delta * delta;
                    }
                    maximum = Math.Max(maximum, largest);
                    if (largest > 16)
                    {
                        outliers++;
                    }
                }
            }
            var rmse = Math.Sqrt(sumSquares / (area * 3));
            var fraction = outliers / (double)area;
            var label = $"CSS foreignObject raster : {name}, RMSE = {rmse:F4}, outliers = {fraction:P4}, max = {maximum}";
            Console.WriteLine(label);
            check(rmse <= 2.5 && fraction <= .005, label);
        }
    }

    private readonly struct Button : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => requestedSize;
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction) => context.Frame(bounds, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
    }
}
